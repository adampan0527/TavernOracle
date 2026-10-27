using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TavernOracle.Presentation.Theme;
using TavernOracle.Simulation;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 轻量级战力与暴毙预警胶囊仪表盘控件 (LethalGaugeControl)。
/// 悬浮置顶或集成于 TransparentOverlayBase 之上，呈现胜率、平局率、败率以及 P_lethal 暴毙率与平均伤害。
/// 严格遵守极低时延与零 GC 优化规范，全部画刷使用 HudPalette 静态冷冻画刷 (Freeze)。
/// </summary>
public class LethalGaugeControl : UserControl, IDisposable
{
    private static readonly DoubleAnimation BreathingAnimation = CreateBreathingAnimation();

    private readonly Border _mainBorder;
    private readonly Border _alertGlowBorder;
    private readonly Border _lethalBadgeBorder;
    private readonly Border _progressBarContainer;
    private readonly Grid _barGrid;

    private readonly ColumnDefinition _winColumn;
    private readonly ColumnDefinition _tieColumn;
    private readonly ColumnDefinition _lossColumn;
    private readonly ColumnDefinition _lethalColumn;

    private readonly Border _winBar;
    private readonly Border _tieBar;
    private readonly Border _lossBar;
    private readonly Border _lethalBar;

    private readonly TextBlock _titleTextBlock;
    private readonly TextBlock _lethalRateTextBlock;
    private readonly TextBlock _winRateTextBlock;
    private readonly TextBlock _tieRateTextBlock;
    private readonly TextBlock _lossRateTextBlock;
    private readonly TextBlock _avgDamageTextBlock;

    private bool _isBreathingActive;
    private bool _isDisposed;

    #region Properties

    /// <summary>
    /// 数据模型 ViewModel
    /// </summary>
    public LethalGaugeViewModel ViewModel { get; }

    /// <summary>
    /// 最外层半透明亚克力胶囊主边框
    /// </summary>
    public Border MainBorder => _mainBorder;

    /// <summary>
    /// 暴毙微光呼吸报警发光层
    /// </summary>
    public Border AlertGlowBorder => _alertGlowBorder;

    /// <summary>
    /// 暴毙率右上角微标药丸容器
    /// </summary>
    public Border LethalBadgeBorder => _lethalBadgeBorder;

    /// <summary>
    /// 动态多段颜色进度条外框容器
    /// </summary>
    public Border ProgressBarContainer => _progressBarContainer;

    /// <summary>
    /// 进度条分段网格
    /// </summary>
    public Grid ProgressBarGrid => _barGrid;

    public ColumnDefinition WinColumn => _winColumn;
    public ColumnDefinition TieColumn => _tieColumn;
    public ColumnDefinition SafeLossColumn => _lossColumn;
    public ColumnDefinition LossColumn => _lossColumn;
    public ColumnDefinition LethalColumn => _lethalColumn;

    public Border WinBar => _winBar;
    public Border TieBar => _tieBar;
    public Border LossBar => _lossBar;
    public Border LethalBar => _lethalBar;

    public TextBlock TitleTextBlock => _titleTextBlock;
    public TextBlock LethalRateTextBlock => _lethalRateTextBlock;
    public TextBlock WinRateTextBlock => _winRateTextBlock;
    public TextBlock TieRateTextBlock => _tieRateTextBlock;
    public TextBlock LossRateTextBlock => _lossRateTextBlock;
    public TextBlock AverageDamageTextBlock => _avgDamageTextBlock;

    /// <summary>
    /// 当前是否处于暴毙危急警报态 (P_lethal > 30%)
    /// </summary>
    public bool IsCriticalAlert => ViewModel.IsCriticalAlert;

    /// <summary>
    /// 暴毙微光呼吸动效当前是否处于激活状态
    /// </summary>
    public bool IsBreathingActive => _isBreathingActive;

    public double WinRatio => ViewModel.WinBarRatio;
    public double TieRatio => ViewModel.TieBarRatio;
    public double LossRatio => ViewModel.LossBarRatio;
    public double SafeLossRatio => ViewModel.SafeLossBarRatio;
    public double LethalRatio => ViewModel.LethalBarRatio;

    #endregion

    #region Constructor

    public LethalGaugeControl(LethalGaugeViewModel? viewModel = null)
    {
        ViewModel = viewModel ?? new LethalGaugeViewModel();

        // 遵循暴雪纸笔准则与透明穿透规范：默认不可命中测试，保证玩家原生操作穿透
        IsHitTestVisible = false;
        Focusable = false;
        FontFamily = HudStyles.StandardFontFamily;
        Width = 280;

        // 1. 初始化最外层半透明亚克力胶囊边框
        _mainBorder = new Border
        {
            CornerRadius = HudStyles.PanelCornerRadius,
            Background = HudPalette.BackgroundCanvasBrush,
            BorderBrush = HudPalette.BorderSubtleBrush,
            BorderThickness = new Thickness(1.5),
            Padding = HudStyles.PanelPadding,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            IsHitTestVisible = false
        };

        // 2. 根部网格布局
        var rootGrid = new Grid { IsHitTestVisible = false };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress bar
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Metrics

        // 3. 微光呼吸红色报警层 (覆盖在整张面板上方，默认透明)
        _alertGlowBorder = new Border
        {
            CornerRadius = HudStyles.PanelCornerRadius,
            BorderBrush = HudPalette.LethalRedBrush,
            BorderThickness = new Thickness(2),
            Background = HudPalette.LethalRedSubtleBrush,
            Opacity = 0.0,
            IsHitTestVisible = false
        };
        Grid.SetRowSpan(_alertGlowBorder, 3);
        rootGrid.Children.Add(_alertGlowBorder);

        // 4. Header 区域 (标题 + 暴毙率药丸微标)
        var headerGrid = new Grid { IsHitTestVisible = false };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _titleTextBlock = new TextBlock
        {
            Text = "⚔️ 战况预测",
            FontSize = HudStyles.FontSizeNormal,
            Foreground = HudPalette.TextPrimaryBrush,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Grid.SetColumn(_titleTextBlock, 0);
        headerGrid.Children.Add(_titleTextBlock);

        _lethalBadgeBorder = new Border
        {
            CornerRadius = HudStyles.BadgeCornerRadius,
            Padding = HudStyles.BadgePadding,
            Background = HudPalette.BackgroundDeepSpaceBrush,
            BorderBrush = HudPalette.BorderSubtleBrush,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        _lethalRateTextBlock = new TextBlock
        {
            Text = "暴毙 0.0%",
            FontSize = HudStyles.FontSizeSmall,
            FontWeight = FontWeights.Bold,
            Foreground = HudPalette.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        _lethalBadgeBorder.Child = _lethalRateTextBlock;
        Grid.SetColumn(_lethalBadgeBorder, 1);
        headerGrid.Children.Add(_lethalBadgeBorder);

        Grid.SetRow(headerGrid, 0);
        rootGrid.Children.Add(headerGrid);

        // 5. 动态色彩进度条容器
        _progressBarContainer = new Border
        {
            Height = 8,
            Margin = new Thickness(0, 8, 0, 8),
            CornerRadius = new CornerRadius(4),
            Background = HudPalette.BackgroundDeepSpaceBrush,
            BorderBrush = HudPalette.BorderSubtleBrush,
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            IsHitTestVisible = false
        };

        _barGrid = new Grid { IsHitTestVisible = false };
        _winColumn = new ColumnDefinition { Width = new GridLength(ViewModel.WinBarRatio, GridUnitType.Star) };
        _tieColumn = new ColumnDefinition { Width = new GridLength(ViewModel.TieBarRatio, GridUnitType.Star) };
        _lossColumn = new ColumnDefinition { Width = new GridLength(ViewModel.SafeLossBarRatio, GridUnitType.Star) };
        _lethalColumn = new ColumnDefinition { Width = new GridLength(ViewModel.LethalBarRatio, GridUnitType.Star) };

        _barGrid.ColumnDefinitions.Add(_winColumn);
        _barGrid.ColumnDefinitions.Add(_tieColumn);
        _barGrid.ColumnDefinitions.Add(_lossColumn);
        _barGrid.ColumnDefinitions.Add(_lethalColumn);

        _winBar = new Border { Background = HudPalette.SafeTempoBrush, IsHitTestVisible = false };
        _tieBar = new Border { Background = HudPalette.TacticalBlueBrush, IsHitTestVisible = false };
        _lossBar = new Border { Background = HudPalette.LegendaryGoldBrush, IsHitTestVisible = false };
        _lethalBar = new Border { Background = HudPalette.LethalRedBrush, IsHitTestVisible = false };

        Grid.SetColumn(_winBar, 0);
        Grid.SetColumn(_tieBar, 1);
        Grid.SetColumn(_lossBar, 2);
        Grid.SetColumn(_lethalBar, 3);

        _barGrid.Children.Add(_winBar);
        _barGrid.Children.Add(_tieBar);
        _barGrid.Children.Add(_lossBar);
        _barGrid.Children.Add(_lethalBar);

        _progressBarContainer.Child = _barGrid;
        Grid.SetRow(_progressBarContainer, 1);
        rootGrid.Children.Add(_progressBarContainer);

        // 6. 底部数值行 (胜率、平局、败率、均伤)
        var metricsGrid = new Grid { IsHitTestVisible = false };
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _winRateTextBlock = new TextBlock
        {
            Text = $"胜 {ViewModel.WinRateText}",
            Foreground = HudPalette.SafeTempoBrush,
            FontSize = HudStyles.FontSizeSmall,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Grid.SetColumn(_winRateTextBlock, 0);
        metricsGrid.Children.Add(_winRateTextBlock);

        _tieRateTextBlock = new TextBlock
        {
            Text = $"平 {ViewModel.TieRateText}",
            Foreground = HudPalette.TacticalBlueBrush,
            FontSize = HudStyles.FontSizeSmall,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Grid.SetColumn(_tieRateTextBlock, 1);
        metricsGrid.Children.Add(_tieRateTextBlock);

        _lossRateTextBlock = new TextBlock
        {
            Text = $"负 {ViewModel.LossRateText}",
            Foreground = HudPalette.LegendaryGoldBrush,
            FontSize = HudStyles.FontSizeSmall,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Grid.SetColumn(_lossRateTextBlock, 2);
        metricsGrid.Children.Add(_lossRateTextBlock);

        _avgDamageTextBlock = new TextBlock
        {
            Text = $"均伤 {ViewModel.AverageDamageText}",
            Foreground = HudPalette.TextSecondaryBrush,
            FontSize = HudStyles.FontSizeSmall,
            FontWeight = FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Grid.SetColumn(_avgDamageTextBlock, 3);
        metricsGrid.Children.Add(_avgDamageTextBlock);

        Grid.SetRow(metricsGrid, 2);
        rootGrid.Children.Add(metricsGrid);

        _mainBorder.Child = rootGrid;
        Content = _mainBorder;

        // 监听 ViewModel 属性变化
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 初始应用视觉状态
        RefreshVisuals();
    }

    #endregion

    #region Update & Business Logic

    /// <summary>
    /// 直接接收 CombatSimulationResult 更新仪表盘
    /// </summary>
    public void UpdateResult(CombatSimulationResult result)
    {
        if (CheckAccess())
        {
            ViewModel.Update(in result);
        }
        else
        {
            Dispatcher.BeginInvoke(() => ViewModel.Update(in result));
        }
    }

    /// <summary>
    /// 重置仪表盘至默认安全先验
    /// </summary>
    public void Reset()
    {
        if (CheckAccess())
        {
            ViewModel.Reset();
        }
        else
        {
            Dispatcher.BeginInvoke(() => ViewModel.Reset());
        }
    }

    /// <summary>
    /// 挂载至 Bob's Buddy 桥接器
    /// </summary>
    public void AttachToBridge(IBobsBuddyBridge bridge)
    {
        ViewModel.AttachToBridge(bridge);
    }

    /// <summary>
    /// 从 Bob's Buddy 桥接器分离
    /// </summary>
    public void DetachFromBridge(IBobsBuddyBridge? bridge = null)
    {
        ViewModel.DetachFromBridge(bridge);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (CheckAccess())
        {
            RefreshVisuals();
        }
        else
        {
            Dispatcher.BeginInvoke(RefreshVisuals);
        }
    }

    /// <summary>
    /// 刷新控件全部视觉状态（零 GC 极速更新）
    /// </summary>
    public void RefreshVisuals()
    {
        // 1. 更新文本
        _winRateTextBlock.Text = $"胜 {ViewModel.WinRateText}";
        _tieRateTextBlock.Text = $"平 {ViewModel.TieRateText}";
        _lossRateTextBlock.Text = $"负 {ViewModel.LossRateText}";
        _avgDamageTextBlock.Text = $"均伤 {ViewModel.AverageDamageText}";

        // 2. 更新进度条比例
        double win = ViewModel.WinBarRatio;
        double tie = ViewModel.TieBarRatio;
        double safeLoss = ViewModel.SafeLossBarRatio;
        double lethal = ViewModel.LethalBarRatio;

        if (win + tie + safeLoss + lethal <= 0.0001)
        {
            win = 0.45;
            tie = 0.10;
            safeLoss = 0.35;
            lethal = 0.10;
        }

        _winColumn.Width = new GridLength(win, GridUnitType.Star);
        _tieColumn.Width = new GridLength(tie, GridUnitType.Star);
        _lossColumn.Width = new GridLength(safeLoss, GridUnitType.Star);
        _lethalColumn.Width = new GridLength(lethal, GridUnitType.Star);

        // 3. 暴毙危急警报态切换 (P_lethal > 30%)
        if (ViewModel.IsCriticalAlert)
        {
            _lethalRateTextBlock.Text = $"⚠️ 暴毙 {ViewModel.LethalRateText}";
            _lethalRateTextBlock.Foreground = HudPalette.LethalRedBrush;
            _lethalBadgeBorder.BorderBrush = HudPalette.LethalRedBrush;
            _lethalBadgeBorder.Background = HudPalette.LethalRedSubtleBrush;
            _mainBorder.BorderBrush = HudPalette.LethalRedBrush;

            StartBreathingAnimation();
        }
        else
        {
            _lethalRateTextBlock.Text = $"暴毙 {ViewModel.LethalRateText}";
            _lethalRateTextBlock.Foreground = ViewModel.RiskBrush;
            _lethalBadgeBorder.BorderBrush = HudPalette.BorderSubtleBrush;
            _lethalBadgeBorder.Background = HudPalette.BackgroundDeepSpaceBrush;
            _mainBorder.BorderBrush = HudPalette.BorderSubtleBrush;

            StopBreathingAnimation();
        }
    }

    #endregion

    #region Breathing Animation & Lifetime

    private static DoubleAnimation CreateBreathingAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = 0.25,
            To = 0.85,
            Duration = TimeSpan.FromMilliseconds(750),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        animation.Freeze();
        return animation;
    }

    private void StartBreathingAnimation()
    {
        if (_isBreathingActive) return;
        _isBreathingActive = true;

        _alertGlowBorder.BeginAnimation(OpacityProperty, BreathingAnimation);
    }

    private void StopBreathingAnimation()
    {
        if (!_isBreathingActive) return;
        _isBreathingActive = false;

        _alertGlowBorder.BeginAnimation(OpacityProperty, null);
        _alertGlowBorder.Opacity = 0.0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        StopBreathingAnimation();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
    }

    #endregion
}
