using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using TavernOracle.Data.State;

namespace TavernOracle.Decision.Positioning;

#if NET8_0_OR_GREATER
/// <summary>
/// 7 槽位固定内联随从缓冲区，确保就地存储重排随从。
/// </summary>
[InlineArray(BoardSnapshot.MaxBoardMinions)]
public struct InlineMinionBuffer
{
    private MinionEntity _element0;
}

/// <summary>
/// 7 槽位固定内联索引缓冲区，确保就地存储最优槽位索引。
/// </summary>
[InlineArray(BoardSnapshot.MaxBoardMinions)]
public struct InlineIndexBuffer
{
    private int _element0;
}
#else
/// <summary>
/// 7 槽位固定内联随从缓冲区，确保就地存储重排随从。
/// 兼容 .NET Framework 4.7.2。
/// </summary>
public struct InlineMinionBuffer
{
    private MinionEntity[]? _items;

    public MinionEntity this[int index]
    {
        get => (_items ??= new MinionEntity[BoardSnapshot.MaxBoardMinions])[index];
        set => (_items ??= new MinionEntity[BoardSnapshot.MaxBoardMinions])[index] = value;
    }

    public Span<MinionEntity> AsSpan() => new Span<MinionEntity>(_items ??= new MinionEntity[BoardSnapshot.MaxBoardMinions]);
    public static implicit operator Span<MinionEntity>(InlineMinionBuffer b) => b.AsSpan();
}

/// <summary>
/// 7 槽位固定内联索引缓冲区，确保就地存储最优槽位索引。
/// 兼容 .NET Framework 4.7.2。
/// </summary>
public struct InlineIndexBuffer
{
    private int[]? _items;

    public int this[int index]
    {
        get => (_items ??= new int[BoardSnapshot.MaxBoardMinions])[index];
        set => (_items ??= new int[BoardSnapshot.MaxBoardMinions])[index] = value;
    }

    public Span<int> AsSpan() => new Span<int>(_items ??= new int[BoardSnapshot.MaxBoardMinions]);
    public static implicit operator Span<int>(InlineIndexBuffer b) => b.AsSpan();
}
#endif

/// <summary>
/// 随从站位排列结果只读结构体 (Zero GC Value Type)。
/// 封装站位优化算法推导出的最优站位结果：
/// - OrderedMinions: 按从左至右攻击槽位 (0 ~ 6) 排序后的随从不可变数组；
/// - OptimalSlotIndices: 输入各随从对应的最优槽位索引映射数组；
/// - Reason / Explanation: 启发式站位决策理由解释；
/// 全链路采用栈内联缓冲与值语义，在热点评估循环中达成 0 GC 堆内存分配。
/// </summary>
public readonly struct PositioningResult : IEquatable<PositioningResult>, IReadOnlyList<MinionEntity>
{
    /// <summary>
    /// 空盘面站位默认静态实例。
    /// </summary>
    public static readonly PositioningResult Empty = new(
        ImmutableArray<MinionEntity>.Empty,
        Array.Empty<int>(),
        "空盘面无需站位重排");

    private static readonly int[][] s_identityIndices =
    [
        Array.Empty<int>(),
        [0],
        [0, 1],
        [0, 1, 2],
        [0, 1, 2, 3],
        [0, 1, 2, 3, 4],
        [0, 1, 2, 3, 4, 5],
        [0, 1, 2, 3, 4, 5, 6]
    ];

    private readonly ImmutableArray<MinionEntity> _orderedMinions;
    private readonly int[]? _optimalSlotIndices;
    private readonly int[]? _originalIndices;
    private readonly string? _reason;
    private readonly InlineMinionBuffer _inlineMinions;
    private readonly InlineIndexBuffer _inlineSlots;
    private readonly InlineIndexBuffer _inlineOriginal;
    private readonly int _count;

    /// <summary>
    /// 重排后的随从总数 (0 ~ 7)。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 重排后的随从不可变数组 (按 ZonePosition 0 ~ 6 升序排列)。
    /// 若从栈内联结果读取，将按需物化。
    /// </summary>
    public ImmutableArray<MinionEntity> OrderedMinions
    {
        get
        {
            if (!_orderedMinions.IsDefault)
            {
                return _orderedMinions;
            }

            if (_count == 0)
            {
                return ImmutableArray<MinionEntity>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<MinionEntity>(_count);
            for (int i = 0; i < _count; i++)
            {
                builder.Add(_inlineMinions[i]);
            }
            return builder.MoveToImmutable();
        }
    }

    /// <summary>
    /// 输入集合中各随从对应的最优目标槽位索引序列 (OptimalSlotIndices[originalIndex] = newSlotIndex)。
    /// 若从栈内联结果读取，将按需物化。
    /// </summary>
    public int[] OptimalSlotIndices
    {
        get
        {
            if (_optimalSlotIndices != null)
            {
                return _optimalSlotIndices;
            }

            if (_count == 0)
            {
                return Array.Empty<int>();
            }

            var array = new int[_count];
            for (int i = 0; i < _count; i++)
            {
                array[i] = _inlineSlots[i];
            }
            return array;
        }
    }

    /// <summary>
    /// 各重排槽位对应的原随从索引 (OriginalIndices[slotIndex] = originalIndex)。
    /// </summary>
    public int[] OriginalIndices
    {
        get
        {
            if (_originalIndices != null)
            {
                return _originalIndices;
            }

            if (_count == 0)
            {
                return Array.Empty<int>();
            }

            var array = new int[_count];
            for (int i = 0; i < _count; i++)
            {
                array[i] = _inlineOriginal[i];
            }
            return array;
        }
    }

    /// <summary>
    /// 站位决策推导理由与战术意图解释。
    /// </summary>
    public string Reason => _reason ?? "启发式站位规则推导完成";

    /// <summary>
    /// 战术理由别名 (对齐 Explain / Explanation 契约)。
    /// </summary>
    public string Explanation => Reason;

    /// <summary>
    /// 是否与输入原站位完全一致 (无需任何拖拽移动)。
    /// </summary>
    public bool IsIdentityPermutation
    {
        get
        {
            for (int i = 0; i < _count; i++)
            {
                if (GetOptimalSlot(i) != i)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 按重排后槽位索引获取随从 (0 GC 索引器)。
    /// </summary>
    public MinionEntity this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range (Count: {_count})");
            }

            return !_orderedMinions.IsDefault ? _orderedMinions[index] : _inlineMinions[index];
        }
    }

    /// <summary>
    /// 获取指定原随从索引对应的最优槽位 (0 GC 快速查询)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOptimalSlot(int originalIndex)
    {
        if ((uint)originalIndex >= (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(originalIndex), $"Original index {originalIndex} out of range (Count: {_count})");
        }

        return _optimalSlotIndices != null ? _optimalSlotIndices[originalIndex] : _inlineSlots[originalIndex];
    }

    /// <summary>
    /// 获取指定目标槽位原本来自输入随从集合的哪个索引 (0 GC 快速查询)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOriginalIndexForSlot(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index {slotIndex} out of range (Count: {_count})");
        }

        return _originalIndices != null ? _originalIndices[slotIndex] : _inlineOriginal[slotIndex];
    }

    /// <summary>
    /// 获取指定重排槽位的随从实体。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MinionEntity GetMinionAtSlot(int slotIndex) => this[slotIndex];

    /// <summary>
    /// 将重排后的随从复制到目标切片中 (0 GC 零堆分配)。
    /// </summary>
    public void CopyOrderedMinionsTo(Span<MinionEntity> destination)
    {
        if (destination.Length < _count)
        {
            throw new ArgumentException($"Destination span too short (Length: {destination.Length}, Count: {_count})");
        }

        for (int i = 0; i < _count; i++)
        {
            destination[i] = this[i];
        }
    }

    /// <summary>
    /// 将各随从最优槽位索引复制到目标切片中 (0 GC 零堆分配)。
    /// </summary>
    public void CopyOptimalSlotsTo(Span<int> destination)
    {
        if (destination.Length < _count)
        {
            throw new ArgumentException($"Destination span too short (Length: {destination.Length}, Count: {_count})");
        }

        for (int i = 0; i < _count; i++)
        {
            destination[i] = GetOptimalSlot(i);
        }
    }

    /// <summary>
    /// 标准构造函数 (用于已有不可变集合及持久化对象)。
    /// </summary>
    public PositioningResult(
        ImmutableArray<MinionEntity> orderedMinions,
        int[] optimalSlotIndices,
        string reason)
    {
        _orderedMinions = orderedMinions.IsDefault ? ImmutableArray<MinionEntity>.Empty : orderedMinions;
        _optimalSlotIndices = optimalSlotIndices ?? Array.Empty<int>();
        _reason = reason;
        _count = _orderedMinions.Length;
        _inlineMinions = default;
        _inlineSlots = default;
        _inlineOriginal = default;

        int limit = Math.Min(_count, BoardSnapshot.MaxBoardMinions);
        for (int i = 0; i < limit; i++)
        {
            _inlineMinions[i] = _orderedMinions[i];
            if (i < _optimalSlotIndices.Length)
            {
                _inlineSlots[i] = _optimalSlotIndices[i];
            }
        }

        // 推导 OriginalIndices
        if (_count > 0 && _optimalSlotIndices.Length >= _count)
        {
            var orig = new int[_count];
            for (int i = 0; i < _count; i++)
            {
                int targetSlot = _optimalSlotIndices[i];
                if (targetSlot >= 0 && targetSlot < _count)
                {
                    orig[targetSlot] = i;
                    _inlineOriginal[targetSlot] = i;
                }
            }
            _originalIndices = orig;
        }
        else
        {
            _originalIndices = Array.Empty<int>();
        }
    }

    /// <summary>
    /// 栈内联零 GC 构造函数 (适用于热点高频决策链路)。
    /// </summary>
    public PositioningResult(
        InlineMinionBuffer inlineMinions,
        InlineIndexBuffer inlineSlots,
        InlineIndexBuffer inlineOriginal,
        int count,
        string reason,
        ImmutableArray<MinionEntity> optionalPrebuiltOrdered = default,
        int[]? optionalPrebuiltSlots = null,
        int[]? optionalPrebuiltOriginal = null)
    {
        _inlineMinions = inlineMinions;
        _inlineSlots = inlineSlots;
        _inlineOriginal = inlineOriginal;
        _count = MathEx.Clamp(count, 0, BoardSnapshot.MaxBoardMinions);
        _reason = reason;
        _orderedMinions = optionalPrebuiltOrdered;
        _optimalSlotIndices = optionalPrebuiltSlots;
        _originalIndices = optionalPrebuiltOriginal;
    }

    public static int[] GetIdentityIndices(int count)
    {
        if (count >= 0 && count < s_identityIndices.Length)
        {
            return s_identityIndices[count];
        }
        var arr = new int[count];
        for (int i = 0; i < count; i++) arr[i] = i;
        return arr;
    }

    #region Enumeration & Equality

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<MinionEntity> IEnumerable<MinionEntity>.GetEnumerator() => new ObjectEnumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => new ObjectEnumerator(this);

    public bool Equals(PositioningResult other)
    {
        if (_count != other._count) return false;
        for (int i = 0; i < _count; i++)
        {
            if (this[i].EntityId != other[i].EntityId || GetOptimalSlot(i) != other.GetOptimalSlot(i))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is PositioningResult other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_count);
        for (int i = 0; i < _count; i++)
        {
            hash.Add(this[i].EntityId);
            hash.Add(GetOptimalSlot(i));
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(PositioningResult left, PositioningResult right) => left.Equals(right);

    public static bool operator !=(PositioningResult left, PositioningResult right) => !left.Equals(right);

    public struct Enumerator
    {
        private readonly PositioningResult _result;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(PositioningResult result)
        {
            _result = result;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _result.Count)
            {
                _index = next;
                return true;
            }
            return false;
        }

        public readonly MinionEntity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _result[_index];
        }
    }

    private sealed class ObjectEnumerator : IEnumerator<MinionEntity>
    {
        private readonly PositioningResult _result;
        private int _index = -1;

        public ObjectEnumerator(PositioningResult result) => _result = result;

        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _result.Count)
            {
                _index = next;
                return true;
            }
            return false;
        }

        public void Reset() => _index = -1;

        public MinionEntity Current => _result[_index];

        object IEnumerator.Current => Current;

        public void Dispose() { }
    }

    #endregion

    public override string ToString()
    {
        return $"[PositioningResult Count={_count}] {Reason}";
    }
}
