using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TavernOracle.Presentation.Theme;
using TavernOracle.Simulation;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 暴毙预警与胜率仪表盘核心数据模型 (ViewModel)。
/// 遵循极低时延与零 GC 优化规范，直接消费 T07 Bob's Buddy 桥接器输出的 CombatSimulationResult。
/// 所有画刷均绑定自 HudPalette 冻结静态画刷，绝不产生运行时托管堆碎片。
/// </summary>
public class LethalGaugeViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// 默认暴毙红色危急警报阈值 (P_lethal > 30% 即 0.30)
    /// </summary>
    public const double DefaultEmergencyThreshold = 0.30;

    private CombatSimulationResult _currentResult;
    private double _emergencyThreshold = DefaultEmergencyThreshold;
    private IBobsBuddyBridge? _attachedBridge;
    private bool _isDisposed;

    #region Properties

    /// <summary>
    /// 最新的战斗蒙特卡洛模拟原始结果快照
    /// </summary>
    public CombatSimulationResult CurrentResult => _currentResult;

    /// <summary>
    /// 预测胜率 [0.0, 1.0]
    /// </summary>
    public double WinRate => _currentResult.WinRate;

    /// <summary>
    /// 预测打平率 [0.0, 1.0]
    /// </summary>
    public double TieRate => _currentResult.TieRate;

    /// <summary>
    /// 预测败率 [0.0, 1.0]
    /// </summary>
    public double LossRate => _currentResult.LossRate;

    /// <summary>
    /// 核心暴毙率 P_lethal [0.0, 1.0]
    /// </summary>
    public double LethalRate => _currentResult.LethalRate;

    /// <summary>
    /// 暴毙率数学别名 P_lethal，供数学公式直接引用
    /// </summary>
    public double PLethal => _currentResult.PLethal;

    /// <summary>
    /// 预期承受平均伤害值 (正值表示玩家预期掉血，负值表示玩家预期胜出造成伤害)
    /// </summary>
    public double AverageDamageTaken => _currentResult.AverageDamageTaken;

    /// <summary>
    /// 蒙特卡洛模拟总轮次
    /// </summary>
    public int SimulationsCount => _currentResult.SimulationsCount;

    /// <summary>
    /// 是否为优雅降级安全先验状态
    /// </summary>
    public bool IsDegraded => _currentResult.IsDegraded;

    /// <summary>
    /// 状态或诊断消息
    /// </summary>
    public string? StatusMessage => _currentResult.StatusMessage;

    /// <summary>
    /// 触发暴毙危急警报的阈值，默认 0.30 (30%)
    /// </summary>
    public double EmergencyThreshold
    {
        get => _emergencyThreshold;
        set
        {
            if (Math.Abs(_emergencyThreshold - value) > 0.0001)
            {
                _emergencyThreshold = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCriticalAlert));
                OnPropertyChanged(nameof(AlertBorderBrush));
                OnPropertyChanged(nameof(RiskTierText));
            }
        }
    }

    /// <summary>
    /// 是否处于暴毙极度危险危急态 (P_lethal > EmergencyThreshold)
    /// </summary>
    public bool IsCriticalAlert => LethalRate > _emergencyThreshold;

    #endregion

    #region Progress Bar Proportions (0.0 ~ 1.0)

    /// <summary>
    /// 进度条胜率段比例
    /// </summary>
    public double WinBarRatio { get; private set; }

    /// <summary>
    /// 进度条平局段比例
    /// </summary>
    public double TieBarRatio { get; private set; }

    /// <summary>
    /// 进度条败率段总比例 (包含非暴毙败率与暴毙败率)
    /// </summary>
    public double LossBarRatio { get; private set; }

    /// <summary>
    /// 进度条非暴毙安全败率段比例
    /// </summary>
    public double SafeLossBarRatio { get; private set; }

    /// <summary>
    /// 进度条暴毙败率段比例
    /// </summary>
    public double LethalBarRatio { get; private set; }

    #endregion

    #region Formatted Strings (Zero GC Cached)

    public string WinRateText { get; private set; } = "0.0%";
    public string TieRateText { get; private set; } = "0.0%";
    public string LossRateText { get; private set; } = "0.0%";
    public string LethalRateText { get; private set; } = "0.0%";
    public string AverageDamageText { get; private set; } = "0.0";
    public string SimulationsCountText { get; private set; } = "0";
    public string RiskTierText { get; private set; } = "战力安全";

    #endregion

    #region Frozen Brushes (Zero GC & Thread-Safe)

    public SolidColorBrush RiskBrush => HudPalette.GetRiskColor(LethalRate);
    public SolidColorBrush WinBrush => HudPalette.SafeTempoBrush;
    public SolidColorBrush TieBrush => HudPalette.TacticalBlueBrush;
    public SolidColorBrush LossBrush => HudPalette.LegendaryGoldBrush;
    public SolidColorBrush LethalBrush => HudPalette.LethalRedBrush;
    public SolidColorBrush AlertBorderBrush => IsCriticalAlert ? HudPalette.LethalRedBrush : HudPalette.BorderSubtleBrush;

    #endregion

    #region Constructors

    /// <summary>
    /// 初始化仪表盘 ViewModel，默认装载安全先验
    /// </summary>
    public LethalGaugeViewModel()
    {
        Update(CombatSimulationResult.CreateDefaultSafePrior());
    }

    /// <summary>
    /// 使用指定的模拟结果初始化仪表盘 ViewModel
    /// </summary>
    public LethalGaugeViewModel(CombatSimulationResult initialResult)
    {
        Update(in initialResult);
    }

    #endregion

    #region Update & Business Logic

    /// <summary>
    /// 使用新的战力模拟快照更新 ViewModel，全链路零 GC 堆分配。
    /// </summary>
    public void Update(in CombatSimulationResult result)
    {
        bool alertChanged = (LethalRate > _emergencyThreshold) != (result.LethalRate > _emergencyThreshold);
        _currentResult = result;

        // 计算进度条四段式/三段式分布
        double win = result.WinRate;
        double tie = result.TieRate;
        double loss = result.LossRate;
        double lethal = result.LethalRate;

        // 规范化分布，确保四段相加趋于 1.0
        double safeLethal = Math.Min(loss, lethal);
        double safeLoss = Math.Max(0.0, loss - safeLethal);

        WinBarRatio = win;
        TieBarRatio = tie;
        LossBarRatio = loss;
        SafeLossBarRatio = safeLoss;
        LethalBarRatio = safeLethal;

        // 零 GC 格式化文本
        WinRateText = GaugeFormatter.FormatPercentage(win);
        TieRateText = GaugeFormatter.FormatPercentage(tie);
        LossRateText = GaugeFormatter.FormatPercentage(loss);
        LethalRateText = GaugeFormatter.FormatPercentage(lethal);
        AverageDamageText = GaugeFormatter.FormatDamage(result.AverageDamageTaken);
        SimulationsCountText = result.SimulationsCount.ToString();

        RiskTierText = result.LethalRate switch
        {
            > 0.30 => "极度危险",
            > 0.15 => "中度预警",
            _ => "战力安全"
        };

        NotifyAllPropertiesChanged(alertChanged);
    }

    /// <summary>
    /// 重置仪表盘至初始安全先验状态
    /// </summary>
    public void Reset()
    {
        Update(CombatSimulationResult.CreateDefaultSafePrior("Reset to Default Prior"));
    }

    /// <summary>
    /// 挂载并监听 Bob's Buddy 桥接器的模拟输出事件
    /// </summary>
    public void AttachToBridge(IBobsBuddyBridge bridge)
    {
        if (bridge == null) throw new ArgumentNullException(nameof(bridge));

        if (_attachedBridge != null)
        {
            DetachFromBridge();
        }

        _attachedBridge = bridge;
        _attachedBridge.SimulationCompleted += OnSimulationCompleted;

        // 立即同步当前最新结果
        Update(bridge.GetEffectiveResult());
    }

    /// <summary>
    /// 取消与 Bob's Buddy 桥接器的事件监听
    /// </summary>
    public void DetachFromBridge(IBobsBuddyBridge? bridge = null)
    {
        var target = bridge ?? _attachedBridge;
        if (target != null)
        {
            target.SimulationCompleted -= OnSimulationCompleted;
            if (ReferenceEquals(_attachedBridge, target))
            {
                _attachedBridge = null;
            }
        }
    }

    private void OnSimulationCompleted(CombatSimulationResult result)
    {
        Update(in result);
    }

    #endregion

    #region INotifyPropertyChanged & Clean up

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyAllPropertiesChanged(bool alertChanged)
    {
        var handler = PropertyChanged;
        if (handler == null) return;

        handler.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentResult)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(WinRate)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(TieRate)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(LossRate)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(LethalRate)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(PLethal)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(AverageDamageTaken)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(SimulationsCount)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(IsDegraded)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));

        handler.Invoke(this, new PropertyChangedEventArgs(nameof(WinBarRatio)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(TieBarRatio)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(LossBarRatio)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(SafeLossBarRatio)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(LethalBarRatio)));

        handler.Invoke(this, new PropertyChangedEventArgs(nameof(WinRateText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(TieRateText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(LossRateText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(LethalRateText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(AverageDamageText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(SimulationsCountText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(RiskTierText)));
        handler.Invoke(this, new PropertyChangedEventArgs(nameof(RiskBrush)));

        if (alertChanged)
        {
            handler.Invoke(this, new PropertyChangedEventArgs(nameof(IsCriticalAlert)));
            handler.Invoke(this, new PropertyChangedEventArgs(nameof(AlertBorderBrush)));
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        DetachFromBridge();
    }

    #endregion
}
