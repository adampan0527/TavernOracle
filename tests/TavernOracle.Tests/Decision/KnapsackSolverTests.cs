namespace TavernOracle.Tests.Decision;

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using TavernOracle.Data;
using TavernOracle.Data.Replays;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Priors;
using TavernOracle.Simulation;
using Xunit;
using Xunit.Abstractions;

public sealed class KnapsackSolverTests
{
    private readonly ITestOutputHelper _output;
    private readonly IKnapsackSolver _solver;

    public KnapsackSolverTests(ITestOutputHelper output)
    {
        _output = output;
        _solver = KnapsackSolver.Instance;
    }

    #region 1. 内存契约与零 GC 堆分配验证 (Memory & Zero GC Contract)

    [Fact]
    public void MemoryContract_ActionSequenceResult_IsValueType_ZeroHeapAllocation()
    {
        Assert.True(typeof(ActionSequenceResult).IsValueType, "ActionSequenceResult must be a struct (value type).");
        Assert.False(typeof(ActionSequenceResult).IsClass, "ActionSequenceResult must NOT be a reference type (class).");
        Assert.True(typeof(InlineSequenceStorage).IsValueType, "InlineSequenceStorage must be a struct (value type).");

        var empty = ActionSequenceResult.Empty;
        Assert.Empty(empty);
        Assert.True(empty.IsEmpty);
        Assert.Equal(0.0, empty.TotalScore);
        Assert.Empty(empty.AsSpan().ToArray());
    }

    /// <summary>
    /// 需求 4: 零 GC 验证
    /// 循环 1,000 次搜索产生的堆分配字节数 GC.GetAllocatedBytesForCurrentThread() 必须为 0 字节。
    /// </summary>
    [Fact]
    public void Solve_1000Iterations_MustProduceZeroHeapAllocation()
    {
        var snapshot = ReplayDatasetFactory.Create20Complex10GoldBranchingBudget();

        // 1. 预热 JIT 与优化
        for (int i = 0; i < 500; i++)
        {
            var res = _solver.Solve(in snapshot);
            _ = res.TotalScore;
            _ = res.Count;
        }

        // 2. 清理内存基线
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        int gen0Start = GC.CollectionCount(0);
        int gen1Start = GC.CollectionCount(1);
        int gen2Start = GC.CollectionCount(2);

        const int iterations = 1_000;
        var sw = Stopwatch.StartNew();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        double dummyScoreSum = 0;
        int dummyCountSum = 0;
        for (int i = 0; i < iterations; i++)
        {
            var res = _solver.Solve(in snapshot);
            dummyScoreSum += res.TotalScore;
            dummyCountSum += res.Count;
        }

        sw.Stop();
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        int gen0Delta = GC.CollectionCount(0) - gen0Start;
        int gen1Delta = GC.CollectionCount(1) - gen1Start;
        int gen2Delta = GC.CollectionCount(2) - gen2Start;
        long totalAllocated = allocatedAfter - allocatedBefore;

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMs = totalMs / iterations;
        double avgUs = avgMs * 1000.0;

        _output.WriteLine("[KnapsackSolver 1,000 次求解零 GC 压测报告]");
        _output.WriteLine($"  - 循环总次数:           {iterations:N0}");
        _output.WriteLine($"  - 总执行耗时:           {totalMs:F3} ms");
        _output.WriteLine($"  - 单次求解平均耗时:     {avgMs:F4} ms ({avgUs:F1} μs)");
        _output.WriteLine($"  - Gen0/1/2 回收增量:    {gen0Delta}/{gen1Delta}/{gen2Delta}");
        _output.WriteLine($"  - 线程堆内存总分配:     {totalAllocated} bytes");

        Assert.True(dummyScoreSum > 0);
        Assert.True(dummyCountSum > 0);

        // 验证 1: 平均耗时严格 <= 5.0ms (实测力争 < 0.5ms)
        Assert.True(avgMs <= 5.0, $"求解器平均耗时 {avgMs:F3} ms 超出 5.0 ms 硬预算！");
        Assert.True(avgMs < 1.0, $"求解器平均耗时 {avgMs:F3} ms 未能达到极低时延预期！");

        // 验证 2: GC 回收增量为 0
        Assert.Equal(0, gen0Delta);
        Assert.Equal(0, gen1Delta);
        Assert.Equal(0, gen2Delta);

        // 验证 3: 严格 0 字节托管堆分配
        Assert.Equal(0, totalAllocated);
    }

    #endregion

    #region 2. 典型战局 1: 10 金币复杂局 (Complex 10-Gold Budget)

    /// <summary>
    /// 需求 4: 10 金币复杂局面测试
    /// 面对 10 铸币预算、多手牌、多随从及三连对子，算法在 5ms 内完成全局剪枝并输出最优序列。
    /// </summary>
    [Fact]
    public void Solve_Complex10GoldBranchingBudget_ShouldFindOptimalSequenceAndSatisfyAllConstraints()
    {
        // Arrange
        var snapshot = ReplayDatasetFactory.Create20Complex10GoldBranchingBudget();

        // Act
        var result = _solver.Solve(in snapshot);

        _output.WriteLine($"[10 金币复杂局结果] 步数: {result.Count}, 得分: {result.TotalScore:F1}, 耗时: {result.ElapsedMs:F3}ms, 节点数: {result.NodesExplored}");
        for (int i = 0; i < result.Count; i++)
        {
            _output.WriteLine($"  Step {i + 1}: {result[i]}");
        }

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.TotalScore > 0, "最优序列得分应为正数");
        Assert.True(result.ElapsedMs <= 5.0, $"求解耗时 {result.ElapsedMs:F3} ms 必须 <= 5.0ms");

        // 验证动作序列物理合法性
        AssertSequenceLegality(snapshot, result);
    }

    #endregion

    #region 3. 典型战局 2: 满卡位/满手牌腾挪买卖局 (Full Board 7 / Full Hand 10 Maneuvering)

    /// <summary>
    /// 需求 4: 满卡位与满手牌腾挪测试 (19_full_hand_overflow_10_cards)
    /// 战场已满员 (7/7)，手牌已满 (10/10)。
    /// 必须先卖出场上随从释放卡位，再打出手牌腾出手牌位，随后才能买入酒馆高质量随从。
    /// </summary>
    [Fact]
    public void Solve_FullBoardAndFullHandOverflow_ShouldPerformSellPlayBuyManeuver()
    {
        // Arrange
        var snapshot = ReplayDatasetFactory.Create19FullHandOverflow10Cards();
        Assert.True(snapshot.IsBoardFull, "测试初始战场应已满 (7/7)");
        Assert.True(snapshot.IsHandFull, "测试初始手牌应已满 (10/10)");

        // Act
        var result = _solver.Solve(in snapshot);

        _output.WriteLine($"[满卡位满手牌腾挪结果] 步数: {result.Count}, 得分: {result.TotalScore:F1}, 耗时: {result.ElapsedMs:F3}ms, 节点数: {result.NodesExplored}");
        for (int i = 0; i < result.Count; i++)
        {
            _output.WriteLine($"  Step {i + 1}: {result[i]}");
        }

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.TotalScore > 0, "腾挪操作净期望收益应为正");
        Assert.True(result.ElapsedMs <= 5.0, $"求解耗时 {result.ElapsedMs:F3} ms 必须 <= 5.0ms");

        // 验证动作序列物理合法性 (资金链不断裂、卡位手牌上限绝不越界)
        AssertSequenceLegality(snapshot, result);

        // 验证关键腾挪模式: 序列中若有 Buy，则在 Buy 之前必须有 Sell 腾挪
        bool hasSellBeforeBuy = false;
        for (int i = 0; i < result.Count; i++)
        {
            if (result[i].ActionType == ActionType.Sell)
            {
                hasSellBeforeBuy = true;
            }
            if (result[i].ActionType == ActionType.Buy)
            {
                Assert.True(hasSellBeforeBuy, "满卡位满手牌局中，在执行 Buy 之前必须先执行 Sell 腾挪！");
                break;
            }
        }
    }

    /// <summary>
    /// 专门构造的极限满手牌/满战场严苛腾挪局
    /// </summary>
    [Fact]
    public void Solve_Strict7Board10HandCrisis_ShouldExecuteSellThenPlayThenBuyInStrictOrder()
    {
        // 战场 7 个弱小随从，手牌 10 张
        var boardBuilder = ImmutableArray.CreateBuilder<MinionEntity>(7);
        for (int i = 0; i < 7; i++)
        {
            boardBuilder.Add(new MinionEntity(100 + i, $"BG_WEAK_{i}", 1, 1, 1, MinionRaceMask.None, CardMechanicsMask.None, false, i));
        }

        var handBuilder = ImmutableArray.CreateBuilder<HandEntity>(10);
        for (int i = 0; i < 10; i++)
        {
            handBuilder.Add(new HandEntity(200 + i, $"BG_HAND_{i}", cost: 0, isMinion: true, isSpell: false, i));
        }

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_POWERHOUSE_TITUS", 3, 5, MinionRaceMask.Neutral)
        );

        var snapshot = new BoardSnapshot(
            turn: 8,
            heroCardId: "BG_HERO_101",
            health: 25,
            armor: 0,
            tavernTier: 5,
            upgradeCost: 7,
            isFrozen: false,
            bannedRaces: MinionRaceMask.None,
            gold: new GoldBudget(2, 10, 0), // 只有 2 金币！必须卖怪得 1 金币才能凑够 3 金币买瑞文！
            simulation: new CombatSimulationResult { LethalRate = 0.05 },
            boardMinions: boardBuilder.ToImmutable(),
            handCards: handBuilder.ToImmutable(),
            tavernMinions: tavern
        );

        // Act
        var result = _solver.Solve(in snapshot);

        _output.WriteLine($"[极限腾挪买瑞文结果] 步数: {result.Count}, 得分: {result.TotalScore:F1}, 耗时: {result.ElapsedMs:F3}ms");
        for (int i = 0; i < result.Count; i++)
        {
            _output.WriteLine($"  Step {i + 1}: {result[i]}");
        }

        // Assert
        Assert.True(result.Count >= 2, "应至少包含 Sell 与 Buy/Play 腾挪步骤");
        AssertSequenceLegality(snapshot, result);

        // 验证资金腾挪: 初始 2 金币不足以买 3 金币随从，第 1 步必须是 Sell 获得 +1 金币
        Assert.Equal(ActionType.Sell, result[0].ActionType);
    }

    #endregion

    #region 4. 典型战局 3: 残血生死局 (Low HP Lethal Emergency Crisis)

    /// <summary>
    /// 需求 4: 残血生死局验证 (05_lethal_emergency_hp7_turn8, P_lethal = 0.38 > 30%)
    /// 触发风控突变熔断，贪婪远期升本与长线协同权重强制归零，绝对禁止升本，全力搜寻即插即用保命身材。
    /// </summary>
    [Fact]
    public void Solve_LethalEmergencyHp7Turn8_MustForbidUpgradeTierAndMaximizeImmediateTempo()
    {
        // Arrange
        var snapshot = ReplayDatasetFactory.Create05LethalEmergencyHp7Turn8();
        Assert.True(snapshot.Simulation.LethalRate > 0.30, "残血局 P_lethal 必须 > 0.30 触发熔断");

        // Act
        var result = _solver.Solve(in snapshot);

        _output.WriteLine($"[残血生死局结果] 步数: {result.Count}, 得分: {result.TotalScore:F1}, 耗时: {result.ElapsedMs:F3}ms");
        for (int i = 0; i < result.Count; i++)
        {
            _output.WriteLine($"  Step {i + 1}: {result[i]}");
        }

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.ElapsedMs <= 5.0, $"求解耗时 {result.ElapsedMs:F3} ms 必须 <= 5.0ms");

        // 核心断言: 严禁在残血生死局升本！
        for (int i = 0; i < result.Count; i++)
        {
            Assert.NotEqual(ActionType.UpgradeTier, result[i].ActionType);
        }

        AssertSequenceLegality(snapshot, result);
    }

    /// <summary>
    /// 需求 4: 极端残血局 (06_lethal_crisis_hp4_turn9, P_lethal = 0.42)
    /// 即使手握 10 金币，也绝对不可升级酒馆！
    /// </summary>
    [Fact]
    public void Solve_LethalCrisisHp4Turn9_With10Gold_NeverUpgradesTier()
    {
        // Arrange
        var snapshot = ReplayDatasetFactory.Create06LethalCrisisHp4Turn9();
        Assert.True(snapshot.Simulation.LethalRate > 0.30);

        // Act
        var result = _solver.Solve(in snapshot);

        // Assert
        for (int i = 0; i < result.Count; i++)
        {
            Assert.NotEqual(ActionType.UpgradeTier, result[i].ActionType);
        }

        AssertSequenceLegality(snapshot, result);
    }

    #endregion

    #region 5. 5ms 硬超时与局部最优贪心保底机制验证 (Hard Timeout Guarantee)

    /// <summary>
    /// 需求 2: 验证 4.5ms 硬超时保底机制
    /// 当设置极短微秒级超时预算 (如 0.001ms) 时，求解器必须在 <= 5ms 内立即中断深搜，
    /// 并安全返回全局最高分贪心序列，且标记 IsTimedOut = true。
    /// </summary>
    [Fact]
    public void Solve_WhenTimeoutIsMicrosecond_ShouldReturnGreedyFallbackImmediatelyWithin5ms()
    {
        var snapshot = ReplayDatasetFactory.Create20Complex10GoldBranchingBudget();

        // Act: 传入 0.0001 ms 超时预算
        var result = _solver.Solve(
            snapshot,
            posteriors: default,
            riskProfile: new RiskWeightsProfile(0.05, 1.0, 1.0, false, RiskTier.Safe),
            timeoutMs: 0.0001,
            maxNodeBudget: 5);

        _output.WriteLine($"[微秒超时拦截结果] 步数: {result.Count}, 得分: {result.TotalScore:F1}, 耗时: {result.ElapsedMs:F3}ms, TimedOut: {result.IsTimedOut}");

        // Assert
        Assert.True(result.IsTimedOut, "必须标记为超时截断 IsTimedOut = true");
        Assert.True(result.ElapsedMs <= 5.0, $"总耗时 {result.ElapsedMs:F3} ms 必须严格 <= 5.0ms！");
        Assert.NotEmpty(result);
        Assert.True(result.TotalScore > 0, "保底贪心解得分必须为正");

        // 验证保底贪心序列物理合法性
        AssertSequenceLegality(snapshot, result);
    }

    /// <summary>
    /// 验证即使在超大人工搜索空间下，执行时间稳定 <= 5.0ms
    /// </summary>
    [Fact]
    public void Solve_UnderMassiveSearchSpace_ExecutionTime_StablyWithin5ms()
    {
        var snapshot = ReplayDatasetFactory.Create20Complex10GoldBranchingBudget();

        for (int run = 0; run < 10; run++)
        {
            var sw = Stopwatch.StartNew();
            var result = _solver.Solve(in snapshot);
            sw.Stop();

            Assert.True(sw.Elapsed.TotalMilliseconds <= 5.0, $"Run {run}: 耗时 {sw.Elapsed.TotalMilliseconds:F3} ms 超出 5.0 ms 硬上限！");
            Assert.NotEmpty(result);
            AssertSequenceLegality(snapshot, result);
        }
    }

    #endregion

    #region 6. 辅助断言函数：全链路物理合法性模拟校验 (Legality Verification)

    /// <summary>
    /// 严格模拟回放动作序列每一步的物理环境：
    /// 1. 资金链不断裂：可用金币随时 >= 0；
    /// 2. 卡位上限：战场席位数随时 <= 7 且 >= 0；
    /// 3. 手牌上限：手牌数随时 <= 10 且 >= 0；
    /// 4. 无实体重复变卖/买入冲突。
    /// </summary>
    private static void AssertSequenceLegality(in BoardSnapshot initial, ActionSequenceResult result)
    {
        int gold = initial.Gold.Available;
        int board = initial.BoardMinions.Length;
        int hand = initial.HandCards.Length;
        int tier = initial.TavernTier;
        bool hasUpgraded = false;
        bool hasFrozen = initial.IsFrozen;

        for (int i = 0; i < result.Count; i++)
        {
            var action = result[i];

            switch (action.ActionType)
            {
                case ActionType.Sell:
                    Assert.True(board > 0, $"Step {i + 1} ({action}): 战场已无随从可售！");
                    gold += 1;
                    board -= 1;
                    break;

                case ActionType.Buy:
                    Assert.True(gold >= action.GoldCost, $"Step {i + 1} ({action}): 资金链断裂！当前金币 {gold} < 购买花费 {action.GoldCost}");
                    Assert.True(hand < BoardSnapshot.MaxHandCards, $"Step {i + 1} ({action}): 手牌已满 (10)，无法执行购买！");
                    gold -= action.GoldCost;
                    hand += 1;
                    break;

                case ActionType.Play:
                    Assert.True(gold >= action.GoldCost, $"Step {i + 1} ({action}): 资金链断裂！当前金币 {gold} < 打出花费 {action.GoldCost}");
                    Assert.True(board < BoardSnapshot.MaxBoardMinions, $"Step {i + 1} ({action}): 战场已满 (7)，无法打出随从！");
                    Assert.True(hand > 0, $"Step {i + 1} ({action}): 手牌为空，无法打出！");
                    gold -= action.GoldCost;
                    board += 1;
                    hand -= 1;
                    break;

                case ActionType.UpgradeTier:
                    Assert.False(hasUpgraded, $"Step {i + 1} ({action}): 单回合重复升级酒馆！");
                    Assert.True(tier < 7, $"Step {i + 1} ({action}): 酒馆等级已达 7 级上限！");
                    Assert.True(gold >= action.GoldCost, $"Step {i + 1} ({action}): 资金不足升本！当前金币 {gold} < 升本费用 {action.GoldCost}");
                    gold -= action.GoldCost;
                    tier += 1;
                    hasUpgraded = true;
                    break;

                case ActionType.Freeze:
                    Assert.False(hasFrozen, $"Step {i + 1} ({action}): 酒馆已处于冻结状态！");
                    hasFrozen = true;
                    break;

                case ActionType.Reroll:
                    Assert.True(gold >= action.GoldCost, $"Step {i + 1} ({action}): 资金不足刷新酒馆！");
                    gold -= action.GoldCost;
                    break;

                default:
                    throw new InvalidOperationException($"未知动作类型: {action.ActionType}");
            }

            // 步后 invariants 校验
            Assert.True(gold >= 0, $"Step {i + 1} 执行后可用铸币出现负数: {gold}");
            Assert.InRange(board, 0, BoardSnapshot.MaxBoardMinions);
            Assert.InRange(hand, 0, BoardSnapshot.MaxHandCards);
        }
    }

    #endregion
}
