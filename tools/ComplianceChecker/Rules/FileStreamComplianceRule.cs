using ComplianceChecker.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplianceChecker.Rules;

/// <summary>
/// COMP004: 日志与输入来源规范审查
/// 核心原则：根据“纸笔准则”，对游戏数据（如 Power.log）仅允许只读流访问（FileAccess.Read），
/// 严禁向游戏日志或游戏目录执行写入、清空、篡改或删除操作。
/// </summary>
public sealed class FileStreamComplianceRule : IComplianceRule
{
    public string RuleId => "COMP004";
    public string Title => "游戏日志与外部输入必须严格遵守只读流访问规范";
    public string Description => "对 Power.log 等游戏核心文件的访问必须为只读流（FileAccess.Read, FileMode.Open, FileShare.ReadWrite），严禁写入或修改游戏日志。";
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    private static readonly HashSet<string> WriteFileMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "File.WriteAllText",
        "File.WriteAllLines",
        "File.WriteAllBytes",
        "File.AppendAllText",
        "File.AppendAllLines",
        "File.Delete",
        "File.OpenWrite"
    };

    public IEnumerable<ComplianceViolation> Analyze(SyntaxTree tree, SemanticModel? semanticModel, string filePath)
    {
        var root = tree.GetRoot();

        // 1. 检查方法调用：File.Write... 涉及 Power.log
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            string expression = invocation.Expression.ToString();
            foreach (var writeMethod in WriteFileMethods)
            {
                if (expression.EndsWith(writeMethod, StringComparison.OrdinalIgnoreCase))
                {
                    // 检查参数中是否涉及日志或游戏目录
                    string argsText = invocation.ArgumentList.ToString();
                    if (argsText.Contains("Power.log", StringComparison.OrdinalIgnoreCase) ||
                        argsText.Contains("Hearthstone", StringComparison.OrdinalIgnoreCase))
                    {
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        yield return new ComplianceViolation(
                            RuleId,
                            Title,
                            $"[违规写操作] 检测到对游戏日志或游戏目录执行写操作: '{expression}'。游戏日志必须保持只读！",
                            Severity,
                            filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1,
                            invocation.ToString()
                        );
                    }
                }
            }
        }

        // 2. 检查 new FileStream 对象创建中的 FileMode / FileAccess
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            string typeName = creation.Type.ToString();
            if (typeName.EndsWith("FileStream", StringComparison.Ordinal) ||
                typeName.EndsWith("StreamWriter", StringComparison.Ordinal))
            {
                string argsText = creation.ArgumentList?.ToString() ?? string.Empty;
                if (argsText.Contains("Power.log", StringComparison.OrdinalIgnoreCase) ||
                    argsText.Contains("Hearthstone", StringComparison.OrdinalIgnoreCase))
                {
                    // 如果是 StreamWriter，直接违规
                    if (typeName.EndsWith("StreamWriter", StringComparison.Ordinal))
                    {
                        var lineSpan = creation.GetLocation().GetLineSpan();
                        yield return new ComplianceViolation(
                            RuleId,
                            Title,
                            "[违规流写入] 检测到使用 StreamWriter 尝试写入游戏日志。游戏日志仅支持只读读取！",
                            Severity,
                            filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1,
                            creation.ToString()
                        );
                    }
                    // 如果是 FileStream，检查是否包含 FileAccess.Write 或 FileMode.Create/Truncate/Append
                    else if (argsText.Contains("FileAccess.Write") ||
                             argsText.Contains("FileAccess.ReadWrite") ||
                             argsText.Contains("FileMode.Create") ||
                             argsText.Contains("FileMode.Truncate") ||
                             argsText.Contains("FileMode.Append"))
                    {
                        var lineSpan = creation.GetLocation().GetLineSpan();
                        yield return new ComplianceViolation(
                            RuleId,
                            Title,
                            $"[违规写模式流] 检测到针对游戏日志使用写权限构造 FileStream: {argsText}。必须且只能使用只读权限（FileMode.Open, FileAccess.Read, FileShare.ReadWrite）！",
                            Severity,
                            filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1,
                            creation.ToString()
                        );
                    }
                }
            }
        }
    }
}
