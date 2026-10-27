using ComplianceChecker.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplianceChecker.Rules;

/// <summary>
/// COMP005: 表现层 Overlay 悬浮窗透明点击穿透规范审查
/// 核心原则：HUD 悬浮层必须设置 WS_EX_TRANSPARENT (0x20) 和 WS_EX_LAYERED (0x80000)，
/// 确保玩家鼠标对游戏的原生点击完全无阻挡穿透，不得拦截游戏原生输入。
/// </summary>
public sealed class OverlayTransparencyRule : IComplianceRule
{
    public string RuleId => "COMP005";
    public string Title => "HUD 悬浮窗必须挂载透明点击穿透样式 (WS_EX_TRANSPARENT | WS_EX_LAYERED)";
    public string Description => "任何 Overlay 窗口样式初始化中，必须显式包含 WS_EX_TRANSPARENT (0x00000020) 与 WS_EX_LAYERED (0x00080000)，绝不允许阻挡或截获玩家鼠标原生交互。";
    public ComplianceSeverity Severity => ComplianceSeverity.Warning;

    public IEnumerable<ComplianceViolation> Analyze(SyntaxTree tree, SemanticModel? semanticModel, string filePath)
    {
        var root = tree.GetRoot();

        // 仅对明确为 Overlay / HUD / Window 相关的类文件或方法进行检查
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            string className = classDecl.Identifier.Text;
            bool isOverlayRelated = className.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
                                    className.Contains("Hud", StringComparison.OrdinalIgnoreCase) ||
                                    className.Contains("Badge", StringComparison.OrdinalIgnoreCase);

            if (!isOverlayRelated)
            {
                continue;
            }

            // 检查类中是否调用了 SetWindowLong / SetWindowLongPtr 修改窗口样式
            var invocations = classDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
            bool hasSetWindowLong = false;
            bool hasTransparentFlag = false;

            foreach (var inv in invocations)
            {
                string invText = inv.Expression.ToString();
                if (invText.Contains("SetWindowLong", StringComparison.OrdinalIgnoreCase))
                {
                    hasSetWindowLong = true;
                    string fullInvocation = inv.ToString();
                    if (fullInvocation.Contains("WS_EX_TRANSPARENT", StringComparison.OrdinalIgnoreCase) ||
                        fullInvocation.Contains("0x00000020") ||
                        fullInvocation.Contains("0x20"))
                    {
                        hasTransparentFlag = true;
                    }
                }
            }

            // 如果修改了 ExStyle 但缺少 WS_EX_TRANSPARENT
            if (hasSetWindowLong && !hasTransparentFlag)
            {
                var lineSpan = classDecl.GetLocation().GetLineSpan();
                yield return new ComplianceViolation(
                    RuleId,
                    Title,
                    $"[缺失点击穿透] 类 '{className}' 调用了 SetWindowLong 修改窗口样式，但未检测到 WS_EX_TRANSPARENT (0x20)。HUD 必须确保鼠标点击完全穿透！",
                    Severity,
                    filePath,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.StartLinePosition.Character + 1,
                    classDecl.Identifier.ToString()
                );
            }
        }
    }
}
