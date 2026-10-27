namespace TavernOracle.Simulation;

/// <summary>
/// Bob's Buddy 战力模拟生命周期状态
/// </summary>
public enum BobsBuddySimulationState
{
    /// <summary>
    /// 初始未激活状态 / 准备中
    /// </summary>
    Initial = 0,

    /// <summary>
    /// 战斗进行中，模拟正在并行运算
    /// </summary>
    CombatRunning = 1,

    /// <summary>
    /// 模拟已收敛计算完成，数据已就绪
    /// </summary>
    SimulationDone = 2,

    /// <summary>
    /// 战斗阶段已结束，准备结算或切换至酒馆购买阶段
    /// </summary>
    CombatEnded = 3
}
