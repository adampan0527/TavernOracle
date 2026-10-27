using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TavernOracle.Presentation.Settings;

/// <summary>
/// 玩家个性化配置与快捷控制面板视图模型 (T22 ViewModel)。
/// 提供响应式属性绑定、参数边界规整、即时事件广播以及自动持久化存储。
/// 严格遵守暴雪“纸笔准则”。
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly PluginSettings _settings;
    private string? _settingsFilePath;
    private bool _autoSaveEnabled = true;

    /// <summary>
    /// 当任一设置项发生变化并已同步生效时广播此事件。
    /// 订阅者可获取最新的只读配置副本以实时更新 HUD 显隐或决策权重。
    /// </summary>
    public event EventHandler<PluginSettings>? SettingsChanged;

    /// <summary>
    /// 标准 INotifyPropertyChanged 属性变更事件。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 初始化设置视图模型。
    /// </summary>
    /// <param name="settings">初始配置对象；若为 null 则自动加载或使用默认配置</param>
    /// <param name="settingsFilePath">持久化文件路径；若为 null 则采用系统默认路径</param>
    public SettingsViewModel(PluginSettings? settings = null, string? settingsFilePath = null)
    {
        _settings = settings ?? PluginSettings.Load(settingsFilePath);
        _settings.ClampValues();
        _settingsFilePath = settingsFilePath;
    }

    #region Bindable Properties

    /// <summary>
    /// HUD 悬浮层全局总开关。
    /// </summary>
    public bool IsOverlayEnabled
    {
        get => _settings.IsOverlayEnabled;
        set
        {
            if (_settings.IsOverlayEnabled != value)
            {
                _settings.IsOverlayEnabled = value;
                OnPropertyChanged();
                NotifySettingsChanged();
            }
        }
    }

    /// <summary>
    /// 激进/保守度即时战力调节权重 (范围: [0.5, 2.5])。
    /// </summary>
    public double TempoWeight
    {
        get => _settings.TempoWeight;
        set
        {
            double clamped = (double.IsNaN(value) || double.IsInfinity(value))
                ? PluginSettings.DefaultTempoWeight
                : MathEx.Clamp(value, PluginSettings.MinTempoWeight, PluginSettings.MaxTempoWeight);

            if (Math.Abs(_settings.TempoWeight - clamped) > 0.0001)
            {
                _settings.TempoWeight = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TempoWeightDisplay));
                NotifySettingsChanged();
            }
        }
    }

    /// <summary>
    /// 激进/保守度友好的文本呈现标签。
    /// </summary>
    public string TempoWeightDisplay
    {
        get
        {
            if (TempoWeight < 0.85)
            {
                return $"{TempoWeight:F2}x · 偏重发育 (Greed / Scale)";
            }
            if (TempoWeight > 1.25)
            {
                return $"{TempoWeight:F2}x · 即时战力 (Tempo / Survival)";
            }
            return $"{TempoWeight:F2}x · 均衡节奏 (Balanced)";
        }
    }

    /// <summary>
    /// 生死线暴毙报警警戒线门槛 (范围: [0.10, 0.50])。
    /// </summary>
    public double LethalThreshold
    {
        get => _settings.LethalThreshold;
        set
        {
            double clamped = (double.IsNaN(value) || double.IsInfinity(value))
                ? PluginSettings.DefaultLethalThreshold
                : MathEx.Clamp(value, PluginSettings.MinLethalThreshold, PluginSettings.MaxLethalThreshold);

            if (Math.Abs(_settings.LethalThreshold - clamped) > 0.0001)
            {
                _settings.LethalThreshold = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LethalThresholdDisplay));
                NotifySettingsChanged();
            }
        }
    }

    /// <summary>
    /// 暴毙警戒门槛友好的百分比呈现文本。
    /// </summary>
    public string LethalThresholdDisplay => $"{LethalThreshold:P0}";

    /// <summary>
    /// 顶部大局策略横条可见性。
    /// </summary>
    public bool IsBannerVisible
    {
        get => _settings.IsBannerVisible;
        set
        {
            if (_settings.IsBannerVisible != value)
            {
                _settings.IsBannerVisible = value;
                OnPropertyChanged();
                NotifySettingsChanged();
            }
        }
    }

    /// <summary>
    /// 随从头顶战术微标可见性。
    /// </summary>
    public bool AreBadgesVisible
    {
        get => _settings.AreBadgesVisible;
        set
        {
            if (_settings.AreBadgesVisible != value)
            {
                _settings.AreBadgesVisible = value;
                OnPropertyChanged();
                NotifySettingsChanged();
            }
        }
    }

    /// <summary>
    /// 胜率与暴毙概率仪表盘可见性。
    /// </summary>
    public bool IsLethalGaugeVisible
    {
        get => _settings.IsLethalGaugeVisible;
        set
        {
            if (_settings.IsLethalGaugeVisible != value)
            {
                _settings.IsLethalGaugeVisible = value;
                OnPropertyChanged();
                NotifySettingsChanged();
            }
        }
    }

    /// <summary>
    /// 是否在修改属性时自动落盘保存 (默认: true)。
    /// </summary>
    public bool AutoSaveEnabled
    {
        get => _autoSaveEnabled;
        set
        {
            if (_autoSaveEnabled != value)
            {
                _autoSaveEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 当前持久化文件绝对路径。
    /// </summary>
    public string? SettingsFilePath
    {
        get => _settingsFilePath;
        set
        {
            if (_settingsFilePath != value)
            {
                _settingsFilePath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 获取当前生效配置的独立深拷贝副本。
    /// </summary>
    public PluginSettings CurrentSettings => _settings.Clone();

    #endregion

    #region Actions & Operations

    /// <summary>
    /// 重置所有配置项为官方默认值并广播更新。
    /// </summary>
    public void ResetDefaults()
    {
        _settings.ResetToDefaults();

        OnPropertyChanged(nameof(IsOverlayEnabled));
        OnPropertyChanged(nameof(TempoWeight));
        OnPropertyChanged(nameof(TempoWeightDisplay));
        OnPropertyChanged(nameof(LethalThreshold));
        OnPropertyChanged(nameof(LethalThresholdDisplay));
        OnPropertyChanged(nameof(IsBannerVisible));
        OnPropertyChanged(nameof(AreBadgesVisible));
        OnPropertyChanged(nameof(IsLethalGaugeVisible));

        NotifySettingsChanged();
    }

    /// <summary>
    /// 从外部配置对象同步更新当前视图模型。
    /// </summary>
    public void UpdateFrom(PluginSettings other)
    {
        if (other is null) return;

        IsOverlayEnabled = other.IsOverlayEnabled;
        TempoWeight = other.TempoWeight;
        LethalThreshold = other.LethalThreshold;
        IsBannerVisible = other.IsBannerVisible;
        AreBadgesVisible = other.AreBadgesVisible;
        IsLethalGaugeVisible = other.IsLethalGaugeVisible;
    }

    /// <summary>
    /// 立即将当前内存配置持久化至本地磁盘并通知订阅者。
    /// </summary>
    public bool Save()
    {
        bool success = _settings.Save(_settingsFilePath);
        SettingsChanged?.Invoke(this, _settings.Clone());
        return success;
    }

    #endregion

    #region Internal Helpers

    private void NotifySettingsChanged()
    {
        if (_autoSaveEnabled)
        {
            _settings.Save(_settingsFilePath);
        }

        SettingsChanged?.Invoke(this, _settings.Clone());
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
