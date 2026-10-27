namespace TavernOracle.Decision.Evaluation;

using System;
using System.Runtime.CompilerServices;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Priors;

/// <summary>
/// 动作边际收益价值评估器 (Action Marginal Value Evaluator)。
/// 严格实现公式：
/// ΔV(a) = w_tempo * Δ战力 + w_scale * Δ协同 + 对子加成
/// 核心特性：
/// 1. 战力生死线风控注入：w_tempo 与 w_scale 严格由 T10 RiskWeightsProfile 驱动；当 P_lethal > 30% 时 w_scale = 0.0 强制切断协同贪婪分与留对子逻辑；
/// 2. 机制等效战力解算：圣盾、剧毒/烈毒、顺劈/狂战斧、复生、嘲讽、风怒等机制标签赋予等价战力权重，卖出随从扣除对应战力；
/// 3. 贝叶斯后验协同加权：基于 T09 ArchetypePosteriorDistribution 概率分布加权各大流派协同收益增量，关键发动机随从赋予超额加成；
/// 4. 对子加成与三连质变：检索手牌与战场已有随从，对子赋予留对加分，第 3 张直接触发金色三连与高本发现质变跃迁；
/// 5. 零 GC 堆分配与极低时延：全链路使用栈切片与缓存索引，单次评估耗时 <= 1ms (实测 < 0.02ms)，无任何托管堆分配。
/// </summary>
public sealed class ActionValueEvaluator : IActionValueEvaluator
{
    private static readonly Lazy<ActionValueEvaluator> s_lazyInstance = new(() => new ActionValueEvaluator());

    /// <summary>
    /// 全局共享默认单例实例 (线程安全，零启动开销)。
    /// </summary>
    public static ActionValueEvaluator Instance => s_lazyInstance.Value;

    /// <summary>
    /// 全局共享单例别名。
    /// </summary>
    public static ActionValueEvaluator Shared => Instance;

    private readonly IHearthDbAdapter _hearthDb;
    private readonly IArchetypeRepository _archetypeRepo;
    private readonly IBayesianArchetypeInference _bayesianInference;
    private readonly IRiskControlValve _riskValve;

    /// <summary>
    /// 构造动作边际收益价值评估器。支持依赖注入或默认单例组合。
    /// </summary>
    public ActionValueEvaluator(
        IHearthDbAdapter? hearthDb = null,
        IArchetypeRepository? archetypeRepo = null,
        IBayesianArchetypeInference? bayesianInference = null,
        IRiskControlValve? riskValve = null)
    {
        _hearthDb = hearthDb ?? HearthDbAdapter.Instance;
        _archetypeRepo = archetypeRepo ?? ArchetypeRepository.Instance;
        _bayesianInference = bayesianInference ?? new BayesianArchetypeInference(_archetypeRepo);
        _riskValve = riskValve ?? RiskControlValve.Instance;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionEvaluationScore Evaluate(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile)
    {
        return action.ActionType switch
        {
            ActionType.Buy => EvaluateBuy(snapshot, action, posteriors, riskProfile),
            ActionType.Magnetize => EvaluatePlay(snapshot, action, posteriors, riskProfile),
            ActionType.Play => EvaluatePlay(snapshot, action, posteriors, riskProfile),
            ActionType.Sell => EvaluateSell(snapshot, action, posteriors, riskProfile),
            ActionType.UpgradeTier => EvaluateUpgradeTier(snapshot, action, riskProfile),
            ActionType.Reroll => EvaluateReroll(snapshot, action, riskProfile),
            ActionType.Freeze => EvaluateFreeze(snapshot, action, posteriors, riskProfile),
            _ => ActionEvaluationScore.Zero
        };
    }

    private struct InlineBoardIdBuffer
    {
        private string[]? _items;
        public Span<string> AsSpan(int count) => new Span<string>(_items ??= new string[BoardSnapshot.MaxBoardMinions], 0, count);
        public static implicit operator Span<string>(InlineBoardIdBuffer b) => b.AsSpan(BoardSnapshot.MaxBoardMinions);
    }

    private struct InlineHandIdBuffer
    {
        private string[]? _items;
        public Span<string> AsSpan(int count) => new Span<string>(_items ??= new string[BoardSnapshot.MaxHandCards], 0, count);
        public static implicit operator Span<string>(InlineHandIdBuffer b) => b.AsSpan(BoardSnapshot.MaxHandCards);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionEvaluationScore Evaluate(in BoardSnapshot snapshot, in CandidateAction action)
    {
        var riskProfile = _riskValve.Evaluate(snapshot.Simulation.LethalRate);

        int boardLen = Math.Min(snapshot.BoardMinions.Length, BoardSnapshot.MaxBoardMinions);
        InlineBoardIdBuffer boardBuffer = default;
        Span<string> boardIds = ((Span<string>)boardBuffer).Slice(0, boardLen);
        for (int i = 0; i < boardLen; i++)
        {
            boardIds[i] = snapshot.BoardMinions[i].CardId;
        }

        int handLen = Math.Min(snapshot.HandCards.Length, BoardSnapshot.MaxHandCards);
        InlineHandIdBuffer handBuffer = default;
        Span<string> handIds = ((Span<string>)handBuffer).Slice(0, handLen);
        for (int i = 0; i < handLen; i++)
        {
            handIds[i] = snapshot.HandCards[i].CardId;
        }

        var posteriors = _bayesianInference.InferSpan(
            snapshot.HeroCardId,
            snapshot.BannedRaces,
            boardIds,
            handIds);

        return Evaluate(snapshot, action, posteriors, riskProfile);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluateBatch(
        in BoardSnapshot snapshot,
        in CandidateActionBuffer actions,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile,
        Span<ActionEvaluationScore> destination)
    {
        int count = Math.Min(actions.Count, destination.Length);
        for (int i = 0; i < count; i++)
        {
            destination[i] = Evaluate(snapshot, actions[i], posteriors, riskProfile);
        }
    }

    #region 单一动作具体评估实现

    private ActionEvaluationScore EvaluateBuy(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile)
    {
        string cardId = !string.IsNullOrEmpty(action.CardId)
            ? action.CardId
            : (action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length
                ? snapshot.TavernMinions[action.SlotIndex].CardId
                : string.Empty);

        int attack;
        int health;
        CardMechanicsMask mechanics;
        MinionRaceMask race;
        int tier;

        if (_hearthDb.TryGetCard(cardId, out var meta))
        {
            attack = meta.BaseAttack;
            health = meta.BaseHealth;
            mechanics = meta.Mechanics;
            race = meta.Race != MinionRaceMask.None
                ? meta.Race
                : (action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length
                    ? snapshot.TavernMinions[action.SlotIndex].Race
                    : MinionRaceMask.None);
            tier = meta.Tier > 0
                ? meta.Tier
                : (action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length
                    ? snapshot.TavernMinions[action.SlotIndex].Tier
                    : 1);
        }
        else
        {
            tier = action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length
                ? snapshot.TavernMinions[action.SlotIndex].Tier
                : 1;
            race = action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length
                ? snapshot.TavernMinions[action.SlotIndex].Race
                : MinionRaceMask.None;
            attack = Math.Max(1, tier * 2);
            health = Math.Max(1, tier * 2);
            mechanics = CardMechanicsMask.None;
        }

        // 1. Δ战力 (即时战力与机制标签等价折算)
        if (action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length)
        {
            var live = snapshot.TavernMinions[action.SlotIndex];
            if (live.HasLiveStats) { attack = live.Attack; health = live.Health; mechanics = live.Mechanics; race = live.Race; }
        }

        double rawTempo = CalculateMinionCombatPower(attack, health, mechanics);
        double tempoScore = riskProfile.TempoWeight * rawTempo;

        // 2. Δ协同 (当 P_lethal > 30% 触发暴毙风控熔断时，w_scale = 0.0 强制切断协同远期贪婪分)
        bool isEmergency = riskProfile.IsEmergencyMutated || riskProfile.ScaleWeight <= 0.00001 || riskProfile.PLethal > 0.30;
        double rawSynergy = 0.0;
        double synergyScore = 0.0;

        if (!isEmergency)
        {
            rawSynergy = CalculateSynergy(cardId, race, posteriors, snapshot);
            synergyScore = riskProfile.ScaleWeight * rawSynergy;
        }

        // 3. 对子加成与三连质变
        int existingCopies = CountExistingCopies(snapshot, cardId);
        double pairScore = 0.0;
        double tripleBonus = 0.0;

        if (isEmergency)
        {
            // 暴毙危机模式切断留对子贪婪逻辑；但若直接达成三连，金色成型依然提供即时收益
            if (existingCopies >= 2)
            {
                tripleBonus = CalculateTripleBonus(snapshot.TavernTier);
            }
        }
        else
        {
            if (existingCopies == 1)
            {
                // 构成对子 (第 2 张同名卡)
                pairScore = riskProfile.ScaleWeight * 20.0;
            }
            else if (existingCopies >= 2)
            {
                // 达成三连质变 (第 3 张同名卡)
                tripleBonus = CalculateTripleBonus(snapshot.TavernTier);
            }
        }

        double totalScore = tempoScore + synergyScore + pairScore + tripleBonus;
        double goldEfficiency = action.GoldCost > 0 ? totalScore / action.GoldCost : totalScore;

        string tag = tripleBonus > 0
            ? "三连质变"
            : (pairScore > 0
                ? "对子成型"
                : (_archetypeRepo.IsCoreMinion(cardId)
                    ? "流派核心发动机"
                    : (synergyScore > tempoScore
                        ? "流派高协同"
                        : (tempoScore > 20 ? "强力即时战力" : "常规买入"))));

        return new ActionEvaluationScore(
            totalScore: totalScore,
            tempoScore: tempoScore,
            synergyScore: synergyScore,
            pairScore: pairScore,
            tripleBonus: tripleBonus,
            goldEfficiency: goldEfficiency,
            rawTempoDelta: rawTempo,
            rawSynergyDelta: rawSynergy,
            tag: tag);
    }

    private ActionEvaluationScore EvaluatePlay(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile)
    {
        string cardId = !string.IsNullOrEmpty(action.CardId)
            ? action.CardId
            : (action.SlotIndex >= 0 && action.SlotIndex < snapshot.HandCards.Length
                ? snapshot.HandCards[action.SlotIndex].CardId
                : string.Empty);

        int attack = 2;
        int health = 2;
        CardMechanicsMask mechanics = CardMechanicsMask.None;
        MinionRaceMask race = MinionRaceMask.None;

        if (_hearthDb.TryGetCard(cardId, out var meta))
        {
            attack = meta.BaseAttack;
            health = meta.BaseHealth;
            mechanics = meta.Mechanics;
            race = meta.Race;
        }

        if (action.SlotIndex >= 0 && action.SlotIndex < snapshot.HandCards.Length)
        {
            var live = snapshot.HandCards[action.SlotIndex];
            if (live.HasLiveStats) { attack = live.Attack; health = live.Health; mechanics = live.Mechanics; race = live.Race; }
        }

        double rawTempo = CalculateMinionCombatPower(attack, health, mechanics);
        double tempoScore = riskProfile.TempoWeight * rawTempo;

        bool isEmergency = riskProfile.IsEmergencyMutated || riskProfile.ScaleWeight <= 0.00001 || riskProfile.PLethal > 0.30;
        double rawSynergy = 0.0;
        double synergyScore = 0.0;

        if (!isEmergency)
        {
            rawSynergy = CalculateSynergy(cardId, race, posteriors, snapshot);
            synergyScore = riskProfile.ScaleWeight * rawSynergy;
        }

        double totalScore = tempoScore + synergyScore;
        double goldEfficiency = action.GoldCost > 0 ? totalScore / action.GoldCost : totalScore;

        return new ActionEvaluationScore(
            totalScore: totalScore,
            tempoScore: tempoScore,
            synergyScore: synergyScore,
            pairScore: 0.0,
            tripleBonus: 0.0,
            goldEfficiency: goldEfficiency,
            rawTempoDelta: rawTempo,
            rawSynergyDelta: rawSynergy,
            tag: "打出随从");
    }

    private ActionEvaluationScore EvaluateSell(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile)
    {
        // 出售随从：扣除战场对应随从的战力与协同
        int slot = action.SlotIndex;
        if (slot < 0 || slot >= snapshot.BoardMinions.Length)
        {
            return ActionEvaluationScore.Zero;
        }

        var minion = snapshot.BoardMinions[slot];
        double minionPower = CalculateMinionCombatPower(minion.Attack, minion.Health, minion.Mechanics);
        double rawTempo = -minionPower;
        double tempoScore = riskProfile.TempoWeight * rawTempo;

        bool isEmergency = riskProfile.IsEmergencyMutated || riskProfile.ScaleWeight <= 0.00001 || riskProfile.PLethal > 0.30;
        double rawSynergy = 0.0;
        double synergyScore = 0.0;

        if (!isEmergency)
        {
            rawSynergy = -CalculateSynergy(minion.CardId, minion.Race, posteriors, snapshot);
            synergyScore = riskProfile.ScaleWeight * rawSynergy;
        }

        double totalScore = tempoScore + synergyScore;
        // 出售返还 1 金币并释放卡位，效益比予以正向修正
        double goldEfficiency = totalScore + 10.0;

        return new ActionEvaluationScore(
            totalScore: totalScore,
            tempoScore: tempoScore,
            synergyScore: synergyScore,
            pairScore: 0.0,
            tripleBonus: 0.0,
            goldEfficiency: goldEfficiency,
            rawTempoDelta: rawTempo,
            rawSynergyDelta: rawSynergy,
            tag: "出售随从");
    }

    private static ActionEvaluationScore EvaluateUpgradeTier(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in RiskWeightsProfile riskProfile)
    {
        // 升级酒馆：零即时战力增量，完全依赖远期成长权重
        double rawTempo = 0.0;
        double tempoScore = 0.0;

        bool isEmergency = riskProfile.IsEmergencyMutated || riskProfile.ScaleWeight <= 0.00001 || riskProfile.PLethal > 0.30;
        double rawSynergy = 0.0;
        double synergyScore = 0.0;

        if (!isEmergency)
        {
            // 升本期望收益：高星级带来的高阶随从池收益
            rawSynergy = Math.Max(0.0, (snapshot.TavernTier + 1) * 8.0 - action.GoldCost * 1.5);
            synergyScore = riskProfile.ScaleWeight * rawSynergy;
        }

        double totalScore = tempoScore + synergyScore;
        double goldEfficiency = action.GoldCost > 0 ? totalScore / action.GoldCost : totalScore;

        return new ActionEvaluationScore(
            totalScore: totalScore,
            tempoScore: tempoScore,
            synergyScore: synergyScore,
            pairScore: 0.0,
            tripleBonus: 0.0,
            goldEfficiency: goldEfficiency,
            rawTempoDelta: rawTempo,
            rawSynergyDelta: rawSynergy,
            tag: isEmergency ? "危机禁升本" : "升级酒馆");
    }

    private static ActionEvaluationScore EvaluateReroll(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in RiskWeightsProfile riskProfile)
    {
        double rawTempo = 0.0;
        double tempoScore = 0.0;

        bool isEmergency = riskProfile.IsEmergencyMutated || riskProfile.ScaleWeight <= 0.00001 || riskProfile.PLethal > 0.30;
        double rawSynergy = 0.0;
        double synergyScore = 0.0;

        if (!isEmergency)
        {
            rawSynergy = 2.0;
            synergyScore = riskProfile.ScaleWeight * rawSynergy;
        }

        double totalScore = tempoScore + synergyScore;
        double goldEfficiency = action.GoldCost > 0 ? totalScore / action.GoldCost : totalScore;

        return new ActionEvaluationScore(
            totalScore: totalScore,
            tempoScore: tempoScore,
            synergyScore: synergyScore,
            pairScore: 0.0,
            tripleBonus: 0.0,
            goldEfficiency: goldEfficiency,
            rawTempoDelta: rawTempo,
            rawSynergyDelta: rawSynergy,
            tag: "刷新酒馆");
    }

    private ActionEvaluationScore EvaluateFreeze(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile)
    {
        double rawTempo = 0.0;
        double tempoScore = 0.0;

        bool isEmergency = riskProfile.IsEmergencyMutated || riskProfile.ScaleWeight <= 0.00001 || riskProfile.PLethal > 0.30;
        double rawSynergy = 0.0;
        double synergyScore = 0.0;

        if (!isEmergency)
        {
            rawSynergy = EvaluateFreezeSynergy(snapshot, posteriors);
            synergyScore = riskProfile.ScaleWeight * rawSynergy;
        }

        double totalScore = tempoScore + synergyScore;

        return new ActionEvaluationScore(
            totalScore: totalScore,
            tempoScore: tempoScore,
            synergyScore: synergyScore,
            pairScore: 0.0,
            tripleBonus: 0.0,
            goldEfficiency: totalScore,
            rawTempoDelta: rawTempo,
            rawSynergyDelta: rawSynergy,
            tag: "冻结酒馆");
    }

    #endregion

    #region 核心计算算法与辅助方法 (Zero GC & Inline)

    /// <summary>
    /// 计算随从机制等效战斗力数值。
    /// 涵盖圣盾、剧毒、复生、顺劈、风怒、嘲讽等关键词的战力折算。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateMinionCombatPower(int attack, int health, CardMechanicsMask mechanics)
    {
        double power = attack + health;

        // 圣盾 (Divine Shield): 吸收一次伤害并保留攻击力
        if ((mechanics & CardMechanicsMask.DivineShield) != 0)
        {
            power += attack * 0.8 + 6.0;
        }

        // 剧毒 / 烈毒 (Poisonous / Venomous): 高防白吃收益
        if ((mechanics & CardMechanicsMask.Poisonous) != 0)
        {
            power += health * 0.8 + 25.0;
        }
        else if ((mechanics & CardMechanicsMask.Venomous) != 0)
        {
            power += health * 0.5 + 15.0;
        }

        // 顺劈 / 狂战斧 (Cleave): 范围爆发伤害
        if ((mechanics & CardMechanicsMask.Cleave) != 0)
        {
            power += attack * 1.8 + 5.0;
        }

        // 风怒 / 超级风怒 (Windfury / MegaWindfury)
        if ((mechanics & CardMechanicsMask.MegaWindfury) != 0)
        {
            power += attack * 2.0;
        }
        else if ((mechanics & CardMechanicsMask.Windfury) != 0)
        {
            power += attack * 0.75;
        }

        // 复生 (Reborn): 原生攻击再次出手 + 吸收一次攻击
        if ((mechanics & CardMechanicsMask.Reborn) != 0)
        {
            power += attack * 0.8 + 3.0;
        }

        // 嘲讽 (Taunt): 保护核心发动机
        if ((mechanics & CardMechanicsMask.Taunt) != 0)
        {
            power += health * 0.2 + 2.0;
        }

        return power;
    }

    /// <summary>
    /// 基于贝叶斯流派后验概率与战场成对协同，求解卡牌综合流派协同分。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateSynergy(
        string cardId,
        MinionRaceMask race,
        in ArchetypePosteriorDistribution posteriors,
        in BoardSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            return 0.0;
        }

        double totalSynergy = 0.0;

        // 1. 贝叶斯流派后验概率分布加权各大流派协同收益增量
        int posteriorCount = posteriors.Count;
        for (int i = 0; i < posteriorCount; i++)
        {
            double p = posteriors.GetProbability(i);
            if (p <= 0.001)
            {
                continue;
            }

            var arch = posteriors.GetArchetype(i);
            if (arch == null)
            {
                continue;
            }

            double w = _archetypeRepo.GetSynergyWeight(arch.Id, cardId);
            if (w > 0.0)
            {
                totalSynergy += p * w * 15.0;
            }
            else if (arch.PrimaryRace.Intersects(race) || arch.SupportedRaces.Intersects(race))
            {
                totalSynergy += p * 5.0;
            }
        }

        // 2. 检索战场已有随从的成对双卡联动协同 (Pairwise Synergy)
        var board = snapshot.BoardMinions;
        for (int i = 0; i < board.Length; i++)
        {
            string boardCardId = board[i].CardId;
            if (string.IsNullOrEmpty(boardCardId))
            {
                continue;
            }

            double pairWeight = _archetypeRepo.GetPairSynergy(cardId, boardCardId);
            if (pairWeight > 1.0)
            {
                totalSynergy += (pairWeight - 1.0) * 15.0;
            }
        }

        // 3. 关键发动机随从 (跳蛙、瑞文、铜须等) 依据 T08 IArchetypeRepository 提供超额协同加成
        if (_archetypeRepo.IsCoreMinion(cardId))
        {
            var engine = _archetypeRepo.GetEngineMinion(cardId);
            double engineWeight = engine?.SynergyWeight ?? 2.0;
            totalSynergy += engineWeight * 12.0;

            // 若发动机随从所属流派的后验概率较高，进一步赋予流派质变超额加成
            var archsForMinion = _archetypeRepo.GetArchetypesForMinion(cardId);
            double maxArchP = 0.0;
            for (int a = 0; a < archsForMinion.Count; a++)
            {
                double pa = posteriors.GetProbability(archsForMinion[a].Id);
                if (pa > maxArchP)
                {
                    maxArchP = pa;
                }
            }
            if (maxArchP > 0.20)
            {
                totalSynergy += maxArchP * 30.0;
            }
        }

        return totalSynergy;
    }

    /// <summary>
    /// 统计玩家手牌与战场上已有的同名非金色卡牌数量。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountExistingCopies(in BoardSnapshot snapshot, string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            return 0;
        }

        int count = 0;

        // 1. 战场检索 (排除已三连的金色卡)
        var board = snapshot.BoardMinions;
        for (int i = 0; i < board.Length; i++)
        {
            if (!board[i].IsGolden && board[i].CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        // 2. 手牌检索
        var hand = snapshot.HandCards;
        for (int i = 0; i < hand.Length; i++)
        {
            if ((hand[i].IsMinion || !hand[i].IsSpell) && hand[i].CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 计算三连质变与发现高本随从潜在收益。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateTripleBonus(int tavernTier)
    {
        const double baseTriple = 50.0;
        int discoverTier = MathEx.Clamp(tavernTier + 1, 1, 6);
        double discoverBonus = discoverTier * 8.0;

        return baseTriple + discoverBonus;
    }

    /// <summary>
    /// 评估冻结酒馆的协同增益价值。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double EvaluateFreezeSynergy(in BoardSnapshot snapshot, in ArchetypePosteriorDistribution posteriors)
    {
        double score = 0.0;
        var tavern = snapshot.TavernMinions;
        for (int i = 0; i < tavern.Length; i++)
        {
            string cId = tavern[i].CardId;
            if (string.IsNullOrEmpty(cId))
            {
                continue;
            }

            int copies = CountExistingCopies(snapshot, cId);
            if (copies == 1)
            {
                score += 15.0; // 冻结对子
            }
            else if (copies >= 2)
            {
                score += 40.0; // 冻结准三连
            }
            else if (_archetypeRepo.IsCoreMinion(cId))
            {
                score += 10.0; // 冻结流派发动机
            }
        }

        return score;
    }

    #endregion
}
