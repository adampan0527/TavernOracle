namespace TavernOracle.Simulation;

/// <summary>
/// HDT 原生 BobsBuddyInvoker 的解耦适配接口
/// </summary>
public interface IBobsBuddyInvokerAdapter : IDisposable
{
    /// <summary>
    /// 底层 Invoker 实例是否有效且可用
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 从底层读取当前状态
    /// </summary>
    BobsBuddySimulationState State { get; }

    /// <summary>
    /// 状态发生变化事件
    /// </summary>
    event Action<BobsBuddySimulationState>? StateChanged;

    /// <summary>
    /// 模拟数据输出更新事件
    /// </summary>
    event Action<CombatSimulationResult>? OutputUpdated;

    /// <summary>
    /// 主动拉取底层 Invoker 的最新数据
    /// </summary>
    bool TryPollLatestData(out CombatSimulationResult result, out BobsBuddySimulationState state);

    /// <summary>
    /// 解除绑定
    /// </summary>
    void Detach();
}
