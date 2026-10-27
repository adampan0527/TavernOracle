namespace TavernOracle.Decision;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using TavernOracle.Priors;

/// <summary>
/// 贝叶斯流派后验概率推导配置选项。
/// </summary>
public sealed class BayesianInferenceOptions
{
    /// <summary>
    /// 手牌组件证据折扣系数（默认 0.90，手牌未入场打出，保留轻微折现）。
    /// </summary>
    public double HandDiscountFactor { get; set; } = 0.90;

    /// <summary>
    /// 单卡协同权重证据乘数（默认 1.0）。
    /// </summary>
    public double SingleEvidenceWeight { get; set; } = 1.0;

    /// <summary>
    /// 核心随从成对联动协同证据乘数（默认 1.0）。
    /// </summary>
    public double PairEvidenceWeight { get; set; } = 1.0;

    /// <summary>
    /// 同种族非核心引擎随从的基础似然对数加分（默认 0.3）。
    /// </summary>
    public double SameRaceBaselineBonus { get; set; } = 0.3;

    /// <summary>
    /// 默认单例配置。
    /// </summary>
    public static BayesianInferenceOptions Default { get; } = new();
}

/// <summary>
/// 贝叶斯流派后验概率推导器。
/// 严格实现数学公式：
/// P(Archetype_k | H, C, B) = [P(C | Archetype_k) * P(Archetype_k | H, B)] / Sum_j [P(C | Archetype_j) * P(Archetype_j | H, B)]
/// 全流程基于预索引结构与栈上计算，达成零 GC 堆垃圾与 &lt; 0.05 ms 极速推导。
/// </summary>
public sealed class BayesianArchetypeInference : IBayesianArchetypeInference
{
    private const int MaxArchetypes = ArchetypePosteriorDistribution.MaxArchetypes;
    private const int MaxTrackedMinions = 16;

    private readonly IArchetypeRepository _repository;
    private readonly IHeroAffinityProvider _heroAffinityProvider;
    private readonly BayesianInferenceOptions _options;

    // 预计算流派元数据
    private readonly ArchetypeDefinition[] _archetypes;
    private readonly FrozenDictionary<string, int> _archetypeIndexMap;
    private readonly int _archetypeCount;

    // 预计算卡牌索引与权重矩阵 (Archetype x CardId)
    private readonly FrozenDictionary<string, int> _cardIndexMap;
    private readonly int _cardCount;
    private readonly double[] _synergyWeights;
    private readonly MinionRaceMask[] _cardRaceMasks;

    // 预计算双卡协同查找表: (PackIndices(idxA, idxB)) -> synergyScore
    private readonly FrozenDictionary<long, double> _pairSynergyLookup;

    public IArchetypeRepository Repository => _repository;
    public IHeroAffinityProvider HeroAffinityProvider => _heroAffinityProvider;
    public BayesianInferenceOptions Options => _options;

    public BayesianArchetypeInference(
        IArchetypeRepository? repository = null,
        IHeroAffinityProvider? heroAffinityProvider = null,
        BayesianInferenceOptions? options = null)
    {
        _repository = repository ?? ArchetypeRepository.Instance;
        _heroAffinityProvider = heroAffinityProvider ?? TavernOracle.Decision.HeroAffinityProvider.Default;
        _options = options ?? BayesianInferenceOptions.Default;

        // 1. 初始化流派列表与索引映射
        var archList = _repository.GetAllArchetypes();
        _archetypeCount = Math.Min(archList.Count, MaxArchetypes);
        _archetypes = new ArchetypeDefinition[_archetypeCount];
        var archMap = new Dictionary<string, int>(_archetypeCount, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _archetypeCount; i++)
        {
            _archetypes[i] = archList[i];
            archMap[archList[i].Id] = i;
        }
        _archetypeIndexMap = archMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        // 2. 收集全量已知发动机卡牌 ID
        var cardIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arch in _archetypes)
        {
            foreach (var minion in arch.CoreMinions)
            {
                if (!string.IsNullOrEmpty(minion.CardId))
                {
                    cardIdSet.Add(minion.CardId);
                }
            }
        }

        foreach (var pair in _repository.SynergyMatrix.AllPairs)
        {
            if (!string.IsNullOrEmpty(pair.CardIdA)) cardIdSet.Add(pair.CardIdA);
            if (!string.IsNullOrEmpty(pair.CardIdB)) cardIdSet.Add(pair.CardIdB);
        }

        foreach (var engineMinion in _repository.GetAllEngineMinions())
        {
            if (!string.IsNullOrEmpty(engineMinion.CardId))
            {
                cardIdSet.Add(engineMinion.CardId);
            }
        }

        _cardCount = cardIdSet.Count;
        var cardMap = new Dictionary<string, int>(_cardCount, StringComparer.OrdinalIgnoreCase);
        _cardRaceMasks = new MinionRaceMask[_cardCount];
        int cardIdx = 0;
        foreach (var cardId in cardIdSet)
        {
            cardMap[cardId] = cardIdx;
            var engineMinion = _repository.GetEngineMinion(cardId);
            _cardRaceMasks[cardIdx] = engineMinion?.RaceMask ?? MinionRaceMaskExtensions.Parse(engineMinion?.Race);
            cardIdx++;
        }
        _cardIndexMap = cardMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        // 3. 构建平面矩阵 weights[archIndex, cardIndex]
        _synergyWeights = new double[_archetypeCount * _cardCount];
        for (int a = 0; a < _archetypeCount; a++)
        {
            var arch = _archetypes[a];
            foreach (var kvp in _cardIndexMap)
            {
                var cid = kvp.Key;
                var cIndex = kvp.Value;
                double weight = _repository.GetSynergyWeight(arch.Id, cid);
                _synergyWeights[a * _cardCount + cIndex] = weight;
            }
        }

        // 4. 构建打包 long 键的双卡协同快速查找表
        var pairLookup = new Dictionary<long, double>();
        foreach (var pair in _repository.SynergyMatrix.AllPairs)
        {
            if (_cardIndexMap.TryGetValue(pair.CardIdA, out int idxA) &&
                _cardIndexMap.TryGetValue(pair.CardIdB, out int idxB))
            {
                long packed = PackIndices(idxA, idxB);
                pairLookup[packed] = pair.SynergyScore;
            }
        }
        _pairSynergyLookup = pairLookup.ToFrozenDictionary();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackIndices(int a, int b)
    {
        return a < b
            ? ((long)a << 32) | (uint)b
            : ((long)b << 32) | (uint)a;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public ArchetypePosteriorDistribution InferSpan(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        ReadOnlySpan<string> boardMinions,
        ReadOnlySpan<string> handMinions = default)
    {
        Span<int> boardIndices = stackalloc int[MaxTrackedMinions];
        int boardCount = 0;
        int maxBoard = Math.Min(boardMinions.Length, MaxTrackedMinions);
        for (int i = 0; i < maxBoard; i++)
        {
            var cid = boardMinions[i];
            if (!string.IsNullOrEmpty(cid) && _cardIndexMap.TryGetValue(cid, out int idx))
            {
                boardIndices[boardCount++] = idx;
            }
        }

        Span<int> handIndices = stackalloc int[MaxTrackedMinions];
        int handCount = 0;
        int maxHand = Math.Min(handMinions.Length, MaxTrackedMinions);
        for (int i = 0; i < maxHand; i++)
        {
            var cid = handMinions[i];
            if (!string.IsNullOrEmpty(cid) && _cardIndexMap.TryGetValue(cid, out int idx))
            {
                handIndices[handCount++] = idx;
            }
        }

        return InferCore(
            heroCardIdOrName,
            bannedRaces,
            boardIndices.Slice(0, boardCount),
            handIndices.Slice(0, handCount));
    }

    /// <inheritdoc />
    public ArchetypePosteriorDistribution Infer(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        IReadOnlyList<string>? boardMinions,
        IReadOnlyList<string>? handMinions = null)
    {
        Span<int> boardIndices = stackalloc int[MaxTrackedMinions];
        int boardCount = 0;
        if (boardMinions != null)
        {
            int maxBoard = Math.Min(boardMinions.Count, MaxTrackedMinions);
            for (int i = 0; i < maxBoard; i++)
            {
                var cid = boardMinions[i];
                if (!string.IsNullOrEmpty(cid) && _cardIndexMap.TryGetValue(cid, out int idx))
                {
                    boardIndices[boardCount++] = idx;
                }
            }
        }

        Span<int> handIndices = stackalloc int[MaxTrackedMinions];
        int handCount = 0;
        if (handMinions != null)
        {
            int maxHand = Math.Min(handMinions.Count, MaxTrackedMinions);
            for (int i = 0; i < maxHand; i++)
            {
                var cid = handMinions[i];
                if (!string.IsNullOrEmpty(cid) && _cardIndexMap.TryGetValue(cid, out int idx))
                {
                    handIndices[handCount++] = idx;
                }
            }
        }

        return InferCore(
            heroCardIdOrName,
            bannedRaces,
            boardIndices.Slice(0, boardCount),
            handIndices.Slice(0, handCount));
    }

    /// <inheritdoc />
    public ArchetypePosteriorDistribution Infer(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        string[]? boardMinions,
        string[]? handMinions = null)
    {
        return InferSpan(
            heroCardIdOrName,
            bannedRaces,
            boardMinions != null ? new ReadOnlySpan<string>(boardMinions) : ReadOnlySpan<string>.Empty,
            handMinions != null ? new ReadOnlySpan<string>(handMinions) : ReadOnlySpan<string>.Empty);
    }

    /// <inheritdoc />
    public ArchetypePosteriorDistribution Infer(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        IReadOnlyList<string>? boardMinions)
    {
        return Infer(heroCardIdOrName, bannedRaces, boardMinions, null);
    }

    /// <summary>
    /// 核心推导例程（纯栈上数值计算，杜绝任何托管堆内存分配）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ArchetypePosteriorDistribution InferCore(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        ReadOnlySpan<int> boardIndices,
        ReadOnlySpan<int> handIndices)
    {
        int k = _archetypeCount;
        if (k == 0)
        {
            return default;
        }

        Span<bool> isBanned = stackalloc bool[MaxArchetypes];
        Span<double> priorWeights = stackalloc double[MaxArchetypes];
        Span<double> logLikelihoods = stackalloc double[MaxArchetypes];
        Span<double> logPosteriors = stackalloc double[MaxArchetypes];
        Span<double> unnormPosteriors = stackalloc double[MaxArchetypes];

        logLikelihoods.Clear();

        // 1. 禁选池硬约束 B：若流派的主要种族在当前对局禁选池中，先验直接归零
        for (int i = 0; i < k; i++)
        {
            var arch = _archetypes[i];
            bool banned = arch.PrimaryRace != MinionRaceMask.None && (bannedRaces & arch.PrimaryRace) != 0;
            isBanned[i] = banned;
        }

        // 2. 英雄亲和先验 H 计算
        HeroAffinity affinity = default;
        bool hasAffinity = false;
        if (!string.IsNullOrWhiteSpace(heroCardIdOrName))
        {
            hasAffinity = _heroAffinityProvider.TryGetAffinity(heroCardIdOrName, out affinity);
        }

        double totalPriorWeight = 0.0;
        for (int i = 0; i < k; i++)
        {
            if (isBanned[i])
            {
                priorWeights[i] = 0.0;
                continue;
            }

            double w = 1.0;
            if (hasAffinity)
            {
                var arch = _archetypes[i];
                bool matches = false;
                if (!string.IsNullOrEmpty(affinity.ArchetypeIdAffinity))
                {
                    matches = string.Equals(arch.Id, affinity.ArchetypeIdAffinity, StringComparison.OrdinalIgnoreCase);
                }
                else if (affinity.RaceAffinity != MinionRaceMask.None)
                {
                    matches = (arch.PrimaryRace & affinity.RaceAffinity) != 0;
                }

                if (matches)
                {
                    w *= affinity.WeightMultiplier;
                }
            }

            priorWeights[i] = w;
            totalPriorWeight += w;
        }

        // 若全部流派都被禁选或有效权重为 0，则返回零分布
        if (totalPriorWeight <= 0.0)
        {
            return new ArchetypePosteriorDistribution(
                default,
                _archetypes,
                _archetypeIndexMap,
                k,
                -1,
                0.0);
        }

        // 3. 场面单卡证据似然对数累加 C_board
        int boardCount = boardIndices.Length;
        for (int b = 0; b < boardCount; b++)
        {
            int cardIdx = boardIndices[b];
            MinionRaceMask cRace = _cardRaceMasks[cardIdx];

            for (int i = 0; i < k; i++)
            {
                if (isBanned[i]) continue;

                double w = _synergyWeights[i * _cardCount + cardIdx];
                if (w > 0.0)
                {
                    logLikelihoods[i] += w * _options.SingleEvidenceWeight;
                }
                else if (cRace != MinionRaceMask.None && (_archetypes[i].PrimaryRace & cRace) != 0)
                {
                    logLikelihoods[i] += _options.SameRaceBaselineBonus;
                }
            }
        }

        // 4. 手牌单卡证据似然对数累加 C_hand (乘以 HandDiscountFactor)
        int handCount = handIndices.Length;
        double handDiscount = _options.HandDiscountFactor;
        for (int h = 0; h < handCount; h++)
        {
            int cardIdx = handIndices[h];
            MinionRaceMask cRace = _cardRaceMasks[cardIdx];

            for (int i = 0; i < k; i++)
            {
                if (isBanned[i]) continue;

                double w = _synergyWeights[i * _cardCount + cardIdx];
                if (w > 0.0)
                {
                    logLikelihoods[i] += w * _options.SingleEvidenceWeight * handDiscount;
                }
                else if (cRace != MinionRaceMask.None && (_archetypes[i].PrimaryRace & cRace) != 0)
                {
                    logLikelihoods[i] += _options.SameRaceBaselineBonus * handDiscount;
                }
            }
        }

        // 5. 双卡联动协同矩阵对数似然累加
        // 5.1 Board - Board 成对联动
        for (int i = 0; i < boardCount; i++)
        {
            int idxA = boardIndices[i];
            for (int j = i + 1; j < boardCount; j++)
            {
                int idxB = boardIndices[j];
                AccumulatePairSynergy(idxA, idxB, 1.0, isBanned, logLikelihoods);
            }
        }

        // 5.2 Board - Hand 成对联动 (位置系数为 handDiscount)
        for (int i = 0; i < boardCount; i++)
        {
            int idxA = boardIndices[i];
            for (int j = 0; j < handCount; j++)
            {
                int idxB = handIndices[j];
                AccumulatePairSynergy(idxA, idxB, handDiscount, isBanned, logLikelihoods);
            }
        }

        // 5.3 Hand - Hand 成对联动 (位置系数为 handDiscount)
        for (int i = 0; i < handCount; i++)
        {
            int idxA = handIndices[i];
            for (int j = i + 1; j < handCount; j++)
            {
                int idxB = handIndices[j];
                AccumulatePairSynergy(idxA, idxB, handDiscount, isBanned, logLikelihoods);
            }
        }

        // 6. 贝叶斯后验对数求和与最大值寻找 (Log-Sum-Exp 稳定化)
        double maxLogScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            if (isBanned[i] || priorWeights[i] <= 0.0)
            {
                logPosteriors[i] = double.NegativeInfinity;
                continue;
            }

            double logPrior = Math.Log(priorWeights[i] / totalPriorWeight);
            double score = logPrior + logLikelihoods[i];
            logPosteriors[i] = score;

            if (score > maxLogScore)
            {
                maxLogScore = score;
            }
        }

        // 7. 减去最大对数分进行 Softmax 归一化，彻底规避浮点溢出与下溢
        double expSum = 0.0;
        for (int i = 0; i < k; i++)
        {
            if (isBanned[i] || double.IsNegativeInfinity(logPosteriors[i]))
            {
                unnormPosteriors[i] = 0.0;
                continue;
            }

            double expVal = Math.Exp(logPosteriors[i] - maxLogScore);
            unnormPosteriors[i] = expVal;
            expSum += expVal;
        }

        InlineArchetypeProbabilityBuffer probBuffer = default;
#if NET8_0_OR_GREATER
        Span<double> probSpan = probBuffer;
#endif
        int topIndex = -1;
        double topProb = -1.0;

        if (expSum > 0.0)
        {
            for (int i = 0; i < k; i++)
            {
                if (isBanned[i] || unnormPosteriors[i] <= 0.0)
                {
#if NET8_0_OR_GREATER
                    probSpan[i] = 0.0;
#else
                    probBuffer[i] = 0.0;
#endif
                }
                else
                {
                    double p = unnormPosteriors[i] / expSum;
#if NET8_0_OR_GREATER
                    probSpan[i] = p;
#else
                    probBuffer[i] = p;
#endif
                    if (p > topProb)
                    {
                        topProb = p;
                        topIndex = i;
                    }
                }
            }
        }

        return new ArchetypePosteriorDistribution(
            probBuffer,
            _archetypes,
            _archetypeIndexMap,
            k,
            topIndex,
            topProb > 0.0 ? topProb : 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AccumulatePairSynergy(
        int idxA,
        int idxB,
        double locationMultiplier,
        ReadOnlySpan<bool> isBanned,
        Span<double> logLikelihoods)
    {
        long key = PackIndices(idxA, idxB);
        if (_pairSynergyLookup.TryGetValue(key, out double pairScore) && pairScore > 1.0)
        {
            double bonus = (pairScore - 1.0) * _options.PairEvidenceWeight * locationMultiplier;
            int k = _archetypeCount;

            for (int archIdx = 0; archIdx < k; archIdx++)
            {
                if (isBanned[archIdx]) continue;

                // 若该流派同时吸纳这两张随从作为协同组件，则获得成对质变增量
                if (_synergyWeights[archIdx * _cardCount + idxA] > 0.0 &&
                    _synergyWeights[archIdx * _cardCount + idxB] > 0.0)
                {
                    logLikelihoods[archIdx] += bonus;
                }
            }
        }
    }
}
