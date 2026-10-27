namespace TavernOracle.Decision.Knapsack;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TavernOracle.Decision.ActionGeneration;

#if NET8_0_OR_GREATER
/// <summary>
/// 动作序列定长栈内联底层存储。
/// 容量固定为 32 个动作，完全满足单回合最大买卖腾挪序列上限。
/// </summary>
[InlineArray(Capacity)]
public struct InlineSequenceStorage
{
    public const int Capacity = 32;
    private CandidateAction _element0;
}
#else
/// <summary>
/// 动作序列定长栈内联底层存储。
/// 容量固定为 32 个动作，完全满足单回合最大买卖腾挪序列上限。
/// 兼容 .NET Framework 4.7.2。
/// </summary>
public struct InlineSequenceStorage
{
    public const int Capacity = 32;
    private CandidateAction[]? _items;

    public CandidateAction this[int index]
    {
        get => (_items ??= new CandidateAction[Capacity])[index];
        set => (_items ??= new CandidateAction[Capacity])[index] = value;
    }

    public Span<CandidateAction> AsSpan() => new Span<CandidateAction>(_items ??= new CandidateAction[Capacity]);

    public readonly ReadOnlySpan<CandidateAction> AsSpan(int count)
    {
        if (_items == null || count == 0) return ReadOnlySpan<CandidateAction>.Empty;
        return new ReadOnlySpan<CandidateAction>(_items, 0, count);
    }

    public static implicit operator Span<CandidateAction>(InlineSequenceStorage s) => s.AsSpan();
    public static implicit operator ReadOnlySpan<CandidateAction>(InlineSequenceStorage s) => s.AsSpan();
}
#endif

/// <summary>
/// 最优操作序列结果只读结构体 (Zero GC Value Type)。
/// 封装分支定界有界背包 DP 求解器输出的最优决策结果：
/// - Actions: 按严格执行依赖拓扑排序的原子候选动作不可变列表；
/// - TotalScore: 累积边际收益期望总得分;
/// - ElapsedMs: 算法求解执行耗时 (毫秒);
/// - NodesExplored: 搜索树实际探索的有效节点总数;
/// - IsTimedOut: 是否触发 4.5ms 硬超时保底截断;
/// - AsSpan(): 提供只读栈切片，零 GC 访问。
/// </summary>
public readonly struct ActionSequenceResult : IReadOnlyList<CandidateAction>, IEquatable<ActionSequenceResult>
{
    public const int MaxCapacity = InlineSequenceStorage.Capacity;

    /// <summary>
    /// 空动作序列默认静态实例。
    /// </summary>
    public static readonly ActionSequenceResult Empty = new(
        ReadOnlySpan<CandidateAction>.Empty,
        totalScore: 0.0,
        elapsedMs: 0.0,
        nodesExplored: 0,
        isTimedOut: false);

    private readonly InlineSequenceStorage _storage;
    private readonly int _count;
    private readonly double _totalScore;
    private readonly double _elapsedMs;
    private readonly int _nodesExplored;
    private readonly bool _isTimedOut;
    private readonly ImmutableArray<CandidateAction> _actions;

    /// <summary>
    /// 当前操作序列中动作总步数 (0 ~ 32)。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 序列累积边际收益总得分。
    /// </summary>
    public double TotalScore => _totalScore;

    /// <summary>
    /// 求解器搜索执行耗时 (毫秒)。
    /// </summary>
    public double ElapsedMs => _elapsedMs;

    /// <summary>
    /// 搜索树探索有效节点总数。
    /// </summary>
    public int NodesExplored => _nodesExplored;

    /// <summary>
    /// 是否由于达到 4.5ms 超时截断而触发局部最优贪心保底。
    /// </summary>
    public bool IsTimedOut => _isTimedOut;

    /// <summary>
    /// 操作序列是否为空 (无需任何操作)。
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// 操作序列按需物化的不可变数组。
    /// </summary>
    public ImmutableArray<CandidateAction> Actions
    {
        get
        {
            if (!_actions.IsDefault)
            {
                return _actions;
            }

            if (_count == 0)
            {
                return ImmutableArray<CandidateAction>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<CandidateAction>(_count);
            for (int i = 0; i < _count; i++)
            {
                builder.Add(_storage[i]);
            }
            return builder.MoveToImmutable();
        }
    }

    /// <summary>
    /// 获取当前动作序列的只读切片 (ReadOnlySpan)，零堆内存分配。
    /// </summary>
    public ReadOnlySpan<CandidateAction> AsSpan()
    {
#if NET8_0_OR_GREATER
        return _count == 0
            ? ReadOnlySpan<CandidateAction>.Empty
            : MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _storage[0]), _count);
#else
        return _storage.AsSpan(_count);
#endif
    }

    /// <summary>
    /// 只读索引器。
    /// </summary>
    public CandidateAction this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index was out of range [0, {_count - 1}].");
            }
            return _storage[index];
        }
    }

    /// <summary>
    /// 从只读切片快速构造最优操作序列结果 (零 GC 主入口)。
    /// </summary>
    public ActionSequenceResult(
        ReadOnlySpan<CandidateAction> actions,
        double totalScore,
        double elapsedMs,
        int nodesExplored,
        bool isTimedOut)
    {
        _storage = default;
        _count = Math.Min(actions.Length, MaxCapacity);
        for (int i = 0; i < _count; i++)
        {
            _storage[i] = actions[i];
        }
        _totalScore = totalScore;
        _elapsedMs = elapsedMs;
        _nodesExplored = nodesExplored;
        _isTimedOut = isTimedOut;
        _actions = default;
    }

    /// <summary>
    /// 从 ImmutableArray 显式构造最优操作序列结果。
    /// </summary>
    public ActionSequenceResult(
        ImmutableArray<CandidateAction> actions,
        double totalScore,
        double elapsedMs,
        int nodesExplored,
        bool isTimedOut)
    {
        _storage = default;
        _count = actions.IsDefault ? 0 : Math.Min(actions.Length, MaxCapacity);
        for (int i = 0; i < _count; i++)
        {
            _storage[i] = actions[i];
        }
        _totalScore = totalScore;
        _elapsedMs = elapsedMs;
        _nodesExplored = nodesExplored;
        _isTimedOut = isTimedOut;
        _actions = actions.IsDefault ? ImmutableArray<CandidateAction>.Empty : actions;
    }

    /// <summary>
    /// 导出为数组 (仅限调试与单测使用)。
    /// </summary>
    public CandidateAction[] ToArray() => AsSpan().ToArray();

    /// <summary>
    /// 提供高性能只读 Span 迭代器，支持 foreach 零装箱零 GC 遍历。
    /// </summary>
    public ReadOnlySpan<CandidateAction>.Enumerator GetEnumerator() => AsSpan().GetEnumerator();

    IEnumerator<CandidateAction> IEnumerable<CandidateAction>.GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _storage[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<CandidateAction>)this).GetEnumerator();

    public bool Equals(ActionSequenceResult other)
    {
        if (_count != other._count ||
            Math.Abs(_totalScore - other._totalScore) > 1e-6 ||
            _isTimedOut != other._isTimedOut)
        {
            return false;
        }

        for (int i = 0; i < _count; i++)
        {
            if (!_storage[i].Equals(other._storage[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is ActionSequenceResult other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_count);
        hash.Add(_totalScore);
        hash.Add(_isTimedOut);
        for (int i = 0; i < _count; i++)
        {
            hash.Add(_storage[i]);
        }
        return hash.ToHashCode();
    }

    public override string ToString() =>
        $"[ActionSequence: {_count} steps, Score: {_totalScore:F1}, Elapsed: {_elapsedMs:F3}ms, Nodes: {_nodesExplored}{( _isTimedOut ? " (TimedOut)" : "")}]";
}
