using ComplianceChecker;
using ComplianceChecker.Models;
using Xunit;

namespace TavernOracle.ComplianceTests;

public class BannedMethodInvocationRuleTests
{
    private readonly ComplianceScanner _scanner = new();

    [Fact]
    public void ScanSource_WithSendKeysSend_MustDetectViolation()
    {
        string maliciousCode = """
            using System.Windows.Forms;

            public class KeySimulator
            {
                public void PressSpace()
                {
                    SendKeys.Send(" ");
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "KeySimulator.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP002" && v.Message.Contains("SendKeys.Send"));
    }

    [Fact]
    public void ScanSource_WithSendKeysSendWait_MustDetectViolation()
    {
        string maliciousCode = """
            using System.Windows.Forms;

            public class MacroBot
            {
                public void Trigger()
                {
                    SendKeys.SendWait("{ENTER}");
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "MacroBot.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP002" && v.Message.Contains("SendKeys.SendWait"));
    }

    [Fact]
    public void ScanSource_WithProcessEnterDebugMode_MustDetectViolation()
    {
        string maliciousCode = """
            using System.Diagnostics;

            public class PrivilegeElevator
            {
                public void Elevate()
                {
                    Process.EnterDebugMode();
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "PrivilegeElevator.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP002" && v.Message.Contains("Process.EnterDebugMode"));
    }

    [Fact]
    public void ScanSource_WithStandardMathAndAlgorithm_MustPassAudit()
    {
        string compliantCode = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class KnapsackDpSolver
            {
                public double ComputeDelta(double lethalProb, double tempoWeight)
                {
                    double wTempo = 1.0 + 3.0 * Math.Pow(lethalProb, 2);
                    return wTempo * tempoWeight;
                }

                public List<int> SelectOptimal(IEnumerable<int> candidates)
                {
                    return candidates.Where(c => c > 0).OrderByDescending(c => c).ToList();
                }
            }
            """;

        var result = _scanner.ScanSource(compliantCode, "KnapsackDpSolver.cs");

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }
}
