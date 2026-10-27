using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TavernOracle.Decision;
using TavernOracle.Presentation.Controls;
using TavernOracle.Presentation.Overlay;
using TavernOracle.Presentation.Theme;
using Xunit;

namespace TavernOracle.Tests.Presentation;

public class DecisionBannerTests
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

    #region Default Properties & Pencil & Paper Rule

    [Fact]
    public void DecisionBanner_DefaultState_MatchesSpecifications()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();

            // 纸笔准则与合规：绝对穿透，不拦截鼠标与键盘
            Assert.False(banner.IsHitTestVisible);
            Assert.False(banner.Focusable);

            // 默认对齐与样式
            Assert.Equal(HorizontalAlignment.Center, banner.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, banner.VerticalAlignment);

            // 磨砂亚克力半透明深空背景 (rgba(22, 27, 34, 0.88))
            Assert.Same(HudPalette.BackgroundCanvasBrush, banner.OuterBorder.Background);
            Assert.Equal(HudStyles.BannerCornerRadius, banner.OuterBorder.CornerRadius);
            Assert.Equal(HudStyles.BannerPadding, banner.OuterBorder.Padding);

            // 默认状态为 🟢 Normal (节奏稳健)
            Assert.Equal(BannerAlertLevel.Normal, banner.AlertLevel);
            Assert.Equal("【本回合推荐】", banner.Headline);
            Assert.Empty(banner.Message);
            Assert.Null(banner.Detail);

            // 微标默认呈现
            Assert.Contains("🟢", banner.BadgeTextBlock.Text);
            Assert.Same(HudPalette.SafeTempoBrush, banner.BadgeBorder.BorderBrush);
            Assert.Same(HudPalette.BorderSubtleBrush, banner.OuterBorder.BorderBrush);
        });
    }

    #endregion

    #region Alert Level Visual State Switching

    [Fact]
    public void DecisionBanner_AlertLevelSwitching_Normal_Warning_Critical()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();

            // 1. 🟢 Normal 状态
            banner.AlertLevel = BannerAlertLevel.Normal;
            Assert.Equal("🟢 稳健节奏", banner.BadgeTextBlock.Text);
            Assert.Same(HudPalette.SafeTempoBrush, banner.BadgeBorder.BorderBrush);
            Assert.Same(HudPalette.SafeTempoBrush, banner.BadgeTextBlock.Foreground);
            Assert.Same(HudPalette.BorderSubtleBrush, banner.OuterBorder.BorderBrush);
            Assert.Same(HudPalette.TextPrimaryBrush, banner.HeadlineTextBlock.Foreground);

            // 2. 🟡 Warning 状态
            banner.AlertLevel = BannerAlertLevel.Warning;
            Assert.Equal("🟡 战力警戒", banner.BadgeTextBlock.Text);
            Assert.Same(HudPalette.LegendaryGoldBrush, banner.BadgeBorder.BorderBrush);
            Assert.Same(HudPalette.LegendaryGoldBrush, banner.BadgeTextBlock.Foreground);
            Assert.Same(HudPalette.LegendaryGoldBrush, banner.OuterBorder.BorderBrush);
            Assert.Same(HudPalette.LegendaryGoldBrush, banner.HeadlineTextBlock.Foreground);

            // 3. 🔴 Critical 状态
            banner.AlertLevel = BannerAlertLevel.Critical;
            Assert.Equal("🔴 暴毙危机", banner.BadgeTextBlock.Text);
            Assert.Same(HudPalette.LethalRedBrush, banner.BadgeBorder.BorderBrush);
            Assert.Same(HudPalette.LethalRedBrush, banner.BadgeTextBlock.Foreground);
            Assert.Same(HudPalette.LethalRedBrush, banner.OuterBorder.BorderBrush);
            Assert.Same(HudPalette.LethalRedBrush, banner.HeadlineTextBlock.Foreground);
        });
    }

    #endregion

    #region Dynamic Text & Risk Updates

    [Fact]
    public void DecisionBanner_Update_ReflectsNewTextsAndVisibility()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();

            banner.Update(
                message: "升至 4 本并冻结跳蛙",
                alertLevel: BannerAlertLevel.Normal,
                headline: "【本回合推荐】",
                detail: "当前胜率 78%，留对子为佳");

            Assert.Equal("升至 4 本并冻结跳蛙", banner.MessageTextBlock.Text);
            Assert.Equal("【本回合推荐】", banner.HeadlineTextBlock.Text);
            Assert.Equal("(当前胜率 78%，留对子为佳)", banner.DetailTextBlock.Text);
            Assert.Equal(Visibility.Visible, banner.DetailTextBlock.Visibility);

            // 清空细节时 DetailTextBlock 折叠
            banner.Update("快速过费", BannerAlertLevel.Normal, detail: null);
            Assert.Equal(string.Empty, banner.DetailTextBlock.Text);
            Assert.Equal(Visibility.Collapsed, banner.DetailTextBlock.Visibility);
        });
    }

    [Fact]
    public void DecisionBanner_UpdateFromRisk_CalculatesAlertLevelAccurately()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();

            // 1. 低风险 (5% 暴毙率) -> Normal
            banner.UpdateFromRisk(0.05, "升至 4 本并冻结跳蛙");
            Assert.Equal(BannerAlertLevel.Normal, banner.AlertLevel);
            Assert.Equal("【本回合推荐】", banner.Headline);
            Assert.Equal(Visibility.Visible, banner.StatsPillBorder.Visibility);
            Assert.Equal("暴毙率: 5%", banner.StatsPillTextBlock.Text);
            Assert.Same(HudPalette.SafeTempoBrush, banner.StatsPillTextBlock.Foreground);

            // 2. 中风险 (20% 暴毙率) -> Warning
            banner.UpdateFromRisk(0.20, "停止盲目刷新，留对子控场");
            Assert.Equal(BannerAlertLevel.Warning, banner.AlertLevel);
            Assert.Equal("【战力预警】", banner.Headline);
            Assert.Equal("暴毙率: 20%", banner.StatsPillTextBlock.Text);
            Assert.Same(HudPalette.LegendaryGoldBrush, banner.StatsPillTextBlock.Foreground);

            // 3. 极高风险 (35% 暴毙率, P_lethal > 30%) -> Critical
            banner.UpdateFromRisk(0.35, "绝地保命：切勿刷新，存铸币防暴毙");
            Assert.Equal(BannerAlertLevel.Critical, banner.AlertLevel);
            Assert.Equal("【绝地保命】", banner.Headline);
            Assert.Equal("暴毙率: 35%", banner.StatsPillTextBlock.Text);
            Assert.Same(HudPalette.LethalRedBrush, banner.StatsPillTextBlock.Foreground);
        });
    }

    [Fact]
    public void DecisionBanner_WinRatePill_RendersWhenLethalRateIsZero()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();

            banner.Update("优势巨大，安心升本", BannerAlertLevel.Normal, lethalRate: 0.0, winRate: 0.88);

            Assert.Equal(Visibility.Visible, banner.StatsPillBorder.Visibility);
            Assert.Equal("胜率: 88%", banner.StatsPillTextBlock.Text);
            Assert.Same(HudPalette.SafeTempoBrush, banner.StatsPillTextBlock.Foreground);
        });
    }

    #endregion

    #region ViewModel Integration & INotifyPropertyChanged

    [Fact]
    public void DecisionBannerViewModel_Mutations_TriggerPropertyChanged()
    {
        var vm = new DecisionBannerViewModel();
        var changedProps = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProps.Add(e.PropertyName);
            }
        };

        vm.AlertLevel = BannerAlertLevel.Warning;
        Assert.Equal(BannerAlertLevel.Warning, vm.AlertLevel);
        Assert.Same(HudPalette.LegendaryGoldBrush, vm.AccentBrush);
        Assert.Equal("🟡 战力警戒", vm.BadgeText);
        Assert.Contains(nameof(vm.AlertLevel), changedProps);
        Assert.Contains(nameof(vm.AccentBrush), changedProps);
        Assert.Contains(nameof(vm.BadgeText), changedProps);

        changedProps.Clear();
        vm.Message = "购买圣盾随从";
        Assert.Contains("Message", changedProps);
        Assert.Equal("购买圣盾随从", vm.Message);

        changedProps.Clear();
        vm.Detail = "提升即时战力";
        Assert.Contains("Detail", changedProps);
        Assert.Contains("HasDetail", changedProps);
        Assert.True(vm.HasDetail);

        Assert.Contains("购买圣盾随从", vm.FormattedSummary);
        Assert.Contains("提升即时战力", vm.FormattedSummary);

        // 重置
        vm.Reset();
        Assert.Equal(BannerAlertLevel.Normal, vm.AlertLevel);
        Assert.Empty(vm.Message);
        Assert.Null(vm.Detail);
    }

    [Fact]
    public void DecisionBanner_ViewModelBinding_SynchronizesAutomatically()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();
            var vm = new DecisionBannerViewModel();

            banner.ViewModel = vm;
            Assert.Same(vm, banner.DataContext);

            // 驱动 ViewModel 发生变化
            vm.UpdateFromRisk(0.32, "绝境突变：全买战力牌", detail: "切勿贪婪");

            Assert.Equal(BannerAlertLevel.Critical, banner.AlertLevel);
            Assert.Equal("绝境突变：全买战力牌", banner.MessageTextBlock.Text);
            Assert.Equal("【绝地保命】", banner.HeadlineTextBlock.Text);
            Assert.Equal("(切勿贪婪)", banner.DetailTextBlock.Text);
            Assert.Equal("暴毙率: 32%", banner.StatsPillTextBlock.Text);
            Assert.Same(HudPalette.LethalRedBrush, banner.OuterBorder.BorderBrush);

            // 测试 ViewModel 隐藏
            vm.IsVisible = false;
            Assert.Equal(Visibility.Collapsed, banner.Visibility);

            vm.IsVisible = true;
            Assert.Equal(Visibility.Visible, banner.Visibility);
        });
    }

    #endregion

    #region Responsive Layout & Size Calculation

    [Fact]
    public void DecisionBanner_AdaptToViewportWidth_LimitsBannerMaxWidth()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();

            // 1080P 视口 (1920)
            banner.AdaptToViewportWidth(1920);
            Assert.True(banner.MaxWidth <= 960);
            Assert.True(banner.MaxWidth >= 800);

            // 720P 视口 (1280)
            banner.AdaptToViewportWidth(1280);
            Assert.Equal(1280 * 0.75, banner.MaxWidth);
            Assert.True(banner.ContentViewbox.MaxWidth < banner.MaxWidth);

            // 狭窄视口 (400)
            banner.AdaptToViewportWidth(400);
            Assert.Equal(320, banner.MaxWidth); // 最小兜底 320
        });
    }

    [Fact]
    public void DecisionBanner_LongText_WrapsAtReadableSizeWithoutScaling()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();
            banner.AdaptToViewportWidth(800); // MaxWidth = 600

            string extremelyLongMessage = new string('宏', 120);
            banner.Update(extremelyLongMessage, BannerAlertLevel.Normal, detail: "补充说明也十分冗长冗长冗长冗长");

            // 触发 WPF 测量与布局计算
            banner.Measure(new Size(800, 600));
            banner.Arrange(new Rect(0, 0, banner.DesiredSize.Width, banner.DesiredSize.Height));

            // 必须严格受控于 MaxWidth，绝不溢出屏幕两翼
            Assert.True(banner.DesiredSize.Width <= 600.01,
                $"DesiredSize.Width ({banner.DesiredSize.Width}) 应不超过 MaxWidth (600)");
            Assert.Equal(TextWrapping.Wrap, banner.MessageTextBlock.TextWrapping);
            Assert.Equal(24, banner.MessageTextBlock.FontSize);
            Assert.Equal(Stretch.None, banner.ContentViewbox.Stretch);
            Assert.Equal(Orientation.Vertical, banner.TextStackPanel.Orientation);
        });
    }

    [Fact]
    public void DecisionBanner_UpdatePosition_CentersHorizontallyInContainer()
    {
        RunInSta(() =>
        {
            var banner = new DecisionBannerControl();
            banner.Update("升至 4 本并冻结跳蛙", BannerAlertLevel.Normal);

            double containerWidth = 1920;
            double topOffset = 24.0;

            banner.UpdatePosition(containerWidth, topOffset);

            double left = Canvas.GetLeft(banner);
            double top = Canvas.GetTop(banner);

            Assert.Equal(topOffset, top);
            Assert.True(left > 0, "居中 left 坐标应为正数");
            Assert.True(left < containerWidth / 2.0, "居中 left 坐标应处于左半区向中对齐");
        });
    }

    #endregion

    #region Integration with TransparentOverlayBase

    [Fact]
    public void DecisionBanner_CanBeMountedIntoTransparentOverlay()
    {
        RunInSta(() =>
        {
            using var overlay = new TransparentOverlayBase();
            var banner = new DecisionBannerControl();

            overlay.SetBounds(0, 0, 1920, 1080);
            banner.Update("绝地保命：切勿刷新，存铸币防暴毙", BannerAlertLevel.Critical);

            overlay.AddChild(banner, 560, 20, 10);

            Assert.Single(overlay.Children);
            Assert.Contains(banner, overlay.Children);
            Assert.Contains(banner, overlay.OverlayContainer.Children.Cast<UIElement>());

            Assert.Equal(560, Canvas.GetLeft(banner));
            Assert.Equal(20, Canvas.GetTop(banner));
            Assert.Equal(10, Canvas.GetZIndex(banner));

            // 验证穿透合规与无输入拦截
            Assert.False(banner.IsHitTestVisible);
            Assert.False(overlay.OverlayContainer.IsHitTestVisible);
        });
    }

    #endregion

    #region Zero GC & Design Tokens

    [Fact]
    public void DecisionBanner_AllBrushes_AreFrozenForZeroGc()
    {
        // 验证告警级别使用的所有微标背景画刷都已冷冻
        Assert.True(BannerAlertLevelExtensions.BadgeNormalBgBrush.IsFrozen);
        Assert.True(BannerAlertLevelExtensions.BadgeWarningBgBrush.IsFrozen);
        Assert.True(BannerAlertLevelExtensions.BadgeCriticalBgBrush.IsFrozen);

        // 验证不同告警级别高光画刷已冷冻
        Assert.True(BannerAlertLevel.Normal.GetAccentBrush().IsFrozen);
        Assert.True(BannerAlertLevel.Warning.GetAccentBrush().IsFrozen);
        Assert.True(BannerAlertLevel.Critical.GetAccentBrush().IsFrozen);
    }

    [Fact]
    public void BannerAlertLevel_FromRiskTier_ConvertsProperly()
    {
        Assert.Equal(BannerAlertLevel.Normal, BannerAlertLevelExtensions.FromRiskTier(RiskTier.Safe));
        Assert.Equal(BannerAlertLevel.Normal, BannerAlertLevelExtensions.FromRiskTier(RiskTier.Low));
        Assert.Equal(BannerAlertLevel.Warning, BannerAlertLevelExtensions.FromRiskTier(RiskTier.Warning));
        Assert.Equal(BannerAlertLevel.Critical, BannerAlertLevelExtensions.FromRiskTier(RiskTier.Critical));
        Assert.Equal(BannerAlertLevel.Critical, BannerAlertLevelExtensions.FromRiskTier(RiskTier.Lethal));
    }

    #endregion
}
