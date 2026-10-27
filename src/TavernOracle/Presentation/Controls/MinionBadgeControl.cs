using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TavernOracle.Presentation.Theme;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 酒馆卡面头顶推荐微标控件 (MinionBadgeControl)。
/// 遵循 docs/dev_specifications.md 第 3.2 节微标规范：
/// 1. 锚定于随从卡面上方居中 15px 处，自适应尺寸，绝不遮挡底部攻防身材；
/// 2. 分类支持：🔥三连对子(金色渐变+发光呼吸)、★流派质变怪(魔能紫+协同得分)、🛡️即时保命单卡(翡翠绿盾牌)、⛔陷阱预警(半透明下沉+细斜杠)；
/// 3. 所有画刷均引用 HudPalette 冻结画刷 (Freeze)，动画复用轻量 DoubleAnimation，保证零 GC 开销与绝对点击穿透。
/// </summary>
public class MinionBadgeControl : UserControl, IDisposable
{
    /// <summary>
    /// 默认锚定在随从卡面上方的间距 (px)
    /// </summary>
    public const double DefaultAnchorOffset = 15.0;

    /// <summary>
    /// 标题最大宽度约束，配合 CharacterEllipsis 彻底杜绝文本溢出
    /// </summary>
    public const double MaxTitleWidth = 170.0;

    /// <summary>
    /// 得分徽章最大宽度约束
    /// </summary>
    public const double MaxScoreWidth = 55.0;

    #region Static Cached Animations (Zero GC Allocation)

    private static readonly DoubleAnimation CachedBlurAnimation;
    private static readonly DoubleAnimation CachedOpacityAnimation;

    static MinionBadgeControl()
    {
        // 呼吸动效：光晕扩散模糊度从 4px 渐变至 14px，周期 900ms，无限循环并自动反转
        CachedBlurAnimation = new DoubleAnimation
        {
            From = 4.0,
            To = 14.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(900)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        CachedBlurAnimation.Freeze();

        // 呼吸动效：光晕不透明度从 0.45 渐变至 0.95
        CachedOpacityAnimation = new DoubleAnimation
        {
            From = 0.45,
            To = 0.95,
            Duration = new Duration(TimeSpan.FromMilliseconds(900)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        CachedOpacityAnimation.Freeze();
    }

    #endregion

    #region Visual Elements

    private readonly Border _rootBorder;
    private readonly Grid _mainGrid;
    private readonly StackPanel _contentPanel;
    private readonly TextBlock _iconText;
    private readonly TextBlock _titleText;
    private readonly Border _scorePill;
    private readonly TextBlock _scoreText;
    private readonly Line _trapSlashLine;
    private readonly DropShadowEffect _dropShadowEffect;

    private bool _isPulseActive;
    private bool _isDisposed;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MinionBadgeViewModel),
            typeof(MinionBadgeControl),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public static readonly DependencyProperty BadgeTypeProperty =
        DependencyProperty.Register(
            nameof(BadgeType),
            typeof(BadgeType),
            typeof(MinionBadgeControl),
            new PropertyMetadata(BadgeType.TriplePair, OnDirectPropertyChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(MinionBadgeControl),
            new PropertyMetadata(string.Empty, OnDirectPropertyChanged));

    public static readonly DependencyProperty ScoreTextProperty =
        DependencyProperty.Register(
            nameof(ScoreText),
            typeof(string),
            typeof(MinionBadgeControl),
            new PropertyMetadata(null, OnDirectPropertyChanged));

    public static readonly DependencyProperty IconTextProperty =
        DependencyProperty.Register(
            nameof(IconText),
            typeof(string),
            typeof(MinionBadgeControl),
            new PropertyMetadata(null, OnDirectPropertyChanged));

    public static readonly DependencyProperty EnableGlowPulseProperty =
        DependencyProperty.Register(
            nameof(EnableGlowPulse),
            typeof(bool),
            typeof(MinionBadgeControl),
            new PropertyMetadata(true, OnDirectPropertyChanged));

    #endregion

    #region Constructors

    public MinionBadgeControl()
    {
        // 表现层穿透红线：悬浮微标不可阻挡鼠标底层点击
        IsHitTestVisible = false;
        Focusable = false;
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        // 初始化发光投影效果（初始赋予金色三连）
        _dropShadowEffect = new DropShadowEffect
        {
            Color = HudPalette.ColorLegendaryGold,
            BlurRadius = 8.0,
            Opacity = 0.85,
            ShadowDepth = 0.0
        };

        // 外层胶囊容器
        _rootBorder = new Border
        {
            CornerRadius = HudStyles.BadgeCornerRadius,
            Padding = HudStyles.BadgePadding,
            BorderThickness = new Thickness(1.5),
            Background = HudPalette.BackgroundCanvasBrush,
            BorderBrush = HudPalette.LegendaryGoldGradientBrush,
            Effect = _dropShadowEffect,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        _mainGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 图标 TextBlock
        _iconText = new TextBlock
        {
            Text = "🔥",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };

        // 标题 TextBlock（严格截断防溢出）
        _titleText = new TextBlock
        {
            Text = "三连对子",
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = HudPalette.LegendaryGoldLightBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxWidth = MaxTitleWidth
        };

        // 得分/副标胶囊小药丸
        _scoreText = new TextBlock
        {
            FontFamily = HudStyles.StandardFontFamily,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = HudPalette.LegendaryGoldLightBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxWidth = MaxScoreWidth
        };

        _scorePill = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = HudPalette.BadgePillBackgroundBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = HudPalette.LegendaryGoldBrush,
            Child = _scoreText,
            Visibility = Visibility.Collapsed
        };

        _contentPanel.Children.Add(_iconText);
        _contentPanel.Children.Add(_titleText);
        _contentPanel.Children.Add(_scorePill);

        // 陷阱卡细斜杠装饰线（绝对居中覆盖）
        _trapSlashLine = new Line
        {
            Stroke = HudPalette.LethalRedBrush,
            StrokeThickness = 1.5,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        _mainGrid.Children.Add(_contentPanel);
        _mainGrid.Children.Add(_trapSlashLine);

        _rootBorder.Child = _mainGrid;
        Content = _rootBorder;

        // 监听尺寸变化以更新细斜杠坐标
        _rootBorder.SizeChanged += OnRootBorderSizeChanged;
        Unloaded += OnControlUnloaded;

        // 刷新初始外观
        UpdateVisualState();
    }

    #endregion

    #region Properties

    /// <summary>
    /// 数据上下文 ViewModel
    /// </summary>
    public MinionBadgeViewModel? ViewModel
    {
        get => (MinionBadgeViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// 微标类型
    /// </summary>
    public BadgeType BadgeType
    {
        get => (BadgeType)GetValue(BadgeTypeProperty);
        set => SetValue(BadgeTypeProperty, value);
    }

    /// <summary>
    /// 微标标题
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// 得分或数值徽章
    /// </summary>
    public string? ScoreText
    {
        get => (string?)GetValue(ScoreTextProperty);
        set => SetValue(ScoreTextProperty, value);
    }

    /// <summary>
    /// 自定义图标
    /// </summary>
    public string? IconText
    {
        get => (string?)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    /// <summary>
    /// 是否开启金色发光呼吸动效
    /// </summary>
    public bool EnableGlowPulse
    {
        get => (bool)GetValue(EnableGlowPulseProperty);
        set => SetValue(EnableGlowPulseProperty, value);
    }

    /// <summary>
    /// 当前呼吸发光动效是否处于播放状态
    /// </summary>
    public bool IsPulseActive => _isPulseActive;

    #region Visual Elements Accessors (For Unit & Visual Tests)

    public Border RootBorder => _rootBorder;
    public TextBlock TitleBlock => _titleText;
    public TextBlock IconBlock => _iconText;
    public TextBlock ScoreBlock => _scoreText;
    public Border ScorePill => _scorePill;
    public Line TrapSlashLine => _trapSlashLine;
    public DropShadowEffect DropShadow => _dropShadowEffect;

    #endregion

    #endregion

    #region Visual State Updates

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MinionBadgeControl control)
        {
            return;
        }

        if (e.OldValue is MinionBadgeViewModel oldVm)
        {
            oldVm.PropertyChanged -= control.OnViewModelInnerPropertyChanged;
        }

        if (e.NewValue is MinionBadgeViewModel newVm)
        {
            newVm.PropertyChanged += control.OnViewModelInnerPropertyChanged;
        }

        control.UpdateVisualState();
    }

    private void OnViewModelInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private static void OnDirectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MinionBadgeControl control)
        {
            control.UpdateVisualState();
        }
    }

    /// <summary>
    /// 根据当前绑定的 ViewModel 或依赖属性刷新微标全部视觉样式
    /// </summary>
    public void UpdateVisualState()
    {
        var vm = ViewModel;

        BadgeType type = vm?.BadgeType ?? BadgeType;
        string title = !string.IsNullOrEmpty(vm?.Title) ? vm.Title : Title;
        string? score = vm?.DisplayScore ?? ScoreText;
        string icon = !string.IsNullOrEmpty(vm?.DisplayIcon) ? vm.DisplayIcon : (!string.IsNullOrEmpty(IconText) ? IconText : GetDefaultIcon(type));
        bool enablePulse = vm?.EnableGlowPulse ?? EnableGlowPulse;
        double opacity = vm != null ? vm.Opacity : 1.0;

        // 设置文本与图标
        _iconText.Text = icon;
        _titleText.Text = !string.IsNullOrEmpty(title) ? title : GetDefaultTitle(type);

        // 设置得分药丸状态
        if (!string.IsNullOrEmpty(score))
        {
            _scoreText.Text = score;
            _scorePill.Visibility = Visibility.Visible;
        }
        else
        {
            _scorePill.Visibility = Visibility.Collapsed;
        }

        // 应用分类视觉规范与色彩 Token (全部引用冻结画刷)
        switch (type)
        {
            case BadgeType.TriplePair:
                _rootBorder.BorderBrush = HudPalette.LegendaryGoldGradientBrush;
                _rootBorder.Background = HudPalette.BackgroundCanvasBrush;
                _rootBorder.Opacity = opacity;
                _titleText.Foreground = HudPalette.LegendaryGoldLightBrush;
                _iconText.Foreground = HudPalette.LegendaryGoldLightBrush;
                _scorePill.BorderBrush = HudPalette.LegendaryGoldBrush;
                _scoreText.Foreground = HudPalette.LegendaryGoldLightBrush;
                _trapSlashLine.Visibility = Visibility.Collapsed;

                _dropShadowEffect.Color = HudPalette.ColorLegendaryGold;
                _rootBorder.Effect = _dropShadowEffect;

                if (enablePulse)
                {
                    StartGlowPulse();
                }
                else
                {
                    StopGlowPulse();
                }
                break;

            case BadgeType.KeyEngine:
                StopGlowPulse();
                _rootBorder.BorderBrush = HudPalette.ArcanePurpleBrush;
                _rootBorder.Background = HudPalette.BadgePurpleTintBackgroundBrush;
                _rootBorder.Opacity = opacity;
                _titleText.Foreground = HudPalette.TextPrimaryBrush;
                _iconText.Foreground = HudPalette.ArcanePurpleBrush;
                _scorePill.BorderBrush = HudPalette.ArcanePurpleBrush;
                _scoreText.Foreground = HudPalette.ArcanePurpleBrush;
                _trapSlashLine.Visibility = Visibility.Collapsed;

                _dropShadowEffect.Color = HudPalette.ColorArcanePurple;
                _dropShadowEffect.BlurRadius = 6.0;
                _dropShadowEffect.Opacity = 0.60;
                _rootBorder.Effect = _dropShadowEffect;
                break;

            case BadgeType.TempoFix:
                StopGlowPulse();
                _rootBorder.BorderBrush = HudPalette.SafeTempoBrush;
                _rootBorder.Background = HudPalette.BadgeGreenTintBackgroundBrush;
                _rootBorder.Opacity = opacity;
                _titleText.Foreground = HudPalette.TextPrimaryBrush;
                _iconText.Foreground = HudPalette.SafeTempoBrush;
                _scorePill.BorderBrush = HudPalette.SafeTempoBrush;
                _scoreText.Foreground = HudPalette.SafeTempoBrush;
                _trapSlashLine.Visibility = Visibility.Collapsed;

                _dropShadowEffect.Color = HudPalette.ColorSafeTempo;
                _dropShadowEffect.BlurRadius = 6.0;
                _dropShadowEffect.Opacity = 0.60;
                _rootBorder.Effect = _dropShadowEffect;
                break;

            case BadgeType.TrapWarning:
                StopGlowPulse();
                _rootBorder.BorderBrush = HudPalette.LethalRedBrush;
                _rootBorder.Background = HudPalette.TrapMutedBackgroundBrush;
                _rootBorder.Opacity = opacity < 1.0 ? opacity : 0.80; // 半透明下沉
                _titleText.Foreground = HudPalette.LethalRedBrush;
                _iconText.Foreground = HudPalette.LethalRedBrush;
                _scorePill.BorderBrush = HudPalette.LethalRedBrush;
                _scoreText.Foreground = HudPalette.LethalRedBrush;
                _trapSlashLine.Visibility = Visibility.Visible; // 红色细斜杠警示

                _rootBorder.Effect = null;
                break;
        }

        UpdateSlashLineCoordinates();
    }

    private static string GetDefaultIcon(BadgeType type) => type switch
    {
        BadgeType.TriplePair => "🔥",
        BadgeType.KeyEngine => "★",
        BadgeType.TempoFix => "🛡️",
        BadgeType.TrapWarning => "⛔",
        _ => "•"
    };

    private static string GetDefaultTitle(BadgeType type) => type switch
    {
        BadgeType.TriplePair => "三连对子",
        BadgeType.KeyEngine => "流派核心",
        BadgeType.TempoFix => "即时保命",
        BadgeType.TrapWarning => "避坑预警",
        _ => "推荐"
    };

    #endregion

    #region Glow Pulse Animation Lifecycle

    /// <summary>
    /// 启动金色发光呼吸动效（零 GC 开销）
    /// </summary>
    public void StartGlowPulse()
    {
        if (_isPulseActive)
        {
            return;
        }

        _dropShadowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, CachedBlurAnimation);
        _dropShadowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, CachedOpacityAnimation);
        _isPulseActive = true;
    }

    /// <summary>
    /// 停止发光呼吸动效
    /// </summary>
    public void StopGlowPulse()
    {
        if (!_isPulseActive)
        {
            return;
        }

        _dropShadowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
        _dropShadowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, null);
        _dropShadowEffect.BlurRadius = 8.0;
        _dropShadowEffect.Opacity = 0.85;
        _isPulseActive = false;
    }

    #endregion

    #region Layout & Anchoring

    /// <summary>
    /// 计算微标在随从正上方居中对齐的目标坐标
    /// </summary>
    /// <param name="minionBounds">随从卡牌在 Canvas 中的包围盒</param>
    /// <param name="badgeSize">微标控件的测算或实际尺寸</param>
    /// <param name="offset">微标底部与随从顶部之间的垂直间距（默认 15px）</param>
    /// <returns>微标控件在 Canvas 中的 (Left, Top) 坐标</returns>
    public static Point CalculateAnchorPosition(Rect minionBounds, Size badgeSize, double offset = DefaultAnchorOffset)
    {
        if (minionBounds.IsEmpty || minionBounds.Width <= 0 || minionBounds.Height <= 0)
        {
            return new Point(0, 0);
        }

        double left = minionBounds.X + (minionBounds.Width - badgeSize.Width) / 2.0;
        double top = minionBounds.Y - badgeSize.Height - offset;
        return new Point(left, top);
    }

    /// <summary>
    /// 将微标控件锚定至指定的随从卡面上方居中 15px 处。
    /// 自动设置 Canvas.Left 与 Canvas.Top。
    /// </summary>
    /// <param name="minionBounds">随从卡牌在 Canvas 中的矩形包围盒</param>
    /// <param name="offset">上方锚定偏移（默认 15px）</param>
    public void AnchorToMinion(Rect minionBounds, double offset = DefaultAnchorOffset)
    {
        if (DesiredSize.Width <= 0 || DesiredSize.Height <= 0)
        {
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        double width = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
        double height = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;

        Point anchorPos = CalculateAnchorPosition(minionBounds, new Size(width, height), offset);
        Canvas.SetLeft(this, anchorPos.X);
        Canvas.SetTop(this, anchorPos.Y);
    }

    /// <summary>
    /// 获取微标锚定后的完整矩形范围
    /// </summary>
    public Rect GetAnchoredBounds(Rect minionBounds, double offset = DefaultAnchorOffset)
    {
        if (DesiredSize.Width <= 0 || DesiredSize.Height <= 0)
        {
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        double width = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
        double height = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;

        Point anchorPos = CalculateAnchorPosition(minionBounds, new Size(width, height), offset);
        return new Rect(anchorPos.X, anchorPos.Y, width, height);
    }

    /// <summary>
    /// 验证微标是否绝对不遮挡随从底部的攻击力与生命值身材区域。
    /// 炉石随从攻防区域位于卡牌最底部（通常占用卡面底部 20%~25% 的高度）。
    /// </summary>
    /// <param name="minionBounds">随从卡牌包围盒</param>
    /// <param name="statsRegionHeight">随从底部身材高度（默认 40px）</param>
    /// <param name="offset">上方锚定偏移</param>
    /// <returns>若无遮挡则为 true</returns>
    public bool DoesNotOccludeBottomStats(Rect minionBounds, double statsRegionHeight = 40.0, double offset = DefaultAnchorOffset)
    {
        if (minionBounds.IsEmpty || minionBounds.Height <= 0)
        {
            return true;
        }

        Rect badgeRect = GetAnchoredBounds(minionBounds, offset);
        double statsTop = minionBounds.Bottom - statsRegionHeight;
        Rect statsRect = new Rect(minionBounds.X, statsTop, minionBounds.Width, statsRegionHeight);

        // 确保微标完全在卡面顶部上方，且与底部身材区域无任何几何重叠
        return badgeRect.Bottom <= minionBounds.Top && !badgeRect.IntersectsWith(statsRect);
    }

    private void OnRootBorderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSlashLineCoordinates();
    }

    private void UpdateSlashLineCoordinates()
    {
        if (_trapSlashLine.Visibility != Visibility.Visible)
        {
            return;
        }

        double width = _rootBorder.ActualWidth;
        double height = _rootBorder.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            width = _rootBorder.DesiredSize.Width;
            height = _rootBorder.DesiredSize.Height;
        }

        if (width > 0 && height > 0)
        {
            _trapSlashLine.X1 = 4;
            _trapSlashLine.Y1 = height - 4;
            _trapSlashLine.X2 = width - 4;
            _trapSlashLine.Y2 = 4;
        }
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        StopGlowPulse();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopGlowPulse();

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelInnerPropertyChanged;
        }

        _rootBorder.SizeChanged -= OnRootBorderSizeChanged;
        Unloaded -= OnControlUnloaded;
    }

    #endregion
}
