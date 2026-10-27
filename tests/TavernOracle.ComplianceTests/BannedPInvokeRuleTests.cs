using ComplianceChecker;
using ComplianceChecker.Models;
using Xunit;

namespace TavernOracle.ComplianceTests;

public class BannedPInvokeRuleTests
{
    private readonly ComplianceScanner _scanner = new();

    [Fact]
    public void ScanSource_WithMouseEvent_MustDetectViolation()
    {
        string maliciousCode = """
            using System;
            using System.Runtime.InteropServices;

            public class BotAutomation
            {
                [DllImport("user32.dll")]
                public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

                public void ClickMinion()
                {
                    mouse_event(0x0002, 100, 200, 0, 0); // MOUSEEVENTF_LEFTDOWN
                }
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "BotAutomation.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP001" && v.Message.Contains("mouse_event"));
    }

    [Fact]
    public void ScanSource_WithSendInput_MustDetectViolation()
    {
        string maliciousCode = """
            using System;
            using System.Runtime.InteropServices;

            public class BotInput
            {
                [DllImport("user32.dll", SetLastError = true)]
                public static extern uint SendInput(uint nInputs, IntPtr pInputs, int cbSize);
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "BotInput.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP001" && v.Message.Contains("SendInput"));
    }

    [Fact]
    public void ScanSource_WithCustomNameAndEntryPoint_MustDetectViolation()
    {
        string maliciousCode = """
            using System;
            using System.Runtime.InteropServices;

            public class StealthInput
            {
                [DllImport("user32.dll", EntryPoint = "keybd_event")]
                public static extern void SimulateKey(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "StealthInput.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP001" && v.Message.Contains("keybd_event"));
    }

    [Theory]
    [InlineData("OpenProcess")]
    [InlineData("WriteProcessMemory")]
    [InlineData("ReadProcessMemory")]
    [InlineData("VirtualAllocEx")]
    [InlineData("CreateRemoteThread")]
    public void ScanSource_WithMemoryHackingPInvoke_MustDetectViolation(string bannedApi)
    {
        string maliciousCode = $$"""
            using System;
            using System.Runtime.InteropServices;

            public class MemoryInjector
            {
                [DllImport("kernel32.dll", SetLastError = true)]
                public static extern IntPtr {{bannedApi}}(IntPtr hProcess, IntPtr lpAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
            }
            """;

        var result = _scanner.ScanSource(maliciousCode, "MemoryInjector.cs");

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, v => v.RuleId == "COMP001" && v.Message.Contains(bannedApi));
    }

    [Fact]
    public void ScanSource_WithSafePInvoke_MustPassAudit()
    {
        string compliantCode = """
            using System;
            using System.Runtime.InteropServices;

            public static class WindowHelper
            {
                [StructLayout(LayoutKind.Sequential)]
                public struct RECT { public int Left, Top, Right, Bottom; }

                [DllImport("user32.dll", SetLastError = true)]
                public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

                [DllImport("dwmapi.dll")]
                public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
            }
            """;

        var result = _scanner.ScanSource(compliantCode, "WindowHelper.cs");

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }
}
