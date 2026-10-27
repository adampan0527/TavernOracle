using ComplianceChecker.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplianceChecker.Rules;

/// <summary>
/// COMP001: 绝对禁止 Win32 模拟输入与进程内存破坏/注入 P/Invoke API
/// 违背原则：暴雪纸笔准则（Pencil & Paper Rule）以及零注入、零模拟操作底线。
/// </summary>
public sealed class BannedPInvokeRule : IComplianceRule
{
    public string RuleId => "COMP001";
    public string Title => "严禁引入 Win32 模拟按键/鼠标及进程注入 P/Invoke API";
    public string Description => "代码库严禁使用 DllImport/LibraryImport 声明 SendInput, mouse_event, keybd_event, OpenProcess, WriteProcessMemory, ReadProcessMemory, VirtualAllocEx 等违规函数。";
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    // 绝对禁止的输入模拟相关 API（大小写不敏感）
    private static readonly HashSet<string> BannedInputFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SendInput",
        "mouse_event",
        "keybd_event",
        "SetCursorPos",
        "SetPhysicalCursorPos",
        "GetCursorPos", // 如果需要获取鼠标用于模拟移动需谨慎，但主要是模拟修改
        "SendKeys"
    };

    // 绝对禁止的内存篡改/句柄注入相关 API（大小写不敏感）
    private static readonly HashSet<string> BannedMemoryFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "OpenProcess",
        "VirtualAllocEx",
        "VirtualProtectEx",
        "WriteProcessMemory",
        "ReadProcessMemory",
        "CreateRemoteThread",
        "CreateRemoteThreadEx",
        "NtOpenProcess",
        "NtWriteVirtualMemory",
        "NtReadVirtualMemory",
        "NtAllocateVirtualMemory",
        "ZwOpenProcess",
        "ZwWriteVirtualMemory",
        "ZwReadVirtualMemory"
    };

    public IEnumerable<ComplianceViolation> Analyze(SyntaxTree tree, SemanticModel? semanticModel, string filePath)
    {
        var root = tree.GetRoot();
        var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methodDeclarations)
        {
            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeName = attribute.Name.ToString();
                    bool isDllImport = attributeName.EndsWith("DllImport", StringComparison.Ordinal) ||
                                       attributeName.EndsWith("DllImportAttribute", StringComparison.Ordinal) ||
                                       attributeName.EndsWith("LibraryImport", StringComparison.Ordinal) ||
                                       attributeName.EndsWith("LibraryImportAttribute", StringComparison.Ordinal);

                    if (!isDllImport)
                    {
                        continue;
                    }

                    // 提取 DllName 与 EntryPoint
                    string? dllName = null;
                    string targetFunctionName = method.Identifier.Text;

                    if (attribute.ArgumentList != null && attribute.ArgumentList.Arguments.Count > 0)
                    {
                        // 第一个参数通常是 dllName
                        var firstArg = attribute.ArgumentList.Arguments[0];
                        dllName = firstArg.Expression.ToString().Trim('\"');

                        // 查找 EntryPoint 命名字参
                        foreach (var arg in attribute.ArgumentList.Arguments)
                        {
                            if (arg.NameEquals != null && arg.NameEquals.Name.Identifier.Text == "EntryPoint")
                            {
                                targetFunctionName = arg.Expression.ToString().Trim('\"');
                            }
                        }
                    }

                    // 检查是否属于违规输入 API
                    if (BannedInputFunctions.Contains(targetFunctionName) || BannedInputFunctions.Contains(method.Identifier.Text))
                    {
                        var lineSpan = method.GetLocation().GetLineSpan();
                        yield return new ComplianceViolation(
                            RuleId,
                            Title,
                            $"[违规输入模拟] 检测到非法声明 P/Invoke 自动化操作 API: '{targetFunctionName}' (DLL: {dllName ?? "未知"})。违反暴雪“纸笔准则”，存在 100% 封号判定风险！",
                            Severity,
                            filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1,
                            method.ToString().Split('\n').FirstOrDefault()?.Trim() ?? method.Identifier.Text
                        );
                    }
                    // 检查是否属于违规内存篡改/进程注入 API
                    else if (BannedMemoryFunctions.Contains(targetFunctionName) || BannedMemoryFunctions.Contains(method.Identifier.Text))
                    {
                        var lineSpan = method.GetLocation().GetLineSpan();
                        yield return new ComplianceViolation(
                            RuleId,
                            Title,
                            $"[违规内存注入] 检测到非法声明 P/Invoke 内存操作/注入 API: '{targetFunctionName}' (DLL: {dllName ?? "未知"})。严禁获取游戏进程句柄或修改内存！",
                            Severity,
                            filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1,
                            method.ToString().Split('\n').FirstOrDefault()?.Trim() ?? method.Identifier.Text
                        );
                    }
                }
            }
        }
    }
}
