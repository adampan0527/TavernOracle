namespace TavernOracle.Decision;

using System;
using System.Runtime.CompilerServices;
using TavernOracle.Data.State;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Decision.Positioning;

/// <summary>
/// 统一决策求解流水线实现 (串联 DP 求解、站位重排与边际价值评估)。
/// 保证热路径下零 GC 堆内存分配，并在远低于 15ms 时延预算内完成全局求解。
/// </summary>
public sealed class DecisionPipeline : IDecisionPipeline
{
    private static readonly Lazy<DecisionPipeline> s_lazyInstance = new(() => new DecisionPipeline());

    /// <summary>
    /// 全局共享单例。
    /// </summary>
    public static DecisionPipeline Instance => s_lazyInstance.Value;

    private readonly IKnapsackSolver _knapsackSolver;
    private readonly IMinionPositionOptimizer _positionOptimizer;

    /// <summary>
    /// 构造统一决策流水线。
    /// </summary>
    public DecisionPipeline(
        IKnapsackSolver? knapsackSolver = null,
        IMinionPositionOptimizer? positionOptimizer = null)
    {
        _knapsackSolver = knapsackSolver ?? TavernOracle.Decision.Knapsack.KnapsackSolver.Instance;
        _positionOptimizer = positionOptimizer ?? TavernOracle.Decision.Positioning.MinionPositionOptimizer.Instance;
    }

    /// <inheritdoc />
    public IKnapsackSolver KnapsackSolver => _knapsackSolver;

    /// <inheritdoc />
    public IMinionPositionOptimizer PositionOptimizer => _positionOptimizer;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DecisionPipelineResult Execute(in BoardSnapshot snapshot)
    {
        var actionResult = _knapsackSolver.Solve(in snapshot);
        var positioningResult = _positionOptimizer.OptimizePosition(in snapshot);
        return new DecisionPipelineResult(actionResult, positioningResult);
    }
}
