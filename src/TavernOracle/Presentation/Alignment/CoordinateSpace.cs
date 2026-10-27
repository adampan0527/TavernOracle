namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 锚点与视觉组件的坐标变换空间。
/// 支持从 WPF 设备无关像素 (DIP) 到 Win32 物理像素的多维度自由转换。
/// </summary>
public enum CoordinateSpace
{
    /// <summary>
    /// 相对窗口客户区左上角的 WPF 设备无关像素 (DIP)。
    /// 适用于挂载至 HDT Core.OverlayCanvas 或在游戏客户区内布局。
    /// </summary>
    RelativeDip = 0,

    /// <summary>
    /// 相对 16:9 核心活跃游戏视口左上角的 WPF 设备无关像素 (DIP)。
    /// 此时 (0,0) 为 16:9 视口本身的左上角。
    /// </summary>
    ViewportRelativeDip = 1,

    /// <summary>
    /// 屏幕绝对坐标下的 WPF 设备无关像素 (DIP)。
    /// 适用于独立置顶透明 WPF Window 的 Left / Top 定位。
    /// </summary>
    ScreenDip = 2,

    /// <summary>
    /// 屏幕绝对坐标下的原生物理像素 (Physical Pixels)。
    /// 适用于底层 Win32 API 交互与高精度像素级验证。
    /// </summary>
    ScreenPhysical = 3,

    /// <summary>
    /// 相对窗口客户区左上角的原生物理像素 (Physical Pixels)。
    /// </summary>
    RelativePhysical = 4,

    /// <summary>
    /// 相对 16:9 活跃视口左上角的原生物理像素 (Physical Pixels)。
    /// </summary>
    ViewportRelativePhysical = 5
}
