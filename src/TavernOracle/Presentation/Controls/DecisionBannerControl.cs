using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TavernOracle.Presentation.Theme;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 屏幕顶部大局决策横条控件 (Top Recommendation Banner)。
/// 遵循暴雪“纸笔准则（Pencil &amp; Paper Rule）”与 COMP005 规范：
/// 1. 采用透明穿透（IsHitTestVisible = false），完全绝不阻挡玩家原生点击与出牌操作；
/// 2. 居中胶囊磨砂亚克力视觉风格（rgba(22, 27, 34, 0.88)）；
/// 3. 动态响应式缩放（Viewbox + TextTrimming），绝不超出屏幕两翼；
/// 4. 支持 🟢 Normal (节奏) / 🟡 Warning (警戒) / 🔴 Critical (绝境求生) 视觉自适应切换；
/// 5. 全面使用 HudPalette 预置冷冻画刷，实现零堆内存分配。
/// </summary>
public class DecisionBannerControl : ContentControl
{
    #region UI Visual Elements

    private readonly Border _outerBorder;
    private readonly Viewbox _viewbox;
    private readonly Grid _layoutGrid;
    private readonly Border _badgeBorder;
    private readonly TextBlock _badgeTextBlock;
    private readonly Border _separator;
    private readonly StackPanel _textStackPanel;
    private readonly TextBlock _headlineTextBlock;
    private readonly TextBlock _messageTextBlock;
    private readonly TextBlock _detailTextBlock;
    private readonly Border _statsPillBorder;
    private readonly TextBlock _statsPillTextBlock;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(DecisionBannerViewModel),
            typeof(DecisionBannerControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty AlertLevelProperty =
        DependencyProperty.Register(
            nameof(AlertLevel),
            typeof(BannerAlertLevel),
            typeof(DecisionBannerControl),
            new PropertyMetadata(BannerAlertLevel.Normal, OnVisualPropertyChanged));

    public static readonly DependencyProperty HeadlineProperty =
        DependencyProperty.Register(
            nameof(Headline),
            typeof(string),
            typeof(DecisionBannerControl),
            new PropertyMetadata("【本回合推荐】", OnVisualPropertyChanged));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(DecisionBannerControl),
            new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(
            nameof(Detail),
            typeof(string),
            typeof(DecisionBannerControl),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty LethalRateProperty =
        DependencyProperty.Register(
            nameof(LethalRate),
            typeof(double),
            typeof(DecisionBannerControl),
            new PropertyMetadata(0.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty WinRateProperty =
        DependencyProperty.Register(
            nameof(WinRate),
            typeof(double?),
            typeof(DecisionBannerControl),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty MaxBannerWidthProperty =
        DependencyProperty.Register(
            nameof(MaxBannerWidth),
            typeof(double),
            typeof(DecisionBannerControl),
            new PropertyMetadata(800.0, OnMaxBannerWidthChanged));

    #endregion

    #region CLR Properties

    /// <summary>
    /// 数据模型 ViewModel
    /// </summary>
    public DecisionBannerViewModel? ViewModel
    {
        get => (DecisionBannerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// 当前告警级别
    /// </summary>
    public BannerAlertLevel AlertLevel
    {
        get => (BannerAlertLevel)GetValue(AlertLevelProperty);
        set => SetValue(AlertLevelProperty, value);
    }

    /// <summary>
    /// 标题前缀（例如“【本回合推荐】”）
    /// </summary>
    public string Headline
    {
        get => (string)GetValue(HeadlineProperty);
        set => SetValue(HeadlineProperty, value);
    }

    /// <summary>
    /// 核心决策指令
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// 补充细节提示
    /// </summary>
    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>
    /// 预测暴毙概率 (0.0 ~ 1.0)
    /// </summary>
    public double LethalRate
    {
        get => (double)GetValue(LethalRateProperty);
        set => SetValue(LethalRateProperty, value);
    }

    /// <summary>
    /// 预测对战胜率 (0.0 ~ 1.0)
    /// </summary>
    public double? WinRate
    {
        get => (double?)GetValue(WinRateProperty);
        set => SetValue(WinRateProperty, value);
    }

    /// <summary>
    /// 横条最大宽度，超出时 Viewbox 自动无缝下采样缩小字号
    /// </summary>
    public double MaxBannerWidth
    {
        get => (double)GetValue(MaxBannerWidthProperty);
        set => SetValue(MaxBannerWidthProperty, value);
    }

    #endregion

    #region Inspection Getters (For Unit Testing & External Binding)

    public Border OuterBorder => _outerBorder;
    public Viewbox ContentViewbox => _viewbox;
    public Grid LayoutGrid => _layoutGrid;
    public Border BadgeBorder => _badgeBorder;
    public TextBlock BadgeTextBlock => _badgeTextBlock;
    public Border Separator => _separator;
    public StackPanel TextStackPanel => _textStackPanel;
    public TextBlock HeadlineTextBlock => _headlineTextBlock;
    public TextBlock MessageTextBlock => _messageTextBlock;
    public TextBlock DetailTextBlock => _detailTextBlock;
    public Border StatsPillBorder => _statsPillBorder;
    public TextBlock StatsPillTextBlock => _statsPillTextBlock;

    #endregion

    #region Constructor

    public DecisionBannerControl()
    {
        // 核心穿透合规设置：HUD 横条绝不拦截鼠标点击与出牌操作
        IsHitTestVisible = false;
        Focusable = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        // 1. 胶囊外壳磨砂亚克力容器 (rgba(22, 27, 34, 0.88))
        _outerBorder = new Border
        {
            CornerRadius = HudStyles.BannerCornerRadius,
            Padding = HudStyles.BannerPadding,
            Background = HudPalette.BackgroundCanvasBrush,
            BorderBrush = HudPalette.BorderSubtleBrush,
            BorderThickness = new Thickness(1.5),
            SnapsToDevicePixels = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // 2. 响应式字号下采样缩放容器 Viewbox (绝不超出两翼)
        _viewbox = new Viewbox
        {
            Stretch = Stretch.None,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = MaxBannerWidth - HudStyles.BannerPadding.Left - HudStyles.BannerPadding.Right
        };

        // 3. 水平栅格内容布局
        _layoutGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: 状态微标
        _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 1: 分隔竖线
        _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: 核心文案 (Headline + Message + Detail)
        _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 3: 概率指示徽标 (可选)

        // 4. 状态微标胶囊 (🟢/🟡/🔴)
        _badgeTextBlock = new TextBlock
        {
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Text = BannerAlertLevel.Normal.GetBadgeText(),
            Foreground = HudPalette.SafeTempoBrush
        };

        _badgeBorder = new Border
        {
            CornerRadius = HudStyles.BadgeCornerRadius,
            Padding = HudStyles.BadgePadding,
            Background = BannerAlertLevelExtensions.BadgeNormalBgBrush,
            BorderBrush = HudPalette.SafeTempoBrush,
            BorderThickness = new Thickness(1.0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = _badgeTextBlock
        };
        Grid.SetColumn(_badgeBorder, 0);
        _layoutGrid.Children.Add(_badgeBorder);

        // 5. 分隔竖线
        _separator = new Border
        {
            Width = 1.0,
            Background = HudPalette.BorderSubtleBrush,
            Margin = new Thickness(0, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(_separator, 1);
        _layoutGrid.Children.Add(_separator);

        // 6. 核心文本 StackPanel
        _textStackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center
        };

        _headlineTextBlock = new TextBlock
        {
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = HudPalette.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "【本回合推荐】"
        };
        _textStackPanel.Children.Add(_headlineTextBlock);

        _messageTextBlock = new TextBlock
        {
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = HudPalette.TextPrimaryBrush,
            Margin = new Thickness(0, 3, 0, 3),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 650,
            TextTrimming = TextTrimming.None
        };
        _textStackPanel.Children.Add(_messageTextBlock);

        _detailTextBlock = new TextBlock
        {
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 14,
            Foreground = HudPalette.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _textStackPanel.Children.Add(_detailTextBlock);

        Grid.SetColumn(_textStackPanel, 2);
        _layoutGrid.Children.Add(_textStackPanel);

        // 7. 概率统计指示徽标 (暴毙率 / 胜率)
        _statsPillTextBlock = new TextBlock
        {
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        _statsPillBorder = new Border
        {
            CornerRadius = HudStyles.BadgeCornerRadius,
            Padding = HudStyles.BadgePadding,
            Margin = new Thickness(10, 0, 0, 0),
            BorderThickness = new Thickness(1.0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Child = _statsPillTextBlock
        };
        Grid.SetColumn(_statsPillBorder, 3);
        _layoutGrid.Children.Add(_statsPillBorder);

        // 组装根容器
        _viewbox.Child = _layoutGrid;
        _outerBorder.Child = _viewbox;
        Content = _outerBorder;

        // 应用初始状态视觉表现
        UpdateVisuals();
    }

    #endregion

    #region Layout & Responsive Viewport Adaptation

    /// <summary>
    /// 根据底层视口/窗口宽度自适应调整横条的最大宽度，确保横条绝不溢出屏幕两翼
    /// </summary>
    /// <param name="viewportWidth">视口宽度 (如 1920, 1280 等)</param>
    public void AdaptToViewportWidth(double viewportWidth)
    {
        if (viewportWidth <= 0)
        {
            return;
        }

        // 横条宽度限制在视口 75% 内，区间 [320, 960]
        double targetMaxWidth = MathEx.Clamp(viewportWidth * 0.75, 320.0, 960.0);
        MaxBannerWidth = targetMaxWidth;
        MaxWidth = targetMaxWidth;

        double paddingHoriz = HudStyles.BannerPadding.Left + HudStyles.BannerPadding.Right;
        _viewbox.MaxWidth = Math.Max(120.0, targetMaxWidth - paddingHoriz - 4.0);
        _textStackPanel.MaxWidth = Math.Max(120, targetMaxWidth - 190);
        _messageTextBlock.MaxWidth = _textStackPanel.MaxWidth;
        _detailTextBlock.MaxWidth = _textStackPanel.MaxWidth;
        _detailTextBlock.TextWrapping = TextWrapping.Wrap;
    }

    /// <summary>
    /// 在宿主 Canvas 中将决策横条居中吸附至顶部指定偏移处
    /// </summary>
    /// <param name="containerWidth">宿主容器总宽度</param>
    /// <param name="topMargin">顶部边距 (默认 16px)</param>
    public void UpdatePosition(double containerWidth, double topMargin = 16.0)
    {
        AdaptToViewportWidth(containerWidth);

        Measure(new Size(containerWidth, double.PositiveInfinity));
        double effectiveWidth = DesiredSize.Width > 0 ? DesiredSize.Width : ActualWidth;
        if (effectiveWidth <= 0 && Width > 0)
        {
            effectiveWidth = Width;
        }

        double left = Math.Max(0, (containerWidth - effectiveWidth) / 2.0);
        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, topMargin);
    }

    #endregion

    #region Public State Update Methods

    /// <summary>
    /// 显式更新决策横条文案与状态
    /// </summary>
    public void Update(
        string message,
        BannerAlertLevel alertLevel = BannerAlertLevel.Normal,
        string? headline = null,
        string? detail = null,
        double lethalRate = 0.0,
        double? winRate = null)
    {
        AlertLevel = alertLevel;
        Headline = headline ?? alertLevel.GetDefaultHeadline();
        Message = message;
        Detail = detail;
        LethalRate = lethalRate;
        WinRate = winRate;

        // 若挂载了 ViewModel，同步更新 ViewModel
        if (ViewModel != null)
        {
            ViewModel.Update(message, alertLevel, headline, detail, lethalRate, winRate);
        }
        else
        {
            UpdateVisuals();
        }
    }

    /// <summary>
    /// 根据当前对局暴毙率 P_lethal 智能推导告警级别并更新横条
    /// </summary>
    public void UpdateFromRisk(
        double lethalRate,
        string message,
        string? headline = null,
        string? detail = null,
        double? winRate = null)
    {
        var level = BannerAlertLevelExtensions.FromLethalRate(lethalRate);
        Update(message, level, headline, detail, lethalRate, winRate);
    }

    #endregion

    #region Visual State Updates

    private void UpdateVisuals()
    {
        // 1. 告警级别微标与边框高亮自适应
        var level = AlertLevel;
        SolidColorBrush accentBrush = level.GetAccentBrush();
        SolidColorBrush badgeBgBrush = level.GetBadgeBackgroundBrush();

        _badgeBorder.Background = badgeBgBrush;
        _badgeBorder.BorderBrush = accentBrush;
        _badgeTextBlock.Foreground = accentBrush;
        _badgeTextBlock.Text = level.GetBadgeText();

        switch (level)
        {
            case BannerAlertLevel.Critical:
                _outerBorder.BorderBrush = HudPalette.LethalRedBrush;
                _headlineTextBlock.Foreground = HudPalette.LethalRedBrush;
                break;
            case BannerAlertLevel.Warning:
                _outerBorder.BorderBrush = HudPalette.LegendaryGoldBrush;
                _headlineTextBlock.Foreground = HudPalette.LegendaryGoldBrush;
                break;
            default:
                _outerBorder.BorderBrush = HudPalette.BorderSubtleBrush;
                _headlineTextBlock.Foreground = HudPalette.TextPrimaryBrush;
                break;
        }

        // 2. 核心文案更新
        _headlineTextBlock.Text = !string.IsNullOrWhiteSpace(Headline) ? Headline : level.GetDefaultHeadline();
        _messageTextBlock.Text = Message;

        if (!string.IsNullOrWhiteSpace(Detail))
        {
            _detailTextBlock.Text = $"({Detail})";
            _detailTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            _detailTextBlock.Text = string.Empty;
            _detailTextBlock.Visibility = Visibility.Collapsed;
        }

        // 3. 概率统计指示徽标更新
        if (LethalRate > 0.0)
        {
            _statsPillBorder.Visibility = Visibility.Visible;
            _statsPillTextBlock.Text = $"暴毙率: {LethalRate * 100.0:F0}%";
            SolidColorBrush riskColor = HudPalette.GetRiskColor(LethalRate);
            _statsPillBorder.BorderBrush = riskColor;
            _statsPillBorder.Background = badgeBgBrush;
            _statsPillTextBlock.Foreground = riskColor;
        }
        else if (WinRate.HasValue)
        {
            _statsPillBorder.Visibility = Visibility.Visible;
            _statsPillTextBlock.Text = $"胜率: {WinRate.Value * 100.0:F0}%";
            _statsPillBorder.BorderBrush = HudPalette.SafeTempoBrush;
            _statsPillBorder.Background = BannerAlertLevelExtensions.BadgeNormalBgBrush;
            _statsPillTextBlock.Foreground = HudPalette.SafeTempoBrush;
        }
        else
        {
            _statsPillBorder.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DecisionBannerControl banner)
        {
            banner.UpdateVisuals();
        }
    }

    private static void OnMaxBannerWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DecisionBannerControl banner && e.NewValue is double newMaxW)
        {
            banner.MaxWidth = newMaxW;
            double paddingHoriz = HudStyles.BannerPadding.Left + HudStyles.BannerPadding.Right;
            banner._viewbox.MaxWidth = Math.Max(120.0, newMaxW - paddingHoriz - 4.0);
        }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DecisionBannerControl banner)
        {
            return;
        }

        if (e.OldValue is DecisionBannerViewModel oldVm)
        {
            oldVm.PropertyChanged -= banner.OnViewModelPropertyChanged;
        }

        if (e.NewValue is DecisionBannerViewModel newVm)
        {
            banner.DataContext = newVm;
            newVm.PropertyChanged += banner.OnViewModelPropertyChanged;
            banner.SyncFromViewModel(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is DecisionBannerViewModel vm)
        {
            SyncFromViewModel(vm);
        }
    }

    private void SyncFromViewModel(DecisionBannerViewModel vm)
    {
        AlertLevel = vm.AlertLevel;
        Headline = vm.Headline;
        Message = vm.Message;
        Detail = vm.Detail;
        LethalRate = vm.LethalRate;
        WinRate = vm.WinRate;
        Visibility = vm.IsVisible ? Visibility.Visible : Visibility.Collapsed;

        UpdateVisuals();
    }

    #endregion
}
