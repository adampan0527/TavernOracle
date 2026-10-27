using System;
using System.Windows;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 窗口外框或客户区矩形发生移动或尺寸变化事件参数
/// </summary>
public sealed class WindowBoundsChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更前的窗口外框屏幕绝对物理矩形
    /// </summary>
    public Rect OldWindowRect { get; }

    /// <summary>
    /// 变更后的窗口外框屏幕绝对物理矩形
    /// </summary>
    public Rect NewWindowRect { get; }

    /// <summary>
    /// 变更前的客户区屏幕绝对物理矩形
    /// </summary>
    public Rect OldClientRect { get; }

    /// <summary>
    /// 变更后的客户区屏幕绝对物理矩形
    /// </summary>
    public Rect NewClientRect { get; }

    public WindowBoundsChangedEventArgs(
        Rect oldWindowRect,
        Rect newWindowRect,
        Rect oldClientRect,
        Rect newClientRect)
    {
        OldWindowRect = oldWindowRect;
        NewWindowRect = newWindowRect;
        OldClientRect = oldClientRect;
        NewClientRect = newClientRect;
    }
}
