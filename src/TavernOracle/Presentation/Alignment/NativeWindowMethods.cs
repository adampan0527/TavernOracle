using System;
using System.Runtime.InteropServices;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// Win32 原生矩形结构体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

/// <summary>
/// Win32 原生点结构体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// 安全的只读 Win32 窗口位置与 DPI 查询原生封装。
/// 严格遵守暴雪“纸笔准则（Pencil & Paper Rule）”及 COMP001 规范，
/// 100% 只读，严禁声明任何写入、篡改或模拟输入相关的 API。
/// </summary>
internal static class NativeWindowMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetDpiForSystem();

    [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    /// <summary>
    /// 安全获取窗口 DPI，具备针对 Win10 1607 之前版本的向下兼容优雅降级机制
    /// </summary>
    public static uint SafeGetDpiForWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return 96;
        }

        try
        {
            uint dpi = GetDpiForWindow(hWnd);
            if (dpi > 0)
            {
                return dpi;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // 降级使用 GetDpiForSystem
        }
        catch
        {
            // 其他未知异常兜底
        }

        try
        {
            uint systemDpi = GetDpiForSystem();
            if (systemDpi > 0)
            {
                return systemDpi;
            }
        }
        catch
        {
            // 兜底返回标准 100% DPI (96)
        }

        return 96;
    }
}
