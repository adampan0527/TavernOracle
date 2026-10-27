namespace TavernOracle.Decision;

using TavernOracle.Simulation;

/// <summary>
/// 战力生死线风控调节阀契约接口。
/// 基于下一场暴毙率 P_lethal 动态调节保命即时战力权重 w_tempo 与远期成长贪婪权重 w_scale，
/// 并在达到危机临界点时触发策略突变 (Strategy Mutation)。
/// </summary>
public interface IRiskControlValve
{
    /// <summary>
    /// 生死线危机突变阈值 (默认 0.30，即暴毙率 &gt; 30% 时触发策略突变)
    /// </summary>
    double EmergencyThreshold { get; }

    /// <summary>
    /// 输入异常 (NaN / Infinity) 时的安全先验暴毙率 (默认 0.10)
    /// </summary>
    double FallbackPLethal { get; }

    /// <summary>
    /// 激进/保守度即时战力调节倍率 (默认 1.0，范围 [0.1, 5.0])
    /// </summary>
    double TempoMultiplier { get; }

    /// <summary>
    /// 评估对战暴毙率并求解风控权重。
    /// </summary>
    /// <param name="pLethal">对战暴毙概率 [0.0, 1.0]</param>
    /// <returns>只读风控权重配置快照 (零 GC 栈分配)</returns>
    RiskWeightsProfile Evaluate(double pLethal);

    /// <summary>
    /// 评估单精度浮点数暴毙率。
    /// </summary>
    /// <param name="pLethal">单精度暴毙概率</param>
    /// <returns>只读风控权重配置快照</returns>
    RiskWeightsProfile Evaluate(float pLethal);

    /// <summary>
    /// 从蒙特卡洛战力模拟结果 (CombatSimulationResult) 中提取暴毙率并进行风控评估。
    /// </summary>
    /// <param name="combatResult">蒙特卡洛对战模拟结果</param>
    /// <returns>只读风控权重配置快照</returns>
    RiskWeightsProfile Evaluate(CombatSimulationResult combatResult);
}
