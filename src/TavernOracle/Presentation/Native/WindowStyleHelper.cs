using System;
using System.Runtime.InteropServices;

namespace TavernOracle.Presentation.Native;

/// <summary>
/// Win32 窗口样式与点击穿透操作辅助工具。
/// 严格遵循暴雪“纸笔准则（Pencil & Paper Rule）”及 COMP005 规范，
/// 仅用于将 HUD 悬浮层设置为完全无阻挡穿透 (WS_EX_TRANSPARENT | WS_EX_LAYERED)，
/// 绝不拦截或模拟任何用户输入。
/// </summary>
public static class WindowStyleHelper
{
    /// <summary>
    /// 获取/设置窗口扩展样式的索引值
    /// </summary>
    public const int GWL_EXSTYLE = -20;

    /// <summary>
    /// 透明点击穿透样式标志：指示鼠标输入直接穿透至下层游戏窗口 (0x00000020)
    /// </summary>
    public const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>
    /// 分层窗口样式标志：用于支持透明通道与硬件加速无缝混合绘制 (0x00080000)
    /// </summary>
    public const int WS_EX_LAYERED = 0x00080000;

    /// <summary>
    /// 工具窗口样式标志：不在任务栏及 Alt+Tab 切换列表中显示 (0x00000080)
    /// </summary>
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>
    /// 无激活窗口样式标志：被点击或呈现时不抢占游戏主进程的输入焦点 (0x08000000)
    /// </summary>
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// 跨平台 (32位 / 64位兼容) 获取指定窗口的扩展样式长整型值
    /// </summary>
    /// <param name="hWnd">目标窗口原生句柄</param>
    /// <param name="nIndex">样式索引 (通常为 GWL_EXSTYLE = -20)</param>
    /// <returns>窗口扩展样式句柄指针值</returns>
    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (hWnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : GetWindowLong32(hWnd, nIndex);
    }

    /// <summary>
    /// 跨平台 (32位 / 64位兼容) 设置指定窗口的扩展样式长整型值
    /// </summary>
    /// <param name="hWnd">目标窗口原生句柄</param>
    /// <param name="nIndex">样式索引 (通常为 GWL_EXSTYLE = -20)</param>
    /// <param name="dwNewLong">新的样式标志长整型值</param>
    /// <returns>修改前的旧样式指针值</returns>
    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (hWnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    /// <summary>
    /// 安全开启透明点击穿透样式 (WS_EX_TRANSPARENT | WS_EX_LAYERED)。
    /// 具备幂等性：若已开启则直接返回 true，避免无谓的系统 API 调用。
    /// </summary>
    /// <param name="hwnd">目标窗口原生句柄</param>
    /// <returns>操作是否成功</returns>
    public static bool EnableClickThrough(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        int currentStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt32();
        int transparentFlags = WS_EX_TRANSPARENT | WS_EX_LAYERED;

        // 幂等性检查：若已包含穿透与分层标志，无需重复设置
        if ((currentStyle & transparentFlags) == transparentFlags)
        {
            return true;
        }

        int newStyle = currentStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED;
        IntPtr result = SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)newStyle);
        return result != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
    }

    /// <summary>
    /// 检查指定窗口句柄是否已成功挂载透明点击穿透样式
    /// </summary>
    /// <param name="hwnd">目标窗口原生句柄</param>
    /// <returns>是否已启用穿透与分层样式</returns>
    public static bool IsClickThroughEnabled(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        int currentStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt32();
        int transparentFlags = WS_EX_TRANSPARENT | WS_EX_LAYERED;
        return (currentStyle & transparentFlags) == transparentFlags;
    }

    /// <summary>
    /// 一站式应用 HUD 专用窗口扩展样式组合 (点击穿透 + 工具窗口 + 无激活)。
    /// </summary>
    /// <param name="hwnd">目标窗口原生句柄</param>
    /// <param name="clickThrough">是否开启点击穿透与分层</param>
    /// <param name="toolWindow">是否隐藏任务栏图标</param>
    /// <param name="noActivate">是否禁止抢占输入焦点</param>
    /// <returns>操作是否成功</returns>
    public static bool ApplyOverlayStyles(IntPtr hwnd, bool clickThrough = true, bool toolWindow = true, bool noActivate = true)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        int currentStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt32();
        int newStyle = currentStyle;

        if (clickThrough)
        {
            newStyle |= (WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        if (toolWindow)
        {
            newStyle |= WS_EX_TOOLWINDOW;
        }

        if (noActivate)
        {
            newStyle |= WS_EX_NOACTIVATE;
        }

        if (newStyle == currentStyle)
        {
            return true;
        }

        IntPtr result = SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)newStyle);
        return result != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
    }
}
