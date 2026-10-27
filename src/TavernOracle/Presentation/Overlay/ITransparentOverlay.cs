using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace TavernOracle.Presentation.Overlay;

/// <summary>
/// TavernOracle 透明点击穿透覆盖层契约接口。
/// 支持两种运行形态：
/// 1. 独立运行/脱机模式：透明顶层置顶 WPF 窗体 (WS_EX_TRANSPARENT | WS_EX_LAYERED)。
/// 2. HDT 宿主模式：附加挂载至 HDT 的 Core.OverlayCanvas 容器中。
/// </summary>
public interface ITransparentOverlay : IDisposable
{
    /// <summary>
    /// 当前覆盖层是否处于可见状态
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// 当前宿主窗口是否已启用 Win32 点击穿透样式
    /// </summary>
    bool IsClickThrough { get; }

    /// <summary>
    /// 宿主原生窗口句柄 (HWND)。在独立窗口形态下有效，若为纯外部 Canvas 挂载或未就绪时为 IntPtr.Zero。
    /// </summary>
    IntPtr WindowHandle { get; }

    /// <summary>
    /// 承载所有 HUD 子视觉元素（微标、横条、仪表盘）的根 Canvas 容器
    /// </summary>
    Canvas OverlayContainer { get; }

    /// <summary>
    /// 当前挂载的所有子视觉元素只读集合
    /// </summary>
    IReadOnlyList<UIElement> Children { get; }

    /// <summary>
    /// 当前是否已被挂载到外部 Canvas (例如 HDT 的 Core.OverlayCanvas)
    /// </summary>
    bool IsAttachedToHostCanvas { get; }

    /// <summary>
    /// 挂载的外部宿主 Canvas 实例 (未挂载时为 null)
    /// </summary>
    Canvas? HostCanvas { get; }

    /// <summary>
    /// 挂载/附加至外部指定的 Canvas (例如 HDT Core.OverlayCanvas)
    /// </summary>
    /// <param name="hostCanvas">外部 Canvas 容器</param>
    void AttachToHostCanvas(Canvas hostCanvas);

    /// <summary>
    /// 从外部宿主 Canvas 分离，恢复独立容器管理
    /// </summary>
    void DetachFromHostCanvas();

    /// <summary>
    /// 显示覆盖层
    /// </summary>
    void ShowOverlay();

    /// <summary>
    /// 隐藏覆盖层
    /// </summary>
    void HideOverlay();

    /// <summary>
    /// 调整覆盖层的位置与尺寸 (支持 T19 窗口吸附与分辨率适配)
    /// </summary>
    /// <param name="left">左上角 X 像素坐标</param>
    /// <param name="top">左上角 Y 像素坐标</param>
    /// <param name="width">覆盖层宽度</param>
    /// <param name="height">覆盖层高度</param>
    void SetBounds(double left, double top, double width, double height);

    /// <summary>
    /// 添加一个子视觉元素（微标、横条、仪表盘等）
    /// </summary>
    /// <param name="element">WPF 视觉元素</param>
    void AddChild(UIElement element);

    /// <summary>
    /// 在指定坐标位置添加并布局子视觉元素
    /// </summary>
    /// <param name="element">WPF 视觉元素</param>
    /// <param name="left">相对 OverlayContainer 的 X 坐标</param>
    /// <param name="top">相对 OverlayContainer 的 Y 坐标</param>
    /// <param name="zIndex">Z 轴层级深度</param>
    void AddChild(UIElement element, double left, double top, int zIndex = 0);

    /// <summary>
    /// 移除指定的子视觉元素
    /// </summary>
    /// <param name="element">WPF 视觉元素</param>
    /// <returns>若成功移除则返回 true</returns>
    bool RemoveChild(UIElement element);

    /// <summary>
    /// 清空所有子视觉元素
    /// </summary>
    void ClearChildren();
}
