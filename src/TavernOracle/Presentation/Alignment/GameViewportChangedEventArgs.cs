using System;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 16:9 核心活跃视口发生变化事件参数
/// </summary>
public sealed class GameViewportChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更前的视口模型
    /// </summary>
    public GameViewport OldViewport { get; }

    /// <summary>
    /// 变更后的视口模型
    /// </summary>
    public GameViewport NewViewport { get; }

    public GameViewportChangedEventArgs(GameViewport oldViewport, GameViewport newViewport)
    {
        OldViewport = oldViewport;
        NewViewport = newViewport;
    }
}
