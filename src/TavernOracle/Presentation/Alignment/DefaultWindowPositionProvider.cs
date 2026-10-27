using System;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 默认 Win32 真实 API 窗口几何信息提供者。
/// 严格使用只读 Win32 API 查询炉石主窗口位置与 DPI。
/// </summary>
public sealed class DefaultWindowPositionProvider : IWindowPositionProvider
{
    private static readonly Lazy<DefaultWindowPositionProvider> _instance =
        new(() => new DefaultWindowPositionProvider());

    /// <summary>
    /// 全局单例实例
    /// </summary>
    public static DefaultWindowPositionProvider Instance => _instance.Value;

    /// <inheritdoc />
    public IntPtr FindHearthstoneWindow()
    {
        // 炉石 Unity 客户端主窗口的 WindowClass 通常为 "UnityWndClass"
        IntPtr hwnd = NativeWindowMethods.FindWindow("UnityWndClass", "Hearthstone");
        if (hwnd != IntPtr.Zero)
        {
            return hwnd;
        }

        // 备用：不限定类名仅按窗口名查找
        return NativeWindowMethods.FindWindow(null, "Hearthstone");
    }

    /// <inheritdoc />
    public bool GetWindowRect(IntPtr hWnd, out RECT rect)
    {
        return NativeWindowMethods.GetWindowRect(hWnd, out rect);
    }

    /// <inheritdoc />
    public bool GetClientRect(IntPtr hWnd, out RECT rect)
    {
        return NativeWindowMethods.GetClientRect(hWnd, out rect);
    }

    /// <inheritdoc />
    public bool ClientToScreen(IntPtr hWnd, ref POINT point)
    {
        return NativeWindowMethods.ClientToScreen(hWnd, ref point);
    }

    /// <inheritdoc />
    public uint GetDpiForWindow(IntPtr hWnd)
    {
        return NativeWindowMethods.SafeGetDpiForWindow(hWnd);
    }
}
