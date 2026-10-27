namespace TavernOracle.Tests.Decision;

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Decision.Evaluation;
using TavernOracle.Priors;
using TavernOracle.Simulation;
using Xunit;
using Xunit.Abstractions;

public sealed class ActionValueEvaluatorTests
{
    private readonly ITestOutputHelper _output;
    private readonly IActionValueEvaluator _evaluator;

    public ActionValueEvaluatorTests(ITestOutputHelper output)
    {
        _output = output;
        _evaluator = ActionValueEvaluator.Instance;
    }

    #region 1. 核心需求契约测试 (Core Requirements & Verification)

    /// <summary>
    /// 需求 1: 对子加成验证
    /// 检索手牌与战场已有随从，若该买入动作构成对子 (已有 1 张)，打分显著高于孤立白板随从。
    /// </summary>
    [Fact]
    public void Evaluate_FormingPair_ShouldHaveSignificantPairBonusOverIsolatedMinion()
    {
        // Arrange: 战场已有 1 张 2/2 白板随从 ("VANILLA_A")，手牌为空
        var existingMinion = new MinionEntity(1, "VANILLA_A", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 0);
        var snapshot = new BoardSnapshot
        {
            Turn = 3,
            TavernTier = 2,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = ImmutableArray.Create(existingMinion),
            HandCards = ImmutableArray<HandEntity>.Empty,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(101, "VANILLA_A", 3, 1, MinionRaceMask.None),
                new TavernMinion(102, "VANILLA_B", 3, 1, MinionRaceMask.None))
        };

        var buyPairAction = CandidateAction.CreateBuy(101, 0, 3, "VANILLA_A");
        var buyIsolatedAction = CandidateAction.CreateBuy(102, 1, 3, "VANILLA_B");

        var risk = new RiskWeightsProfile(0.05, 1.0, 1.0, false, RiskTier.Safe);
        var posteriors = default(ArchetypePosteriorDistribution);

        // Act
        var scorePair = _evaluator.Evaluate(snapshot, buyPairAction, posteriors, risk);
        var scoreIsolated = _evaluator.Evaluate(snapshot, buyIsolatedAction, posteriors, risk);

        _output.WriteLine($"[Pair Score]     {scorePair}");
        _output.WriteLine($"[Isolated Score] {scoreIsolated}");

        // Assert
        Assert.True(scorePair.PairScore > 0, "构成对子时 PairScore 应大于 0");
        Assert.Equal(0.0, scoreIsolated.PairScore);
        Assert.True(scorePair.TotalScore > scoreIsolated.TotalScore, "构成对子的总分应显著高于孤立随从");
        Assert.Equal(scorePair.PairScore, scorePair.TotalScore - scoreIsolated.TotalScore, 2);
        Assert.Equal("对子成型", scorePair.Tag);
    }

    /// <summary>
    /// 需求 2: 三连质变验证
    /// 面对已有 2 张同名卡牌时，买入第 3 张直接达成三连质变，打分出现跳跃性质变。
    /// </summary>
    [Fact]
    public void Evaluate_CompletingTriple_ShouldHaveMassiveJumpOverPairAndIsolated()
    {
        // Arrange: 战场已有 2 张同名卡 ("VANILLA_A")，1 张 ("VANILLA_B")
        var minionA1 = new MinionEntity(1, "VANILLA_A", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 0);
        var minionA2 = new MinionEntity(2, "VANILLA_A", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 1);
        var minionB1 = new MinionEntity(3, "VANILLA_B", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 2);

        var snapshot = new BoardSnapshot
        {
            Turn = 5,
            TavernTier = 3,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = ImmutableArray.Create(minionA1, minionA2, minionB1),
            HandCards = ImmutableArray<HandEntity>.Empty,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(101, "VANILLA_A", 3, 1, MinionRaceMask.None),
                new TavernMinion(102, "VANILLA_B", 3, 1, MinionRaceMask.None),
                new TavernMinion(103, "VANILLA_C", 3, 1, MinionRaceMask.None))
        };

        var buyTripleAction = CandidateAction.CreateBuy(101, 0, 3, "VANILLA_A"); // 第 3 张 (三连)
        var buyPairAction = CandidateAction.CreateBuy(102, 1, 3, "VANILLA_B");   // 第 2 张 (对子)
        var buyIsolatedAction = CandidateAction.CreateBuy(103, 2, 3, "VANILLA_C");// 第 1 张 (孤立)

        var risk = new RiskWeightsProfile(0.05, 1.0, 1.0, false, RiskTier.Safe);
        var posteriors = default(ArchetypePosteriorDistribution);

        // Act
        var scoreTriple = _evaluator.Evaluate(snapshot, buyTripleAction, posteriors, risk);
        var scorePair = _evaluator.Evaluate(snapshot, buyPairAction, posteriors, risk);
        var scoreIsolated = _evaluator.Evaluate(snapshot, buyIsolatedAction, posteriors, risk);

        _output.WriteLine($"[Triple Score]   {scoreTriple}");
        _output.WriteLine($"[Pair Score]     {scorePair}");
        _output.WriteLine($"[Isolated Score] {scoreIsolated}");

        // Assert
        Assert.True(scoreTriple.TripleBonus >= 50.0, "达成三连时 TripleBonus 应有巨额质变分 (>= 50)");
        Assert.Equal(0.0, scoreTriple.PairScore);
        Assert.True(scorePair.PairScore > 0, "对子应有 PairScore");
        Assert.Equal(0.0, scorePair.TripleBonus);

        // 跃迁验证: 三连总分与对子产生巨大断层差距 (>= 40 分跳跃)
        double jump = scoreTriple.TotalScore - scorePair.TotalScore;
        Assert.True(jump >= 40.0, $"三连与对子差值应出现跳跃性质变 (实际跳跃差: {jump:F1})");
        Assert.True(scoreTriple.TotalScore > scoreIsolated.TotalScore);
        Assert.Equal("三连质变", scoreTriple.Tag);
    }

    /// <summary>
    /// 需求 3: 高协同质变怪验证
    /// 在野兽流派后验概率高时，跳蛙骑士与瑞文戴尔获得极高协同分，显著超越中立孤立随从。
    /// </summary>
    [Fact]
    public void Evaluate_HighSynergyEngineMinions_LeapfroggerAndRivendare_ShouldHaveHighSynergyScore()
    {
        // Arrange: 构造高野兽后验分布 (场上有跳蛙 BG21_000 与提图斯·瑞文戴尔 BG25_352)
        var inference = new BayesianArchetypeInference(ArchetypeRepository.Instance);
        var posteriors = inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: MinionRaceMask.None,
            boardMinions: new[] { "BG21_000", "BG25_352" },
            handMinions: null);

        Assert.True(posteriors["Beasts_Leapfrogger"] >= 0.75, "野兽流派后验概率应 >= 75%");

        var snapshot = new BoardSnapshot
        {
            Turn = 6,
            TavernTier = 4,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = ImmutableArray.Create(
                new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 0),
                new MinionEntity(2, "BG25_352", 2, 14, 5, MinionRaceMask.None, CardMechanicsMask.Deathrattle, true, 1)),
            HandCards = ImmutableArray<HandEntity>.Empty,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(101, "BG21_000", 3, 2, MinionRaceMask.Beast),   // 跳蛙骑士
                new TavernMinion(102, "BG25_352", 3, 5, MinionRaceMask.None),    // 提图斯·瑞文戴尔
                new TavernMinion(103, "VANILLA_3_3", 3, 2, MinionRaceMask.None)) // 孤立中立白板 3/3
        };

        var buyLeapfrogger = CandidateAction.CreateBuy(101, 0, 3, "BG21_000");
        var buyRivendare = CandidateAction.CreateBuy(102, 1, 3, "BG25_352");
        var buyVanilla = CandidateAction.CreateBuy(103, 2, 3, "VANILLA_3_3");

        var risk = new RiskWeightsProfile(0.05, 1.0, 1.0, false, RiskTier.Safe);

        // Act
        var scoreLeapfrogger = _evaluator.Evaluate(snapshot, buyLeapfrogger, posteriors, risk);
        var scoreRivendare = _evaluator.Evaluate(snapshot, buyRivendare, posteriors, risk);
        var scoreVanilla = _evaluator.Evaluate(snapshot, buyVanilla, posteriors, risk);

        _output.WriteLine($"[Leapfrogger] Syn: {scoreLeapfrogger.SynergyScore:F1}, Total: {scoreLeapfrogger.TotalScore:F1}");
        _output.WriteLine($"[Rivendare]   Syn: {scoreRivendare.SynergyScore:F1}, Total: {scoreRivendare.TotalScore:F1}");
        _output.WriteLine($"[Vanilla]     Syn: {scoreVanilla.SynergyScore:F1}, Total: {scoreVanilla.TotalScore:F1}");

        // Assert
        Assert.True(scoreLeapfrogger.SynergyScore >= 25.0, "跳蛙在野兽高后验下应获得极高协同分 (>= 25)");
        Assert.True(scoreRivendare.SynergyScore >= 25.0, "瑞文戴尔在野兽高后验下应获得极高协同分 (>= 25)");
        Assert.True(scoreVanilla.SynergyScore < 5.0, "中立白板协同分应接近 0");
        Assert.True(scoreLeapfrogger.SynergyScore > scoreVanilla.SynergyScore * 5);
        Assert.True(scoreRivendare.SynergyScore > scoreVanilla.SynergyScore * 5);
    }

    /// <summary>
    /// 需求 4: 暴毙风控熔断验证
    /// 当 P_lethal = 0.40 时，触发风控突变熔断，协同分与留对分强制归零，仅保留保命即时战力分。
    /// </summary>
    [Fact]
    public void Evaluate_EmergencyLethal_ShouldForciblyZeroSynergyScoreAndRetainTempoScoreOnly()
    {
        // Arrange: 设置高暴毙率 P_lethal = 0.40 (触发风控突变熔断)
        var valve = RiskControlValve.Instance;
        var emergencyRisk = valve.Evaluate(0.40);

        Assert.True(emergencyRisk.IsEmergencyMutated, "P_lethal = 0.40 应触发策略突变");
        Assert.Equal(0.0, emergencyRisk.ScaleWeight);
        Assert.True(emergencyRisk.TempoWeight > 1.0);

        var inference = new BayesianArchetypeInference(ArchetypeRepository.Instance);
        var posteriors = inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: MinionRaceMask.None,
            boardMinions: new[] { "BG21_000" });

        var snapshot = new BoardSnapshot
        {
            Turn = 8,
            TavernTier = 4,
            Gold = new GoldBudget(10, 10, 0),
            Simulation = new CombatSimulationResult { LethalRate = 0.40 },
            BoardMinions = ImmutableArray.Create(
                new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 0)),
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(101, "BG25_352", 3, 5, MinionRaceMask.None)) // 瑞文戴尔 (强流派协同单卡)
        };

        var buyRivendare = CandidateAction.CreateBuy(101, 0, 3, "BG25_352");

        // Act
        var score = _evaluator.Evaluate(snapshot, buyRivendare, posteriors, emergencyRisk);

        _output.WriteLine($"[Emergency Lethal 40%] Score: {score}");

        // Assert 1: 协同分强制切断归零！
        Assert.Equal(0.0, score.SynergyScore);

        // Assert 2: 留对子贪婪分强制归零！
        Assert.Equal(0.0, score.PairScore);

        // Assert 3: 即时保命战力分有效保留
        Assert.True(score.TempoScore > 0, "即时保命战力分应保留且 > 0");

        // Assert 4: 总分严格等于即时保命战力分 (仅保留保命即时战力分)
        Assert.Equal(score.TempoScore, score.TotalScore, 3);
    }

    /// <summary>
    /// 需求 5: 性能压测与零 GC 堆垃圾
    /// 循环评估 20,000 次，单次耗时 &lt;= 1ms (实测 &lt; 0.02ms)，0 字节堆垃圾。
    /// </summary>
    [Fact]
    public void Benchmark_20k_Evaluations_ShouldMeetSubMillisecondAndZeroGCAllocation()
    {
        // Arrange: 典型盘面与候选动作集
        var board = ImmutableArray.Create(
            new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 0),
            new MinionEntity(2, "BG25_352", 2, 14, 5, MinionRaceMask.None, CardMechanicsMask.Deathrattle, true, 1),
            new MinionEntity(3, "LOOT_078", 12, 10, 4, MinionRaceMask.Beast, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 2));

        var tavern = ImmutableArray.Create(
            new TavernMinion(101, "BG21_000", 3, 2, MinionRaceMask.Beast),
            new TavernMinion(102, "BG_BOT_911", 3, 4, MinionRaceMask.Mech));

        var snapshot = new BoardSnapshot
        {
            Turn = 7,
            TavernTier = 4,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = board,
            TavernMinions = tavern
        };

        var buyAction = CandidateAction.CreateBuy(101, 0, 3, "BG21_000");
        var risk = new RiskWeightsProfile(0.10, 1.03, 0.91, false, RiskTier.Safe);
        var posteriors = default(ArchetypePosteriorDistribution);

        const int iterations = 20_000;

        // 1. 预热 JIT 与 OSR 优化
        for (int i = 0; i < 2000; i++)
        {
            var s = _evaluator.Evaluate(snapshot, buyAction, posteriors, risk);
            _ = s.TotalScore;
        }

        // 2. 清理内存基线
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        int gen0Start = GC.CollectionCount(0);
        int gen1Start = GC.CollectionCount(1);
        int gen2Start = GC.CollectionCount(2);

        var sw = new Stopwatch();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Start();

        double dummySum = 0;
        for (int i = 0; i < iterations; i++)
        {
            var res = _evaluator.Evaluate(snapshot, buyAction, posteriors, risk);
            dummySum += res.TotalScore;
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

        _output.WriteLine("[20k 动作边际收益评估压测结果报告]");
        _output.WriteLine($"  - 循环总次数:           {iterations:N0}");
        _output.WriteLine($"  - 总执行耗时:           {totalMs:F3} ms");
        _output.WriteLine($"  - 单次评估平均耗时:     {avgMs:F6} ms ({avgUs:F2} μs)");
        _output.WriteLine($"  - Gen0/1/2 回收增量:    {gen0Delta}/{gen1Delta}/{gen2Delta}");
        _output.WriteLine($"  - 线程堆内存总分配:     {totalAllocated} bytes");

        Assert.True(dummySum > 0);

        // 性能指标 1: 单次评估耗时 <= 1 ms (实测力争 < 0.05 ms)
        Assert.True(avgMs <= 1.0, $"单次打分耗时 {avgMs:F4} ms 超出 1.0 ms 性能红线！");
        Assert.True(avgMs < 0.05, $"单次打分耗时 {avgMs:F4} ms 应进入高吞吐区间！");

        // 性能指标 2: GC 回收增量为 0
        Assert.Equal(0, gen0Delta);
        Assert.Equal(0, gen1Delta);
        Assert.Equal(0, gen2Delta);

        // 性能指标 3: 严格 0 字节堆内存分配
        Assert.Equal(0, totalAllocated);
    }

    #endregion

    #region 2. 机制等效战力与多动作类型扩展测试 (Mechanics & Action Types)

    [Fact]
    public void CalculateMinionCombatPower_ShouldReflectMechanicsBonusesEquivalently()
    {
        // 白板 4/4
        double vanilla = ActionValueEvaluator.CalculateMinionCombatPower(4, 4, CardMechanicsMask.None);
        Assert.Equal(8.0, vanilla);

        // 圣盾 4/4
        double ds = ActionValueEvaluator.CalculateMinionCombatPower(4, 4, CardMechanicsMask.DivineShield);
        Assert.True(ds > vanilla, "圣盾战力应显著高于白板");

        // 剧毒 1/1
        double poison = ActionValueEvaluator.CalculateMinionCombatPower(1, 1, CardMechanicsMask.Poisonous);
        Assert.True(poison >= 25.0, "剧毒应具备高额消灭收益");

        // 狂战斧 6/6
        double cleave = ActionValueEvaluator.CalculateMinionCombatPower(6, 6, CardMechanicsMask.Cleave);
        Assert.True(cleave >= 25.0, "狂战斧范围爆发收益应显著");

        // 复生 3/3
        double reborn = ActionValueEvaluator.CalculateMinionCombatPower(3, 3, CardMechanicsMask.Reborn);
        Assert.True(reborn > 6.0, "复生战力应高于白板");
    }

    [Fact]
    public void Evaluate_SellAction_ShouldDeductCombatPowerAndSynergy()
    {
        var boardMinion = new MinionEntity(1, "TEST_SELL", 8, 8, 3, MinionRaceMask.Beast, CardMechanicsMask.DivineShield, false, 0);
        var snapshot = new BoardSnapshot
        {
            Turn = 4,
            TavernTier = 2,
            Gold = new GoldBudget(5, 10, 0),
            BoardMinions = ImmutableArray.Create(boardMinion)
        };

        var sellAction = CandidateAction.CreateSell(1, 0, "TEST_SELL");
        var risk = new RiskWeightsProfile(0.10, 1.0, 1.0, false, RiskTier.Safe);

        var score = _evaluator.Evaluate(snapshot, sellAction, default, risk);

        Assert.True(score.TempoScore < 0, "卖出动作 TempoScore 应为负数 (扣除在场战力)");
        Assert.True(score.RawTempoDelta < 0);
        Assert.Equal("出售随从", score.Tag);
    }

    [Fact]
    public void Evaluate_UpgradeTier_UnderEmergency_ShouldBeForciblySuppressed()
    {
        var snapshot = new BoardSnapshot
        {
            Turn = 5,
            TavernTier = 3,
            UpgradeCost = 6,
            Gold = new GoldBudget(7, 10, 0)
        };

        var upgradeAction = CandidateAction.CreateUpgradeTier(6);

        // 1. 常规安全状态下升本：有成长分
        var safeRisk = new RiskWeightsProfile(0.05, 1.0, 1.0, false, RiskTier.Safe);
        var safeScore = _evaluator.Evaluate(snapshot, upgradeAction, default, safeRisk);
        Assert.True(safeScore.SynergyScore > 0, "常规安全状态升本应具备正向远期成长分");

        // 2. 暴毙危机模式下升本：强制切断升本
        var valve = RiskControlValve.Instance;
        var emergencyRisk = valve.Evaluate(0.40);
        var emergencyScore = _evaluator.Evaluate(snapshot, upgradeAction, default, emergencyRisk);
        Assert.Equal(0.0, emergencyScore.SynergyScore);
        Assert.Equal(0.0, emergencyScore.TotalScore);
    }

    [Fact]
    public void EvaluateBatch_ShouldPopulateSpanInPlaceWithZeroAllocations()
    {
        var snapshot = new BoardSnapshot
        {
            Turn = 4,
            TavernTier = 2,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = ImmutableArray.Create(new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 0)),
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(101, "BG21_000", 3, 2, MinionRaceMask.Beast),
                new TavernMinion(102, "BG_BOT_911", 3, 4, MinionRaceMask.Mech))
        };

        var buffer = new CandidateActionBuffer();
        CandidateActionGenerator.Instance.Generate(snapshot, ref buffer);

        var scores = new ActionEvaluationScore[buffer.Count];
        var risk = new RiskWeightsProfile(0.10, 1.0, 1.0, false, RiskTier.Safe);

        _evaluator.EvaluateBatch(snapshot, buffer, default, risk, scores);

        Assert.Equal(buffer.Count, scores.Length);
        for (int i = 0; i < scores.Length; i++)
        {
            Assert.False(double.IsNaN(scores[i].TotalScore));
        }
    }

    [Fact]
    public void Evaluate_ConvenienceOverload_ShouldInferPosteriorsAndRiskAutomatically()
    {
        var snapshot = new BoardSnapshot
        {
            Turn = 5,
            TavernTier = 3,
            Gold = new GoldBudget(8, 10, 0),
            Simulation = new CombatSimulationResult { LethalRate = 0.05 },
            BoardMinions = ImmutableArray.Create(new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 0)),
            TavernMinions = ImmutableArray.Create(new TavernMinion(101, "BG21_000", 3, 2, MinionRaceMask.Beast))
        };

        var buyAction = CandidateAction.CreateBuy(101, 0, 3, "BG21_000");

        var score = _evaluator.Evaluate(snapshot, buyAction);

        Assert.True(score.TotalScore > 0);
        Assert.True(score.PairScore > 0, "自动推导应识别出对子加成");
    }

    #endregion
}
