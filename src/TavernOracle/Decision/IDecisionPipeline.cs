namespace TavernOracle.Decision;

using TavernOracle.Data.State;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Decision.Positioning;

/// <summary>
/// 统一决策求解流水线契约接口。
/// 串联有界背包 DP 搜索、启发式战力站位重排与边际价值评估，
/// 在 15ms 时延预算内完成全局决策并确保热路径零 GC 堆内存分配。
/// </summary>
public interface IDecisionPipeline
{
    /// <summary>
    /// 背包 DP 求解器实例。
    /// </summary>
    IKnapsackSolver KnapsackSolver { get; }

    /// <summary>
    /// 随从站位排列启发式优化器实例。
    /// </summary>
    IMinionPositionOptimizer PositionOptimizer { get; }

    /// <summary>
    /// 为给定的盘面物理快照执行完整的决策求解流程 (DP 求解 + 站位排序 + 价值评估)。
    /// </summary>
    /// <param name="snapshot">盘面物理快照</param>
    /// <returns>包含最优动作序列与随从最优站位的 0 GC 结构体</returns>
    DecisionPipelineResult Execute(in BoardSnapshot snapshot);
}
