using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Analysis;

/// <summary>A generated document, tied to the analysis version and result hash it was produced from.</summary>
public sealed class Report : Entity, ITenantScoped
{
    public Report(Guid id, Guid firmId, Guid clientId, Guid analysisId, int analysisVersion, string analysisResultHash, ReportKind kind, ReportFormat format, string templateVersion, Guid generatedBy, string sha256, string storagePath, long sizeBytes, DateTime generatedAtUtc)
        : base(id, generatedAtUtc)
    {
        FirmId = Guard.NotEmpty(firmId);
        ClientId = Guard.NotEmpty(clientId);
        AnalysisId = Guard.NotEmpty(analysisId);
        AnalysisVersion = Guard.Positive(analysisVersion);
        AnalysisResultHash = Guard.NotNullOrWhiteSpace(analysisResultHash);
        Kind = Guard.Defined(kind);
        Format = Guard.Defined(format);
        TemplateVersion = Guard.NotNullOrWhiteSpace(templateVersion);
        GeneratedBy = Guard.NotEmpty(generatedBy);
        Sha256 = Guard.NotNullOrWhiteSpace(sha256);
        StoragePath = Guard.NotNullOrWhiteSpace(storagePath);
        Guard.Against(sizeBytes < 0, "Report size cannot be negative.");
        SizeBytes = sizeBytes;
        GeneratedAtUtc = generatedAtUtc;
    }

    public Guid FirmId { get; }
    public Guid ClientId { get; }
    public Guid AnalysisId { get; }
    public int AnalysisVersion { get; }
    public string AnalysisResultHash { get; }
    public ReportKind Kind { get; }
    public ReportFormat Format { get; }
    public string TemplateVersion { get; }
    public Guid GeneratedBy { get; }
    public string Sha256 { get; }
    public string StoragePath { get; }
    public long SizeBytes { get; }
    public DateTime GeneratedAtUtc { get; }
}
