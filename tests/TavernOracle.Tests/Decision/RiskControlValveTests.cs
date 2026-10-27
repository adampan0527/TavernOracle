namespace TavernOracle.Tests.Decision;

using System;
using TavernOracle.Decision;
using TavernOracle.Simulation;
using Xunit;

public class RiskControlValveTests
{
    private readonly IRiskControlValve _valve = new RiskControlValve();

    [Fact]
    public void MemoryContract_RiskWeightsProfile_IsValueType_ZeroHeapAllocation()
    {
        // Assert: 验证 RiskWeightsProfile 是值类型 (readonly record struct)，杜绝高频堆垃圾
        Assert.True(typeof(RiskWeightsProfile).IsValueType, "RiskWeightsProfile must be a value type (struct).");
        Assert.False(typeof(RiskWeightsProfile).IsClass, "RiskWeightsProfile must NOT be a reference type (class).");
    }

    [Fact]
    public void Boundary_LowRisk_P005_MaintainsGreedyScalingWeight()
    {
        // Arrange: P_lethal = 0.05 (极低暴毙风险)
        double pLethal = 0.05;

        // Act
        var profile = _valve.Evaluate(pLethal);

        // Assert:
        // w_tempo = 1.0 + 3.0 * (0.05)^2 = 1.0 + 3.0 * 0.0025 = 1.0075
        // w_scale = 1.0 - 0.9 * 0.05 = 0.955
        Assert.Equal(0.05, profile.PLethal, precision: 6);
        Assert.Equal(1.0075, profile.TempoWeight, precision: 6);
        Assert.Equal(0.955, profile.ScaleWeight, precision: 6);
        Assert.Equal(profile.TempoWeight, profile.WTempo);
        Assert.Equal(profile.ScaleWeight, profile.WScale);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Safe, profile.RiskTier);
    }

    [Fact]
    public void Boundary_ZeroRisk_P000_ReturnsBaseWeights()
    {
        // Arrange
        double pLethal = 0.0;

        // Act
        var profile = _valve.Evaluate(pLethal);

        // Assert:
        // w_tempo = 1.0 + 3.0 * 0 = 1.0
        // w_scale = 1.0 - 0 = 1.0
        Assert.Equal(0.0, profile.PLethal, precision: 6);
        Assert.Equal(1.0, profile.TempoWeight, precision: 6);
        Assert.Equal(1.0, profile.ScaleWeight, precision: 6);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Safe, profile.RiskTier);
    }

    [Fact]
    public void CriticalBoundary_SmoothTransition_AtThresholdP030()
    {
        // Arrange: P_lethal = 0.30 (刚好处于生死线突变阈值临界点)
        double pLethal = 0.30;

        // Act
        var profile = _valve.Evaluate(pLethal);

        // Assert:
        // w_tempo = 1.0 + 3.0 * (0.30)^2 = 1.0 + 3.0 * 0.09 = 1.27
        // w_scale = 1.0 - 0.9 * 0.30 = 0.73
        // P <= 0.30 仍属常态连续区间，未触发策略突变
        Assert.Equal(0.30, profile.PLethal, precision: 6);
        Assert.Equal(1.27, profile.TempoWeight, precision: 6);
        Assert.Equal(0.73, profile.ScaleWeight, precision: 6);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Warning, profile.RiskTier);
    }

    [Theory]
    [InlineData(0.31)]
    [InlineData(0.40)]
    [InlineData(0.50)]
    [InlineData(0.85)]
    public void EmergencyMutation_TriggeredWhenExceedingThreshold_GreedyScaleCutToZero(double pLethal)
    {
        // Act
        var profile = _valve.Evaluate(pLethal);

        // Assert: 超过 30% 生死线，策略强制突变，w_scale 必须绝对为 0.0
        Assert.True(profile.IsEmergencyMutated);
        Assert.Equal(0.0, profile.ScaleWeight);
        Assert.Equal(RiskTier.Critical, profile.RiskTier);
        Assert.Equal(RiskTier.Lethal, profile.RiskTier);

        // 保命战力权重随 P_lethal 二次加速增长
        double expectedTempo = 1.0 + 3.0 * (pLethal * pLethal);
        Assert.Equal(expectedTempo, profile.TempoWeight, precision: 6);
    }

    [Fact]
    public void ExtremeBoundary_AbsoluteLethal_P100()
    {
        // Arrange: P_lethal = 1.0 (必死无疑对战)
        double pLethal = 1.0;

        // Act
        var profile = _valve.Evaluate(pLethal);

        // Assert:
        // w_tempo = 1.0 + 3.0 * 1.0 = 4.0
        // w_scale = 0.0 (强制切断贪婪)
        Assert.Equal(1.0, profile.PLethal, precision: 6);
        Assert.Equal(4.0, profile.TempoWeight, precision: 6);
        Assert.Equal(0.0, profile.ScaleWeight, precision: 6);
        Assert.True(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Critical, profile.RiskTier);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(-100.0)]
    public void FaultTolerance_NegativeValues_ClampedToZero(double negativeP)
    {
        // Act
        var profile = _valve.Evaluate(negativeP);

        // Assert: 负数自动 Clamp 为 0.0
        Assert.Equal(0.0, profile.PLethal, precision: 6);
        Assert.Equal(1.0, profile.TempoWeight, precision: 6);
        Assert.Equal(1.0, profile.ScaleWeight, precision: 6);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Safe, profile.RiskTier);
    }

    [Theory]
    [InlineData(1.01)]
    [InlineData(2.5)]
    [InlineData(100.0)]
    public void FaultTolerance_ExcessiveValues_ClampedToOne(double excessiveP)
    {
        // Act
        var profile = _valve.Evaluate(excessiveP);

        // Assert: > 1.0 自动 Clamp 为 1.0
        Assert.Equal(1.0, profile.PLethal, precision: 6);
        Assert.Equal(4.0, profile.TempoWeight, precision: 6);
        Assert.Equal(0.0, profile.ScaleWeight, precision: 6);
        Assert.True(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Critical, profile.RiskTier);
    }

    [Fact]
    public void FaultTolerance_NaN_FallsBackToSafePriorOfTenPercent()
    {
        // Act
        var profile = _valve.Evaluate(double.NaN);

        // Assert: NaN 自动降级为安全先验值 0.10
        Assert.Equal(0.10, profile.PLethal, precision: 6);
        // w_tempo = 1.0 + 3.0 * (0.1)^2 = 1.03
        // w_scale = 1.0 - 0.9 * 0.1 = 0.91
        Assert.Equal(1.03, profile.TempoWeight, precision: 6);
        Assert.Equal(0.91, profile.ScaleWeight, precision: 6);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Low, profile.RiskTier);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FaultTolerance_Infinity_FallsBackToSafePriorOfTenPercent(double infP)
    {
        // Act
        var profile = _valve.Evaluate(infP);

        // Assert: Infinity 自动降级为安全先验值 0.10
        Assert.Equal(0.10, profile.PLethal, precision: 6);
        Assert.Equal(1.03, profile.TempoWeight, precision: 6);
        Assert.Equal(0.91, profile.ScaleWeight, precision: 6);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(RiskTier.Low, profile.RiskTier);
    }

    [Fact]
    public void FloatOverload_EvaluatesCorrectly()
    {
        // Arrange
        float floatP = 0.05f;

        // Act
        var profile = _valve.Evaluate(floatP);

        // Assert
        Assert.Equal(0.05, profile.PLethal, precision: 5);
        Assert.Equal(0.955, profile.ScaleWeight, precision: 5);

        // Act: Float NaN
        var nanProfile = _valve.Evaluate(float.NaN);
        Assert.Equal(0.10, nanProfile.PLethal, precision: 5);

        // Act: Float Infinity
        var infProfile = _valve.Evaluate(float.PositiveInfinity);
        Assert.Equal(0.10, infProfile.PLethal, precision: 5);
    }

    [Fact]
    public void CombatSimulationResultOverload_ExtractsPLethalAndEvaluates()
    {
        // Arrange: 构造高危暴毙模拟结果 (P_lethal = 0.45)
        var lethalSim = new CombatSimulationResult(
            winRate: 0.20,
            tieRate: 0.05,
            lossRate: 0.75,
            lethalRate: 0.45,
            averageDamageTaken: 12.5,
            simulationsCount: 5000
        );

        // Act
        var profile = _valve.Evaluate(lethalSim);

        // Assert
        Assert.Equal(0.45, profile.PLethal, precision: 6);
        Assert.True(profile.IsEmergencyMutated);
        Assert.Equal(0.0, profile.ScaleWeight);
        Assert.Equal(1.0 + 3.0 * (0.45 * 0.45), profile.TempoWeight, precision: 6);
        Assert.Equal(RiskTier.Critical, profile.RiskTier);
    }

    [Fact]
    public void CombatSimulationResultOverload_DefaultDegradedSafePrior()
    {
        // Arrange: 默认安全先验
        var degradedSim = CombatSimulationResult.CreateDefaultSafePrior();

        // Act
        var profile = _valve.Evaluate(degradedSim);

        // Assert: P_lethal = 0.10
        Assert.Equal(0.10, profile.PLethal, precision: 6);
        Assert.False(profile.IsEmergencyMutated);
        Assert.Equal(0.91, profile.ScaleWeight, precision: 6);
        Assert.Equal(1.03, profile.TempoWeight, precision: 6);
        Assert.Equal(RiskTier.Low, profile.RiskTier);
    }

    [Fact]
    public void StaticCalculate_ProducesIdenticalResultToInstance()
    {
        // Arrange
        double p = 0.25;

        // Act
        var instanceProfile = _valve.Evaluate(p);
        var staticProfile = RiskControlValve.Calculate(p);

        // Assert
        Assert.Equal(instanceProfile, staticProfile);
    }

    [Fact]
    public void CustomValveConfiguration_CustomThreshold()
    {
        // Arrange: 自定义更保守的生死线阈值 0.20
        var strictValve = new RiskControlValve(emergencyThreshold: 0.20, fallbackPLethal: 0.05);

        // Act & Assert
        Assert.Equal(0.20, strictValve.EmergencyThreshold, precision: 6);
        Assert.Equal(0.05, strictValve.FallbackPLethal, precision: 6);

        // At P = 0.22, standard valve would not mutate, but strict valve must mutate!
        var profile = strictValve.Evaluate(0.22);
        Assert.True(profile.IsEmergencyMutated);
        Assert.Equal(0.0, profile.ScaleWeight);

        // Test fallback on NaN
        var fallbackProfile = strictValve.Evaluate(double.NaN);
        Assert.Equal(0.05, fallbackProfile.PLethal, precision: 6);
    }

    [Fact]
    public void Performance_TenThousandEvaluations_CompletesUnderFifteenMilliseconds()
    {
        // Arrange
        const int iterations = 10_000;
        var valve = RiskControlValve.Instance;

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            _ = valve.Evaluate(0.15);
        }

        // Measure
        int gcBefore = GC.CollectionCount(0);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        double dummySum = 0;
        for (int i = 0; i < iterations; i++)
        {
            double p = (i % 100) / 100.0;
            var res = valve.Evaluate(p);
            dummySum += res.TempoWeight;
        }

        sw.Stop();
        int gcAfter = GC.CollectionCount(0);

        // Assert: 10,000 次求解应在极短时间内完成 (< 15ms)，且不产生第 0 代 GC 分配
        Assert.True(dummySum > 0);
        Assert.True(sw.ElapsedMilliseconds < 15, $"10,000 evaluations took {sw.ElapsedMilliseconds} ms, expected < 15 ms");
        Assert.Equal(gcBefore, gcAfter); // 零 GC 堆分配验证
    }
}
