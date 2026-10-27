using System;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 系统 DPI 或监视器缩放比例发生变化事件参数
/// </summary>
public sealed class DpiChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更前的 DPI 值 (例如 96, 120, 144)
    /// </summary>
    public uint OldDpi { get; }

    /// <summary>
    /// 变更后的 DPI 值
    /// </summary>
    public uint NewDpi { get; }

    /// <summary>
    /// 变更前的 DPI 缩放比例因子 (例如 1.0, 1.25, 1.5)
    /// </summary>
    public double OldDpiScale { get; }

    /// <summary>
    /// 变更后的 DPI 缩放比例因子
    /// </summary>
    public double NewDpiScale { get; }

    public DpiChangedEventArgs(uint oldDpi, uint newDpi, double oldDpiScale, double newDpiScale)
    {
        OldDpi = oldDpi;
        NewDpi = newDpi;
        OldDpiScale = oldDpiScale;
        NewDpiScale = newDpiScale;
    }
}
