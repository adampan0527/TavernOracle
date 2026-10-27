namespace TavernOracle.Simulation;

/// <summary>
/// Bob's Buddy 战力模拟桥接适配器核心契约
/// </summary>
public interface IBobsBuddyBridge
{
    /// <summary>
    /// 当前对战模拟生命周期状态
    /// </summary>
    BobsBuddySimulationState CurrentState { get; }

    /// <summary>
    /// 获取当前最新模拟结果。当处于脱机模式或无有效模拟数据时，优雅降级返回默认安全先验值 (P_lethal = 0.10)
    /// </summary>
    CombatSimulationResult LatestResult { get; }

    /// <summary>
    /// 是否强制处于脱机模式 (Offline Mode)。启用脱机模式时，所有查询均返回安全先验值
    /// </summary>
    bool IsOfflineMode { get; set; }

    /// <summary>
    /// 底层 HDT Bob's Buddy 模拟器是否已就绪并活跃
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 模拟数据就绪或刷新时的事件通知
    /// </summary>
    event Action<CombatSimulationResult>? SimulationCompleted;

    /// <summary>
    /// 模拟状态迁移时的事件通知
    /// </summary>
    event Action<BobsBuddySimulationState>? StateChanged;

    /// <summary>
    /// 注入/推送结构化模拟结果
    /// </summary>
    void ReportSimulationResult(CombatSimulationResult result);

    /// <summary>
    /// 注入基础模拟浮点数值
    /// </summary>
    void ReportSimulationOutput(double winRate, double tieRate, double lossRate, double lethalRate, double avgDamageTaken = 0.0, int simulationsCount = 0);

    /// <summary>
    /// 更新当前模拟生命周期状态
    /// </summary>
    void UpdateState(BobsBuddySimulationState newState);

    /// <summary>
    /// 重置状态与结果为初始脱机/先验状态 (例如在回合结束或新对局开始时)
    /// </summary>
    void Reset();

    /// <summary>
    /// 获得当前有效的决策结果（根据是否脱机与是否有数据自动判定降级）
    /// </summary>
    CombatSimulationResult GetEffectiveResult();

    /// <summary>
    /// 附加 HDT 内置的 BobsBuddyInvoker 实例进行动态事件绑定与数据提取
    /// </summary>
    void AttachHdtInvoker(object? invoker);
}
