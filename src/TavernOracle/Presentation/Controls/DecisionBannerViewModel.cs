using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 顶部决策横条视图数据模型 (ViewModel)。
/// 负责驱动横条控件的状态渲染与动态文本展示，支持 INotifyPropertyChanged 响应式绑定。
/// </summary>
public class DecisionBannerViewModel : INotifyPropertyChanged
{
    private BannerAlertLevel _alertLevel = BannerAlertLevel.Normal;
    private string _headline = "【本回合推荐】";
    private string _message = string.Empty;
    private string? _detail;
    private double _lethalRate;
    private double? _winRate;
    private bool _isVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    /// <summary>
    /// 当前横条告警级别（🟢 Normal / 🟡 Warning / 🔴 Critical）
    /// </summary>
    public BannerAlertLevel AlertLevel
    {
        get => _alertLevel;
        set
        {
            if (SetProperty(ref _alertLevel, value))
            {
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(BadgeBackgroundBrush));
                OnPropertyChanged(nameof(BadgeText));
            }
        }
    }

    /// <summary>
    /// 标题或操作类别（例如“【本回合推荐】”、“【战力预警】”、“【绝地保命】”）
    /// </summary>
    public string Headline
    {
        get => _headline;
        set => SetProperty(ref _headline, value);
    }

    /// <summary>
    /// 核心决策指令文本（例如“升至 4 本并冻结跳蛙”、“绝地保命：切勿刷新，存铸币防暴毙”）
    /// </summary>
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// 补充战术细节或原因（例如“胜率 78%，留对子为佳”、“剩余 3 铸币无理财”）
    /// </summary>
    public string? Detail
    {
        get => _detail;
        set
        {
            if (SetProperty(ref _detail, value))
            {
                OnPropertyChanged(nameof(HasDetail));
            }
        }
    }

    /// <summary>
    /// 预测暴毙概率 (0.0 ~ 1.0)
    /// </summary>
    public double LethalRate
    {
        get => _lethalRate;
        set
        {
            if (SetProperty(ref _lethalRate, value))
            {
                OnPropertyChanged(nameof(LethalRatePercent));
            }
        }
    }

    /// <summary>
    /// 预测胜率 (0.0 ~ 1.0, 可空)
    /// </summary>
    public double? WinRate
    {
        get => _winRate;
        set
        {
            if (SetProperty(ref _winRate, value))
            {
                OnPropertyChanged(nameof(WinRatePercent));
            }
        }
    }

    /// <summary>
    /// 横条是否可见
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    #endregion

    #region Computed Convenience Properties

    /// <summary>
    /// 当前告警级别对应的高光冷冻画刷
    /// </summary>
    public SolidColorBrush AccentBrush => _alertLevel.GetAccentBrush();

    /// <summary>
    /// 当前告警级别微标胶囊的半透明背景冷冻画刷
    /// </summary>
    public SolidColorBrush BadgeBackgroundBrush => _alertLevel.GetBadgeBackgroundBrush();

    /// <summary>
    /// 微标胶囊的文本
    /// </summary>
    public string BadgeText => _alertLevel.GetBadgeText();

    /// <summary>
    /// 是否存在补充战术细节
    /// </summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(_detail);

    /// <summary>
    /// 格式化后的暴毙率百分比字符串 (例如 "35%")
    /// </summary>
    public string LethalRatePercent => _lethalRate > 0
        ? $"{_lethalRate * 100.0:F0}%"
        : string.Empty;

    /// <summary>
    /// 格式化后的胜率百分比字符串 (例如 "78%")
    /// </summary>
    public string WinRatePercent => _winRate.HasValue
        ? $"{_winRate.Value * 100.0:F0}%"
        : string.Empty;

    /// <summary>
    /// 完整文本摘要
    /// </summary>
    public string FormattedSummary
    {
        get
        {
            string head = string.IsNullOrWhiteSpace(Headline) ? string.Empty : $"{Headline} ";
            string detail = string.IsNullOrWhiteSpace(Detail) ? string.Empty : $" ({Detail})";
            return $"{head}{Message}{detail}".Trim();
        }
    }

    #endregion

    #region Mutation Methods

    /// <summary>
    /// 显式更新决策横条核心状态与文案
    /// </summary>
    /// <param name="message">核心建议指令</param>
    /// <param name="alertLevel">告警级别</param>
    /// <param name="headline">可选标题（若为空则自动根据 alertLevel 派生）</param>
    /// <param name="detail">可选战术补充细节</param>
    /// <param name="lethalRate">暴毙率</param>
    /// <param name="winRate">胜率</param>
    public void Update(
        string message,
        BannerAlertLevel alertLevel = BannerAlertLevel.Normal,
        string? headline = null,
        string? detail = null,
        double lethalRate = 0.0,
        double? winRate = null)
    {
        Message = message;
        AlertLevel = alertLevel;
        Headline = headline ?? alertLevel.GetDefaultHeadline();
        Detail = detail;
        LethalRate = lethalRate;
        WinRate = winRate;
        IsVisible = true;
    }

    /// <summary>
    /// 根据当前对战暴毙率 P_lethal 智能推导告警级别并更新横条
    /// </summary>
    /// <param name="lethalRate">暴毙率 (0.0 ~ 1.0)</param>
    /// <param name="message">核心建议指令</param>
    /// <param name="headline">可选标题</param>
    /// <param name="detail">可选战术补充细节</param>
    /// <param name="winRate">预测胜率</param>
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

    /// <summary>
    /// 重置横条为初始安全空状态
    /// </summary>
    public void Reset()
    {
        _alertLevel = BannerAlertLevel.Normal;
        _headline = BannerAlertLevel.Normal.GetDefaultHeadline();
        _message = string.Empty;
        _detail = null;
        _lethalRate = 0.0;
        _winRate = null;
        _isVisible = true;

        OnPropertyChanged(string.Empty);
    }

    #endregion

    #region INotifyPropertyChanged Helper

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
