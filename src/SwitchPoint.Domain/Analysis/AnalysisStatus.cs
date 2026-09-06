namespace SwitchPoint.Domain.Analysis;

public enum AnalysisStatus
{
    Draft = 0,
    Calculated = 1,
    /// <summary>Frozen after a report has been issued; any change requires a new version.</summary>
    Locked = 2,
}

public enum ReportKind
{
    Suitability = 0,
    PensionSwitch = 1,
    DbTransfer = 2,
    Cashflow = 3,
    FundComparison = 4,
}

public enum ReportFormat
{
    Pdf = 0,
    Docx = 1,
    Json = 2,
}

/// <summary>Basis on which the adviser is paid for DB transfer advice (COBS 19.1B).</summary>
public enum AdviserChargeBasis
{
    NonContingent = 0,
    /// <summary>Only permitted under the COBS 19.1B.9R carve-outs; raises a compliance warning.</summary>
    Contingent = 1,
}
