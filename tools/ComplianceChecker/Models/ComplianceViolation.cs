namespace ComplianceChecker.Models;

/// <summary>
/// 合规审查违规项详细记录
/// </summary>
public sealed class ComplianceViolation
{
    public string RuleId { get; }
    public string Title { get; }
    public string Message { get; }
    public ComplianceSeverity Severity { get; }
    public string FilePath { get; }
    public int LineNumber { get; }
    public int ColumnNumber { get; }
    public string Snippet { get; }

    public ComplianceViolation(
        string ruleId,
        string title,
        string message,
        ComplianceSeverity severity,
        string filePath,
        int lineNumber,
        int columnNumber,
        string snippet)
    {
        RuleId = ruleId;
        Title = title;
        Message = message;
        Severity = severity;
        FilePath = filePath;
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        Snippet = snippet;
    }

    public override string ToString()
    {
        return $"[{Severity.ToString().ToUpperInvariant()}] {RuleId} ({FilePath}:{LineNumber}:{ColumnNumber}): {Message}\n  --> {Snippet}";
    }
}
