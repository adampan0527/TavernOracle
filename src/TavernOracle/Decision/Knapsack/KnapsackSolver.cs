namespace TavernOracle.Decision.Knapsack;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Decision.Evaluation;

/// <summary>
/// 基于分支定界 (Branch and Bound) 与贪心上下界剪枝的高性能背包求解器。
/// 在当前可用铸币 (含变卖随从金币返还 Refund) 与 7 个战场卡槽、10 个手牌卡槽双重硬性约束下，
/// 对动作决策树执行高效全局剪枝搜索，输出全局预期收益最高的最优操作序列。
/// 包含 4.5ms 硬超时与局部最优贪心保底机制，全热路径 0 GC 堆内存分配。
/// </summary>
public sealed class KnapsackSolver : IKnapsackSolver
{
    private static readonly Lazy<KnapsackSolver> s_lazyInstance = new(() => new KnapsackSolver());

    /// <summary>
    /// 全局共享默认单例实例 (线程安全，零启动开销)。
    /// </summary>
    public static KnapsackSolver Instance => s_lazyInstance.Value;

    /// <summary>
    /// 全局共享单例别名。
    /// </summary>
    public static KnapsackSolver Shared => Instance;

    private readonly IActionValueEvaluator _evaluator;
    private readonly IRiskControlValve _riskValve;
    private readonly IBayesianArchetypeInference _bayesianInference;

    /// <summary>
    /// 构造分支定界背包 DP 求解器。支持依赖注入或默认组合。
    /// </summary>
    public KnapsackSolver(
        IActionValueEvaluator? evaluator = null,
        IRiskControlValve? riskValve = null,
        IBayesianArchetypeInference? bayesianInference = null)
    {
        _evaluator = evaluator ?? ActionValueEvaluator.Instance;
        _riskValve = riskValve ?? RiskControlValve.Instance;
        _bayesianInference = bayesianInference ?? new BayesianArchetypeInference();
    }

#if NET8_0_OR_GREATER
    [InlineArray(BoardSnapshot.MaxBoardMinions)]
    private struct InlineBoardIdBuffer
    {
        private string _element0;
    }

    [InlineArray(BoardSnapshot.MaxHandCards)]
    private struct InlineHandIdBuffer
    {
        private string _element0;
    }

    [InlineArray(BoardSnapshot.MaxTavernMinions)]
    private struct InlineTavernBuffer
    {
        private PrecalculatedTavern _element0;
    }

    [InlineArray(BoardSnapshot.MaxBoardMinions)]
    private struct InlineBoardBuffer
    {
        private PrecalculatedBoard _element0;
    }

    [InlineArray(BoardSnapshot.MaxHandCards)]
    private struct InlineHandBuffer
    {
        private PrecalculatedHand _element0;
    }
#else
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

    private struct InlineTavernBuffer
    {
        private PrecalculatedTavern[]? _items;
        public Span<PrecalculatedTavern> AsSpan() => new Span<PrecalculatedTavern>(_items ??= new PrecalculatedTavern[BoardSnapshot.MaxTavernMinions]);
        public static implicit operator Span<PrecalculatedTavern>(InlineTavernBuffer b) => b.AsSpan();
    }

    private struct InlineBoardBuffer
    {
        private PrecalculatedBoard[]? _items;
        public Span<PrecalculatedBoard> AsSpan() => new Span<PrecalculatedBoard>(_items ??= new PrecalculatedBoard[BoardSnapshot.MaxBoardMinions]);
        public static implicit operator Span<PrecalculatedBoard>(InlineBoardBuffer b) => b.AsSpan();
    }

    private struct InlineHandBuffer
    {
        private PrecalculatedHand[]? _items;
        public Span<PrecalculatedHand> AsSpan() => new Span<PrecalculatedHand>(_items ??= new PrecalculatedHand[BoardSnapshot.MaxHandCards]);
        public static implicit operator Span<PrecalculatedHand>(InlineHandBuffer b) => b.AsSpan();
    }
#endif

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionSequenceResult Solve(in BoardSnapshot snapshot)
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

        return Solve(snapshot, posteriors, riskProfile, timeoutMs: 4.5, maxNodeBudget: 10_000);
    }

    private readonly record struct PrecalculatedTavern(
        CandidateAction Action,
        int Cost,
        double Score,
        double Efficiency,
        string CardId,
        int EntityId,
        bool WorthFreezing);

    private readonly record struct PrecalculatedBoard(
        CandidateAction Action,
        double Score,
        string CardId,
        int EntityId,
        int SlotIndex);

    private readonly record struct PrecalculatedHand(
        CandidateAction Action,
        int Cost,
        double Score,
        string CardId,
        int EntityId,
        int SlotIndex);

    private ref struct SearchContext
    {
        public long StartTimestamp;
        public double TimeoutMs;
        public int MaxNodeBudget;
        public int NodeCount;
        public bool IsTimedOut;

        public double BestScore;
        public int BestCount;
        public InlineSequenceStorage BestSequence;

        public ReadOnlySpan<PrecalculatedTavern> TavernItems;
        public ReadOnlySpan<PrecalculatedBoard> BoardItems;
        public ReadOnlySpan<PrecalculatedHand> HandItems;

        public CandidateAction UpgradeAction;
        public double UpgradeScore;
        public int UpgradeCost;
        public bool CanUpgrade;

        public CandidateAction FreezeAction;
        public double FreezeScore;
        public bool CanFreeze;

        public CandidateAction RerollAction;
        public double RerollScore;
        public bool CanReroll;

        public int InitialBoardCount;
        public int InitialHandCount;
        public int InitialGold;
        public bool IsEmergency;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionSequenceResult Solve(
        in BoardSnapshot snapshot,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile,
        double timeoutMs = 4.5,
        int maxNodeBudget = 10_000)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        bool isEmergency = riskProfile.IsEmergencyMutated ||
                           riskProfile.ScaleWeight <= 0.00001 ||
                           riskProfile.PLethal > 0.30;

        // 1. 预计算酒馆随从评分并排序
        int tavernCount = Math.Min(snapshot.TavernMinions.Length, BoardSnapshot.MaxTavernMinions);
        InlineTavernBuffer tavernStorage = default;
        Span<PrecalculatedTavern> tavernBuffer = (Span<PrecalculatedTavern>)tavernStorage;
        int positiveTavernCount = 0;
        for (int i = 0; i < tavernCount; i++)
        {
            var minion = snapshot.TavernMinions[i];
            if (minion.IsTavernSpell) continue;
            var buyAction = CandidateAction.CreateBuy(minion.EntityId, i, minion.Cost, minion.CardId);
            var score = _evaluator.Evaluate(snapshot, buyAction, posteriors, riskProfile);
            if (score.TotalScore > 0.0)
            {
                double eff = minion.Cost > 0 ? score.TotalScore / minion.Cost : score.TotalScore;
                tavernBuffer[positiveTavernCount++] = new PrecalculatedTavern(
                    buyAction,
                    minion.Cost,
                    score.TotalScore,
                    eff,
                    minion.CardId,
                    minion.EntityId,
                    HasOwnedCopy(snapshot, minion.CardId) || score.RawSynergyDelta >= 10);
            }
        }
        Span<PrecalculatedTavern> tavernItems = tavernBuffer.Slice(0, positiveTavernCount);
        SortTavernByEfficiencyDescending(tavernItems);

        // 2. 预计算我方在场随从出售损失评分并排序 (最弱/损失最小优先)
        int boardCount = Math.Min(snapshot.BoardMinions.Length, BoardSnapshot.MaxBoardMinions);
        InlineBoardBuffer boardStorage = default;
        Span<PrecalculatedBoard> boardBuffer = ((Span<PrecalculatedBoard>)boardStorage).Slice(0, boardCount);
        for (int i = 0; i < boardCount; i++)
        {
            var minion = snapshot.BoardMinions[i];
            var sellAction = CandidateAction.CreateSell(minion.EntityId, i, minion.CardId);
            var score = _evaluator.Evaluate(snapshot, sellAction, posteriors, riskProfile);
            boardBuffer[i] = new PrecalculatedBoard(
                sellAction,
                score.TotalScore,
                minion.CardId,
                minion.EntityId,
                i);
        }
        SortBoardByScoreDescending(boardBuffer); // Score 越接近 0 (越大) 损失越小，优先出售

        // 3. 预计算我方手牌打出评分并排序 (收益最高优先)
        int handCount = Math.Min(snapshot.HandCards.Length, BoardSnapshot.MaxHandCards);
        InlineHandBuffer handStorage = default;
        Span<PrecalculatedHand> handBuffer = (Span<PrecalculatedHand>)handStorage;
        int positiveHandCount = 0;
        for (int i = 0; i < handCount; i++)
        {
            var card = snapshot.HandCards[i];
            if (!card.IsMinion || HandActionPlanner.ShouldHold(snapshot, card)) continue;
            var playAction = HandActionPlanner.Plan(snapshot, card, i);
            var score = _evaluator.Evaluate(snapshot, playAction, posteriors, riskProfile);
            if (score.TotalScore > 0.0)
            {
                handBuffer[positiveHandCount++] = new PrecalculatedHand(
                    playAction,
                    card.Cost,
                    score.TotalScore,
                    card.CardId,
                    card.EntityId,
                    i);
            }
        }
        Span<PrecalculatedHand> handItems = handBuffer.Slice(0, positiveHandCount);
        SortHandByScoreDescending(handItems);

        // 4. 预计算升本动作
        CandidateAction upgradeAction = default;
        double upgradeScore = 0.0;
        int upgradeCost = snapshot.UpgradeCost;
        bool canUpgrade = false;
        if (!isEmergency && snapshot.TavernTier < 7 && upgradeCost > 0)
        {
            upgradeAction = CandidateAction.CreateUpgradeTier(upgradeCost);
            var score = _evaluator.Evaluate(snapshot, upgradeAction, posteriors, riskProfile);
            upgradeScore = score.TotalScore;
            canUpgrade = upgradeScore > 0.0;
        }

        // 5. 预计算冻结动作
        CandidateAction freezeAction = default;
        double freezeScore = 0.0;
        bool canFreeze = false;
        if (snapshot.TavernMinions.Length > 0 && !snapshot.IsFrozen)
        {
            freezeAction = CandidateAction.CreateFreeze();
            var score = _evaluator.Evaluate(snapshot, freezeAction, posteriors, riskProfile);
            freezeScore = score.TotalScore;
            canFreeze = freezeScore > 0.0;
        }

        // 6. 预计算刷新动作
        CandidateAction rerollAction = CandidateAction.CreateReroll(1);
        var rerollEval = _evaluator.Evaluate(snapshot, rerollAction, posteriors, riskProfile);
        double rerollScore = rerollEval.TotalScore;
        bool canReroll = rerollScore > 0.0 && !snapshot.IsFrozen;
        foreach (var item in snapshot.TavernMinions)
            if (item.IsTavernSpell && item.Cost <= snapshot.Gold.Available) canReroll = false;

        // 初始化搜索上下文
        SearchContext context = new()
        {
            StartTimestamp = startTimestamp,
            TimeoutMs = timeoutMs,
            MaxNodeBudget = maxNodeBudget,
            NodeCount = 0,
            IsTimedOut = false,
            BestScore = 0.0,
            BestCount = 0,
            BestSequence = default,
            TavernItems = tavernItems,
            BoardItems = boardBuffer,
            HandItems = handItems,
            UpgradeAction = upgradeAction,
            UpgradeScore = upgradeScore,
            UpgradeCost = upgradeCost,
            CanUpgrade = canUpgrade,
            FreezeAction = freezeAction,
            FreezeScore = freezeScore,
            CanFreeze = canFreeze,
            RerollAction = rerollAction,
            RerollScore = rerollScore,
            CanReroll = canReroll,
            InitialBoardCount = boardCount,
            InitialHandCount = handCount,
            InitialGold = snapshot.Gold.Available,
            IsEmergency = isEmergency
        };

        // 7. 贪心快速求解 (提供高质量下界并作为超时保底)
        ExecuteGreedyBaseline(ref context);

        // 8. 执行分支定界剪枝搜索
        ExecuteBranchAndBound(ref context);

        double elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        ReadOnlySpan<CandidateAction> bestSpan = ((Span<CandidateAction>)context.BestSequence).Slice(0, context.BestCount);

        return new ActionSequenceResult(
            bestSpan,
            context.BestScore,
            elapsedMs,
            context.NodeCount,
            context.IsTimedOut);
    }

    #region 排序辅助函数 (纯栈操作，零堆分配)

    private static void SortTavernByEfficiencyDescending(Span<PrecalculatedTavern> items)
    {
        for (int i = 1; i < items.Length; i++)
        {
            var key = items[i];
            int j = i - 1;
            while (j >= 0 && items[j].Efficiency < key.Efficiency)
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = key;
        }
    }

    private static void SortBoardByScoreDescending(Span<PrecalculatedBoard> items)
    {
        for (int i = 1; i < items.Length; i++)
        {
            var key = items[i];
            int j = i - 1;
            while (j >= 0 && items[j].Score < key.Score)
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = key;
        }
    }

    private static void SortHandByScoreDescending(Span<PrecalculatedHand> items)
    {
        for (int i = 1; i < items.Length; i++)
        {
            var key = items[i];
            int j = i - 1;
            while (j >= 0 && items[j].Score < key.Score)
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = key;
        }
    }

    #endregion

    #region 贪心启发式下界求解 (Greedy Baseline)

    private static void ExecuteGreedyBaseline(ref SearchContext context)
    {
        InlineSequenceStorage sequence = default;
        int stepCount = 0;
        double totalScore = 0.0;
        int currentGold = context.InitialGold;
        int currentBoard = context.InitialBoardCount;
        int currentHand = context.InitialHandCount;

        // 若处于危机模式，严禁贪婪升本；否则若升本收益极佳且金币足够，优先升本
        if (context.CanUpgrade && !context.IsEmergency && currentGold >= context.UpgradeCost)
        {
            if (currentGold >= context.UpgradeCost + 3 || context.UpgradeScore > 20.0)
            {
                sequence[stepCount++] = context.UpgradeAction;
                currentGold -= context.UpgradeCost;
                totalScore += context.UpgradeScore;
            }
        }

        // 贪心打出手牌以释放手牌卡位并增强场面 (在卡位允许的前提下)
        for (int i = 0; i < context.HandItems.Length; i++)
        {
            var hand = context.HandItems[i];
            if ((currentBoard < BoardSnapshot.MaxBoardMinions || hand.Action.Type == ActionType.Magnetize) && currentGold >= hand.Cost)
            {
                sequence[stepCount++] = hand.Action with { TargetPosition = Math.Min(hand.Action.TargetPosition, currentBoard) };
                if (hand.Action.Type != ActionType.Magnetize) currentBoard++;
                currentHand--;
                currentGold -= hand.Cost;
                totalScore += hand.Score;
            }
        }

        // 贪心购买酒馆随从
        int soldIndex = 0;
        for (int i = 0; i < context.TavernItems.Length; i++)
        {
            var tavern = context.TavernItems[i];
            if (tavern.Score <= 0.0)
            {
                continue;
            }

            // 若金币不足但可以通过卖出随从获取金币，且卖出后的净收益为正
            while (currentGold < tavern.Cost && soldIndex < context.BoardItems.Length)
            {
                var boardMinion = context.BoardItems[soldIndex];
                if (tavern.Score + boardMinion.Score > 0.0 && !IsMagneticTarget(sequence, stepCount, boardMinion.EntityId))
                {
                    sequence[stepCount++] = boardMinion.Action;
                    currentGold += 1;
                    currentBoard--;
                    totalScore += boardMinion.Score;
                    soldIndex++;
                }
                else
                {
                    break;
                }
            }

            // 购买该随从
            if (currentGold >= tavern.Cost && currentHand < BoardSnapshot.MaxHandCards)
            {
                sequence[stepCount++] = tavern.Action;
                currentGold -= tavern.Cost;
                currentHand++;
                totalScore += tavern.Score;

                // 若战场有卡位，立即打出上场
                if (currentBoard < BoardSnapshot.MaxBoardMinions)
                {
                    sequence[stepCount++] = CandidateAction.CreatePlay(
                        tavern.EntityId,
                        slotIndex: -1,
                        targetPosition: currentBoard,
                        cost: 0,
                        tavern.CardId);
                    currentBoard++;
                    currentHand--;
                }
                // 若战场满员 (7)，但场上有弱小随从值得替换
                else if (soldIndex < context.BoardItems.Length && context.BoardItems[soldIndex].Score + 10.0 > 0.0 && !IsMagneticTarget(sequence, stepCount, context.BoardItems[soldIndex].EntityId))
                {
                    var boardMinion = context.BoardItems[soldIndex];
                    sequence[stepCount++] = boardMinion.Action;
                    currentGold += 1;
                    currentBoard--;
                    totalScore += boardMinion.Score;
                    soldIndex++;

                    sequence[stepCount++] = CandidateAction.CreatePlay(
                        tavern.EntityId,
                        slotIndex: -1,
                        targetPosition: currentBoard,
                        cost: 0,
                        tavern.CardId);
                    currentBoard++;
                    currentHand--;
                }
            }
        }

        // 若酒馆有高价值剩余随从且未冻结，进行冻结
        if (context.CanFreeze && HasUnbought(context, sequence, stepCount))
        {
            sequence[stepCount++] = context.FreezeAction;
            totalScore += context.FreezeScore;
        }

        else if (context.CanReroll && currentGold >= context.RerollAction.GoldCost && stepCount < ActionSequenceResult.MaxCapacity)
        {
            sequence[stepCount++] = context.RerollAction;
            totalScore += context.RerollScore;
        }

        // 保存贪心结果
        if (totalScore > context.BestScore)
        {
            context.BestScore = totalScore;
            context.BestCount = stepCount;
            context.BestSequence = sequence;
        }
    }

    #endregion

    #region 分支定界搜索 (Branch and Bound)

    private static void ExecuteBranchAndBound(ref SearchContext context)
    {
        // 限制卖出随从的最大数量 (最多 3 个，避免无意义削弱战力)
        int maxSells = Math.Min(context.BoardItems.Length, 3);

        for (int sellCount = 0; sellCount <= maxSells; sellCount++)
        {
            if (context.IsTimedOut)
            {
                return;
            }

            // 评估卖出 sellCount 个随从的状态
            double sellScoreDelta = 0.0;
            for (int s = 0; s < sellCount; s++)
            {
                sellScoreDelta += context.BoardItems[s].Score;
            }

            int goldAfterSell = context.InitialGold + sellCount;
            int boardAfterSell = context.InitialBoardCount - sellCount;
            int handAfterSell = context.InitialHandCount;

            // 决策手牌打出数量: 可打出的手牌数量上限为可用卡位
            int maxHandPlays = context.HandItems.Length;

            for (int handPlayCount = 0; handPlayCount <= maxHandPlays; handPlayCount++)
            {
                if (context.IsTimedOut)
                {
                    return;
                }

                double handScoreDelta = 0.0;
                int handCostDelta = 0;
                int boardDelta = 0;
                bool targetSold = false;
                for (int h = 0; h < handPlayCount; h++)
                {
                    handScoreDelta += context.HandItems[h].Score;
                    handCostDelta += context.HandItems[h].Cost;
                    if (context.HandItems[h].Action.Type != ActionType.Magnetize) boardDelta++;
                    else for (int s = 0; s < sellCount; s++)
                        targetSold |= context.BoardItems[s].EntityId == context.HandItems[h].Action.TargetEntityId;
                }

                if (goldAfterSell < handCostDelta || boardAfterSell + boardDelta > BoardSnapshot.MaxBoardMinions || targetSold)
                {
                    continue; // 资金不足打出手牌
                }

                int goldAfterHand = goldAfterSell - handCostDelta;
                int boardAfterHand = boardAfterSell + boardDelta;
                int handAfterHand = handAfterSell - handPlayCount;
                double scoreAfterHand = sellScoreDelta + handScoreDelta;

                // 决策升本 (两种情况: 升本 / 不升本)
                for (int upgradeChoice = 0; upgradeChoice <= (context.CanUpgrade ? 1 : 0); upgradeChoice++)
                {
                    if (context.IsTimedOut)
                    {
                        return;
                    }

                    bool willUpgrade = upgradeChoice == 1;
                    if (willUpgrade && goldAfterHand < context.UpgradeCost)
                    {
                        continue;
                    }

                    int goldAfterUpgrade = willUpgrade ? goldAfterHand - context.UpgradeCost : goldAfterHand;
                    double scoreAfterUpgrade = willUpgrade ? scoreAfterHand + context.UpgradeScore : scoreAfterHand;

                    // 构造前置动作前缀
                    InlineSequenceStorage prefixSequence = default;
                    int prefixCount = 0;

                    for (int s = 0; s < sellCount; s++)
                    {
                        prefixSequence[prefixCount++] = context.BoardItems[s].Action;
                    }

                    for (int h = 0; h < handPlayCount; h++)
                    {
                        var planned = context.HandItems[h].Action;
                        prefixSequence[prefixCount++] = planned with { TargetPosition = Math.Min(planned.TargetPosition, boardAfterHand - 1) };
                    }

                    if (willUpgrade)
                    {
                        prefixSequence[prefixCount++] = context.UpgradeAction;
                    }

                    // 启动酒馆买入的分支定界搜索
                    SearchTavernBuys(
                        tavernIndex: 0,
                        currentGold: goldAfterUpgrade,
                        currentBoard: boardAfterHand,
                        currentHand: handAfterHand,
                        currentScore: scoreAfterUpgrade,
                        currentPrefix: ref prefixSequence,
                        prefixCount: prefixCount,
                        ref context);
                }
            }
        }
    }

    private static void SearchTavernBuys(
        int tavernIndex,
        int currentGold,
        int currentBoard,
        int currentHand,
        double currentScore,
        ref InlineSequenceStorage currentPrefix,
        int prefixCount,
        ref SearchContext context)
    {
        context.NodeCount++;

        // 每 64 个节点检查一次超时截断
        if ((context.NodeCount & 63) == 0)
        {
            double elapsed = (Stopwatch.GetTimestamp() - context.StartTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsed >= context.TimeoutMs || context.NodeCount >= context.MaxNodeBudget)
            {
                context.IsTimedOut = true;
                return;
            }
        }

        // 上界剪枝：计算剩余酒馆随从的最大可能加分 (分数背包上界)
        double remainingUpperBound = CalculateUpperBound(tavernIndex, currentGold, ref context);
        if (currentScore + remainingUpperBound <= context.BestScore)
        {
            return; // 剪枝！即使全拿剩余物品也无法突破已知最优解
        }

        // 已经探索完所有酒馆随从，或已无资金
        if (tavernIndex >= context.TavernItems.Length || currentGold < 3)
        {
            // 考虑冻结加分
            double finalScore = currentScore;
            int finalCount = prefixCount;
            InlineSequenceStorage finalSeq = currentPrefix;

            if (context.CanFreeze)
            {
                bool hasValuableUnbought = HasUnbought(context, currentPrefix, prefixCount);

                if (hasValuableUnbought && finalCount < ActionSequenceResult.MaxCapacity)
                {
                    finalSeq[finalCount++] = context.FreezeAction;
                    finalScore += context.FreezeScore;
                }
            }

            if ((finalCount == 0 || finalSeq[finalCount - 1].Type != ActionType.Freeze) &&
                context.CanReroll && currentGold >= context.RerollAction.GoldCost && finalCount < ActionSequenceResult.MaxCapacity)
            {
                finalSeq[finalCount++] = context.RerollAction;
                finalScore += context.RerollScore;
            }

            if (finalScore > context.BestScore)
            {
                context.BestScore = finalScore;
                context.BestCount = finalCount;
                context.BestSequence = finalSeq;
            }
            return;
        }

        var candidate = context.TavernItems[tavernIndex];

        // 分支 1: 买入当前随从 (若金币足够且手牌未满)
        if (currentGold >= candidate.Cost && currentHand < BoardSnapshot.MaxHandCards && prefixCount + 2 <= ActionSequenceResult.MaxCapacity)
        {
            InlineSequenceStorage buyBranchSeq = currentPrefix;
            int buyBranchCount = prefixCount;

            buyBranchSeq[buyBranchCount++] = candidate.Action;
            int nextHand = currentHand + 1;
            int nextBoard = currentBoard;

            // 若战场有空位，打出该随从
            if (nextBoard < BoardSnapshot.MaxBoardMinions)
            {
                buyBranchSeq[buyBranchCount++] = CandidateAction.CreatePlay(
                    candidate.EntityId,
                    slotIndex: -1,
                    targetPosition: currentBoard,
                    cost: 0,
                    candidate.CardId);
                nextBoard++;
                nextHand--;
            }

            SearchTavernBuys(
                tavernIndex: tavernIndex + 1,
                currentGold: currentGold - candidate.Cost,
                currentBoard: nextBoard,
                currentHand: nextHand,
                currentScore: currentScore + candidate.Score,
                currentPrefix: ref buyBranchSeq,
                prefixCount: buyBranchCount,
                ref context);

            if (context.IsTimedOut)
            {
                return;
            }
        }

        // 分支 2: 不买当前随从，跳过
        SearchTavernBuys(
            tavernIndex: tavernIndex + 1,
            currentGold: currentGold,
            currentBoard: currentBoard,
            currentHand: currentHand,
            currentScore: currentScore,
            currentPrefix: ref currentPrefix,
            prefixCount: prefixCount,
            ref context);
    }

    /// <summary>
    /// 计算剩余未探索随从的分数背包理论上界 (Fractional Knapsack Upper Bound)
    /// </summary>
    private static bool IsMagneticTarget(InlineSequenceStorage sequence, int count, int entityId)
    {
        for (int i = 0; i < count; i++)
            if (sequence[i].Type == ActionType.Magnetize && sequence[i].TargetEntityId == entityId) return true;
        return false;
    }

    private static bool HasUnbought(in SearchContext context, InlineSequenceStorage sequence, int count)
    {
        foreach (var item in context.TavernItems)
        {
            bool bought = false;
            for (int j = 0; j < count; j++)
                if (sequence[j].Type == ActionType.Buy && sequence[j].EntityId == item.EntityId) bought = true;
            if (!bought && item.WorthFreezing) return true;
        }
        return false;
    }

    private static bool HasOwnedCopy(in BoardSnapshot snapshot, string cardId)
    {
        foreach (var card in snapshot.BoardMinions) if (card.CardId == cardId) return true;
        foreach (var card in snapshot.HandCards) if (card.CardId == cardId) return true;
        return false;
    }

    private static double CalculateUpperBound(
        int startIndex,
        int availableGold,
        ref SearchContext context)
    {
        double bound = 0.0;
        int gold = availableGold;

        for (int i = startIndex; i < context.TavernItems.Length; i++)
        {
            if (gold <= 0)
            {
                break;
            }

            var item = context.TavernItems[i];
            if (item.Cost <= gold)
            {
                bound += item.Score;
                gold -= item.Cost;
            }
            else
            {
                bound += item.Efficiency * gold;
                gold = 0;
                break;
            }
        }

        if (context.CanFreeze)
        {
            bound += context.FreezeScore;
        }

        if (context.CanReroll && availableGold >= context.RerollAction.GoldCost) bound += context.RerollScore;
        return bound;
    }

    #endregion
}
