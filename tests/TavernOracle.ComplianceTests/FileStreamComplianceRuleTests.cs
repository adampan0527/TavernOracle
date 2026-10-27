using ComplianceChecker;
using ComplianceChecker.Models;
using Xunit;

namespace TavernOracle.ComplianceTests;

public class FileStreamComplianceRuleTests
{
    private readonly ComplianceScanner _scanner = new();

    [Fact]
    public void ScanSource_WithReadOnlyLogStream_MustPassAudit()
    {
        string compliantCode = """
            using System;
            using System.IO;

            public class LogWatcher
            {
                public void ReadPowerLog(string logPath)
                {
                    using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    string? line = reader.ReadLine();
                }
            }
            """;

        var result = _scanner.ScanSource(compliantCode, "LogWatcher.cs");

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ScanSource_WithFileWriteAllTextToPowerLog_MustDetectViolation()
    {
        string maliciousCode = """
            using System.IO;

            public class LogTamperer
            {
                public void ClearLog()
                {
                    File.WriteAllText("C:\\Games\\Hearthstone\\Logs\\Power.log", string.Empty);
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "LogTamperer.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP004" && v.Message.Contains("写操作"));
    }

    [Fact]
    public void ScanSource_WithStreamWriterToPowerLog_MustDetectViolation()
    {
        string maliciousCode = """
            using System.IO;

            public class FakeLogGenerator
            {
                public void WriteFakeData()
                {
                    using var writer = new StreamWriter("Power.log");
                    writer.WriteLine("TAG_CHANGE Entity=123 tag=HEALTH value=999");
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "FakeLogGenerator.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP004" && v.Message.Contains("StreamWriter"));
    }

    [Fact]
    public void ScanSource_WithFileStreamWriteAccessToPowerLog_MustDetectViolation()
    {
        string maliciousCode = """
            using System.IO;

            public class Corrupter
            {
                public void OpenWithWrite()
                {
                    var stream = new FileStream("Power.log", FileMode.Create, FileAccess.Write);
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "Corrupter.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP004" && v.Message.Contains("FileAccess.Write"));
    }

    [Fact]
    public void ScanSource_WithWritingOwnConfigFile_MustPassAudit()
    {
        string compliantCode = """
            using System.IO;

            public class SettingsManager
            {
                public void SaveSettings(string json)
                {
                    File.WriteAllText("TavernOracleSettings.json", json);
                }
            }
            """;

        var result = _scanner.ScanSource(compliantCode, "SettingsManager.cs");

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }
}
