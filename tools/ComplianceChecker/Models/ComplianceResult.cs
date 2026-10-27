namespace ComplianceChecker.Models;

/// <summary>
/// 合规审查执行结果快照
/// </summary>
public sealed class ComplianceResult
{
    public IReadOnlyList<ComplianceViolation> Violations { get; }
    public int ScannedFilesCount { get; }
    public TimeSpan ScanDuration { get; }

    public bool IsCompliant => !Violations.Any(v => v.Severity == ComplianceSeverity.Error);
    public int ErrorCount => Violations.Count(v => v.Severity == ComplianceSeverity.Error);
    public int WarningCount => Violations.Count(v => v.Severity == ComplianceSeverity.Warning);
    public int InfoCount => Violations.Count(v => v.Severity == ComplianceSeverity.Info);

    public ComplianceResult(IEnumerable<ComplianceViolation> violations, int scannedFilesCount, TimeSpan scanDuration)
    {
        Violations = violations.ToList().AsReadOnly();
        ScannedFilesCount = scannedFilesCount;
        ScanDuration = scanDuration;
    }
}
