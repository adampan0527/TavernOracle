using System;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 窗口几何信息只读提供者接口。
/// 抽象 Win32 窗口位置与尺寸获取逻辑，支持单元测试模拟脱机注入。
/// </summary>
public interface IWindowPositionProvider
{
    /// <summary>
    /// 查找当前运行的炉石传说客户端主窗口句柄
    /// </summary>
    /// <returns>若找到返回有效 HWND，否则返回 IntPtr.Zero</returns>
    IntPtr FindHearthstoneWindow();

    /// <summary>
    /// 获取指定窗口的屏幕外框矩形 (物理像素)
    /// </summary>
    bool GetWindowRect(IntPtr hWnd, out RECT rect);

    /// <summary>
    /// 获取指定窗口的客户区矩形 (物理像素，Left=0, Top=0)
    /// </summary>
    bool GetClientRect(IntPtr hWnd, out RECT rect);

    /// <summary>
    /// 将客户区坐标转换为屏幕绝对坐标 (物理像素)
    /// </summary>
    bool ClientToScreen(IntPtr hWnd, ref POINT point);

    /// <summary>
    /// 获取指定窗口所在监视器的 DPI (例如 96, 120, 144, 192)
    /// </summary>
    uint GetDpiForWindow(IntPtr hWnd);
}
