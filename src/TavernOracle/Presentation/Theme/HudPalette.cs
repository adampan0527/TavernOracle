using System.Windows;
using System.Windows.Media;

namespace TavernOracle.Presentation.Theme;

/// <summary>
/// TavernOracle HUD 调色板与视觉设计系统色彩规范。
/// 对齐 docs/dev_specifications.md 视觉系统设计准则：
/// “暗夜魔幻酒馆 (Dark Fantasy Tavern) + 现代战术电竞 HUD (Tactical Esports Overlay)”。
/// 所有 SolidColorBrush 均经过 Freeze() 处理，实现零堆分配与跨线程安全访问。
/// </summary>
public static class HudPalette
{
    #region Hex Color Constants

    /// <summary>
    /// 背景深空：底板画布 (0.88 半透明深空亚克力磨砂背景)
    /// </summary>
    public const string HexBackgroundCanvas = "#E0161B22"; // rgba(22, 27, 34, 0.88)

    /// <summary>
    /// 背景纯深空 (Opaque)
    /// </summary>
    public const string HexBackgroundDeepSpace = "#0D1117";

    /// <summary>
    /// 传奇金：三连对子、升本时机、核心 Carry 随从微标
    /// </summary>
    public const string HexLegendaryGold = "#F59E0B";

    /// <summary>
    /// 传奇浅金：对子徽标高亮高光
    /// </summary>
    public const string HexLegendaryGoldLight = "#FBBF24";

    /// <summary>
    /// 战术蓝：状态机监控、金币收益模型、中立节奏提示
    /// </summary>
    public const string HexTacticalBlue = "#38BDF8";

    /// <summary>
    /// 安全翠绿：即时战力加成、安全对战胜率、推荐买入单卡
    /// </summary>
    public const string HexSafeTempo = "#10B981";

    /// <summary>
    /// 暴毙警报红：生死线突变 (P_lethal > 30%)、血量危机、避坑警告
    /// </summary>
    public const string HexLethalRed = "#EF4444";

    /// <summary>
    /// 暴毙警报红半透明微光 (20% 不透明度)
    /// </summary>
    public const string HexLethalRedSubtle = "#33EF4444";

    /// <summary>
    /// 魔能协同紫：跨随从强联动、种族核心光环、亡语滚雪球机制
    /// </summary>
    public const string HexArcanePurple = "#A855F7";

    /// <summary>
    /// 主文本白
    /// </summary>
    public const string HexTextPrimary = "#F3F4F6";

    /// <summary>
    /// 次要文本灰
    /// </summary>
    public const string HexTextSecondary = "#9CA3AF";

    /// <summary>
    /// 边框微光线
    /// </summary>
    public const string HexBorderSubtle = "#30363D";

    #endregion

    #region WPF Colors

    public static Color ColorBackgroundCanvas { get; } = Color.FromArgb(224, 22, 27, 34);
    public static Color ColorBackgroundDeepSpace { get; } = Color.FromRgb(13, 17, 23);
    public static Color ColorLegendaryGold { get; } = Color.FromRgb(245, 158, 11);
    public static Color ColorLegendaryGoldLight { get; } = Color.FromRgb(251, 191, 36);
    public static Color ColorTacticalBlue { get; } = Color.FromRgb(56, 189, 248);
    public static Color ColorSafeTempo { get; } = Color.FromRgb(16, 185, 129);
    public static Color ColorLethalRed { get; } = Color.FromRgb(239, 68, 68);
    public static Color ColorLethalRedSubtle { get; } = Color.FromArgb(51, 239, 68, 68);
    public static Color ColorArcanePurple { get; } = Color.FromRgb(168, 85, 247);
    public static Color ColorTextPrimary { get; } = Color.FromRgb(243, 244, 246);
    public static Color ColorTextSecondary { get; } = Color.FromRgb(156, 163, 175);
    public static Color ColorBorderSubtle { get; } = Color.FromRgb(48, 54, 61);

    #endregion

    #region Frozen Brushes (Zero GC & Thread-Safe)

    public static SolidColorBrush BackgroundCanvasBrush { get; } = CreateFrozenBrush(ColorBackgroundCanvas);
    public static SolidColorBrush BackgroundDeepSpaceBrush { get; } = CreateFrozenBrush(ColorBackgroundDeepSpace);
    public static SolidColorBrush LegendaryGoldBrush { get; } = CreateFrozenBrush(ColorLegendaryGold);
    public static SolidColorBrush LegendaryGoldLightBrush { get; } = CreateFrozenBrush(ColorLegendaryGoldLight);
    public static SolidColorBrush TacticalBlueBrush { get; } = CreateFrozenBrush(ColorTacticalBlue);
    public static SolidColorBrush SafeTempoBrush { get; } = CreateFrozenBrush(ColorSafeTempo);
    public static SolidColorBrush LethalRedBrush { get; } = CreateFrozenBrush(ColorLethalRed);
    public static SolidColorBrush LethalRedSubtleBrush { get; } = CreateFrozenBrush(ColorLethalRedSubtle);
    public static SolidColorBrush ArcanePurpleBrush { get; } = CreateFrozenBrush(ColorArcanePurple);
    public static SolidColorBrush TextPrimaryBrush { get; } = CreateFrozenBrush(ColorTextPrimary);
    public static SolidColorBrush TextSecondaryBrush { get; } = CreateFrozenBrush(ColorTextSecondary);
    public static SolidColorBrush BorderSubtleBrush { get; } = CreateFrozenBrush(ColorBorderSubtle);

    /// <summary>
    /// 传奇金渐变：用于三连对子微标外框 (Gold Gradient)
    /// </summary>
    public static LinearGradientBrush LegendaryGoldGradientBrush { get; } = CreateFrozenLinearGradient(
        ColorLegendaryGold,
        ColorLegendaryGoldLight,
        new Point(0, 0),
        new Point(1, 1));

    /// <summary>
    /// 微标评分药丸背景 (半透明深色)
    /// </summary>
    public static SolidColorBrush BadgePillBackgroundBrush { get; } = CreateFrozenBrush(Color.FromArgb(200, 13, 17, 23));

    /// <summary>
    /// 避坑陷阱微标弱化背景 (半透明灰深色)
    /// </summary>
    public static SolidColorBrush TrapMutedBackgroundBrush { get; } = CreateFrozenBrush(Color.FromArgb(180, 31, 41, 55));

    /// <summary>
    /// 避坑陷阱微标弱化边框灰
    /// </summary>
    public static SolidColorBrush TrapMutedBorderBrush { get; } = CreateFrozenBrush(Color.FromRgb(107, 114, 128));

    /// <summary>
    /// 魔能紫微标微光背景
    /// </summary>
    public static SolidColorBrush BadgePurpleTintBackgroundBrush { get; } = CreateFrozenBrush(Color.FromArgb(220, 24, 16, 36));

    /// <summary>
    /// 翡翠绿微标微光背景
    /// </summary>
    public static SolidColorBrush BadgeGreenTintBackgroundBrush { get; } = CreateFrozenBrush(Color.FromArgb(220, 12, 32, 24));

    #endregion

    /// <summary>
    /// 根据当前预估暴毙率 P_lethal 动态映射警示色彩
    /// </summary>
    /// <param name="lethalRate">暴毙概率 (0.0 ~ 1.0)</param>
    /// <returns>对应的冷冻画刷</returns>
    public static SolidColorBrush GetRiskColor(double lethalRate)
    {
        if (lethalRate > 0.30)
        {
            return LethalRedBrush;
        }

        if (lethalRate > 0.15)
        {
            return LegendaryGoldBrush;
        }

        return SafeTempoBrush;
    }

    /// <summary>
    /// 创建并冻结一个纯色画刷，确保零 GC 开销与跨线程渲染安全。
    /// </summary>
    public static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// 创建并冻结一个线性渐变画刷，确保零 GC 开销与跨线程渲染安全。
    /// </summary>
    public static LinearGradientBrush CreateFrozenLinearGradient(Color startColor, Color endColor, Point startPoint, Point endPoint)
    {
        var brush = new LinearGradientBrush(startColor, endColor, startPoint, endPoint);
        brush.Freeze();
        return brush;
    }
}
