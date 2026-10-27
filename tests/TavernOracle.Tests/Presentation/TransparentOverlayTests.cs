using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TavernOracle.Presentation.Native;
using TavernOracle.Presentation.Overlay;
using TavernOracle.Presentation.Theme;
using Xunit;

namespace TavernOracle.Tests.Presentation;

public class TransparentOverlayTests
{
    /// <summary>
    /// 在 STA 单元线程中执行 WPF 窗体与控件相关操作
    /// </summary>
    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught != null)
        {
            ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }

    #region WindowStyleHelper Tests

    [Fact]
    public void WindowStyleHelper_Constants_MatchWin32Specifications()
    {
        Assert.Equal(-20, WindowStyleHelper.GWL_EXSTYLE);
        Assert.Equal(0x00000020, WindowStyleHelper.WS_EX_TRANSPARENT);
        Assert.Equal(0x00080000, WindowStyleHelper.WS_EX_LAYERED);
        Assert.Equal(0x00000080, WindowStyleHelper.WS_EX_TOOLWINDOW);
        Assert.Equal(0x08000000, WindowStyleHelper.WS_EX_NOACTIVATE);
    }

    [Fact]
    public void WindowStyleHelper_ZeroHandle_HandlesGracefully()
    {
        Assert.False(WindowStyleHelper.EnableClickThrough(IntPtr.Zero));
        Assert.False(WindowStyleHelper.IsClickThroughEnabled(IntPtr.Zero));
        Assert.False(WindowStyleHelper.ApplyOverlayStyles(IntPtr.Zero));
        Assert.Equal(IntPtr.Zero, WindowStyleHelper.GetWindowLongPtr(IntPtr.Zero, WindowStyleHelper.GWL_EXSTYLE));
        Assert.Equal(IntPtr.Zero, WindowStyleHelper.SetWindowLongPtr(IntPtr.Zero, WindowStyleHelper.GWL_EXSTYLE, (IntPtr)WindowStyleHelper.WS_EX_TRANSPARENT));
    }

    [Fact]
    public void WindowStyleHelper_BitwiseLogicAndIdempotence()
    {
        RunInSta(() =>
        {
            var window = new Window { AllowsTransparency = true, WindowStyle = WindowStyle.None };
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.EnsureHandle();

            try
            {
                Assert.NotEqual(IntPtr.Zero, hwnd);

                // 首次开启点击穿透
                bool firstSuccess = WindowStyleHelper.EnableClickThrough(hwnd);
                Assert.True(firstSuccess);

                // 验证样式位已设置
                int newStyle = WindowStyleHelper.GetWindowLongPtr(hwnd, WindowStyleHelper.GWL_EXSTYLE).ToInt32();
                Assert.True((newStyle & WindowStyleHelper.WS_EX_TRANSPARENT) == WindowStyleHelper.WS_EX_TRANSPARENT);
                Assert.True((newStyle & WindowStyleHelper.WS_EX_LAYERED) == WindowStyleHelper.WS_EX_LAYERED);
                Assert.True(WindowStyleHelper.IsClickThroughEnabled(hwnd));

                // 幂等性测试：再次调用应直接返回 true 且样式保持不变
                bool secondSuccess = WindowStyleHelper.EnableClickThrough(hwnd);
                Assert.True(secondSuccess);

                int unchangedStyle = WindowStyleHelper.GetWindowLongPtr(hwnd, WindowStyleHelper.GWL_EXSTYLE).ToInt32();
                Assert.Equal(newStyle, unchangedStyle);

                // 组合样式应用
                bool applySuccess = WindowStyleHelper.ApplyOverlayStyles(hwnd, clickThrough: true, toolWindow: true, noActivate: true);
                Assert.True(applySuccess);

                int fullOverlayStyle = WindowStyleHelper.GetWindowLongPtr(hwnd, WindowStyleHelper.GWL_EXSTYLE).ToInt32();
                Assert.True((fullOverlayStyle & WindowStyleHelper.WS_EX_TRANSPARENT) == WindowStyleHelper.WS_EX_TRANSPARENT);
                Assert.True((fullOverlayStyle & WindowStyleHelper.WS_EX_LAYERED) == WindowStyleHelper.WS_EX_LAYERED);
                Assert.True((fullOverlayStyle & WindowStyleHelper.WS_EX_TOOLWINDOW) == WindowStyleHelper.WS_EX_TOOLWINDOW);
                Assert.True((fullOverlayStyle & WindowStyleHelper.WS_EX_NOACTIVATE) == WindowStyleHelper.WS_EX_NOACTIVATE);
            }
            finally
            {
                window.Close();
            }
        });
    }

    #endregion

    #region TransparentOverlayBase Contract Tests

    [Fact]
    public void TransparentOverlayBase_DefaultProperties_ConformToSpecification()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();

            Assert.True(overlay.AllowsTransparency);
            Assert.Equal(WindowStyle.None, overlay.WindowStyle);
            Assert.False(overlay.ShowInTaskbar);
            Assert.True(overlay.Topmost);
            Assert.Equal(Brushes.Transparent, overlay.Background);
            Assert.False(overlay.Focusable);

            Assert.NotNull(overlay.OverlayContainer);
            Assert.Equal(Brushes.Transparent, overlay.OverlayContainer.Background);
            Assert.False(overlay.OverlayContainer.IsHitTestVisible);
            Assert.Empty(overlay.Children);
            Assert.False(overlay.IsAttachedToHostCanvas);
            Assert.Null(overlay.HostCanvas);
        });
    }

    [Fact]
    public void TransparentOverlayBase_HandleInitialization_AppliesTransparentAndLayeredStyles()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();
            IntPtr hwnd = overlay.EnsureHandle();

            Assert.NotEqual(IntPtr.Zero, hwnd);
            Assert.Equal(hwnd, overlay.WindowHandle);
            Assert.True(overlay.IsClickThrough);

            // 验证 Win32 扩展样式中确实包含必须标志
            int currentStyle = WindowStyleHelper.GetWindowLongPtr(hwnd, WindowStyleHelper.GWL_EXSTYLE).ToInt32();
            Assert.True((currentStyle & WindowStyleHelper.WS_EX_TRANSPARENT) == WindowStyleHelper.WS_EX_TRANSPARENT);
            Assert.True((currentStyle & WindowStyleHelper.WS_EX_LAYERED) == WindowStyleHelper.WS_EX_LAYERED);
            Assert.True((currentStyle & WindowStyleHelper.WS_EX_TOOLWINDOW) == WindowStyleHelper.WS_EX_TOOLWINDOW);
            Assert.True((currentStyle & WindowStyleHelper.WS_EX_NOACTIVATE) == WindowStyleHelper.WS_EX_NOACTIVATE);
        });
    }

    [Fact]
    public void TransparentOverlayBase_ChildVisualElementsManagement_AddsAndRemovesCorrectly()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();

            var badge1 = new Border { Width = 60, Height = 24 };
            overlay.AddChild(badge1, 100, 150, 5);

            Assert.Single(overlay.Children);
            Assert.Same(badge1, overlay.Children[0]);
            Assert.Contains(badge1, overlay.OverlayContainer.Children.Cast<UIElement>());
            Assert.Equal(100, Canvas.GetLeft(badge1));
            Assert.Equal(150, Canvas.GetTop(badge1));
            Assert.Equal(5, Canvas.GetZIndex(badge1));

            var banner = new TextBlock { Text = "本回合推荐升本" };
            overlay.AddChild(banner);

            Assert.Equal(2, overlay.Children.Count);
            Assert.Contains(banner, overlay.Children);
            Assert.Contains(banner, overlay.OverlayContainer.Children.Cast<UIElement>());

            // 重复添加同一个元素不应引起重复
            overlay.AddChild(badge1);
            Assert.Equal(2, overlay.Children.Count);

            // 移除特定子元素
            bool removed = overlay.RemoveChild(badge1);
            Assert.True(removed);
            Assert.Single(overlay.Children);
            Assert.DoesNotContain(badge1, overlay.OverlayContainer.Children.Cast<UIElement>());

            // 清空所有子元素
            overlay.ClearChildren();
            Assert.Empty(overlay.Children);
            Assert.Empty(overlay.OverlayContainer.Children);
        });
    }

    [Fact]
    public void TransparentOverlayBase_AttachAndDetachFromHostCanvas_WorksCorrectly()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();
            var hdtCanvas = new Canvas { Width = 1920, Height = 1080 };

            Assert.False(overlay.IsAttachedToHostCanvas);
            Assert.Null(overlay.HostCanvas);

            // 挂载至外部 HDT Canvas
            overlay.AttachToHostCanvas(hdtCanvas);

            Assert.True(overlay.IsAttachedToHostCanvas);
            Assert.Same(hdtCanvas, overlay.HostCanvas);
            Assert.Contains(overlay.OverlayContainer, hdtCanvas.Children.Cast<UIElement>());
            Assert.Null(overlay.Content);

            // 调整尺寸与位置
            overlay.SetBounds(200, 300, 800, 600);
            Assert.Equal(200, Canvas.GetLeft(overlay.OverlayContainer));
            Assert.Equal(300, Canvas.GetTop(overlay.OverlayContainer));
            Assert.Equal(800, overlay.OverlayContainer.Width);
            Assert.Equal(600, overlay.OverlayContainer.Height);

            // 从外部 Canvas 分离
            overlay.DetachFromHostCanvas();

            Assert.False(overlay.IsAttachedToHostCanvas);
            Assert.Null(overlay.HostCanvas);
            Assert.DoesNotContain(overlay.OverlayContainer, hdtCanvas.Children.Cast<UIElement>());
            Assert.Same(overlay.OverlayContainer, overlay.Content);
        });
    }

    [Fact]
    public void TransparentOverlayBase_SetBounds_InWindowMode_UpdatesBounds()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();

            overlay.SetBounds(50, 80, 1280, 720);

            Assert.Equal(50, overlay.Left);
            Assert.Equal(80, overlay.Top);
            Assert.Equal(1280, overlay.Width);
            Assert.Equal(720, overlay.Height);
            Assert.Equal(1280, overlay.OverlayContainer.Width);
            Assert.Equal(720, overlay.OverlayContainer.Height);
        });
    }

    [Fact]
    public void TransparentOverlayBase_ShowAndHide_InAttachedMode_TogglesVisibility()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();
            var hostCanvas = new Canvas();
            overlay.AttachToHostCanvas(hostCanvas);

            overlay.ShowOverlay();
            Assert.True(overlay.IsVisible);
            Assert.Equal(Visibility.Visible, overlay.OverlayContainer.Visibility);

            overlay.HideOverlay();
            Assert.False(overlay.IsVisible);
            Assert.Equal(Visibility.Collapsed, overlay.OverlayContainer.Visibility);
        });
    }

    [Fact]
    public void TransparentOverlayBase_Dispose_CleansUpAndDetaches()
    {
        RunInSta(() =>
        {
            var overlay = new TransparentOverlayBase();
            var hostCanvas = new Canvas();
            overlay.AttachToHostCanvas(hostCanvas);
            overlay.AddChild(new Border());

            overlay.Dispose();

            Assert.False(overlay.IsAttachedToHostCanvas);
            Assert.Empty(overlay.Children);
            Assert.Empty(hostCanvas.Children);
        });
    }

    #endregion

    #region Design System & Theme Tests

    [Fact]
    public void HudPalette_DesignTokens_ValidateSpecificationValues()
    {
        // 验证十六进制色值契约
        Assert.Equal("#E0161B22", HudPalette.HexBackgroundCanvas);
        Assert.Equal("#0D1117", HudPalette.HexBackgroundDeepSpace);
        Assert.Equal("#F59E0B", HudPalette.HexLegendaryGold);
        Assert.Equal("#FBBF24", HudPalette.HexLegendaryGoldLight);
        Assert.Equal("#38BDF8", HudPalette.HexTacticalBlue);
        Assert.Equal("#10B981", HudPalette.HexSafeTempo);
        Assert.Equal("#EF4444", HudPalette.HexLethalRed);
        Assert.Equal("#A855F7", HudPalette.HexArcanePurple);

        // 验证冷冻画刷属性（确保零 GC 与跨线程安全）
        Assert.True(HudPalette.BackgroundCanvasBrush.IsFrozen);
        Assert.True(HudPalette.LegendaryGoldBrush.IsFrozen);
        Assert.True(HudPalette.TacticalBlueBrush.IsFrozen);
        Assert.True(HudPalette.SafeTempoBrush.IsFrozen);
        Assert.True(HudPalette.LethalRedBrush.IsFrozen);
        Assert.True(HudPalette.ArcanePurpleBrush.IsFrozen);
        Assert.True(HudPalette.TextPrimaryBrush.IsFrozen);
        Assert.True(HudPalette.BorderSubtleBrush.IsFrozen);

        // 验证动态风险色彩映射
        Assert.Same(HudPalette.LethalRedBrush, HudPalette.GetRiskColor(0.35));
        Assert.Same(HudPalette.LegendaryGoldBrush, HudPalette.GetRiskColor(0.20));
        Assert.Same(HudPalette.SafeTempoBrush, HudPalette.GetRiskColor(0.05));
    }

    [Fact]
    public void HudStyles_DesignTokens_ValidateLayoutValues()
    {
        Assert.Equal(12, HudStyles.PillCornerRadius.TopLeft);
        Assert.Equal(6, HudStyles.BadgeCornerRadius.TopLeft);
        Assert.Equal(8, HudStyles.BannerCornerRadius.TopLeft);
        Assert.Equal(10, HudStyles.PanelCornerRadius.TopLeft);

        Assert.Equal(0.88, HudStyles.AcrylicOpacity);
        Assert.Equal(0.35, HudStyles.DimmedOpacity);
    }

    #endregion
}
