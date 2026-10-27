using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 随从卡面头顶推荐微标数据模型 (ViewModel)。
/// 遵循 MVVM 模式，支持属性变更通知与自适应样式绑定。
/// </summary>
public class MinionBadgeViewModel : INotifyPropertyChanged
{
    private BadgeType _badgeType;
    private string _title = string.Empty;
    private string? _subtitle;
    private double? _synergyScore;
    private double? _tempoScore;
    private string? _customIcon;
    private bool _enableGlowPulse = true;
    private double _opacity = 1.0;
    private Rect _minionBounds;
    private double _anchorOffsetY = 15.0;

    /// <summary>
    /// 属性变更事件
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    /// <summary>
    /// 微标类型（三连对子、流派质变怪、即时保命单卡、陷阱预警）
    /// </summary>
    public BadgeType BadgeType
    {
        get => _badgeType;
        set
        {
            if (SetField(ref _badgeType, value))
            {
                OnPropertyChanged(nameof(DisplayIcon));
                OnPropertyChanged(nameof(DisplayScore));
            }
        }
    }

    /// <summary>
    /// 微标标题文本（如“三连对子”、“流派核心”、“即时保命”、“避坑预警”）
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    /// <summary>
    /// 副标题或详情说明（如“凑齐三连”、“圣盾保命”、“负收益”）
    /// </summary>
    public string? Subtitle
    {
        get => _subtitle;
        set => SetField(ref _subtitle, value);
    }

    /// <summary>
    /// 流派协同得分（适用于流派质变怪 ★）
    /// </summary>
    public double? SynergyScore
    {
        get => _synergyScore;
        set
        {
            if (SetField(ref _synergyScore, value))
            {
                OnPropertyChanged(nameof(DisplayScore));
            }
        }
    }

    /// <summary>
    /// 即时战力得分（适用于即时保命单卡 🛡️）
    /// </summary>
    public double? TempoScore
    {
        get => _tempoScore;
        set
        {
            if (SetField(ref _tempoScore, value))
            {
                OnPropertyChanged(nameof(DisplayScore));
            }
        }
    }

    /// <summary>
    /// 自定义图标覆盖（若为空则使用各微标分类默认 Emoji）
    /// </summary>
    public string? CustomIcon
    {
        get => _customIcon;
        set
        {
            if (SetField(ref _customIcon, value))
            {
                OnPropertyChanged(nameof(DisplayIcon));
            }
        }
    }

    /// <summary>
    /// 是否开启金色发光呼吸动效（Glow Pulse，主要用于三连对子）
    /// </summary>
    public bool EnableGlowPulse
    {
        get => _enableGlowPulse;
        set => SetField(ref _enableGlowPulse, value);
    }

    /// <summary>
    /// 整体透明度（陷阱预警等半透明下沉场景使用）
    /// </summary>
    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, value);
    }

    /// <summary>
    /// 目标随从在宿主 Canvas 上的绝对矩形包围盒
    /// </summary>
    public Rect MinionBounds
    {
        get => _minionBounds;
        set => SetField(ref _minionBounds, value);
    }

    /// <summary>
    /// 相对随从卡面上方边界的 Y 轴锚定间距（规范要求默认 15px）
    /// </summary>
    public double AnchorOffsetY
    {
        get => _anchorOffsetY;
        set => SetField(ref _anchorOffsetY, value);
    }

    /// <summary>
    /// 呈现给 UI 的最终图标
    /// </summary>
    public string DisplayIcon
    {
        get
        {
            if (!string.IsNullOrEmpty(_customIcon))
            {
                return _customIcon;
            }

            return _badgeType switch
            {
                BadgeType.TriplePair => "🔥",
                BadgeType.KeyEngine => "★",
                BadgeType.TempoFix => "🛡️",
                BadgeType.TrapWarning => "⛔",
                _ => "•"
            };
        }
    }

    /// <summary>
    /// 呈现给 UI 的格式化得分或数值徽章
    /// </summary>
    public string? DisplayScore
    {
        get
        {
            if (_badgeType == BadgeType.KeyEngine && _synergyScore.HasValue)
            {
                return _synergyScore.Value >= 0
                    ? $"+{_synergyScore.Value.ToString("0.0", CultureInfo.InvariantCulture)}"
                    : _synergyScore.Value.ToString("0.0", CultureInfo.InvariantCulture);
            }

            if (_badgeType == BadgeType.TempoFix && _tempoScore.HasValue)
            {
                return _tempoScore.Value >= 0
                    ? $"+{_tempoScore.Value.ToString("0.0", CultureInfo.InvariantCulture)}"
                    : _tempoScore.Value.ToString("0.0", CultureInfo.InvariantCulture);
            }

            return null;
        }
    }

    #endregion

    #region Layout Calculations

    /// <summary>
    /// 计算微标在宿主 Canvas 上的锚定左上角坐标（水平居中对齐，纵向置于随从上方指定间距处）
    /// </summary>
    /// <param name="badgeSize">微标控件的测算尺寸</param>
    /// <returns>在宿主 Canvas 中的 (Left, Top) 坐标</returns>
    public Point CalculateAnchorPosition(Size badgeSize)
    {
        if (_minionBounds.IsEmpty || _minionBounds.Width <= 0 || _minionBounds.Height <= 0)
        {
            return new Point(0, 0);
        }

        double left = _minionBounds.X + (_minionBounds.Width - badgeSize.Width) / 2.0;
        double top = _minionBounds.Y - badgeSize.Height - _anchorOffsetY;
        return new Point(left, top);
    }

    /// <summary>
    /// 计算微标在宿主 Canvas 上的完整包围盒
    /// </summary>
    public Rect CalculateAnchoredBounds(Size badgeSize)
    {
        var pos = CalculateAnchorPosition(badgeSize);
        return new Rect(pos.X, pos.Y, badgeSize.Width, badgeSize.Height);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// 构建 🔥 三连对子推荐微标
    /// </summary>
    public static MinionBadgeViewModel CreateTriplePair(
        string? title = null,
        string? subtitle = null,
        bool enableGlow = true)
    {
        return new MinionBadgeViewModel
        {
            BadgeType = BadgeType.TriplePair,
            Title = string.IsNullOrWhiteSpace(title) ? "三连对子" : title,
            Subtitle = subtitle ?? "凑齐三连",
            EnableGlowPulse = enableGlow,
            Opacity = 1.0
        };
    }

    /// <summary>
    /// 构建 ★ 流派质变怪推荐微标
    /// </summary>
    public static MinionBadgeViewModel CreateKeyEngine(
        string? title = null,
        double? synergyScore = null,
        string? subtitle = null)
    {
        return new MinionBadgeViewModel
        {
            BadgeType = BadgeType.KeyEngine,
            Title = string.IsNullOrWhiteSpace(title) ? "流派核心" : title,
            Subtitle = subtitle,
            SynergyScore = synergyScore,
            EnableGlowPulse = false,
            Opacity = 1.0
        };
    }

    /// <summary>
    /// 构建 🛡️ 即时保命单卡推荐微标
    /// </summary>
    public static MinionBadgeViewModel CreateTempoFix(
        string? title = null,
        string? subtitle = null,
        double? tempoScore = null)
    {
        return new MinionBadgeViewModel
        {
            BadgeType = BadgeType.TempoFix,
            Title = string.IsNullOrWhiteSpace(title) ? "即时保命" : title,
            Subtitle = subtitle ?? "圣盾/保命",
            TempoScore = tempoScore,
            EnableGlowPulse = false,
            Opacity = 1.0
        };
    }

    /// <summary>
    /// 构建 ⛔ 避坑预警微标
    /// </summary>
    public static MinionBadgeViewModel CreateTrapWarning(
        string? title = null,
        string? subtitle = null,
        double? penaltyScore = null)
    {
        return new MinionBadgeViewModel
        {
            BadgeType = BadgeType.TrapWarning,
            Title = string.IsNullOrWhiteSpace(title) ? "避坑预警" : title,
            Subtitle = subtitle ?? "低收益陷阱",
            SynergyScore = penaltyScore,
            EnableGlowPulse = false,
            Opacity = 0.80 // 半透明下沉警示
        };
    }

    #endregion

    #region INotifyPropertyChanged Helpers

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
