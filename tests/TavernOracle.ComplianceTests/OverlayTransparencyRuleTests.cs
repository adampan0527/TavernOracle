using ComplianceChecker;
using ComplianceChecker.Models;
using Xunit;

namespace TavernOracle.ComplianceTests;

public class OverlayTransparencyRuleTests
{
    private readonly ComplianceScanner _scanner = new();

    [Fact]
    public void ScanSource_WithCompliantOverlayTransparency_MustPassAudit()
    {
        string compliantOverlayCode = """
            using System;
            using System.Runtime.InteropServices;

            public class OverlayWindow
            {
                private const int GWL_EXSTYLE = -20;
                private const int WS_EX_TRANSPARENT = 0x00000020;
                private const int WS_EX_LAYERED = 0x00080000;

                [DllImport("user32.dll", SetLastError = true)]
                private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

                [DllImport("user32.dll", SetLastError = true)]
                private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

                public void MakeClickThrough(IntPtr hwnd)
                {
                    int currentStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                }
            }
            """;

        var result = _scanner.ScanSource(compliantOverlayCode, "OverlayWindow.cs");

        Assert.True(result.IsCompliant);
        Assert.DoesNotContain(result.Violations, v => v.RuleId == "COMP005");
    }

    [Fact]
    public void ScanSource_WithOverlayMissingTransparentStyle_MustEmitWarning()
    {
        string missingTransparentCode = """
            using System;
            using System.Runtime.InteropServices;

            public class BadOverlayWindow
            {
                private const int GWL_EXSTYLE = -20;
                private const int WS_EX_TOPMOST = 0x00000008;

                [DllImport("user32.dll", SetLastError = true)]
                private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

                public void InitWindow(IntPtr hwnd)
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_TOPMOST);
                }
            }
            """;

        var result = _scanner.ScanSource(missingTransparentCode, "BadOverlayWindow.cs");

        Assert.Contains(result.Violations, v => v.RuleId == "COMP005" && v.Severity == ComplianceSeverity.Warning);
    }
}
