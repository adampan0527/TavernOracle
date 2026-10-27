using System.Text.Json.Serialization;

namespace TavernOracle.Simulation;

/// <summary>
/// 不可变值类型，表示单场对战的战力模拟结果。
/// 采用 readonly record struct 避免任何堆垃圾分配 (Zero GC Allocation)。
/// </summary>
public readonly record struct CombatSimulationResult
{
    /// <summary>
    /// 脱机或数据缺失时的默认安全先验暴毙率 (P_lethal = 0.10)
    /// </summary>
    public const double DefaultDegradedLethalRate = 0.10;

    /// <summary>
    /// 预测胜率 [0.0, 1.0]
    /// </summary>
    public double WinRate { get; init; }

    /// <summary>
    /// 预测平局率 [0.0, 1.0]
    /// </summary>
    public double TieRate { get; init; }

    /// <summary>
    /// 预测败率 [0.0, 1.0]
    /// </summary>
    public double LossRate { get; init; }

    /// <summary>
    /// 预测暴毙率 (P_lethal) [0.0, 1.0]
    /// </summary>
    public double LethalRate { get; init; }

    /// <summary>
    /// 暴毙率数学别名 P_lethal，供决策引擎调节阀直接引用
    /// </summary>
    [JsonIgnore]
    public double PLethal => LethalRate;

    /// <summary>
    /// 预期承受平均伤害值 (正值表示玩家预期掉血，负值表示玩家预期胜出造成伤害)
    /// </summary>
    public double AverageDamageTaken { get; init; }

    /// <summary>
    /// 蒙特卡洛模拟总场次 (通常为 1,000 ~ 10,000 次)
    /// </summary>
    public int SimulationsCount { get; init; }

    /// <summary>
    /// 是否为优雅降级生成的安全先验数据
    /// </summary>
    public bool IsDegraded { get; init; }

    /// <summary>
    /// 状态或诊断消息说明
    /// </summary>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// 构造完整的对战模拟结果
    /// </summary>
    [JsonConstructor]
    public CombatSimulationResult(
        double winRate,
        double tieRate,
        double lossRate,
        double lethalRate,
        double averageDamageTaken = 0.0,
        int simulationsCount = 0,
        bool isDegraded = false,
        string? statusMessage = null)
    {
        WinRate = NormalizeRate(winRate);
        TieRate = NormalizeRate(tieRate);
        LossRate = NormalizeRate(lossRate);
        LethalRate = NormalizeRate(lethalRate);
        AverageDamageTaken = double.IsNaN(averageDamageTaken) || double.IsInfinity(averageDamageTaken) ? 0.0 : averageDamageTaken;
        SimulationsCount = Math.Max(0, simulationsCount);
        IsDegraded = isDegraded;
        StatusMessage = statusMessage;
    }

    /// <summary>
    /// 将概率数值统一归一化为 [0.0, 1.0] 区间
    /// </summary>
    public static double NormalizeRate(double rate)
    {
        if (double.IsNaN(rate) || double.IsInfinity(rate))
            return 0.0;

        // 若输入大于 1.0 (例如 85.0 表示 85%)，自动换算为 0.85
        if (rate > 1.0)
            rate /= 100.0;

        return MathEx.Clamp(rate, 0.0, 1.0);
    }

    /// <summary>
    /// 创建默认的安全先验结果 (P_lethal = 0.10)
    /// </summary>
    public static CombatSimulationResult CreateDefaultSafePrior(string reason = "Offline Mode / Default Safe Prior")
    {
        return new CombatSimulationResult(
            winRate: 0.45,
            tieRate: 0.10,
            lossRate: 0.45,
            lethalRate: DefaultDegradedLethalRate,
            averageDamageTaken: 0.0,
            simulationsCount: 0,
            isDegraded: true,
            statusMessage: reason
        );
    }

    public override string ToString()
    {
        return $"[CombatSimResult: Win={WinRate:P1}, Tie={TieRate:P1}, Loss={LossRate:P1}, P_lethal={LethalRate:P1}, AvgDmg={AverageDamageTaken:F1}, SimCount={SimulationsCount}, Degraded={IsDegraded}]";
    }
}
