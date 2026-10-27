using System;
using System.Windows;
using TavernOracle.Presentation.Overlay;
using DpiChangedEventArgs = TavernOracle.Presentation.Alignment.DpiChangedEventArgs;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 炉石游戏窗口追踪与自适应吸附对齐服务契约接口。
/// 严格遵循暴雪“纸笔准则（Pencil & Paper Rule）”，仅支持只读捕获窗口位置与尺寸，
/// 零输入模拟，零内存注入。
/// </summary>
public interface IGameWindowTracker : IDisposable
{
    /// <summary>
    /// 当前追踪的目标游戏原生窗口句柄 (HWND)
    /// </summary>
    IntPtr WindowHandle { get; }

    /// <summary>
    /// 当前是否已成功锁定并正在追踪游戏窗口
    /// </summary>
    bool IsTracking { get; }

    /// <summary>
    /// 目标窗口外框屏幕绝对矩形 (原生物理像素，包含边框与标题栏)
    /// </summary>
    Rect WindowRect { get; }

    /// <summary>
    /// 目标窗口客户区屏幕绝对矩形 (原生物理像素，不含外边框与标题栏)
    /// </summary>
    Rect ClientRect { get; }

    /// <summary>
    /// 当前窗口所在监视器的 DPI (例如 96, 120, 144, 192)
    /// </summary>
    uint Dpi { get; }

    /// <summary>
    /// 当前 DPI 缩放比例因子 (例如 1.0, 1.25, 1.5, 2.0)
    /// </summary>
    double DpiScale { get; }

    /// <summary>
    /// 经过 Letterbox / Pillarbox 自适应黑边计算后的 16:9 核心活跃视口几何模型
    /// </summary>
    GameViewport Viewport { get; }

    /// <summary>
    /// 窗口位置或尺寸发生变化事件
    /// </summary>
    event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;

    /// <summary>
    /// 系统 DPI 或监视器缩放发生变化事件
    /// </summary>
    event EventHandler<DpiChangedEventArgs>? DpiChanged;

    /// <summary>
    /// 核心游戏视口（尺寸、黑边或位置）发生变化事件
    /// </summary>
    event EventHandler<GameViewportChangedEventArgs>? ViewportChanged;

    /// <summary>
    /// 启动游戏窗口追踪循环
    /// </summary>
    /// <param name="targetHandle">可选指定目标 HWND，若为 IntPtr.Zero 则自动查找“Hearthstone”主窗口</param>
    /// <param name="pollIntervalMs">轮询周期毫秒数 (默认 50ms)</param>
    void StartTracking(IntPtr targetHandle = default, int pollIntervalMs = 50);

    /// <summary>
    /// 停止游戏窗口追踪
    /// </summary>
    void StopTracking();

    /// <summary>
    /// 立即强制执行一次窗口几何状态同步检测（支持外部主动事件驱动）
    /// </summary>
    /// <returns>若窗口有效且位置成功更新返回 true</returns>
    bool UpdateNow();

    /// <summary>
    /// 将透明覆盖层 (ITransparentOverlay) 绑定至此追踪器，自动保持几何吸附同步
    /// </summary>
    /// <param name="overlay">透明 HUD 覆盖层实例</param>
    void AttachOverlay(ITransparentOverlay overlay);

    /// <summary>
    /// 解除透明覆盖层的自动吸附绑定
    /// </summary>
    /// <param name="overlay">透明 HUD 覆盖层实例</param>
    void DetachOverlay(ITransparentOverlay overlay);

    /// <summary>
    /// 将物理像素坐标转换为 WPF 设备无关像素 (DIP)
    /// </summary>
    Point PhysicalToDip(Point physicalPoint);

    /// <summary>
    /// 将 WPF 设备无关像素 (DIP) 转换为物理像素坐标
    /// </summary>
    Point DipToPhysical(Point dipPoint);

    /// <summary>
    /// 将物理像素矩形转换为 WPF 设备无关矩形 (DIP)
    /// </summary>
    Rect PhysicalToDip(Rect physicalRect);

    /// <summary>
    /// 将 WPF 设备无关矩形 (DIP) 转换为物理像素矩形
    /// </summary>
    Rect DipToPhysical(Rect dipRect);
}
