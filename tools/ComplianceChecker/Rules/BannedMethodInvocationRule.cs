using ComplianceChecker.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplianceChecker.Rules;

/// <summary>
/// COMP002: 严禁调用托管层模拟按键/自动化输入 API 与调试提权 API
/// 违背原则：暴雪纸笔准则与零注入原则
/// </summary>
public sealed class BannedMethodInvocationRule : IComplianceRule
{
    public string RuleId => "COMP002";
    public string Title => "严禁调用托管按键模拟及进程调试提权方法";
    public string Description => "代码严禁调用 SendKeys.Send, SendKeys.SendWait, Process.EnterDebugMode 等托管模拟或提权方法。";
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    private static readonly HashSet<string> BannedInvocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "SendKeys.Send",
        "SendKeys.SendWait",
        "System.Windows.Forms.SendKeys.Send",
        "System.Windows.Forms.SendKeys.SendWait",
        "Process.EnterDebugMode",
        "System.Diagnostics.Process.EnterDebugMode",
        "mouse_event",
        "SendInput",
        "keybd_event",
        "OpenProcess",
        "WriteProcessMemory",
        "ReadProcessMemory"
    };

    public IEnumerable<ComplianceViolation> Analyze(SyntaxTree tree, SemanticModel? semanticModel, string filePath)
    {
        var root = tree.GetRoot();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            string expressionText = invocation.Expression.ToString();

            // 检查表达式是否匹配或以违规调用结尾
            bool isViolating = false;
            string matchedKey = string.Empty;

            foreach (var banned in BannedInvocations)
            {
                if (expressionText.Equals(banned, StringComparison.OrdinalIgnoreCase) ||
                    expressionText.EndsWith("." + banned, StringComparison.OrdinalIgnoreCase))
                {
                    isViolating = true;
                    matchedKey = banned;
                    break;
                }
            }

            if (isViolating)
            {
                var lineSpan = invocation.GetLocation().GetLineSpan();
                yield return new ComplianceViolation(
                    RuleId,
                    Title,
                    $"[违规方法调用] 检测到调用被禁用的自动化或提权方法: '{matchedKey}'。严禁执行任何模拟按键或进程介入操作！",
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
