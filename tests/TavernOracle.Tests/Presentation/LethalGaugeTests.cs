using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TavernOracle.Presentation.Controls;
using TavernOracle.Presentation.Overlay;
using TavernOracle.Presentation.Theme;
using TavernOracle.Simulation;
using Xunit;

namespace TavernOracle.Tests.Presentation;

public class LethalGaugeTests
{
    /// <summary>
    /// 在 STA 单元线程中运行 WPF 控件逻辑
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

    #region LethalGaugeViewModel Tests

    [Fact]
    public void LethalGaugeViewModel_Defaults_MatchSafePrior()
    {
        var vm = new LethalGaugeViewModel();

        Assert.Equal(0.45, vm.WinRate, 3);
        Assert.Equal(0.10, vm.TieRate, 3);
        Assert.Equal(0.45, vm.LossRate, 3);
        Assert.Equal(0.10, vm.LethalRate, 3);
        Assert.Equal(0.10, vm.PLethal, 3);
        Assert.Equal(0.0, vm.AverageDamageTaken, 1);
        Assert.True(vm.IsDegraded);

        Assert.Equal("45.0%", vm.WinRateText);
        Assert.Equal("10.0%", vm.TieRateText);
        Assert.Equal("45.0%", vm.LossRateText);
        Assert.Equal("10.0%", vm.LethalRateText);
        Assert.Equal("0.0", vm.AverageDamageText);

        Assert.Equal(0.30, vm.EmergencyThreshold);
        Assert.False(vm.IsCriticalAlert);
        Assert.Equal("战力安全", vm.RiskTierText);
    }

    [Fact]
    public void LethalGaugeViewModel_Update_ValidatesProportionsAndFormatting()
    {
        var vm = new LethalGaugeViewModel();

        // 场景 1：优势对局 (80% 胜率，5% 平局，15% 败率，0% 暴毙率，均伤 -8.5 胜出)
        var advantage = new CombatSimulationResult(
            winRate: 0.80,
            tieRate: 0.05,
            lossRate: 0.15,
            lethalRate: 0.0,
            averageDamageTaken: -8.5,
            simulationsCount: 10000);

        vm.Update(in advantage);

        Assert.Equal(0.80, vm.WinBarRatio, 2);
        Assert.Equal(0.05, vm.TieBarRatio, 2);
        Assert.Equal(0.15, vm.SafeLossBarRatio, 2);
        Assert.Equal(0.0, vm.LethalBarRatio, 2);
        Assert.Equal(0.15, vm.LossBarRatio, 2);

        Assert.Equal("80.0%", vm.WinRateText);
        Assert.Equal("5.0%", vm.TieRateText);
        Assert.Equal("15.0%", vm.LossRateText);
        Assert.Equal("0.0%", vm.LethalRateText);
        Assert.Equal("-8.5", vm.AverageDamageText);
        Assert.False(vm.IsCriticalAlert);
        Assert.Equal("战力安全", vm.RiskTierText);

        // 场景 2：暴毙危局 (10% 胜率，5% 平局，85% 败率，45% 暴毙率，均伤 +14.2 扣血)
        var critical = new CombatSimulationResult(
            winRate: 0.10,
            tieRate: 0.05,
            lossRate: 0.85,
            lethalRate: 0.45,
            averageDamageTaken: 14.2,
            simulationsCount: 5000);

        vm.Update(in critical);

        Assert.Equal(0.10, vm.WinBarRatio, 2);
        Assert.Equal(0.05, vm.TieBarRatio, 2);
        Assert.Equal(0.40, vm.SafeLossBarRatio, 2); // 0.85 - 0.45 = 0.40
        Assert.Equal(0.45, vm.LethalBarRatio, 2);
        Assert.Equal(0.85, vm.LossBarRatio, 2);

        Assert.Equal("10.0%", vm.WinRateText);
        Assert.Equal("5.0%", vm.TieRateText);
        Assert.Equal("85.0%", vm.LossRateText);
        Assert.Equal("45.0%", vm.LethalRateText);
        Assert.Equal("+14.2", vm.AverageDamageText);
        Assert.True(vm.IsCriticalAlert);
        Assert.Equal("极度危险", vm.RiskTierText);
    }

    [Theory]
    [InlineData(0.29, false)]
    [InlineData(0.30, false)]
    [InlineData(0.30001, true)]
    [InlineData(0.35, true)]
    [InlineData(0.90, true)]
    public void LethalGaugeViewModel_EmergencyThreshold_BoundaryTesting(double lethalRate, bool expectedAlert)
    {
        var vm = new LethalGaugeViewModel();
        var result = new CombatSimulationResult(0.3, 0.1, 0.6, lethalRate);

        vm.Update(in result);

        Assert.Equal(expectedAlert, vm.IsCriticalAlert);
        Assert.Equal(expectedAlert ? HudPalette.LethalRedBrush : HudPalette.BorderSubtleBrush, vm.AlertBorderBrush);
    }

    [Fact]
    public void LethalGaugeViewModel_CustomEmergencyThreshold_AdjustsAlertState()
    {
        var vm = new LethalGaugeViewModel();
        vm.EmergencyThreshold = 0.20; // 调低警报生死线至 20%

        var result = new CombatSimulationResult(0.5, 0.1, 0.4, 0.25);
        vm.Update(in result);

        Assert.True(vm.IsCriticalAlert);
        Assert.Same(HudPalette.LethalRedBrush, vm.AlertBorderBrush);

        vm.EmergencyThreshold = 0.35; // 调高阈值至 35%
        Assert.False(vm.IsCriticalAlert);
        Assert.Same(HudPalette.BorderSubtleBrush, vm.AlertBorderBrush);
    }

    #endregion

    #region LethalGaugeControl STA Tests

    [Fact]
    public void LethalGaugeControl_STA_Initialization_AndDefaultVisuals()
    {
        RunInSta(() =>
        {
            using var control = new LethalGaugeControl();

            Assert.NotNull(control.ViewModel);
            Assert.NotNull(control.MainBorder);
            Assert.NotNull(control.AlertGlowBorder);
            Assert.NotNull(control.LethalBadgeBorder);
            Assert.NotNull(control.ProgressBarContainer);
            Assert.NotNull(control.ProgressBarGrid);

            // 纸笔准则与透明穿透验证：点击测试必须为 false
            Assert.False(control.IsHitTestVisible);
            Assert.False(control.MainBorder.IsHitTestVisible);
            Assert.False(control.AlertGlowBorder.IsHitTestVisible);
            Assert.False(control.Focusable);

            // 视觉规范尺寸与圆角
            Assert.Equal(HudStyles.PanelCornerRadius, control.MainBorder.CornerRadius);
            Assert.Equal(HudPalette.BackgroundCanvasBrush, control.MainBorder.Background);
            Assert.Equal(HudPalette.BorderSubtleBrush, control.MainBorder.BorderBrush);
            Assert.Equal(0.0, control.AlertGlowBorder.Opacity);
            Assert.False(control.IsBreathingActive);

            // 初始文本呈现
            Assert.Contains("45.0%", control.WinRateTextBlock.Text);
            Assert.Contains("10.0%", control.TieRateTextBlock.Text);
            Assert.Contains("45.0%", control.LossRateTextBlock.Text);
            Assert.Contains("10.0%", control.LethalRateTextBlock.Text);
            Assert.Contains("0.0", control.AverageDamageTextBlock.Text);
        });
    }

    [Fact]
    public void LethalGaugeControl_STA_ProgressBarWidths_AllocatedCorrectly()
    {
        RunInSta(() =>
        {
            using var control = new LethalGaugeControl();

            var result = new CombatSimulationResult(
                winRate: 0.60,
                tieRate: 0.10,
                lossRate: 0.30,
                lethalRate: 0.10);

            control.UpdateResult(result);

            // 校验各分段比例
            Assert.Equal(0.60, control.WinRatio, 2);
            Assert.Equal(0.10, control.TieRatio, 2);
            Assert.Equal(0.30, control.LossRatio, 2);
            Assert.Equal(0.20, control.SafeLossRatio, 2);
            Assert.Equal(0.10, control.LethalRatio, 2);

            // 校验 Grid 列宽分段
            Assert.Equal(GridUnitType.Star, control.WinColumn.Width.GridUnitType);
            Assert.Equal(0.60, control.WinColumn.Width.Value, 2);

            Assert.Equal(GridUnitType.Star, control.TieColumn.Width.GridUnitType);
            Assert.Equal(0.10, control.TieColumn.Width.Value, 2);

            Assert.Equal(GridUnitType.Star, control.SafeLossColumn.Width.GridUnitType);
            Assert.Equal(0.20, control.SafeLossColumn.Width.Value, 2);

            Assert.Equal(GridUnitType.Star, control.LethalColumn.Width.GridUnitType);
            Assert.Equal(0.10, control.LethalColumn.Width.Value, 2);

            // 验证四段比例之和为 1.0
            double total = control.WinColumn.Width.Value +
                           control.TieColumn.Width.Value +
                           control.SafeLossColumn.Width.Value +
                           control.LethalColumn.Width.Value;
            Assert.Equal(1.0, total, 2);
        });
    }

    [Fact]
    public void LethalGaugeControl_STA_CriticalAlert_TogglesVisualStateAndBreathing()
    {
        RunInSta(() =>
        {
            using var control = new LethalGaugeControl();

            // 1. 触发暴毙危急警报 (P_lethal = 0.40 > 30%)
            var critical = new CombatSimulationResult(
                winRate: 0.20,
                tieRate: 0.10,
                lossRate: 0.70,
                lethalRate: 0.40,
                averageDamageTaken: 12.0);

            control.UpdateResult(critical);

            Assert.True(control.IsCriticalAlert);
            Assert.True(control.IsBreathingActive);
            Assert.Same(HudPalette.LethalRedBrush, control.MainBorder.BorderBrush);
            Assert.Same(HudPalette.LethalRedBrush, control.LethalBadgeBorder.BorderBrush);
            Assert.Same(HudPalette.LethalRedSubtleBrush, control.LethalBadgeBorder.Background);
            Assert.Same(HudPalette.LethalRedBrush, control.LethalRateTextBlock.Foreground);
            Assert.Contains("⚠️", control.LethalRateTextBlock.Text);
            Assert.Contains("40.0%", control.LethalRateTextBlock.Text);

            // 2. 战况回升解除危急警报 (P_lethal = 0.05 <= 30%)
            var safe = new CombatSimulationResult(
                winRate: 0.75,
                tieRate: 0.05,
                lossRate: 0.20,
                lethalRate: 0.05,
                averageDamageTaken: -6.0);

            control.UpdateResult(safe);

            Assert.False(control.IsCriticalAlert);
            Assert.False(control.IsBreathingActive);
            Assert.Same(HudPalette.BorderSubtleBrush, control.MainBorder.BorderBrush);
            Assert.Same(HudPalette.BorderSubtleBrush, control.LethalBadgeBorder.BorderBrush);
            Assert.Same(HudPalette.BackgroundDeepSpaceBrush, control.LethalBadgeBorder.Background);
            Assert.DoesNotContain("⚠️", control.LethalRateTextBlock.Text);
            Assert.Contains("5.0%", control.LethalRateTextBlock.Text);
            Assert.Equal(0.0, control.AlertGlowBorder.Opacity);
        });
    }

    [Fact]
    public void LethalGaugeControl_STA_FrozenBrushes_ZeroLeakageAndThreadSafety()
    {
        RunInSta(() =>
        {
            using var control = new LethalGaugeControl();

            // 验证控件所有预装载画刷均已 Freeze
            Assert.True(control.MainBorder.Background.IsFrozen);
            Assert.True(control.MainBorder.BorderBrush.IsFrozen);
            Assert.True(control.AlertGlowBorder.BorderBrush.IsFrozen);
            Assert.True(control.AlertGlowBorder.Background.IsFrozen);
            Assert.True(control.WinBar.Background.IsFrozen);
            Assert.True(control.TieBar.Background.IsFrozen);
            Assert.True(control.LossBar.Background.IsFrozen);
            Assert.True(control.LethalBar.Background.IsFrozen);
            Assert.True(control.TitleTextBlock.Foreground.IsFrozen);
            Assert.True(control.LethalRateTextBlock.Foreground.IsFrozen);
            Assert.True(control.WinRateTextBlock.Foreground.IsFrozen);
            Assert.True(control.TieRateTextBlock.Foreground.IsFrozen);
            Assert.True(control.LossRateTextBlock.Foreground.IsFrozen);
            Assert.True(control.AverageDamageTextBlock.Foreground.IsFrozen);

            // 验证 ViewModel 导出的画刷同样均已 Freeze
            var vm = control.ViewModel;
            Assert.True(vm.RiskBrush.IsFrozen);
            Assert.True(vm.WinBrush.IsFrozen);
            Assert.True(vm.TieBrush.IsFrozen);
            Assert.True(vm.LossBrush.IsFrozen);
            Assert.True(vm.LethalBrush.IsFrozen);
            Assert.True(vm.AlertBorderBrush.IsFrozen);

            // 多次动态刷新后，画刷依然保持为同一个静态冷冻实例，零堆内存泄漏
            var brushBefore = control.WinBar.Background;
            control.UpdateResult(new CombatSimulationResult(0.5, 0.1, 0.4, 0.1));
            control.UpdateResult(new CombatSimulationResult(0.8, 0.1, 0.1, 0.0));
            var brushAfter = control.WinBar.Background;

            Assert.Same(brushBefore, brushAfter);
            Assert.Same(HudPalette.SafeTempoBrush, brushAfter);
        });
    }

    [Fact]
    public void LethalGaugeControl_STA_BridgeIntegration_ReactsToSimulations()
    {
        RunInSta(() =>
        {
            using var bridge = new BobsBuddyBridge(isOfflineMode: true);
            using var control = new LethalGaugeControl();

            control.AttachToBridge(bridge);

            // 初始时同步桥接器的安全先验
            Assert.Equal("45.0%", control.ViewModel.WinRateText);

            // 模拟桥接器推送新一轮蒙特卡洛模拟
            var liveSim = new CombatSimulationResult(
                winRate: 0.65,
                tieRate: 0.10,
                lossRate: 0.25,
                lethalRate: 0.05,
                averageDamageTaken: -4.0,
                simulationsCount: 10000);

            bridge.ReportSimulationResult(liveSim);

            Assert.Equal("65.0%", control.ViewModel.WinRateText);
            Assert.Equal("-4.0", control.ViewModel.AverageDamageText);
            Assert.Equal(0.65, control.WinColumn.Width.Value, 2);

            // 分离后再次推送，控件状态不再受影响
            control.DetachFromBridge(bridge);

            var subsequentSim = new CombatSimulationResult(0.1, 0.1, 0.8, 0.5);
            bridge.ReportSimulationResult(subsequentSim);

            // 依然保持分离前的数值
            Assert.Equal("65.0%", control.ViewModel.WinRateText);
        });
    }

    [Fact]
    public void LethalGaugeControl_STA_MountToTransparentOverlay_ClickThroughVerified()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();
            using var gauge = new LethalGaugeControl();

            overlay.AddChild(gauge, left: 30, top: 40, zIndex: 10);

            Assert.Single(overlay.Children);
            Assert.Same(gauge, overlay.Children[0]);
            Assert.Equal(30, Canvas.GetLeft(gauge));
            Assert.Equal(40, Canvas.GetTop(gauge));
            Assert.Equal(10, Canvas.GetZIndex(gauge));

            // 验证点击穿透契约：整条链路均不拦截点击测试
            Assert.False(overlay.OverlayContainer.IsHitTestVisible);
            Assert.False(gauge.IsHitTestVisible);
            Assert.False(gauge.MainBorder.IsHitTestVisible);
        });
    }

    #endregion

    #region GaugeFormatter Tests

    [Fact]
    public void GaugeFormatter_ZeroGC_StaticStringCacheIntegrity()
    {
        // 验证边界值
        Assert.Equal("0.0%", GaugeFormatter.FormatPercentage(0.0));
        Assert.Equal("100.0%", GaugeFormatter.FormatPercentage(1.0));
        Assert.Equal("45.0%", GaugeFormatter.FormatPercentage(0.45));
        Assert.Equal("33.3%", GaugeFormatter.FormatPercentage(0.333));
        Assert.Equal("0.0%", GaugeFormatter.FormatPercentage(-0.1));
        Assert.Equal("100.0%", GaugeFormatter.FormatPercentage(1.5));
        Assert.Equal("0.0%", GaugeFormatter.FormatPercentage(double.NaN));

        // 验证高频字符串静态引用复用（零 GC 证明）
        string ref1 = GaugeFormatter.FormatPercentage(0.45);
        string ref2 = GaugeFormatter.FormatPercentage(0.45);
        Assert.Same(ref1, ref2);

        // 验证伤害值格式化
        Assert.Equal("0.0", GaugeFormatter.FormatDamage(0.0));
        Assert.Equal("+2.5", GaugeFormatter.FormatDamage(2.5));
        Assert.Equal("-4.2", GaugeFormatter.FormatDamage(-4.2));

        string dmgRef1 = GaugeFormatter.FormatDamage(5.0);
        string dmgRef2 = GaugeFormatter.FormatDamage(5.0);
        Assert.Same(dmgRef1, dmgRef2);
    }

    #endregion
}
