namespace TavernOracle.Decision;

using System;
using System.Runtime.CompilerServices;
using TavernOracle.Data.State;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Decision.Positioning;

/// <summary>
/// 统一决策流水线输出结果结构体 (Zero GC Value Type)。
/// 封装 Knapsack DP 求解输出的最优操作序列与随从启发式站位重排结果。
/// </summary>
public readonly struct DecisionPipelineResult : IEquatable<DecisionPipelineResult>
{
    private readonly ActionSequenceResult _actionResult;
    private readonly PositioningResult _positioningResult;

    /// <summary>
    /// 空决策结果默认静态实例。
    /// </summary>
    public static readonly DecisionPipelineResult Empty = new(ActionSequenceResult.Empty, PositioningResult.Empty);

    /// <summary>
    /// 构造统一决策结果。
    /// </summary>
    public DecisionPipelineResult(ActionSequenceResult actionResult, PositioningResult positioningResult)
    {
        _actionResult = actionResult;
        _positioningResult = positioningResult;
    }

    /// <summary>
    /// 最优动作序列结果。
    /// </summary>
    public ActionSequenceResult ActionResult => _actionResult;

    /// <summary>
    /// 随从最优站位重排结果。
    /// </summary>
    public PositioningResult PositioningResult => _positioningResult;

    /// <summary>
    /// 是否为空结果。
    /// </summary>
    public bool IsEmpty => _actionResult.IsEmpty && _positioningResult.Count == 0;

    /// <inheritdoc />
    public bool Equals(DecisionPipelineResult other)
    {
        return _actionResult.Equals(other._actionResult) &&
               _positioningResult.Equals(other._positioningResult);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DecisionPipelineResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_actionResult, _positioningResult);

    public static bool operator ==(DecisionPipelineResult left, DecisionPipelineResult right) => left.Equals(right);
    public static bool operator !=(DecisionPipelineResult left, DecisionPipelineResult right) => !left.Equals(right);
}
