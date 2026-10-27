using System.Xml.Linq;
using ComplianceChecker.Models;

namespace ComplianceChecker.Rules;

/// <summary>
/// COMP003: 严禁引入注入、驱动级或内存搜索/修改等第三方违规依赖包
/// </summary>
public sealed class BannedPackageRule : IProjectComplianceRule
{
    public string RuleId => "COMP003";
    public string Title => "严禁引入第三方注入、驱动或内存黑客相关依赖包";
    public string Description => "工程文件 (.csproj) 严禁引用 Memory.dll, EasyHook, MinHook, Reloaded.Memory, AutoItX, InputSimulator 等注入/外挂相关组件。";
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    private static readonly HashSet<string> BannedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Memory.dll",
        "EasyHook",
        "MinHook",
        "Reloaded.Memory",
        "Reloaded.Injector",
        "AutoItX",
        "AutoItX3",
        "InputSimulator",
        "WindowsInput",
        "SharpDX.DirectInput",
        "BlackBone",
        "Detours"
    };

    public IEnumerable<ComplianceViolation> AnalyzeProject(string projectFilePath, string projectFileContent)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(projectFileContent);
        }
        catch
        {
            yield break;
        }

        // 检查 PackageReference
        var packageRefs = doc.Descendants("PackageReference");
        foreach (var pRef in packageRefs)
        {
            var includeAttr = pRef.Attribute("Include") ?? pRef.Attribute("Update");
            if (includeAttr == null) continue;

            string packageName = includeAttr.Value;
            if (BannedPackages.Any(b => packageName.Equals(b, StringComparison.OrdinalIgnoreCase) || packageName.StartsWith(b + ".", StringComparison.OrdinalIgnoreCase)))
            {
                yield return new ComplianceViolation(
                    RuleId,
                    Title,
                    $"[违规依赖引用] 检测到工程引用了非法第三方外挂/内存/模拟包: '{packageName}'。违反反作弊安全准则！",
                    Severity,
                    projectFilePath,
                    1,
                    1,
                    pRef.ToString()
                );
            }
        }

        // 检查直接 DLL Reference
        var directRefs = doc.Descendants("Reference");
        foreach (var dRef in directRefs)
        {
            var includeAttr = dRef.Attribute("Include");
            if (includeAttr == null) continue;

            string refName = includeAttr.Value;
            if (BannedPackages.Any(b => refName.Contains(b, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new ComplianceViolation(
                    RuleId,
                    Title,
                    $"[违规程序集引用] 检测到工程直接引用了非法第三方程序集: '{refName}'。违反反作弊安全准则！",
                    Severity,
                    projectFilePath,
                    1,
                    1,
                    dRef.ToString()
                );
            }
        }
    }
}
