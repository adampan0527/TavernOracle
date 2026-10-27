using System.Windows;
using System.Windows.Media;

namespace TavernOracle.Presentation.Theme;

/// <summary>
/// TavernOracle HUD 控件统一布局、圆角与排版规范。
/// 契约源自 docs/dev_specifications.md 第 3 节规范。
/// </summary>
public static class HudStyles
{
    #region Corner Radii

    /// <summary>
    /// 胶囊化药丸圆角 (Pill UI)
    /// </summary>
    public static CornerRadius PillCornerRadius { get; } = new(12);

    /// <summary>
    /// 卡面头顶微标圆角 (Badge)
    /// </summary>
    public static CornerRadius BadgeCornerRadius { get; } = new(6);

    /// <summary>
    /// 顶部大局横条圆角 (Banner)
    /// </summary>
    public static CornerRadius BannerCornerRadius { get; } = new(8);

    /// <summary>
    /// 仪表盘面板圆角 (Gauge)
    /// </summary>
    public static CornerRadius PanelCornerRadius { get; } = new(10);

    #endregion

    #region Margins & Padding

    public static Thickness PillPadding { get; } = new(8, 4, 8, 4);
    public static Thickness BadgePadding { get; } = new(6, 2, 6, 2);
    public static Thickness BannerPadding { get; } = new(16, 8, 16, 8);
    public static Thickness PanelPadding { get; } = new(12, 10, 12, 10);

    #endregion

    #region Font Configurations

    /// <summary>
    /// 推荐标准 HUD 无衬线字体簇
    /// </summary>
    public static FontFamily StandardFontFamily { get; } = new("Segoe UI, Microsoft YaHei, sans-serif");

    public const double FontSizeSmall = 11.0;
    public const double FontSizeNormal = 13.0;
    public const double FontSizeTitle = 15.0;
    public const double FontSizeHero = 18.0;

    #endregion

    #region Opacities & Blur

    /// <summary>
    /// 亚克力磨砂半透明度
    /// </summary>
    public const double AcrylicOpacity = 0.88;

    /// <summary>
    /// 弱化非活动状态透明度
    /// </summary>
    public const double DimmedOpacity = 0.35;

    #endregion
}
