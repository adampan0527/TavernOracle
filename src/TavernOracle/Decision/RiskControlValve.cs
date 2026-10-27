namespace TavernOracle.Decision;

using TavernOracle.Simulation;

/// <summary>
/// 战力生死线风控调节阀。
/// 严格遵循零 GC 分配原则，在 &lt; 1ms 内完成非线性保命权重与成长贪婪权重的求解。
/// </summary>
public sealed class RiskControlValve : IRiskControlValve
{
    /// <summary>
    /// 默认生死线策略突变阈值 (P_lethal = 0.30)
    /// </summary>
    public const double DefaultEmergencyThreshold = 0.30;

    /// <summary>
    /// 默认安全先验暴毙率 (P_lethal = 0.10)
    /// </summary>
    public const double DefaultFallbackPLethal = 0.10;

    /// <summary>
    /// 默认即时战力调节倍率 (1.0)
    /// </summary>
    public const double DefaultTempoMultiplier = 1.0;

    /// <summary>
    /// 默认全局单例实例，便于零开销直接调用
    /// </summary>
    public static RiskControlValve Instance { get; } = new RiskControlValve();

    /// <summary>
    /// 生死线危机突变阈值
    /// </summary>
    public double EmergencyThreshold { get; }

    /// <summary>
    /// 输入异常时的安全先验暴毙率
    /// </summary>
    public double FallbackPLethal { get; }

    /// <summary>
    /// 激进/保守度即时战力调节倍率
    /// </summary>
    public double TempoMultiplier { get; }

    /// <summary>
    /// 初始化战力生死线风控调节阀
    /// </summary>
    /// <param name="emergencyThreshold">生死线突变临界阈值 (默认 0.30)</param>
    /// <param name="fallbackPLethal">异常输入降级安全先验 (默认 0.10)</param>
    /// <param name="tempoMultiplier">激进/保守度即时战力倍率调节阀 (默认 1.0，范围 [0.1, 5.0])</param>
    public RiskControlValve(
        double emergencyThreshold = DefaultEmergencyThreshold,
        double fallbackPLethal = DefaultFallbackPLethal,
        double tempoMultiplier = DefaultTempoMultiplier)
    {
        EmergencyThreshold = (double.IsNaN(emergencyThreshold) || double.IsInfinity(emergencyThreshold))
            ? DefaultEmergencyThreshold
            : MathEx.Clamp(emergencyThreshold, 0.0, 1.0);

        FallbackPLethal = (double.IsNaN(fallbackPLethal) || double.IsInfinity(fallbackPLethal))
            ? DefaultFallbackPLethal
            : MathEx.Clamp(fallbackPLethal, 0.0, 1.0);

        TempoMultiplier = (double.IsNaN(tempoMultiplier) || double.IsInfinity(tempoMultiplier))
            ? DefaultTempoMultiplier
            : MathEx.Clamp(tempoMultiplier, 0.1, 5.0);
    }

    /// <inheritdoc />
    public RiskWeightsProfile Evaluate(double pLethal)
    {
        // 1. 容错与防御处理：NaN / Infinity 自动降级为安全先验值 0.10，其余自动 Clamp 到 [0.0, 1.0]
        double cleanP;
        if (double.IsNaN(pLethal) || double.IsInfinity(pLethal))
        {
            cleanP = FallbackPLethal;
        }
        else
        {
            cleanP = MathEx.Clamp(pLethal, 0.0, 1.0);
        }

        // 2. 保命即时战力权重：w_tempo = (1.0 + 3.0 * (P_lethal)^2) * TempoMultiplier
        double wTempo = (1.0 + 3.0 * (cleanP * cleanP)) * TempoMultiplier;

        // 3. 远期成长/贪婪权重与突变逻辑：
        //    常态 (P_lethal <= 0.30)：w_scale = max(0, 1.0 - 0.9 * P_lethal)
        //    突变 (P_lethal > 0.30)：触发策略突变，强制切断贪婪逻辑，w_scale = 0.0，标记 IsEmergencyMutated = true
        double wScale;
        bool isMutated;

        if (cleanP > EmergencyThreshold)
        {
            wScale = 0.0;
            isMutated = true;
        }
        else
        {
            wScale = Math.Max(0.0, 1.0 - 0.9 * cleanP);
            isMutated = false;
        }

        // 4. 判定风险层级 (Safe, Low, Warning, Critical)
        RiskTier tier = DetermineRiskTier(cleanP);

        return new RiskWeightsProfile(cleanP, wTempo, wScale, isMutated, tier);
    }

    /// <inheritdoc />
    public RiskWeightsProfile Evaluate(float pLethal)
    {
        if (float.IsNaN(pLethal) || float.IsInfinity(pLethal))
        {
            return Evaluate(FallbackPLethal);
        }

        return Evaluate((double)pLethal);
    }

    /// <inheritdoc />
    public RiskWeightsProfile Evaluate(CombatSimulationResult combatResult)
    {
        return Evaluate(combatResult.PLethal);
    }

    /// <summary>
    /// 静态零开销快捷求值方法
    /// </summary>
    public static RiskWeightsProfile Calculate(double pLethal)
    {
        return Instance.Evaluate(pLethal);
    }

    /// <summary>
    /// 根据暴毙率推导风险等级
    /// </summary>
    public static RiskTier DetermineRiskTier(double pLethal)
    {
        if (pLethal <= 0.05)
            return RiskTier.Safe;
        if (pLethal <= 0.15)
            return RiskTier.Low;
        if (pLethal <= 0.30)
            return RiskTier.Warning;
        return RiskTier.Critical;
    }
}
