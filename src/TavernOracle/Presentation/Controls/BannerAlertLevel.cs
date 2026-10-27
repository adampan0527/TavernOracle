using System;
using System.Windows.Media;
using TavernOracle.Decision;
using TavernOracle.Presentation.Theme;

namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 顶部决策横条视觉告警级别。
/// 映射对局危险程度与宏观战术紧迫度，对齐 docs/dev_specifications.md 视觉系统设计准则。
/// </summary>
public enum BannerAlertLevel
{
    /// <summary>
    /// 🟢 安全/节奏局势 (Normal / Tempo)：安全局势，推荐稳健贪婪升本或稳固战力。
    /// 对应翠绿色 (#10B981, SafeTempoBrush)。
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 🟢 节奏状态别名 (Tempo)
    /// </summary>
    Tempo = Normal,

    /// <summary>
    /// 🟡 战力警戒 (Warning)：战力显著承压，提示对子留牌或停止盲目刷新。
    /// 对应传奇金/警示黄 (#F59E0B, LegendaryGoldBrush)。
    /// </summary>
    Warning = 1,

    /// <summary>
    /// 🔴 暴毙危机 (Critical / Emergency)：生死线突变 (P_lethal &gt; 30%)，高亮紧急苟活指令。
    /// 对应暴毙警报红 (#EF4444, LethalRedBrush)。
    /// </summary>
    Critical = 2,

    /// <summary>
    /// 🔴 紧急苟活别名 (Emergency)
    /// </summary>
    Emergency = Critical
}

/// <summary>
/// 告警级别扩展方法与色彩/文案映射工具。
/// </summary>
public static class BannerAlertLevelExtensions
{
    #region Frozen Badge Background Brushes (Zero GC)

    /// <summary>
    /// 🟢 正常状态微标半透明背景 (rgba(16, 185, 129, 0.20))
    /// </summary>
    public static SolidColorBrush BadgeNormalBgBrush { get; } =
        HudPalette.CreateFrozenBrush(Color.FromArgb(51, 16, 185, 129));

    /// <summary>
    /// 🟡 警戒状态微标半透明背景 (rgba(245, 158, 11, 0.20))
    /// </summary>
    public static SolidColorBrush BadgeWarningBgBrush { get; } =
        HudPalette.CreateFrozenBrush(Color.FromArgb(51, 245, 158, 11));

    /// <summary>
    /// 🔴 暴毙危机微标半透明背景 (rgba(239, 68, 68, 0.25))
    /// </summary>
    public static SolidColorBrush BadgeCriticalBgBrush { get; } =
        HudPalette.CreateFrozenBrush(Color.FromArgb(64, 239, 68, 68));

    #endregion

    /// <summary>
    /// 获取当前告警级别对应的主题高亮冷冻画刷
    /// </summary>
    public static SolidColorBrush GetAccentBrush(this BannerAlertLevel level) => level switch
    {
        BannerAlertLevel.Warning => HudPalette.LegendaryGoldBrush,
        BannerAlertLevel.Critical => HudPalette.LethalRedBrush,
        _ => HudPalette.SafeTempoBrush
    };

    /// <summary>
    /// 获取当前告警级别微标胶囊的半透明背景冷冻画刷
    /// </summary>
    public static SolidColorBrush GetBadgeBackgroundBrush(this BannerAlertLevel level) => level switch
    {
        BannerAlertLevel.Warning => BadgeWarningBgBrush,
        BannerAlertLevel.Critical => BadgeCriticalBgBrush,
        _ => BadgeNormalBgBrush
    };

    /// <summary>
    /// 获取告警级别标准微标徽章文本
    /// </summary>
    public static string GetBadgeText(this BannerAlertLevel level) => level switch
    {
        BannerAlertLevel.Warning => "🟡 战力警戒",
        BannerAlertLevel.Critical => "🔴 暴毙危机",
        _ => "🟢 稳健节奏"
    };

    /// <summary>
    /// 获取告警级别对应的默认标题前缀
    /// </summary>
    public static string GetDefaultHeadline(this BannerAlertLevel level) => level switch
    {
        BannerAlertLevel.Warning => "【战力预警】",
        BannerAlertLevel.Critical => "【绝地保命】",
        _ => "【本回合推荐】"
    };

    /// <summary>
    /// 根据暴毙率 P_lethal 映射告警级别：
    /// P_lethal &gt; 0.30 -&gt; Critical
    /// P_lethal &gt; 0.15 -&gt; Warning
    /// 其他 -&gt; Normal
    /// </summary>
    public static BannerAlertLevel FromLethalRate(double lethalRate)
    {
        if (lethalRate > 0.30)
        {
            return BannerAlertLevel.Critical;
        }

        if (lethalRate > 0.15)
        {
            return BannerAlertLevel.Warning;
        }

        return BannerAlertLevel.Normal;
    }

    /// <summary>
    /// 从决策引擎的 RiskTier 转换为 BannerAlertLevel
    /// </summary>
    public static BannerAlertLevel FromRiskTier(RiskTier riskTier) => riskTier switch
    {
        RiskTier.Critical => BannerAlertLevel.Critical,
        RiskTier.Warning => BannerAlertLevel.Warning,
        _ => BannerAlertLevel.Normal
    };
}
