using System.Text;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Analysis;

namespace SwitchPoint.Reports.Rendering;

/// <summary>
/// Entry point for report rendering. JSON is produced here (deterministic, hash-embedding); PDF and DOCX are
/// produced by the format-specific renderers in this namespace.
/// </summary>
public sealed class ReportRenderer : IReportRenderer
{
    public const string TemplateVersion = "2026.09.1";

    public Task<ReportDocument> RenderAsync(ReportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        return request.Format switch
        {
            ReportFormat.Json => Task.FromResult(RenderJson(request)),
            ReportFormat.Pdf => Task.FromResult(PdfReportRenderer.Render(request, TemplateVersion)),
            ReportFormat.Docx => Task.FromResult(DocxReportRenderer.Render(request, TemplateVersion)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Format, "Unknown report format."),
        };
    }

    /// <summary>Deterministic JSON envelope: analysis, client, assumptions and generation metadata.</summary>
    public static ReportDocument RenderJson(ReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var envelope = new
        {
            request.Kind,
            request.AnalysisId,
            request.AnalysisVersion,
            request.AnalysisResultHash,
            TemplateVersion,
            request.GeneratedBy,
            request.GeneratedAtUtc,
            Firm = new { request.FirmName, request.FirmReferenceNumber },
            request.Client,
            request.AssumptionSet,
            request.Analysis,
        };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonDefaults.Serialize(envelope));
        return new ReportDocument(bytes, "application/json", $"{request.Kind}-{request.AnalysisId:N}-v{request.AnalysisVersion}.json", TemplateVersion);
    }
}
