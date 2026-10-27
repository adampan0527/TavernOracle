using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TavernOracle.Presentation.Settings;

/// <summary>
/// 玩家个性化配置与风控参数持久化数据模型 (T22)。
/// 负责存储 HUD 总体开关、推荐激进/保守度权重、暴毙警戒线门槛以及各个表现层组件的可见性。
/// 支持零异常防御的 JSON 本地文件序列化与反序列化，并提供参数边界规整与深拷贝能力。
/// 严格遵守暴雪“纸笔准则（Pencil &amp; Paper Rule）”。
/// </summary>
public sealed class PluginSettings : IEquatable<PluginSettings>
{
    #region Constants & Defaults

    /// <summary>
    /// 默认激进/保守度调节权重 (1.0 = 标准均衡节奏)
    /// </summary>
    public const double DefaultTempoWeight = 1.0;

    /// <summary>
    /// 激进/保守度调节权重允许的最小值 (0.5 = 极度贪心/经济发育)
    /// </summary>
    public const double MinTempoWeight = 0.5;

    /// <summary>
    /// 激进/保守度调节权重允许的最大值 (2.5 = 极度保命/即时战力拉满)
    /// </summary>
    public const double MaxTempoWeight = 2.5;

    /// <summary>
    /// 默认生死线暴毙危机突变警戒门槛 (0.30 = 暴毙率 > 30% 触发绝境求生)
    /// </summary>
    public const double DefaultLethalThreshold = 0.30;

    /// <summary>
    /// 暴毙危机门槛允许的最小值 (0.10 = 敏感预警)
    /// </summary>
    public const double MinLethalThreshold = 0.10;

    /// <summary>
    /// 暴毙危机门槛允许的最大值 (0.50 = 沉着容错)
    /// </summary>
    public const double MaxLethalThreshold = 0.50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Configuration Properties

    /// <summary>
    /// HUD 悬浮层全局总开关 (默认: true)。
    /// 当关闭时，所有横条、微标与仪表盘均不予呈现。
    /// </summary>
    [JsonPropertyName("isOverlayEnabled")]
    public bool IsOverlayEnabled { get; set; } = true;

    /// <summary>
    /// 激进/保守度即时战力调节权重 (默认: 1.0, 范围: [0.5, 2.5])。
    /// 数值越大越优先考虑即时保命战力 (w_tempo 增益)；数值越小越偏向贪经济/升本与留对子。
    /// </summary>
    [JsonPropertyName("tempoWeight")]
    public double TempoWeight { get; set; } = DefaultTempoWeight;

    /// <summary>
    /// 生死线暴毙报警警戒线门槛 (默认: 0.30, 范围: [0.10, 0.50])。
    /// 当对战暴毙率超过此门槛时，顶部横条呈现红色高危警报并切断贪婪协同评分。
    /// </summary>
    [JsonPropertyName("lethalThreshold")]
    public double LethalThreshold { get; set; } = DefaultLethalThreshold;

    /// <summary>
    /// 顶部大局策略横条 (Decision Banner) 是否可见 (默认: true)。
    /// </summary>
    [JsonPropertyName("isBannerVisible")]
    public bool IsBannerVisible { get; set; } = true;

    /// <summary>
    /// 随从头顶三连/对子/核心微标 (Minion Badges) 是否可见 (默认: true)。
    /// </summary>
    [JsonPropertyName("areBadgesVisible")]
    public bool AreBadgesVisible { get; set; } = true;

    /// <summary>
    /// 胜率与暴毙概率仪表盘 (Lethal Gauge) 是否可见 (默认: true)。
    /// </summary>
    [JsonPropertyName("isLethalGaugeVisible")]
    public bool IsLethalGaugeVisible { get; set; } = true;

    #endregion

    #region Validation & Clamping

    /// <summary>
    /// 对浮点数参数进行安全边界规整，抵御 NaN、无穷大或越界非法值。
    /// </summary>
    public void ClampValues()
    {
        if (double.IsNaN(TempoWeight) || double.IsInfinity(TempoWeight))
        {
            TempoWeight = DefaultTempoWeight;
        }
        else
        {
            TempoWeight = MathEx.Clamp(TempoWeight, MinTempoWeight, MaxTempoWeight);
        }

        if (double.IsNaN(LethalThreshold) || double.IsInfinity(LethalThreshold))
        {
            LethalThreshold = DefaultLethalThreshold;
        }
        else
        {
            LethalThreshold = MathEx.Clamp(LethalThreshold, MinLethalThreshold, MaxLethalThreshold);
        }
    }

    /// <summary>
    /// 重置所有配置项为官方默认值。
    /// </summary>
    public void ResetToDefaults()
    {
        IsOverlayEnabled = true;
        TempoWeight = DefaultTempoWeight;
        LethalThreshold = DefaultLethalThreshold;
        IsBannerVisible = true;
        AreBadgesVisible = true;
        IsLethalGaugeVisible = true;
    }

    /// <summary>
    /// 深拷贝当前配置对象。
    /// </summary>
    public PluginSettings Clone()
    {
        return new PluginSettings
        {
            IsOverlayEnabled = IsOverlayEnabled,
            TempoWeight = TempoWeight,
            LethalThreshold = LethalThreshold,
            IsBannerVisible = IsBannerVisible,
            AreBadgesVisible = AreBadgesVisible,
            IsLethalGaugeVisible = IsLethalGaugeVisible
        };
    }

    #endregion

    #region Serialization & File Persistence

    /// <summary>
    /// 获取默认的本地配置存储绝对路径。
    /// 路径形如：%APPDATA%\HearthstoneDeckTracker\TavernOracle\settings.json
    /// </summary>
    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "HearthstoneDeckTracker", "TavernOracle");
        return Path.Combine(dir, "settings.json");
    }

    /// <summary>
    /// 将当前配置持久化保存至本地 JSON 文件。
    /// 采用防御式写入，确保零未经处理异常。
    /// </summary>
    /// <param name="filePath">目标文件路径；若为 null 则使用默认路径</param>
    /// <returns>写入成功返回 true；发生 I/O 错误返回 false</returns>
    public bool Save(string? filePath = null)
    {
        try
        {
            ClampValues();
            string targetPath = !string.IsNullOrWhiteSpace(filePath) ? filePath : GetDefaultFilePath();

            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(targetPath, json);
            return true;
        }
        catch
        {
            // Defensive degradation: Zero unhandled exceptions
            return false;
        }
    }

    /// <summary>
    /// 从本地 JSON 文件反序列化加载配置。
    /// 若文件不存在、损坏或反序列化失败，安全降级返回包含默认值的有效实例。
    /// </summary>
    /// <param name="filePath">源文件路径；若为 null 则使用默认路径</param>
    /// <returns>反序列化后的配置实例，或全新默认配置</returns>
    public static PluginSettings Load(string? filePath = null)
    {
        try
        {
            string targetPath = !string.IsNullOrWhiteSpace(filePath) ? filePath : GetDefaultFilePath();
            if (!File.Exists(targetPath))
            {
                return new PluginSettings();
            }

            string json = File.ReadAllText(targetPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PluginSettings();
            }

            var settings = JsonSerializer.Deserialize<PluginSettings>(json, JsonOptions);
            if (settings != null)
            {
                settings.ClampValues();
                return settings;
            }
        }
        catch
        {
            // Defensive fallback on corrupted files
        }

        return new PluginSettings();
    }

    #endregion

    #region Equality & HashCode

    public bool Equals(PluginSettings? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return IsOverlayEnabled == other.IsOverlayEnabled &&
               Math.Abs(TempoWeight - other.TempoWeight) < 0.0001 &&
               Math.Abs(LethalThreshold - other.LethalThreshold) < 0.0001 &&
               IsBannerVisible == other.IsBannerVisible &&
               AreBadgesVisible == other.AreBadgesVisible &&
               IsLethalGaugeVisible == other.IsLethalGaugeVisible;
    }

    public override bool Equals(object? obj) => Equals(obj as PluginSettings);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            IsOverlayEnabled,
            Math.Round(TempoWeight, 4),
            Math.Round(LethalThreshold, 4),
            IsBannerVisible,
            AreBadgesVisible,
            IsLethalGaugeVisible);
    }

    public override string ToString()
    {
        return $"[PluginSettings: Overlay={IsOverlayEnabled}, Tempo={TempoWeight:F2}, Lethal={LethalThreshold:P0}, Banner={IsBannerVisible}, Badges={AreBadgesVisible}, Gauge={IsLethalGaugeVisible}]";
    }

    #endregion
}
