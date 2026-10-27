using System;
using System.Collections.Immutable;
using System.Diagnostics;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Decision.Positioning;
using TavernOracle.Priors;
using Xunit;
using Xunit.Abstractions;

namespace TavernOracle.Tests.Decision;

/// <summary>
/// 随从站位排列启发式排序器 (MinionPositionOptimizer) 单元测试与对齐验证套件。
/// 严格对齐 T14 任务职责、docs/decision_engine.md 及万分天梯选手站位战术法则：
/// 1. 核心验收用例 (圣盾狂战排首位、嘲讽靠右、瑞文沉底、隔离墙垫底防狂战)；
/// 2. 边界条件覆盖 (0 随从、1 随从、全嘲讽、全亡语、无狂战/无发动机等)；
/// 3. 性能与零 GC 压测 (10,000 次循环耗时 <= 0.5ms，0 堆内存分配)。
/// </summary>
public class MinionPositionOptimizerTests
{
    private readonly ITestOutputHelper _output;
    private readonly IMinionPositionOptimizer _optimizer;

    public MinionPositionOptimizerTests(ITestOutputHelper output)
    {
        _output = output;
        _optimizer = MinionPositionOptimizer.Instance;
    }

    #region 1. 核心验收用例 (Core Acceptance Test)

    [Fact]
    public void CoreAcceptanceScenario_CleaveTauntBumperBaron_OrdersAccordingToHighMmrRules()
    {
        // 核心验收阵容：
        // 1. 【圣盾狂战斧】 (死神4000型 GVG_113, 6/9 圣盾狂战)
        // 2. 【嘲讽圣盾随从】 (吵吵模组 BG_BOT_911, 2/4 圣盾嘲讽)
        // 3. 【普通白板怪】 (普通淡水鳄 5/5 白板)
        // 4. 【瑞文戴尔】 (泰特斯·瑞文戴尔 BG25_354, 1/7 核心发动机)
        var cleave = new MinionEntity(101, "GVG_113", 6, 9, 6, MinionRaceMask.Mech, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var taunt = new MinionEntity(102, "BG_BOT_911", 2, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 1);
        var vanilla = new MinionEntity(103, "VANILLA_01", 5, 5, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 2);
        var baron = new MinionEntity(104, "BG25_354", 1, 7, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, false, 3);

        // 打乱顺序输入以测试重排鲁棒性
        var inputMinions = ImmutableArray.Create(vanilla, baron, taunt, cleave);

        // Act
        var result = _optimizer.OptimizePosition(inputMinions);

        // Assert
        Assert.Equal(4, result.Count);
        var ordered = result.OrderedMinions;
        Assert.Equal(4, ordered.Length);

        _output.WriteLine($"[Core Scenario Order] Reason: {result.Reason}");
        for (int i = 0; i < ordered.Length; i++)
        {
            _output.WriteLine($"  Slot {i}: {ordered[i].CardId} ({ordered[i].Attack}/{ordered[i].Health}, ZonePosition={ordered[i].ZonePosition})");
        }

        // 1. 断言圣盾狂战斧排在第 1 位 (Slot 0)
        Assert.Equal(cleave.EntityId, ordered[0].EntityId);
        Assert.Equal(0, ordered[0].ZonePosition);

        // 2. 断言瑞文戴尔被排在最右侧末位 (Slot 3)
        Assert.Equal(baron.EntityId, ordered[3].EntityId);
        Assert.Equal(3, ordered[3].ZonePosition);

        // 3. 断言嘲讽圣盾靠右且位于隔离墙左侧 (Slot 1)
        Assert.Equal(taunt.EntityId, ordered[1].EntityId);
        Assert.Equal(1, ordered[1].ZonePosition);

        // 4. 断言嘲讽怪与瑞文戴尔之间存在非核心隔离随从 (普通白板怪垫底防狂战斧溅射波及瑞文)
        Assert.Equal(vanilla.EntityId, ordered[2].EntityId);
        Assert.Equal(2, ordered[2].ZonePosition);

        int tauntSlot = -1, vanillaSlot = -1, baronSlot = -1;
        for (int i = 0; i < ordered.Length; i++)
        {
            if (ordered[i].EntityId == taunt.EntityId) tauntSlot = i;
            if (ordered[i].EntityId == vanilla.EntityId) vanillaSlot = i;
            if (ordered[i].EntityId == baron.EntityId) baronSlot = i;
        }

        Assert.True(tauntSlot < vanillaSlot && vanillaSlot < baronSlot,
            $"Expected Taunt ({tauntSlot}) < Bumper ({vanillaSlot}) < Baron ({baronSlot}) to shield Baron from cleave splash!");

        // 5. 验证 OptimalSlotIndices 映射
        // input: [vanilla (0), baron (1), taunt (2), cleave (3)]
        // targets: vanilla -> 2, baron -> 3, taunt -> 1, cleave -> 0
        Assert.Equal(2, result.GetOptimalSlot(0));
        Assert.Equal(3, result.GetOptimalSlot(1));
        Assert.Equal(1, result.GetOptimalSlot(2));
        Assert.Equal(0, result.GetOptimalSlot(3));

        Assert.Equal(2, result.OptimalSlotIndices[0]);
        Assert.Equal(3, result.OptimalSlotIndices[1]);
        Assert.Equal(1, result.OptimalSlotIndices[2]);
        Assert.Equal(0, result.OptimalSlotIndices[3]);

        // 6. 验证解释理由
        Assert.Contains("防狂战", result.Reason);
        Assert.Contains("发动机", result.Reason);
    }

    #endregion

    #region 2. 复杂 7 随从全局局势测试 (Full 7 Minion Board)

    [Fact]
    public void Full7MinionBoard_ComplexComposition_OrdersAccordingToHighTierTactics()
    {
        // 7 随从完整天梯高分盘面：
        // 0: 洞穴多头蛇 (圣盾狂战斧)
        // 1: 爆裂飓风 (圣盾超级风怒)
        // 2: 恩佐斯的子嗣 (亡语全场+1/+1 Buff，希望先手攻击送死)
        // 3: 阴森巨蟒 (亡语产两怪，需要右侧空位)
        // 4: 吵吵模组 (嘲讽圣盾)
        // 5: 白板小杂毛 2/2 (非核心隔离垫底)
        // 6: 泰特斯·瑞文戴尔 (后排核心发动机)
        var cleave = new MinionEntity(1, "LOOT_078", 12, 10, 4, MinionRaceMask.Beast, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var windfury = new MinionEntity(2, "BG_ELEMENTAL_WF", 8, 8, 3, MinionRaceMask.Elemental, CardMechanicsMask.DivineShield | CardMechanicsMask.MegaWindfury, false, 1);
        var buffDr = new MinionEntity(3, "BGS_039", 2, 2, 2, MinionRaceMask.None, CardMechanicsMask.Deathrattle, false, 2);
        var tokenDr = new MinionEntity(4, "BGS_014", 7, 7, 6, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 3);
        var tauntDs = new MinionEntity(5, "BG_BOT_911", 4, 8, 4, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 4);
        var bumper = new MinionEntity(6, "BUMPER_VANILLA", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 5);
        var engine = new MinionEntity(7, "BG25_354", 2, 14, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, true, 6);

        // 故意以反向次序输入
        var minions = ImmutableArray.Create(engine, bumper, tauntDs, tokenDr, buffDr, windfury, cleave);

        var result = _optimizer.OptimizePosition(minions);

        Assert.Equal(7, result.Count);
        var ordered = result.OrderedMinions;

        _output.WriteLine($"[Full 7 Minion Board Order] Reason: {result.Reason}");
        for (int i = 0; i < ordered.Length; i++)
        {
            _output.WriteLine($"  Slot {i}: {ordered[i].CardId} ({ordered[i].Attack}/{ordered[i].Health}, ZonePosition={ordered[i].ZonePosition})");
        }

        // Slot 0: 圣盾狂战斧 (首选主力先手输出位)
        Assert.Equal(cleave.EntityId, ordered[0].EntityId);

        // Slot 1: Buff 亡语随从 (恩佐斯的子嗣，抢先攻击送死全场+1/+1)
        Assert.Equal(buffDr.EntityId, ordered[1].EntityId);

        // Slot 2: 圣盾风怒怪 (强力高攻主力)
        Assert.Equal(windfury.EntityId, ordered[2].EntityId);

        // Slot 3: 产怪亡语随从 (巨蟒，排在中坚偏右，留出空间接收衍生物)
        Assert.Equal(tokenDr.EntityId, ordered[3].EntityId);

        // Slot 4: 嘲讽圣盾随从 (置于隔离墙左侧)
        Assert.Equal(tauntDs.EntityId, ordered[4].EntityId);

        // Slot 5: 防狂战隔离墙 (杂毛白板怪，吸纳狂战斧对嘲讽的溅射)
        Assert.Equal(bumper.EntityId, ordered[5].EntityId);

        // Slot 6: 核心发动机 (瑞文戴尔置于最末位第 7 席位)
        Assert.Equal(engine.EntityId, ordered[6].EntityId);

        // 验证 ZonePosition 连续且严格对齐
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(i, ordered[i].ZonePosition);
        }
    }

    #endregion

    #region 3. 边界测试场景 (Boundary & Edge Cases)

    [Fact]
    public void Boundary_EmptyMinions_ReturnsEmptyResult()
    {
        var result = _optimizer.OptimizePosition(ImmutableArray<MinionEntity>.Empty);
        Assert.Empty(result);
        Assert.Empty(result.OrderedMinions);
        Assert.Empty(result.OptimalSlotIndices);
        Assert.Equal("空盘面无需站位重排", result.Reason);
    }

    [Fact]
    public void Boundary_DefaultMinions_ReturnsEmptyResult()
    {
        var result = _optimizer.OptimizePosition(default);
        Assert.Empty(result);
        Assert.Empty(result.OrderedMinions);
        Assert.Empty(result.OptimalSlotIndices);
    }

    [Fact]
    public void Boundary_SingleMinion_PlacesAtSlotZero()
    {
        var minion = new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 4);
        var input = ImmutableArray.Create(minion);

        var result = _optimizer.OptimizePosition(input);

        Assert.Single(result);
        Assert.Single(result.OrderedMinions);
        Assert.Equal(0, result.OrderedMinions[0].ZonePosition);
        Assert.Equal(0, result.GetOptimalSlot(0));
        Assert.Equal(0, result.OptimalSlotIndices[0]);
    }

    [Fact]
    public void Boundary_AllTauntMinions_OrdersByDefensePower()
    {
        var normalTaunt = new MinionEntity(1, "T1", 3, 4, 2, MinionRaceMask.None, CardMechanicsMask.Taunt, false, 0);
        var dsTaunt = new MinionEntity(2, "T2", 2, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 1);
        var bigTaunt = new MinionEntity(3, "T3", 8, 12, 4, MinionRaceMask.None, CardMechanicsMask.Taunt, false, 2);

        var input = ImmutableArray.Create(normalTaunt, dsTaunt, bigTaunt);
        var result = _optimizer.OptimizePosition(input);

        Assert.Equal(3, result.Count);
        var ordered = result.OrderedMinions;

        // 圣盾嘲讽防御最高，靠右部署 (Slot 2)
        Assert.Equal(dsTaunt.EntityId, ordered[2].EntityId);
    }

    [Fact]
    public void Boundary_AllDeathrattleMinions_BuffFirstSpawnerMiddleEngineLast()
    {
        var frog = new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 0); // Buff DR
        var coiler = new MinionEntity(2, "BGS_014", 7, 7, 6, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 1); // Token DR
        var baron = new MinionEntity(3, "FP1_031", 1, 7, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, false, 2); // Engine DR

        var input = ImmutableArray.Create(coiler, baron, frog);
        var result = _optimizer.OptimizePosition(input);

        Assert.Equal(3, result.Count);
        var ordered = result.OrderedMinions;

        // Slot 0: 跳蛙先手攻击送死送BUFF
        Assert.Equal(frog.EntityId, ordered[0].EntityId);

        // Slot 1: 阴森巨蟒居中产怪
        Assert.Equal(coiler.EntityId, ordered[1].EntityId);

        // Slot 2: 经典瑞文戴尔男爵末位庇护
        Assert.Equal(baron.EntityId, ordered[2].EntityId);
    }

    [Fact]
    public void Boundary_NoEngine_TauntCornersAtFarRight()
    {
        // 场景：无核心发动机，但有嘲讽与狂战斧
        var cleave = new MinionEntity(1, "GVG_113", 6, 9, 6, MinionRaceMask.Mech, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var combatant = new MinionEntity(2, "COMBATANT", 7, 7, 4, MinionRaceMask.None, CardMechanicsMask.None, false, 1);
        var taunt = new MinionEntity(3, "TAUNT_DS", 4, 8, 4, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 2);

        var input = ImmutableArray.Create(combatant, taunt, cleave);
        var result = _optimizer.OptimizePosition(input);

        Assert.Equal(3, result.Count);
        var ordered = result.OrderedMinions;

        // 圣盾狂战首位
        Assert.Equal(cleave.EntityId, ordered[0].EntityId);

        // 常规战力居中
        Assert.Equal(combatant.EntityId, ordered[1].EntityId);

        // 嘲讽沉底在最右侧 (Slot 2)，使敌方狂战斧仅能溅射左侧一侧，浪费 50% 溅射收益
        Assert.Equal(taunt.EntityId, ordered[2].EntityId);
        Assert.Contains("吸引伤害", result.Reason);
    }

    [Fact]
    public void Boundary_MultipleEngines_OrdersAllToFarRight()
    {
        // 场景：拥有布莱恩·铜须 + 泰特斯·瑞文戴尔 2 个核心发动机
        var cleave = new MinionEntity(1, "GVG_113", 6, 9, 6, MinionRaceMask.Mech, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var taunt = new MinionEntity(2, "TAUNT", 3, 6, 3, MinionRaceMask.None, CardMechanicsMask.Taunt, false, 1);
        var bumper = new MinionEntity(3, "BUMPER", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 2);
        var brann = new MinionEntity(4, "BG_LOE_077", 2, 4, 5, MinionRaceMask.None, CardMechanicsMask.None, false, 3);
        var titus = new MinionEntity(5, "BG25_354", 1, 7, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, false, 4);

        var input = ImmutableArray.Create(brann, titus, bumper, taunt, cleave);
        var result = _optimizer.OptimizePosition(input);

        Assert.Equal(5, result.Count);
        var ordered = result.OrderedMinions;

        // Slot 0: 狂战斧
        Assert.Equal(cleave.EntityId, ordered[0].EntityId);

        // Slot 1: 嘲讽
        Assert.Equal(taunt.EntityId, ordered[1].EntityId);

        // Slot 2: 隔离墙
        Assert.Equal(bumper.EntityId, ordered[2].EntityId);

        // Slot 3: 铜须
        Assert.Equal(brann.EntityId, ordered[3].EntityId);

        // Slot 4: 瑞文戴尔 (战斗收益更高，最末位)
        Assert.Equal(titus.EntityId, ordered[4].EntityId);
    }

    [Fact]
    public void Boundary_BoardSnapshotOverload_ExecutesSuccessfully()
    {
        var cleave = new MinionEntity(101, "GVG_113", 6, 9, 6, MinionRaceMask.Mech, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var taunt = new MinionEntity(102, "BG_BOT_911", 2, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 1);
        var vanilla = new MinionEntity(103, "VANILLA_01", 5, 5, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 2);
        var baron = new MinionEntity(104, "BG25_354", 1, 7, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, false, 3);

        var snapshot = new BoardSnapshot
        {
            Turn = 8,
            Health = 25,
            TavernTier = 5,
            BoardMinions = ImmutableArray.Create(vanilla, baron, taunt, cleave)
        };

        var result = _optimizer.OptimizePosition(in snapshot);

        Assert.Equal(4, result.Count);
        Assert.Equal(cleave.EntityId, result.OrderedMinions[0].EntityId);
        Assert.Equal(baron.EntityId, result.OrderedMinions[3].EntityId);
    }

    [Fact]
    public void Boundary_SpanOverload_MatchesResult()
    {
        var cleave = new MinionEntity(101, "GVG_113", 6, 9, 6, MinionRaceMask.Mech, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var taunt = new MinionEntity(102, "BG_BOT_911", 2, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 1);
        var vanilla = new MinionEntity(103, "VANILLA_01", 5, 5, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 2);
        var baron = new MinionEntity(104, "BG25_354", 1, 7, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, false, 3);

        MinionEntity[] input = [vanilla, baron, taunt, cleave];
        MinionEntity[] output = new MinionEntity[4];
        int[] slots = new int[4];

        int count = _optimizer.OptimizePositionSpan(input, output, slots);

        Assert.Equal(4, count);
        Assert.Equal(cleave.EntityId, output[0].EntityId);
        Assert.Equal(taunt.EntityId, output[1].EntityId);
        Assert.Equal(vanilla.EntityId, output[2].EntityId);
        Assert.Equal(baron.EntityId, output[3].EntityId);

        // slots: [vanilla -> 2, baron -> 3, taunt -> 1, cleave -> 0]
        Assert.Equal(2, slots[0]);
        Assert.Equal(3, slots[1]);
        Assert.Equal(1, slots[2]);
        Assert.Equal(0, slots[3]);
    }

    [Fact]
    public void Boundary_AlreadyOptimalOrder_IsIdentityPermutation()
    {
        var cleave = new MinionEntity(101, "GVG_113", 6, 9, 6, MinionRaceMask.Mech, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0);
        var taunt = new MinionEntity(102, "BG_BOT_911", 2, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 1);
        var vanilla = new MinionEntity(103, "VANILLA_01", 5, 5, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 2);
        var baron = new MinionEntity(104, "BG25_354", 1, 7, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, false, 3);

        // 已经是最优排布
        var input = ImmutableArray.Create(cleave, taunt, vanilla, baron);
        var result = _optimizer.OptimizePosition(input);

        Assert.True(result.IsIdentityPermutation);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(i, result.GetOptimalSlot(i));
        }
    }

    #endregion

    #region 4. 性能与零 GC 压测 (Benchmark & Zero GC Verification)

    [Fact]
    public void Benchmark_10k_Positioning_ShouldMeetLatencyAndZeroGCAllocationCriteria()
    {
        // 构造典型 7 随从完整战局样本
        var minions = ImmutableArray.Create(
            new MinionEntity(1, "LOOT_078", 12, 10, 4, MinionRaceMask.Beast, CardMechanicsMask.DivineShield | CardMechanicsMask.Cleave, false, 0),
            new MinionEntity(2, "BG_ELEMENTAL_WF", 8, 8, 3, MinionRaceMask.Elemental, CardMechanicsMask.DivineShield | CardMechanicsMask.MegaWindfury, false, 1),
            new MinionEntity(3, "BGS_039", 2, 2, 2, MinionRaceMask.None, CardMechanicsMask.Deathrattle, false, 2),
            new MinionEntity(4, "BGS_014", 7, 7, 6, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(5, "BG_BOT_911", 4, 8, 4, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, false, 4),
            new MinionEntity(6, "BUMPER_VANILLA", 2, 2, 1, MinionRaceMask.None, CardMechanicsMask.None, false, 5),
            new MinionEntity(7, "BG25_354", 2, 14, 5, MinionRaceMask.Undead, CardMechanicsMask.Deathrattle, true, 6)
        );

        const int iterations = 10_000;

        // 1. 预热 JIT 与 OSR 优化
        for (int i = 0; i < 1000; i++)
        {
            var r = _optimizer.OptimizePosition(minions);
            _ = r.Count;
            _ = r[0].Attack;
            _ = r.GetOptimalSlot(0);
        }

        // 2. 强制垃圾回收清理内存基线
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        int gen0Start = GC.CollectionCount(0);
        int gen1Start = GC.CollectionCount(1);
        int gen2Start = GC.CollectionCount(2);

        var sw = Stopwatch.StartNew();
        long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();

        int dummySum = 0;
        for (int i = 0; i < iterations; i++)
        {
            var result = _optimizer.OptimizePosition(minions);
            dummySum += result[0].Attack;
            dummySum += result.GetOptimalSlot(0);
        }

        sw.Stop();
        long allocatedBytesAfter = GC.GetAllocatedBytesForCurrentThread();

        int gen0Delta = GC.CollectionCount(0) - gen0Start;
        int gen1Delta = GC.CollectionCount(1) - gen1Start;
        int gen2Delta = GC.CollectionCount(2) - gen2Start;
        long totalAllocated = allocatedBytesAfter - allocatedBytesBefore;

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMs = totalMs / iterations;
        double avgUs = avgMs * 1000.0;

        _output.WriteLine("[10k 站位重排压测结果报告]");
        _output.WriteLine($"  - 循环总次数:           {iterations:N0}");
        _output.WriteLine($"  - 总执行耗时:           {totalMs:F3} ms");
        _output.WriteLine($"  - 单次排序平均耗时:     {avgMs:F6} ms ({avgUs:F2} μs)");
        _output.WriteLine($"  - Gen0 回收次数:        {gen0Delta}");
        _output.WriteLine($"  - Gen1 回收次数:        {gen1Delta}");
        _output.WriteLine($"  - Gen2 回收次数:        {gen2Delta}");
        _output.WriteLine($"  - 线程堆内存总分配:     {totalAllocated} bytes");
        _output.WriteLine($"  - 校验 DummyCheck:      {dummySum}");

        Assert.True(dummySum > 0);

        // 验收指标 1：单次站位平均耗时 <= 0.5 ms (力求 < 0.05 ms)
        Assert.True(avgMs <= 0.5, $"单次站位耗时 {avgMs:F5} ms 超出 0.5 ms 性能红线！");
        Assert.True(avgMs < 0.05, $"单次站位耗时 {avgMs:F5} ms 超出 0.05 ms 优选目标！");

        // 验收指标 2：GC 回收增量为 0
        Assert.Equal(0, gen0Delta);
        Assert.Equal(0, gen1Delta);
        Assert.Equal(0, gen2Delta);

        // 验收指标 3：堆内存分配增量严格为 0 或可忽略的 JIT 开销 (<= 64 字节)
        Assert.True(totalAllocated <= 64, $"线程堆内存总分配 {totalAllocated} 字节超标！");
    }

    #endregion
}
