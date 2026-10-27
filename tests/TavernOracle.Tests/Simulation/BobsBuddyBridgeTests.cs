using TavernOracle.Simulation;
using Xunit;

namespace TavernOracle.Tests.Simulation;

public class BobsBuddyBridgeTests
{
    [Fact]
    public void GracefulDegradation_InitialStateWithoutData_ReturnsSafePriorOfTenPercent()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge();

        // Act
        var result = bridge.LatestResult;
        var effective = bridge.GetEffectiveResult();

        // Assert
        Assert.Equal(BobsBuddySimulationState.Initial, bridge.CurrentState);
        Assert.True(result.IsDegraded);
        Assert.Equal(0.10, result.LethalRate, precision: 5);
        Assert.Equal(0.10, result.PLethal, precision: 5);
        Assert.Equal(0.10, effective.LethalRate, precision: 5);
        Assert.False(bridge.IsAvailable);
    }

    [Fact]
    public void GracefulDegradation_ForcedOfflineMode_AlwaysReturnsSafePrior()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge(isOfflineMode: true);

        // Inject simulated data while in offline mode
        bridge.ReportSimulationOutput(
            winRate: 0.90,
            tieRate: 0.05,
            lossRate: 0.05,
            lethalRate: 0.01,
            avgDamageTaken: -8.0,
            simulationsCount: 10000
        );

        // Act
        var result = bridge.LatestResult;
        var effective = bridge.GetEffectiveResult();

        // Assert: 即使注入了数据，在脱机模式下必须严格优雅降级为 P_lethal = 0.10
        Assert.True(bridge.IsOfflineMode);
        Assert.False(bridge.IsAvailable);
        Assert.True(result.IsDegraded);
        Assert.Equal(0.10, result.LethalRate, precision: 5);
        Assert.Equal(0.10, result.PLethal, precision: 5);
        Assert.Equal(0.10, effective.LethalRate, precision: 5);
    }

    [Fact]
    public void SimulationDataInjection_AccurateParsing_DispatchesSimulationCompletedEvent()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge();
        CombatSimulationResult? receivedResult = null;
        bridge.SimulationCompleted += res => receivedResult = res;

        // Act
        bridge.ReportSimulationOutput(
            winRate: 0.65,
            tieRate: 0.10,
            lossRate: 0.25,
            lethalRate: 0.04,
            avgDamageTaken: 3.2,
            simulationsCount: 8000
        );

        // Assert
        Assert.NotNull(receivedResult);
        Assert.False(receivedResult.Value.IsDegraded);
        Assert.Equal(0.65, receivedResult.Value.WinRate, precision: 4);
        Assert.Equal(0.10, receivedResult.Value.TieRate, precision: 4);
        Assert.Equal(0.25, receivedResult.Value.LossRate, precision: 4);
        Assert.Equal(0.04, receivedResult.Value.LethalRate, precision: 4);
        Assert.Equal(0.04, receivedResult.Value.PLethal, precision: 4);
        Assert.Equal(3.2, receivedResult.Value.AverageDamageTaken, precision: 4);
        Assert.Equal(8000, receivedResult.Value.SimulationsCount);

        // Bridge state verification
        Assert.True(bridge.IsAvailable);
        Assert.Equal(0.04, bridge.LatestResult.LethalRate, precision: 4);
    }

    [Fact]
    public void SimulationStateTransitions_TriggersStateChangedEvent()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge();
        var stateHistory = new List<BobsBuddySimulationState>();
        bridge.StateChanged += state => stateHistory.Add(state);

        // Act
        bridge.UpdateState(BobsBuddySimulationState.CombatRunning);
        bridge.UpdateState(BobsBuddySimulationState.SimulationDone);
        bridge.UpdateState(BobsBuddySimulationState.CombatEnded);

        // Assert
        Assert.Equal(3, stateHistory.Count);
        Assert.Equal(BobsBuddySimulationState.CombatRunning, stateHistory[0]);
        Assert.Equal(BobsBuddySimulationState.SimulationDone, stateHistory[1]);
        Assert.Equal(BobsBuddySimulationState.CombatEnded, stateHistory[2]);
        Assert.Equal(BobsBuddySimulationState.CombatEnded, bridge.CurrentState);
    }

    [Fact]
    public void Reset_RestoresInitialStateAndDegradedSafePrior()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge();
        bridge.UpdateState(BobsBuddySimulationState.SimulationDone);
        bridge.ReportSimulationOutput(0.80, 0.10, 0.10, 0.02, -5.0, 5000);

        Assert.False(bridge.LatestResult.IsDegraded);
        Assert.Equal(0.02, bridge.LatestResult.LethalRate, precision: 4);

        // Act
        bridge.Reset();

        // Assert
        Assert.Equal(BobsBuddySimulationState.Initial, bridge.CurrentState);
        Assert.True(bridge.LatestResult.IsDegraded);
        Assert.Equal(0.10, bridge.LatestResult.LethalRate, precision: 5);
        Assert.Equal(0.10, bridge.LatestResult.PLethal, precision: 5);
    }

    [Fact]
    public void Robustness_PercentageInputNormalization_NormalizesAboveOne()
    {
        // Act: 模拟外部传入百分制数据 (如 75.5% 传入 75.5)
        var result = new CombatSimulationResult(
            winRate: 75.5,
            tieRate: 10.0,
            lossRate: 14.5,
            lethalRate: 35.0,
            averageDamageTaken: 4.5,
            simulationsCount: 1000
        );

        // Assert: 必须自动归一化为 0~1 的标准概率
        Assert.Equal(0.755, result.WinRate, precision: 4);
        Assert.Equal(0.100, result.TieRate, precision: 4);
        Assert.Equal(0.145, result.LossRate, precision: 4);
        Assert.Equal(0.350, result.LethalRate, precision: 4);
        Assert.Equal(0.350, result.PLethal, precision: 4);
    }

    [Fact]
    public void Robustness_EdgeCases_HandlesNaNInfinityAndNegativeGracefully()
    {
        // Act
        var result = new CombatSimulationResult(
            winRate: double.NaN,
            tieRate: double.PositiveInfinity,
            lossRate: -0.5,
            lethalRate: 150.0,
            averageDamageTaken: double.NaN,
            simulationsCount: -10
        );

        // Assert
        Assert.Equal(0.0, result.WinRate);
        Assert.Equal(0.0, result.TieRate);
        Assert.Equal(0.0, result.LossRate);
        Assert.Equal(1.0, result.LethalRate); // 150% 换算为 1.5 并 Clamp 到 1.0
        Assert.Equal(0.0, result.AverageDamageTaken);
        Assert.Equal(0, result.SimulationsCount);
    }

    [Fact]
    public void AttachHdtInvoker_MockHdtObject_ReflectsStateAndOutputCorrectly()
    {
        // Arrange: 构造类似 HDT BobsBuddyInvoker 的动态结构
        var mockInvoker = new MockHdtBobsBuddyInvoker
        {
            State = "SimulationDone",
            Output = new MockHdtSimulationOutput
            {
                WinRate = 0.82,
                TieRate = 0.08,
                LossRate = 0.10,
                MyLethalRate = 0.02,
                MyAvgDamageTaken = -6.5,
                Simulations = 10000
            }
        };

        using var bridge = new BobsBuddyBridge();

        // Act
        bridge.AttachHdtInvoker(mockInvoker);

        // Assert
        Assert.Equal(BobsBuddySimulationState.SimulationDone, bridge.CurrentState);
        Assert.True(bridge.IsAvailable);
        Assert.False(bridge.LatestResult.IsDegraded);
        Assert.Equal(0.82, bridge.LatestResult.WinRate, precision: 4);
        Assert.Equal(0.08, bridge.LatestResult.TieRate, precision: 4);
        Assert.Equal(0.10, bridge.LatestResult.LossRate, precision: 4);
        Assert.Equal(0.02, bridge.LatestResult.LethalRate, precision: 4);
        Assert.Equal(0.02, bridge.LatestResult.PLethal, precision: 4);
        Assert.Equal(-6.5, bridge.LatestResult.AverageDamageTaken, precision: 4);
        Assert.Equal(10000, bridge.LatestResult.SimulationsCount);
    }

    [Fact]
    public void AttachHdtInvoker_NullOrMalformedObject_GracefullyHandlesWithoutCrashing()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge();

        // Act: 传入 null 或异常对象
        bridge.AttachHdtInvoker(null);
        var nullResult = bridge.LatestResult;

        bridge.AttachHdtInvoker(new object());
        var emptyResult = bridge.LatestResult;

        // Assert: 均不崩溃且优雅降级
        Assert.True(nullResult.IsDegraded);
        Assert.Equal(0.10, nullResult.LethalRate, precision: 5);
        Assert.True(emptyResult.IsDegraded);
        Assert.Equal(0.10, emptyResult.LethalRate, precision: 5);
    }

    [Fact]
    public void ZeroGC_ImmutableRecordStruct_IsValueType()
    {
        // Assert: 确保 CombatSimulationResult 为结构体，避免堆垃圾
        Assert.True(typeof(CombatSimulationResult).IsValueType);
    }

    // --- Mock Classes for Testing HDT Reflection ---

    private sealed class MockHdtBobsBuddyInvoker
    {
        public string State { get; set; } = "Initial";
        public MockHdtSimulationOutput? Output { get; set; }
    }

    private sealed class MockHdtSimulationOutput
    {
        public double WinRate { get; set; }
        public double TieRate { get; set; }
        public double LossRate { get; set; }
        public double MyLethalRate { get; set; }
        public double MyAvgDamageTaken { get; set; }
        public int Simulations { get; set; }
    }
}
