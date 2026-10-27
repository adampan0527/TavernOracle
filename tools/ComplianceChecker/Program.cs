using ComplianceChecker.Models;

namespace ComplianceChecker;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  TavernOracle 静态代码合规审查规则器 (Blizzard Compliance Gate)");
        Console.WriteLine("  暴雪“纸笔准则” (Pencil & Paper Rule) · 零注入 · 零模拟输入 · 零封号风险");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        string targetDirectory = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

        if (!Directory.Exists(targetDirectory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] 指定的审查目标目录不存在: {targetDirectory}");
            Console.ResetColor();
            return 2;
        }

        Console.WriteLine($"[INFO] 正在扫描目标目录: {Path.GetFullPath(targetDirectory)}");

        var scanner = new ComplianceScanner();
        // 忽略测试工程中的 fixture 目录以防自测样本被误认
        var ignoreList = new[] { "fixtures", "test_fixtures" };
        var result = scanner.ScanDirectory(targetDirectory, ignoreList);

        Console.WriteLine($"[INFO] 扫描完成: 共处理 {result.ScannedFilesCount} 个代码及工程文件，耗时 {result.ScanDuration.TotalMilliseconds:F2} ms");
        Console.WriteLine();

        if (result.Violations.Count > 0)
        {
            foreach (var violation in result.Violations)
            {
                if (violation.Severity == ComplianceSeverity.Error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"[CRITICAL RED LINE] ");
                }
                else if (violation.Severity == ComplianceSeverity.Warning)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"[WARNING] ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write($"[INFO] ");
                }

                Console.ResetColor();
                Console.WriteLine($"{violation.RuleId}: {violation.Title}");
                Console.WriteLine($"  位置: {violation.FilePath}:{violation.LineNumber}:{violation.ColumnNumber}");
                Console.WriteLine($"  详情: {violation.Message}");
                if (!string.IsNullOrWhiteSpace(violation.Snippet))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  代码: {violation.Snippet}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
        }

        if (result.IsCompliant)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("  [PASSED] 合规审查 100% 通过！");
            Console.WriteLine($"  未检测到任何模拟输入 (SendInput/mouse_event) 或内存注入 (OpenProcess/WriteProcessMemory) 代码。");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine($"  [FAILED] 合规审查未通过！发现 {result.ErrorCount} 项触碰绝对红线的违规代码或依赖！");
            Console.WriteLine("  请依据 docs/compliance.md 进行修正，严禁将包含违规代码的分支合并到主干或打包发布。");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();
            return 1;
        }
    }
}
