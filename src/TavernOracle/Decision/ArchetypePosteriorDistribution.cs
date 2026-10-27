namespace TavernOracle.Decision;

using System.Runtime.CompilerServices;
using TavernOracle.Priors;

#if NET8_0_OR_GREATER
/// <summary>
/// 16 槽位固定内联概率缓冲区，确保就地存储各流派后验概率。
/// </summary>
[InlineArray(ArchetypePosteriorDistribution.MaxArchetypes)]
public struct InlineArchetypeProbabilityBuffer
{
    private double _element0;
}
#else
/// <summary>
/// 16 槽位固定内联概率缓冲区，确保就地存储各流派后验概率 (兼容 .NET Framework 4.7.2，零堆分配)。
/// </summary>
public struct InlineArchetypeProbabilityBuffer
{
    private double _e0, _e1, _e2, _e3, _e4, _e5, _e6, _e7, _e8, _e9, _e10, _e11, _e12, _e13, _e14, _e15;

    public double this[int index]
    {
        get => index switch
        {
            0 => _e0, 1 => _e1, 2 => _e2, 3 => _e3,
            4 => _e4, 5 => _e5, 6 => _e6, 7 => _e7,
            8 => _e8, 9 => _e9, 10 => _e10, 11 => _e11,
            12 => _e12, 13 => _e13, 14 => _e14, 15 => _e15,
            _ => 0.0
        };
        set
        {
            switch (index)
            {
                case 0: _e0 = value; break;
                case 1: _e1 = value; break;
                case 2: _e2 = value; break;
                case 3: _e3 = value; break;
                case 4: _e4 = value; break;
                case 5: _e5 = value; break;
                case 6: _e6 = value; break;
                case 7: _e7 = value; break;
                case 8: _e8 = value; break;
                case 9: _e9 = value; break;
                case 10: _e10 = value; break;
                case 11: _e11 = value; break;
                case 12: _e12 = value; break;
                case 13: _e13 = value; break;
                case 14: _e14 = value; break;
                case 15: _e15 = value; break;
            }
        }
    }
}
#endif

/// <summary>
/// 单个流派与对应后验概率条目。
/// </summary>
public readonly record struct ArchetypeProbabilityEntry(ArchetypeDefinition Archetype, double Probability)
{
    public override string ToString() => $"{Archetype.NameCn}: {Probability:P2}";
}

/// <summary>
/// 贝叶斯流派后验概率分布快照（只读值类型，保证热点推导循环零 GC 堆垃圾）。
/// </summary>
public readonly struct ArchetypePosteriorDistribution
{
    /// <summary>
    /// 支持的最大流派数量上限（当前酒馆主流流派为 10 个，预留 16 个槽位）。
    /// </summary>
    public const int MaxArchetypes = 16;

    private readonly InlineArchetypeProbabilityBuffer _probabilities;
    private readonly IReadOnlyList<ArchetypeDefinition>? _archetypes;
    private readonly IReadOnlyDictionary<string, int>? _archetypeIndexMap;
    private readonly int _count;
    private readonly int _topIndex;
    private readonly double _topProbability;

    /// <summary>
    /// 有效流派总数。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 当前后验概率最高的流派定义（若全禁选或无数据则为 null）。
    /// </summary>
    public ArchetypeDefinition? TopArchetype => (_archetypes != null && _topIndex >= 0 && _topIndex < _count)
        ? _archetypes[_topIndex]
        : null;

    /// <summary>
    /// 最高流派 ID。
    /// </summary>
    public string TopArchetypeId => TopArchetype?.Id ?? string.Empty;

    /// <summary>
    /// 最高流派的后验概率值 [0.0, 1.0]。
    /// </summary>
    public double TopProbability => _topProbability;

    /// <summary>
    /// 按流派索引获取后验概率。
    /// </summary>
    public double this[int index] => GetProbability(index);

    /// <summary>
    /// 按流派 ID 获取后验概率。
    /// </summary>
    public double this[string archetypeId] => GetProbability(archetypeId);

    public ArchetypePosteriorDistribution(
        InlineArchetypeProbabilityBuffer probabilities,
        IReadOnlyList<ArchetypeDefinition> archetypes,
        IReadOnlyDictionary<string, int> archetypeIndexMap,
        int count,
        int topIndex,
        double topProbability)
    {
        _probabilities = probabilities;
        _archetypes = archetypes;
        _archetypeIndexMap = archetypeIndexMap;
        _count = count;
        _topIndex = topIndex;
        _topProbability = topProbability;
    }

    /// <summary>
    /// 获取指定索引的流派概率。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetProbability(int index)
    {
        if (index < 0 || index >= _count)
        {
            return 0.0;
        }

#if NET8_0_OR_GREATER
        ReadOnlySpan<double> span = _probabilities;
        return span[index];
#else
        return _probabilities[index];
#endif
    }

    /// <summary>
    /// 获取指定流派 ID 的概率。
    /// </summary>
    public double GetProbability(string? archetypeId)
    {
        if (string.IsNullOrEmpty(archetypeId) || _archetypeIndexMap == null)
        {
            return 0.0;
        }

        return _archetypeIndexMap.TryGetValue(archetypeId, out var index)
            ? GetProbability(index)
            : 0.0;
    }

    /// <summary>
    /// 获取指定流派对象的概率。
    /// </summary>
    public double GetProbability(ArchetypeDefinition? archetype) =>
        archetype != null ? GetProbability(archetype.Id) : 0.0;

    /// <summary>
    /// 获取指定索引的流派定义。
    /// </summary>
    public ArchetypeDefinition? GetArchetype(int index)
    {
        if (_archetypes == null || index < 0 || index >= _count)
        {
            return null;
        }

        return _archetypes[index];
    }

    /// <summary>
    /// 提供零 GC 分配的只读迭代器。
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// 转换为字典映射（用于调试或脱离热点循环时呈现）。
    /// </summary>
    public Dictionary<string, double> ToDictionary()
    {
        var dict = new Dictionary<string, double>(_count, StringComparer.OrdinalIgnoreCase);
        if (_archetypes == null)
        {
            return dict;
        }

        for (int i = 0; i < _count; i++)
        {
            dict[_archetypes[i].Id] = GetProbability(i);
        }

        return dict;
    }

    /// <summary>
    /// 转换为条目数组。
    /// </summary>
    public ArchetypeProbabilityEntry[] ToArray()
    {
        if (_archetypes == null || _count == 0)
        {
            return Array.Empty<ArchetypeProbabilityEntry>();
        }

        var array = new ArchetypeProbabilityEntry[_count];
        for (int i = 0; i < _count; i++)
        {
            array[i] = new ArchetypeProbabilityEntry(_archetypes[i], GetProbability(i));
        }

        return array;
    }

    public override string ToString()
    {
        return TopArchetype != null
            ? $"[Top: {TopArchetype.NameCn} ({TopProbability:P1}) | Total {_count} Archetypes]"
            : "[Empty Posterior Distribution]";
    }

    /// <summary>
    /// 零 GC 栈上迭代器。
    /// </summary>
    public struct Enumerator
    {
        private readonly ArchetypePosteriorDistribution _distribution;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator(ArchetypePosteriorDistribution distribution)
        {
            _distribution = distribution;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _index++;
            return _index < _distribution.Count;
        }

        public ArchetypeProbabilityEntry Current => new(
            _distribution.GetArchetype(_index)!,
            _distribution.GetProbability(_index));
    }
}
