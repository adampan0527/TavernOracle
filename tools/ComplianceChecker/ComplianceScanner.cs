using System.Diagnostics;
using ComplianceChecker.Models;
using ComplianceChecker.Rules;
using Microsoft.CodeAnalysis.CSharp;

namespace ComplianceChecker;

/// <summary>
/// 静态合规扫描器主控制器
/// 基于 Roslyn (Microsoft.CodeAnalysis.CSharp) 抽象语法树（AST）与 XML 配置解析，
/// 自动化扫描源码与项目依赖，确保 100% 符合暴雪安全准则。
/// </summary>
public sealed class ComplianceScanner
{
    private readonly List<IComplianceRule> _syntaxRules = new();
    private readonly List<IProjectComplianceRule> _projectRules = new();

    public IReadOnlyList<IComplianceRule> SyntaxRules => _syntaxRules.AsReadOnly();
    public IReadOnlyList<IProjectComplianceRule> ProjectRules => _projectRules.AsReadOnly();

    public ComplianceScanner()
    {
        // 注册内置审查规则
        RegisterDefaultRules();
    }

    private void RegisterDefaultRules()
    {
        _syntaxRules.Add(new BannedPInvokeRule());
        _syntaxRules.Add(new BannedMethodInvocationRule());
        _syntaxRules.Add(new FileStreamComplianceRule());
        _syntaxRules.Add(new OverlayTransparencyRule());

        _projectRules.Add(new BannedPackageRule());
    }

    /// <summary>
    /// 扫描单段内存中的 C# 源码
    /// </summary>
    public ComplianceResult ScanSource(string sourceCode, string filePath = "InMemory.cs")
    {
        var sw = Stopwatch.StartNew();
        var violations = new List<ComplianceViolation>();

        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath);
        foreach (var rule in _syntaxRules)
        {
            var ruleViolations = rule.Analyze(tree, semanticModel: null, filePath);
            violations.AddRange(ruleViolations);
        }

        sw.Stop();
        return new ComplianceResult(violations, scannedFilesCount: 1, sw.Elapsed);
    }

    /// <summary>
    /// 扫描单个文件（.cs 或 .csproj）
    /// </summary>
    public ComplianceResult ScanFile(string filePath)
    {
        var sw = Stopwatch.StartNew();
        var violations = new List<ComplianceViolation>();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"待扫描的文件不存在: {filePath}");
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".cs")
        {
            string content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
            foreach (var rule in _syntaxRules)
            {
                violations.AddRange(rule.Analyze(tree, null, filePath));
            }
        }
        else if (ext == ".csproj")
        {
            string content = File.ReadAllText(filePath);
            foreach (var pRule in _projectRules)
            {
                violations.AddRange(pRule.AnalyzeProject(filePath, content));
            }
        }

        sw.Stop();
        return new ComplianceResult(violations, scannedFilesCount: 1, sw.Elapsed);
    }

    /// <summary>
    /// 递归扫描指定目录下的所有 .cs 与 .csproj 文件
    /// </summary>
    public ComplianceResult ScanDirectory(string directoryPath, IEnumerable<string>? ignoreSubstrings = null)
    {
        var sw = Stopwatch.StartNew();
        var violations = new List<ComplianceViolation>();
        int scannedCount = 0;

        var defaultIgnores = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".vs" + Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar + "bin" + Path.AltDirectorySeparatorChar,
            Path.AltDirectorySeparatorChar + "obj" + Path.AltDirectorySeparatorChar,
            Path.AltDirectorySeparatorChar + ".git" + Path.AltDirectorySeparatorChar,
            Path.AltDirectorySeparatorChar + ".vs" + Path.AltDirectorySeparatorChar
        };

        if (ignoreSubstrings != null)
        {
            foreach (var ig in ignoreSubstrings)
            {
                defaultIgnores.Add(ig);
            }
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"扫描目录不存在: {directoryPath}");
        }

        // 收集所有 .cs 文件
        var csFiles = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            if (defaultIgnores.Any(ig => file.Contains(ig, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                string content = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                foreach (var rule in _syntaxRules)
                {
                    violations.AddRange(rule.Analyze(tree, null, file));
                }
                scannedCount++;
            }
            catch (Exception ex)
            {
                violations.Add(new ComplianceViolation(
                    "COMP999",
                    "文件解析异常",
                    $"无法解析文件 {file}: {ex.Message}",
                    ComplianceSeverity.Warning,
                    file,
                    1,
                    1,
                    string.Empty
                ));
            }
        }

        // 收集所有 .csproj 文件
        var csprojFiles = Directory.EnumerateFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
        foreach (var file in csprojFiles)
        {
            if (defaultIgnores.Any(ig => file.Contains(ig, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                string content = File.ReadAllText(file);
                foreach (var pRule in _projectRules)
                {
                    violations.AddRange(pRule.AnalyzeProject(file, content));
                }
                scannedCount++;
            }
            catch (Exception ex)
            {
                violations.Add(new ComplianceViolation(
                    "COMP999",
                    "工程配置解析异常",
                    $"无法解析项目文件 {file}: {ex.Message}",
                    ComplianceSeverity.Warning,
                    file,
                    1,
                    1,
                    string.Empty
                ));
            }
        }

        sw.Stop();
        return new ComplianceResult(violations, scannedCount, sw.Elapsed);
    }
}
