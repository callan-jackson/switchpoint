using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Application.UseCases.Analyses;
using SwitchPoint.Application.UseCases.Clients;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Tenancy;

namespace SwitchPoint.Application.UseCases.Reports;

/// <summary>Generates, lists and serves reports. Generating a report locks the analysis it was produced from.</summary>
public sealed class ReportHandlers
{
    private readonly IReportRepository _reports;
    private readonly IReportStore _store;
    private readonly IReportRenderer _renderer;
    private readonly IAnalysisRepository _analyses;
    private readonly IAssumptionSetRepository _assumptionSets;
    private readonly IFirmRepository _firms;
    private readonly AnalysisHandlers _analysisHandlers;
    private readonly GetClientHandler _getClient;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public ReportHandlers(IReportRepository reports, IReportStore store, IReportRenderer renderer, IAnalysisRepository analyses, IAssumptionSetRepository assumptionSets, IFirmRepository firms, AnalysisHandlers analysisHandlers, GetClientHandler getClient, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    {
        _reports = reports;
        _store = store;
        _renderer = renderer;
        _analyses = analyses;
        _assumptionSets = assumptionSets;
        _firms = firms;
        _analysisHandlers = analysisHandlers;
        _getClient = getClient;
        _audit = audit;
        _uow = uow;
        _user = user;
        _clock = clock;
    }

    public async Task<ReportDto> CreateAsync(CreateReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        AnalysisBase analysis = await _analyses.GetAsync<AnalysisBase>(_user.FirmId, request.AnalysisId, ct) ?? throw new NotFoundException("Analysis", request.AnalysisId);
        if (analysis.Status == AnalysisStatus.Draft || analysis.ResultJson is null || analysis.ResultHash is null)
        {
            throw new ConflictException("Calculate the analysis before generating a report.");
        }

        object analysisDto = analysis switch
        {
            PensionSwitchAnalysis => await _analysisHandlers.GetPensionSwitchAsync(analysis.Id, ct),
            DbTransferAnalysis => await _analysisHandlers.GetDbTransferAsync(analysis.Id, ct),
            CashflowPlan => await _analysisHandlers.GetCashflowAsync(analysis.Id, ct),
            _ => throw new ValidationException("analysisId", "Unsupported analysis type."),
        };

        ValidateKind(request.Kind, analysis);

        ClientDetail client = await _getClient.HandleAsync(analysis.ClientId, ct);
        Firm firm = await _firms.GetAsync(_user.FirmId, ct) ?? throw new NotFoundException("Firm", _user.FirmId);
        AssumptionSet set = await _assumptionSets.GetAsync(_user.FirmId, analysis.AssumptionSetId, ct) ?? throw new NotFoundException("AssumptionSet", analysis.AssumptionSetId);

        ReportRequest renderRequest = new(request.Kind, request.Format, analysis.Id, analysis.Version, analysis.ResultHash, analysisDto, client, firm.Name, firm.FcaFirmReferenceNumber, set.ToDto(), _user.DisplayName, _clock.UtcNow);
        ReportDocument doc = await _renderer.RenderAsync(renderRequest, ct);

        Guid reportId = Guid.NewGuid();
        string path = await _store.SaveAsync(_user.FirmId, reportId, doc.FileName, doc.Content, ct);
        Report report = new(reportId, _user.FirmId, analysis.ClientId, analysis.Id, analysis.Version, analysis.ResultHash, request.Kind, request.Format, doc.TemplateVersion, _user.UserId, JsonDefaults.Sha256(doc.Content.Span), path, doc.Content.Length, _clock.UtcNow);
        await _reports.AddAsync(report, ct);

        if (!analysis.IsLocked)
        {
            analysis.Lock(_clock.UtcNow);
            await _audit.AppendAsync(_user.FirmId, _user.UserId, analysis.GetType().Name, analysis.Id, "Locked", new { analysis.Id, analysis.Version, analysis.ResultHash, Reason = "Report issued" }, ct);
        }

        await _audit.AppendAsync(_user.FirmId, _user.UserId, "Report", report.Id, "Generated", new { report.Id, report.AnalysisId, report.AnalysisVersion, report.Kind, report.Format, report.Sha256, report.SizeBytes }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(report);
    }

    public async Task<ReportDto> GetAsync(Guid reportId, CancellationToken ct) => ToDto(await _reports.GetAsync(_user.FirmId, reportId, ct) ?? throw new NotFoundException("Report", reportId));

    public async Task<IReadOnlyList<ReportDto>> ListForClientAsync(Guid clientId, CancellationToken ct) => [.. (await _reports.ListForClientAsync(_user.FirmId, clientId, ct)).Select(ToDto)];

    public async Task<IReadOnlyList<ReportDto>> ListRecentAsync(int take, CancellationToken ct) => [.. (await _reports.ListAsync(_user.FirmId, Math.Clamp(take, 1, 200), ct)).Select(ToDto)];

    public async Task<ReportFile> DownloadAsync(Guid reportId, CancellationToken ct)
    {
        Report report = await _reports.GetAsync(_user.FirmId, reportId, ct) ?? throw new NotFoundException("Report", reportId);
        ReadOnlyMemory<byte> bytes = await _store.LoadAsync(report.StoragePath, ct);
        if (!string.Equals(JsonDefaults.Sha256(bytes.Span), report.Sha256, StringComparison.Ordinal))
        {
            throw new ConflictException("The stored report does not match its recorded hash; it may have been altered.");
        }

        await _audit.AppendAsync(_user.FirmId, _user.UserId, "Report", report.Id, "Downloaded", new { report.Id }, ct);
        await _uow.SaveChangesAsync(ct);
        (string contentType, string extension) = report.Format switch
        {
            ReportFormat.Pdf => ("application/pdf", "pdf"),
            ReportFormat.Docx => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
            _ => ("application/json", "json"),
        };
        return new ReportFile(bytes, contentType, $"{report.Kind}-{report.AnalysisId:N}-v{report.AnalysisVersion}.{extension}");
    }

    private static void ValidateKind(ReportKind kind, AnalysisBase analysis)
    {
        bool ok = (kind, analysis) switch
        {
            (ReportKind.Suitability, _) => true,
            (ReportKind.PensionSwitch, PensionSwitchAnalysis) => true,
            (ReportKind.FundComparison, PensionSwitchAnalysis) => true,
            (ReportKind.DbTransfer, DbTransferAnalysis) => true,
            (ReportKind.Cashflow, CashflowPlan) => true,
            _ => false,
        };
        if (!ok)
        {
            throw new ValidationException("kind", $"A {kind} report cannot be produced from a {analysis.GetType().Name}.");
        }
    }

    private static ReportDto ToDto(Report r) => new(r.Id, r.ClientId, r.AnalysisId, r.AnalysisVersion, r.AnalysisResultHash, r.Kind, r.Format, r.TemplateVersion, r.GeneratedBy, r.Sha256, r.SizeBytes, r.GeneratedAtUtc, $"/api/v1/reports/{r.Id}/download");
}
