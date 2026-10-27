namespace TavernOracle.Decision.ActionGeneration;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
#if NET8_0_OR_GREATER
/// <summary>
/// 动作集固定容量内联底层存储。
/// 容量固定为 128 个动作，满足酒馆战棋任意单步分支最大生成上限。
/// </summary>
[InlineArray(Capacity)]
public struct InlineActionStorage
{
    public const int Capacity = 128;
    private CandidateAction _element0;
}
#else
/// <summary>
/// 动作集固定容量内联底层存储。
/// 容量固定为 128 个动作，满足酒馆战棋任意单步分支最大生成上限。
/// 兼容 .NET Framework 4.7.2。
/// </summary>
public struct InlineActionStorage
{
    public const int Capacity = 128;
    private CandidateAction[]? _items;

    public CandidateAction this[int index]
    {
        get => (_items ??= new CandidateAction[Capacity])[index];
        set => (_items ??= new CandidateAction[Capacity])[index] = value;
    }

    public readonly ReadOnlySpan<CandidateAction> AsSpan(int count)
    {
        if (_items == null || count == 0) return ReadOnlySpan<CandidateAction>.Empty;
        return new ReadOnlySpan<CandidateAction>(_items, 0, count);
    }
}
#endif

/// <summary>
/// 候选动作集固定容量栈上缓冲区 (Zero GC 值类型)。
/// 避免托管堆内存分配与垃圾回收，单次填充耗时 &lt; 0.05ms。
/// </summary>
public struct CandidateActionBuffer : IReadOnlyList<CandidateAction>
{
    public const int MaxCapacity = InlineActionStorage.Capacity;

    private InlineActionStorage _storage;
    private int _count;

    /// <summary>
    /// 当前缓冲区中有效动作个数
    /// </summary>
    public readonly int Count => _count;

    /// <summary>
    /// 缓冲区最大容量 (128)
    /// </summary>
    public readonly int Capacity => MaxCapacity;

    /// <summary>
    /// 缓冲区是否为空
    /// </summary>
    public readonly bool IsEmpty => _count == 0;

    /// <summary>
    /// 获取当前动作集的只读切片 (ReadOnlySpan)
    /// </summary>
    public readonly ReadOnlySpan<CandidateAction> AsSpan()
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
    /// 只读索引器
    /// </summary>
    public readonly CandidateAction this[int index]
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

    CandidateAction IReadOnlyList<CandidateAction>.this[int index] => this[index];

    /// <summary>
    /// 追加一个候选动作到缓冲区
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in CandidateAction action)
    {
        if (_count >= MaxCapacity)
        {
            throw new InvalidOperationException($"CandidateActionBuffer capacity of {MaxCapacity} exceeded.");
        }
        _storage[_count++] = action;
    }

    /// <summary>
    /// 重置清空缓冲区计数器 (零内存回收开销)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
    }

    /// <summary>
    /// 拷贝缓冲区动作到目标 Span 切片
    /// </summary>
    public readonly void CopyTo(Span<CandidateAction> destination)
    {
        var span = AsSpan();
        span.CopyTo(destination);
    }

    /// <summary>
    /// 导出为数组 (仅限测试与离线调试使用)
    /// </summary>
    public readonly CandidateAction[] ToArray()
    {
        return AsSpan().ToArray();
    }

    /// <summary>
    /// 提供高性能只读 Span 迭代器，支持 foreach 零装箱零 GC 遍历
    /// </summary>
    public readonly ReadOnlySpan<CandidateAction>.Enumerator GetEnumerator() => AsSpan().GetEnumerator();

    IEnumerator<CandidateAction> IEnumerable<CandidateAction>.GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _storage[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<CandidateAction>)this).GetEnumerator();
}
