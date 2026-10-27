namespace TavernOracle.Decision.Knapsack;

using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.Evaluation;

/// <summary>
/// 分支定界有界背包 DP 求解器契约接口。
/// 在当前可用铸币 (含变卖随从金币返还 Refund) 与 7 个战场卡槽、10 个手牌卡槽双重硬性约束下，
/// 对动作决策树执行高性能分支定界与贪心上下界剪枝搜索，输出全局预期收益最高的最优操作序列。
/// 严格遵守 5ms 硬超时限制与零 GC 堆内存分配规范。
/// </summary>
public interface IKnapsackSolver
{
    /// <summary>
    /// 为指定盘面快照求解全局最优操作序列 (默认 4.5ms 超时拦截)。
    /// 自动就地推导贝叶斯流派后验概率分布与生死线风控权重。
    /// </summary>
    /// <param name="snapshot">当前只读盘面物理快照</param>
    /// <returns>最优操作序列结果结构体 (零 GC 值类型)</returns>
    ActionSequenceResult Solve(in BoardSnapshot snapshot);

    /// <summary>
    /// 为指定盘面快照及流派后验与风控配置求解全局最优操作序列，支持自定义超时预算与探索节点预算。
    /// </summary>
    /// <param name="snapshot">当前只读盘面物理快照</param>
    /// <param name="posteriors">贝叶斯流派后验概率分布快照</param>
    /// <param name="riskProfile">战力生死线风控权重快照</param>
    /// <param name="timeoutMs">硬超时截断毫秒数 (默认 4.5ms)</param>
    /// <param name="maxNodeBudget">最大搜索节点预算 (默认 10,000)</param>
    /// <returns>最优操作序列结果结构体</returns>
    ActionSequenceResult Solve(
        in BoardSnapshot snapshot,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile,
        double timeoutMs = 4.5,
        int maxNodeBudget = 10_000);
}
