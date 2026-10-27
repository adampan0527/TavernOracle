using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TavernOracle.Presentation.Controls;
using TavernOracle.Presentation.Theme;
using Xunit;

namespace TavernOracle.Tests.Presentation;

/// <summary>
/// 酒馆卡面头顶微标控件 (MinionBadgeControl) 单元测试与视觉排版规范验收套件。
/// 对齐 docs/dev_specifications.md 第 3.2 节随从微标系统规范。
/// </summary>
public class MinionBadgeTests
{
    /// <summary>
    /// 在 STA 单元线程中执行 WPF 控件与视觉树相关测试
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

    #region BadgeType & MinionBadgeViewModel Tests

    [Fact]
    public void BadgeType_EnumValues_MatchSpecificationContract()
    {
        Assert.Equal(0, (int)BadgeType.TriplePair);
        Assert.Equal(1, (int)BadgeType.KeyEngine);
        Assert.Equal(2, (int)BadgeType.TempoFix);
        Assert.Equal(3, (int)BadgeType.TrapWarning);
    }

    [Fact]
    public void MinionBadgeViewModel_FactoryMethods_InitializeExpectedProperties()
    {
        // 1. 🔥 三连对子
        var tripleVm = MinionBadgeViewModel.CreateTriplePair("对子", "凑齐三连", enableGlow: true);
        Assert.Equal(BadgeType.TriplePair, tripleVm.BadgeType);
        Assert.Equal("对子", tripleVm.Title);
        Assert.Equal("凑齐三连", tripleVm.Subtitle);
        Assert.True(tripleVm.EnableGlowPulse);
        Assert.Equal(1.0, tripleVm.Opacity);
        Assert.Equal("🔥", tripleVm.DisplayIcon);
        Assert.Null(tripleVm.DisplayScore);

        // 2. ★ 流派质变怪
        var keyEngineVm = MinionBadgeViewModel.CreateKeyEngine("跳蛙核心", synergyScore: 8.5, subtitle: "亡语狂暴");
        Assert.Equal(BadgeType.KeyEngine, keyEngineVm.BadgeType);
        Assert.Equal("跳蛙核心", keyEngineVm.Title);
        Assert.Equal("亡语狂暴", keyEngineVm.Subtitle);
        Assert.Equal(8.5, keyEngineVm.SynergyScore);
        Assert.False(keyEngineVm.EnableGlowPulse);
        Assert.Equal("★", keyEngineVm.DisplayIcon);
        Assert.Equal("+8.5", keyEngineVm.DisplayScore);

        // 3. 🛡️ 即时保命单卡
        var tempoVm = MinionBadgeViewModel.CreateTempoFix("圣盾狂暴", "即时战力", tempoScore: 6.0);
        Assert.Equal(BadgeType.TempoFix, tempoVm.BadgeType);
        Assert.Equal("圣盾狂暴", tempoVm.Title);
        Assert.Equal("即时战力", tempoVm.Subtitle);
        Assert.Equal(6.0, tempoVm.TempoScore);
        Assert.Equal("🛡️", tempoVm.DisplayIcon);
        Assert.Equal("+6.0", tempoVm.DisplayScore);

        // 4. ⛔ 避坑预警
        var trapVm = MinionBadgeViewModel.CreateTrapWarning("避坑预警", "负收益陷阱", penaltyScore: -2.5);
        Assert.Equal(BadgeType.TrapWarning, trapVm.BadgeType);
        Assert.Equal("避坑预警", trapVm.Title);
        Assert.Equal("负收益陷阱", trapVm.Subtitle);
        Assert.Equal(0.80, trapVm.Opacity);
        Assert.Equal("⛔", trapVm.DisplayIcon);
    }

    [Fact]
    public void MinionBadgeViewModel_INotifyPropertyChanged_FiresProperly()
    {
        var vm = new MinionBadgeViewModel();
        string? lastChangedProp = null;
        vm.PropertyChanged += (s, e) => lastChangedProp = e.PropertyName;

        vm.Title = "测试标题";
        Assert.Equal(nameof(MinionBadgeViewModel.Title), lastChangedProp);

        vm.SynergyScore = 10.0;
        Assert.Equal(nameof(MinionBadgeViewModel.DisplayScore), lastChangedProp);

        vm.CustomIcon = "⭐";
        Assert.Equal(nameof(MinionBadgeViewModel.DisplayIcon), lastChangedProp);
    }

    #endregion

    #region MinionBadgeControl Visual & Styling Tests (STA)

    [Fact]
    public void MinionBadgeControl_STA_TriplePair_AppliesGoldenGradientAndGlowPulse()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            var vm = MinionBadgeViewModel.CreateTriplePair("三连对子", enableGlow: true);
            control.ViewModel = vm;

            // 验证边框应用了冻结金色渐变画刷
            Assert.Same(HudPalette.LegendaryGoldGradientBrush, control.RootBorder.BorderBrush);
            Assert.True(control.RootBorder.BorderBrush.IsFrozen);

            // 验证背景为冻结画刷
            Assert.True(control.RootBorder.Background.IsFrozen);

            // 验证图标与标题文字
            Assert.Equal("🔥", control.IconBlock.Text);
            Assert.Equal("三连对子", control.TitleBlock.Text);
            Assert.Same(HudPalette.LegendaryGoldLightBrush, control.TitleBlock.Foreground);
            Assert.True(control.TitleBlock.Foreground.IsFrozen);

            // 验证发光投影效果与呼吸动效已开启
            Assert.NotNull(control.DropShadow);
            Assert.Equal(HudPalette.ColorLegendaryGold, control.DropShadow.Color);
            Assert.True(control.IsPulseActive);

            // 验证陷阱斜杠处于隐藏状态
            Assert.Equal(Visibility.Collapsed, control.TrapSlashLine.Visibility);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_KeyEngine_AppliesArcanePurpleAndSynergyScore()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            var vm = MinionBadgeViewModel.CreateKeyEngine("跳蛙领头", synergyScore: 12.5);
            control.ViewModel = vm;

            // 验证边框为魔能紫冻结画刷
            Assert.Same(HudPalette.ArcanePurpleBrush, control.RootBorder.BorderBrush);
            Assert.True(control.RootBorder.BorderBrush.IsFrozen);

            // 验证图标与标题
            Assert.Equal("★", control.IconBlock.Text);
            Assert.Equal("跳蛙领头", control.TitleBlock.Text);

            // 验证协同得分药丸显示与颜色
            Assert.Equal(Visibility.Visible, control.ScorePill.Visibility);
            Assert.Equal("+12.5", control.ScoreBlock.Text);
            Assert.Same(HudPalette.ArcanePurpleBrush, control.ScorePill.BorderBrush);
            Assert.True(control.ScorePill.BorderBrush.IsFrozen);

            // 验证阴影为魔能紫且未开启对子呼吸动效
            Assert.NotNull(control.DropShadow);
            Assert.Equal(HudPalette.ColorArcanePurple, control.DropShadow.Color);
            Assert.False(control.IsPulseActive);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_TempoFix_AppliesSafeTempoGreenAndShieldIcon()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            var vm = MinionBadgeViewModel.CreateTempoFix("圣盾巨怪", tempoScore: 7.0);
            control.ViewModel = vm;

            // 验证翡翠绿边框与冻结状态
            Assert.Same(HudPalette.SafeTempoBrush, control.RootBorder.BorderBrush);
            Assert.True(control.RootBorder.BorderBrush.IsFrozen);

            // 验证盾牌 Emoji 与战力药丸
            Assert.Equal("🛡️", control.IconBlock.Text);
            Assert.Equal("圣盾巨怪", control.TitleBlock.Text);
            Assert.Equal(Visibility.Visible, control.ScorePill.Visibility);
            Assert.Equal("+7.0", control.ScoreBlock.Text);
            Assert.Same(HudPalette.SafeTempoBrush, control.ScorePill.BorderBrush);
            Assert.True(control.ScorePill.BorderBrush.IsFrozen);

            // 验证绿色微光
            Assert.NotNull(control.DropShadow);
            Assert.Equal(HudPalette.ColorSafeTempo, control.DropShadow.Color);
            Assert.False(control.IsPulseActive);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_TrapWarning_AppliesDimmedAndSlashLine()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            var vm = MinionBadgeViewModel.CreateTrapWarning("避坑预警", "负收益陷阱");
            control.ViewModel = vm;

            // 验证暴毙警报红边框
            Assert.Same(HudPalette.LethalRedBrush, control.RootBorder.BorderBrush);
            Assert.True(control.RootBorder.BorderBrush.IsFrozen);

            // 验证半透明下沉透明度 (<= 0.85)
            Assert.True(control.RootBorder.Opacity <= 0.85);

            // 验证禁行标志 Emoji
            Assert.Equal("⛔", control.IconBlock.Text);
            Assert.Equal("避坑预警", control.TitleBlock.Text);

            // 验证红色细斜杠警示线处于可见状态
            Assert.Equal(Visibility.Visible, control.TrapSlashLine.Visibility);
            Assert.Same(HudPalette.LethalRedBrush, control.TrapSlashLine.Stroke);

            // 验证未挂载 DropShadow 发光
            Assert.Null(control.RootBorder.Effect);
            Assert.False(control.IsPulseActive);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_DirectProperties_WithoutViewModel_RendersProperly()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl
            {
                BadgeType = BadgeType.KeyEngine,
                Title = "独立设置",
                ScoreText = "+4.5",
                IconText = "🌟"
            };

            Assert.Equal("独立设置", control.TitleBlock.Text);
            Assert.Equal("+4.5", control.ScoreBlock.Text);
            Assert.Equal("🌟", control.IconBlock.Text);
            Assert.Same(HudPalette.ArcanePurpleBrush, control.RootBorder.BorderBrush);
            Assert.Equal(Visibility.Visible, control.ScorePill.Visibility);
        });
    }

    #endregion

    #region Layout, Anchoring & Text Overflow Protection Tests (STA)

    [Fact]
    public void MinionBadgeControl_STA_TextTrimming_ProtectsFromOverflow()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            string extremelyLongTitle = "这是一个超级超级冗长、绝对不能撑破酒馆卡面微标边框的极端超长随从标题文本测试字符串，用于检验TextTrimming字符截断保护";

            control.ViewModel = new MinionBadgeViewModel
            {
                BadgeType = BadgeType.KeyEngine,
                Title = extremelyLongTitle,
                SynergyScore = 99.9
            };

            // 验证文本控件具有 CharacterEllipsis 与 NoWrap
            Assert.Equal(TextTrimming.CharacterEllipsis, control.TitleBlock.TextTrimming);
            Assert.Equal(TextWrapping.NoWrap, control.TitleBlock.TextWrapping);
            Assert.Equal(MinionBadgeControl.MaxTitleWidth, control.TitleBlock.MaxWidth);

            // 执行 WPF 尺寸测算
            control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // 验证控件最大宽度被严格约束在合理区间（例如不超过 220px），绝不会无限膨胀
            Assert.True(control.DesiredSize.Width > 0);
            Assert.True(control.DesiredSize.Width <= 300.0);
            Assert.True(control.DesiredSize.Height > 0);
            Assert.True(control.DesiredSize.Height <= 50.0);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_AnchorAboveMinion_CenteringAndOffset()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            control.ViewModel = MinionBadgeViewModel.CreateTriplePair("对子");

            control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double badgeWidth = control.DesiredSize.Width;
            double badgeHeight = control.DesiredSize.Height;
            Assert.True(badgeWidth > 0);
            Assert.True(badgeHeight > 0);

            // 模拟酒馆随从卡面坐标：X=200, Y=300, Width=140, Height=190
            var cardBounds = new Rect(200, 300, 140, 190);
            double expectedCardCenterX = 200 + 140 / 2.0; // 270

            // 锚定于随从卡面上方 15px 处
            control.AnchorToMinion(cardBounds, offset: 15.0);

            double left = Canvas.GetLeft(control);
            double top = Canvas.GetTop(control);

            // 验证水平严格居中
            double badgeCenterX = left + badgeWidth / 2.0;
            Assert.Equal(expectedCardCenterX, badgeCenterX, 2);

            // 验证纵向微标底部与随从顶部正好相隔 15px
            double badgeBottom = top + badgeHeight;
            Assert.Equal(cardBounds.Y - 15.0, badgeBottom, 2);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_DoesNotOccludeBottomStats_Guaranteed()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            control.ViewModel = MinionBadgeViewModel.CreateTriplePair("三连");

            // 模拟不同分辨率下的标准随从卡面
            var card1 = new Rect(100, 200, 120, 160);
            var card2 = new Rect(350, 400, 180, 240);

            // 验证微标绝不遮挡底部攻防身材区域
            Assert.True(control.DoesNotOccludeBottomStats(card1));
            Assert.True(control.DoesNotOccludeBottomStats(card2));

            // 获取锚定后的完整矩形
            Rect anchoredRect = control.GetAnchoredBounds(card1, offset: 15.0);

            // 验证微标底边严格小于随从卡面的顶部 (anchoredRect.Bottom <= card.Top)
            Assert.True(anchoredRect.Bottom <= card1.Top);

            // 底部身材通常位于卡面最下方 40px
            double statsTop = card1.Bottom - 40;
            Assert.True(anchoredRect.Bottom < statsTop);
            Assert.False(anchoredRect.IntersectsWith(new Rect(card1.X, statsTop, card1.Width, 40)));
        });
    }

    #endregion

    #region Performance, Compliance & Zero Memory Leak Tests

    [Fact]
    public void MinionBadgeControl_STA_HitTestInvisible_EnsuresComplianceClickThrough()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();

            // 暴雪“纸笔准则”红线：微标不可被鼠标命中拦截
            Assert.False(control.IsHitTestVisible);
            Assert.False(control.Focusable);
        });
    }

    [Fact]
    public void MinionBadgeControl_STA_AllStaticBrushesAreFrozen()
    {
        // 验证调色板中所有微标专用画刷均已冷冻，杜绝每帧 GC 堆分配
        Assert.True(HudPalette.LegendaryGoldGradientBrush.IsFrozen);
        Assert.True(HudPalette.BadgePillBackgroundBrush.IsFrozen);
        Assert.True(HudPalette.TrapMutedBackgroundBrush.IsFrozen);
        Assert.True(HudPalette.TrapMutedBorderBrush.IsFrozen);
        Assert.True(HudPalette.BadgePurpleTintBackgroundBrush.IsFrozen);
        Assert.True(HudPalette.BadgeGreenTintBackgroundBrush.IsFrozen);
        Assert.True(HudPalette.BackgroundCanvasBrush.IsFrozen);
        Assert.True(HudPalette.ArcanePurpleBrush.IsFrozen);
        Assert.True(HudPalette.SafeTempoBrush.IsFrozen);
        Assert.True(HudPalette.LethalRedBrush.IsFrozen);
    }

    [Fact]
    public void MinionBadgeControl_STA_GlowPulse_LifecycleControl()
    {
        RunInSta(() =>
        {
            var control = new MinionBadgeControl();
            control.ViewModel = MinionBadgeViewModel.CreateTriplePair("对子", enableGlow: true);

            // 启动动效
            control.StartGlowPulse();
            Assert.True(control.IsPulseActive);

            // 停止动效
            control.StopGlowPulse();
            Assert.False(control.IsPulseActive);

            // 再次启动
            control.StartGlowPulse();
            Assert.True(control.IsPulseActive);

            // 释放清理
            control.Dispose();
            Assert.False(control.IsPulseActive);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDisposeControl()
    {
        var control = new MinionBadgeControl();
        var vm = MinionBadgeViewModel.CreateTriplePair("对子测试");
        control.ViewModel = vm;
        control.StartGlowPulse();
        control.Dispose();

        return new WeakReference(control);
    }

    [Fact]
    public void MinionBadgeControl_STA_MemoryZeroLeak_WeakReferenceCollected()
    {
        RunInSta(() =>
        {
            WeakReference weakRef = CreateAndDisposeControl();

            // 触发垃圾回收并等待终结器执行
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weakRef.IsAlive, "MinionBadgeControl 销毁后未被完全垃圾回收，存在内存泄漏风险！");
        });
    }

    #endregion
}
