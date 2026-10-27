using ComplianceChecker;
using Xunit;

namespace TavernOracle.ComplianceTests;

public class WorkspaceComplianceAuditTests
{
    [Fact]
    public void AuditCurrentWorkspace_MustBeCompletelyCompliant()
    {
        // 向上查找包含 TavernOracle.sln 的根目录
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(currentDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TavernOracle.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        string rootDir = dir.FullName;

        var scanner = new ComplianceScanner();

        // 审查 src 目录
        string srcDir = Path.Combine(rootDir, "src");
        if (Directory.Exists(srcDir))
        {
            var srcResult = scanner.ScanDirectory(srcDir);
            Assert.True(srcResult.IsCompliant,
                $"src 目录未通过合规审查: {string.Join("; ", srcResult.Violations.Select(v => v.Message))}");
            Assert.Equal(0, srcResult.ErrorCount);
        }

        // 审查 tools/ComplianceChecker 目录
        string toolsDir = Path.Combine(rootDir, "tools", "ComplianceChecker");
        if (Directory.Exists(toolsDir))
        {
            var toolsResult = scanner.ScanDirectory(toolsDir);
            Assert.True(toolsResult.IsCompliant,
                $"tools 目录未通过合规审查: {string.Join("; ", toolsResult.Violations.Select(v => v.Message))}");
            Assert.Equal(0, toolsResult.ErrorCount);
        }
    }
}
