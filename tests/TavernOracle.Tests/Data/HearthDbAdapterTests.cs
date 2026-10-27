using System;
using System.Diagnostics;
using TavernOracle.Data;
using TavernOracle.Priors;
using Xunit;
using Xunit.Abstractions;

namespace TavernOracle.Tests.Data;

public class HearthDbAdapterTests
{
    private readonly ITestOutputHelper _output;
    private readonly IHearthDbAdapter _adapter;

    public HearthDbAdapterTests(ITestOutputHelper output)
    {
        _output = output;
        _adapter = HearthDbAdapter.Instance;
    }

    [Fact]
    public void Adapter_Initialization_ShouldSucceedAndHaveCachedCards()
    {
        Assert.True(_adapter.IsInitialized, "HearthDbAdapter 应成功完成预热初始化。");
        Assert.True(_adapter.CachedCardCount > 0, $"已缓存卡牌数量应大于 0，实际为: {_adapter.CachedCardCount}");
        _output.WriteLine($"[Init] 成功缓存 {_adapter.CachedCardCount} 张战棋相关卡牌。");
    }

    [Fact]
    public void TryGetCard_Leapfrogger_ShouldReturnAccurateMetadata()
    {
        // 跳蛙骑士 (Leapfrogger: BG21_000)
        bool found = _adapter.TryGetCard("BG21_000", out var metadata);

        Assert.True(found, "应能成功检索到跳蛙骑士 BG21_000。");
        Assert.NotNull(metadata);
        Assert.Equal("BG21_000", metadata.CardId);
        Assert.Equal("Leapfrogger", metadata.Name);
        Assert.Equal(2, metadata.Tier);
        Assert.True((metadata.Race & MinionRaceMask.Beast) != 0, "跳蛙骑士种族应包含野兽 (Beast)。");
        Assert.Equal(3, metadata.BaseAttack);
        Assert.Equal(3, metadata.BaseHealth);
        Assert.False(metadata.IsGolden, "BG21_000 应为普通非金色卡。");
        Assert.True(metadata.Deathrattle, "跳蛙骑士应具备亡语 (Deathrattle) 机制。");
        Assert.Equal("BG21_000_G", metadata.TripleCardId);

        // 金色跳蛙骑士 (BG21_000_G)
        bool foundGolden = _adapter.TryGetCard("BG21_000_G", out var goldenMeta);
        Assert.True(foundGolden, "应能成功检索到金色跳蛙骑士 BG21_000_G。");
        Assert.NotNull(goldenMeta);
        Assert.Equal("BG21_000_G", goldenMeta.CardId);
        Assert.Equal(2, goldenMeta.Tier);
        Assert.Equal(6, goldenMeta.BaseAttack);
        Assert.Equal(6, goldenMeta.BaseHealth);
        Assert.True(goldenMeta.IsGolden, "BG21_000_G 应为金色卡。");
        Assert.Equal("BG21_000", goldenMeta.TripleCardId);
    }

    [Fact]
    public void TryGetCard_DeflectOBot_ShouldReturnAccurateMetadata()
    {
        // 偏折机器人 (Deflect-o-Bot: BGS_071)
        bool found = _adapter.TryGetCard("BGS_071", out var metadata);

        Assert.True(found, "应能成功检索到偏折机器人 BGS_071。");
        Assert.NotNull(metadata);
        Assert.Equal("BGS_071", metadata.CardId);
        Assert.Equal("Deflect-o-Bot", metadata.Name);
        Assert.Equal(3, metadata.Tier);
        Assert.True((metadata.Race & MinionRaceMask.Mech) != 0, "偏折机器人种族应包含机械 (Mech)。");
        Assert.Equal(3, metadata.BaseAttack);
        Assert.Equal(2, metadata.BaseHealth);
        Assert.True(metadata.DivineShield, "偏折机器人应具备圣盾 (Divine Shield)。");
        Assert.False(metadata.IsGolden, "BGS_071 应为普通非金色卡。");
        Assert.Equal("TB_BaconUps_123", metadata.TripleCardId);

        // 金色偏折机器人 (TB_BaconUps_123)
        bool foundGolden = _adapter.TryGetCard("TB_BaconUps_123", out var goldenMeta);
        Assert.True(foundGolden, "应能成功检索到金色偏折机器人 TB_BaconUps_123。");
        Assert.NotNull(goldenMeta);
        Assert.Equal(3, goldenMeta.Tier);
        Assert.Equal(6, goldenMeta.BaseAttack);
        Assert.Equal(4, goldenMeta.BaseHealth);
        Assert.True(goldenMeta.DivineShield);
        Assert.True(goldenMeta.IsGolden);
        Assert.Equal("BGS_071", goldenMeta.TripleCardId);
    }

    [Fact]
    public void TryGetCard_Rivendare_ShouldReturnAccurateMetadata()
    {
        // 泰特斯·瑞文戴尔 (Titus Rivendare: BG25_354)
        bool foundTitus = _adapter.TryGetCard("BG25_354", out var titus);
        Assert.True(foundTitus, "应能检索到泰特斯·瑞文戴尔 BG25_354。");
        Assert.NotNull(titus);
        Assert.Equal(5, titus.Tier);
        Assert.Equal(1, titus.BaseAttack);
        Assert.Equal(7, titus.BaseHealth);
        Assert.False(titus.IsGolden);
        Assert.Equal("BG25_354_G", titus.TripleCardId);

        // 金色泰特斯·瑞文戴尔 (BG25_354_G)
        bool foundGoldenTitus = _adapter.TryGetCard("BG25_354_G", out var goldenTitus);
        Assert.True(foundGoldenTitus, "应能检索到金色泰特斯·瑞文戴尔 BG25_354_G。");
        Assert.NotNull(goldenTitus);
        Assert.Equal(5, goldenTitus.Tier);
        Assert.Equal(2, goldenTitus.BaseAttack);
        Assert.Equal(14, goldenTitus.BaseHealth);
        Assert.True(goldenTitus.IsGolden);
        Assert.Equal("BG25_354", goldenTitus.TripleCardId);

        // 经典瑞文戴尔男爵 (Baron Rivendare: FP1_031)
        bool foundBaron = _adapter.TryGetCard("FP1_031", out var baron);
        Assert.True(foundBaron, "应能检索到经典瑞文戴尔 FP1_031。");
        Assert.NotNull(baron);
        Assert.Equal(5, baron.Tier);
    }

    [Fact]
    public void TryGetCard_BrannBronzebeard_ShouldReturnAccurateMetadata()
    {
        // 布莱恩·铜须 (Brann Bronzebeard: BG_LOE_077)
        bool found = _adapter.TryGetCard("BG_LOE_077", out var brann);
        Assert.True(found, "应能成功检索到战棋布莱恩·铜须 BG_LOE_077。");
        Assert.NotNull(brann);
        Assert.Equal(5, brann.Tier);
        Assert.Equal(2, brann.BaseAttack);
        Assert.Equal(4, brann.BaseHealth);
        Assert.False(brann.IsGolden);
        Assert.Equal("TB_BaconUps_045", brann.TripleCardId);

        // 金色铜须 (TB_BaconUps_045)
        bool foundGolden = _adapter.TryGetCard("TB_BaconUps_045", out var goldenBrann);
        Assert.True(foundGolden, "应能成功检索到金色布莱恩·铜须 TB_BaconUps_045。");
        Assert.NotNull(goldenBrann);
        Assert.Equal(5, goldenBrann.Tier);
        Assert.Equal(4, goldenBrann.BaseAttack);
        Assert.Equal(8, goldenBrann.BaseHealth);
        Assert.True(goldenBrann.IsGolden);
        Assert.Equal("BG_LOE_077", goldenBrann.TripleCardId);
    }

    [Fact]
    public void TryGetCard_AnnoyOModule_ShouldHaveMagneticAndTauntAndDivineShield()
    {
        // 吵吵模组 (Annoy-o-Module: BG_BOT_911)
        bool found = _adapter.TryGetCard("BG_BOT_911", out var module);
        Assert.True(found, "应能检索到吵吵模组 BG_BOT_911。");
        Assert.NotNull(module);
        Assert.Equal(3, module.Tier);
        Assert.True((module.Race & MinionRaceMask.Mech) != 0, "吵吵模组必须是机械。");
        Assert.True(module.Magnetic, "吵吵模组必须具备磁力 (Magnetic) 特性。");
        Assert.True(module.Taunt, "吵吵模组必须具备嘲讽 (Taunt) 特性。");
        Assert.True(module.DivineShield, "吵吵模组必须具备圣盾 (Divine Shield) 特性。");
    }

    [Theory]
    [InlineData("BG21_000", "BG21_000_G")]
    [InlineData("BG21_000_G", "BG21_000")]
    [InlineData("BGS_071", "TB_BaconUps_123")]
    [InlineData("TB_BaconUps_123", "BGS_071")]
    [InlineData("BG25_354", "BG25_354_G")]
    [InlineData("BG25_354_G", "BG25_354")]
    [InlineData("BG_LOE_077", "TB_BaconUps_045")]
    [InlineData("TB_BaconUps_045", "BG_LOE_077")]
    public void GetTripleCardId_ShouldMapBidirectionally(string inputCardId, string expectedTripleCardId)
    {
        string? actualTripleId = _adapter.GetTripleCardId(inputCardId);
        Assert.Equal(expectedTripleCardId, actualTripleId);
    }

    [Fact]
    public void TryGetCardByDbfId_ShouldReturnCorrespondingMetadata()
    {
        // 跳蛙骑士 (DbfId: 72042)
        bool foundLeapfrogger = _adapter.TryGetCardByDbfId(72042, out var leapfrogger);
        Assert.True(foundLeapfrogger);
        Assert.NotNull(leapfrogger);
        Assert.Equal("BG21_000", leapfrogger.CardId);

        // 偏折机器人 (DbfId: 61930)
        bool foundDeflector = _adapter.TryGetCardByDbfId(61930, out var deflector);
        Assert.True(foundDeflector);
        Assert.NotNull(deflector);
        Assert.Equal("BGS_071", deflector.CardId);

        // 非法 DbfId
        bool foundInvalid = _adapter.TryGetCardByDbfId(-1, out var invalid);
        Assert.False(foundInvalid);
        Assert.Null(invalid);
    }

    [Fact]
    public void GetAllBattlegroundsMinions_Filtering_ShouldRespectTierAndGoldenFlags()
    {
        // 全量基础随从
        var allNormalMinions = _adapter.GetAllBattlegroundsMinions();
        Assert.NotEmpty(allNormalMinions);
        Assert.All(allNormalMinions, m => Assert.False(m.IsGolden));

        // 包含金色随从
        var allWithGolden = _adapter.GetAllBattlegroundsMinions(includeGolden: true);
        Assert.True(allWithGolden.Count > allNormalMinions.Count, "包含金色随从的总数应严格大于仅基础随从数。");

        // 按星级筛选: Tier 2 (应包含跳蛙骑士)
        var tier2Minions = _adapter.GetAllBattlegroundsMinions(tier: 2);
        Assert.NotEmpty(tier2Minions);
        Assert.All(tier2Minions, m => Assert.Equal(2, m.Tier));
        Assert.Contains(tier2Minions, m => m.CardId == "BG21_000");

        // 按星级筛选: Tier 3 (应包含偏折机器人)
        var tier3Minions = _adapter.GetAllBattlegroundsMinions(tier: 3);
        Assert.NotEmpty(tier3Minions);
        Assert.All(tier3Minions, m => Assert.Equal(3, m.Tier));
        Assert.Contains(tier3Minions, m => m.CardId == "BGS_071");

        // 按星级筛选: Tier 5 (应包含铜须与泰特斯)
        var tier5Minions = _adapter.GetAllBattlegroundsMinions(tier: 5);
        Assert.NotEmpty(tier5Minions);
        Assert.All(tier5Minions, m => Assert.Equal(5, m.Tier));
        Assert.Contains(tier5Minions, m => m.CardId == "BG_LOE_077");
        Assert.Contains(tier5Minions, m => m.CardId == "BG25_354");

        // 非法星级应返回空列表
        var invalidTier = _adapter.GetAllBattlegroundsMinions(tier: 99);
        Assert.Empty(invalidTier);
    }

    [Fact]
    public void EdgeCases_NullOrInvalidInputs_ShouldHandleGracefully()
    {
        Assert.False(_adapter.TryGetCard(null!, out var metaNull));
        Assert.Null(metaNull);

        Assert.False(_adapter.TryGetCard(string.Empty, out var metaEmpty));
        Assert.Null(metaEmpty);

        Assert.False(_adapter.TryGetCard("NON_EXISTENT_CARD_ID_12345", out var metaNonExistent));
        Assert.Null(metaNonExistent);

        Assert.Null(_adapter.GetTripleCardId(null!));
        Assert.Null(_adapter.GetTripleCardId(string.Empty));
        Assert.Null(_adapter.GetTripleCardId("NON_EXISTENT_CARD_ID_12345"));
    }

    [Fact]
    public void Benchmark_100k_Queries_ShouldMeetLatencyAndZeroGCAllocationCriteria()
    {
        // 选取典型随从作为热点高频查询样本集
        string[] querySet =
        [
            "BG21_000",       // 跳蛙
            "BG21_000_G",     // 金跳蛙
            "BGS_071",        // 偏折
            "TB_BaconUps_123",// 金偏折
            "BG25_354",       // 泰特斯
            "BG25_354_G",     // 金泰特斯
            "BG_LOE_077",     // 铜须
            "TB_BaconUps_045",// 金铜须
            "BG_BOT_911"      // 吵吵模组
        ];

        const int iterations = 100_000;

        // 1. 充分预热以触发 Tier-1 JIT 优化与 OSR 编译
        RunQueryLoop(_adapter, querySet, iterations);

        var sw = new Stopwatch();
        long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Start();

        int hitCount = RunQueryLoop(_adapter, querySet, iterations);
        long allocatedBytesAfter = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();
        long totalAllocatedBytes = allocatedBytesAfter - allocatedBytesBefore;

        Assert.Equal(iterations, hitCount);

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgTimeMs = totalMs / iterations;
        double avgTimeUs = avgTimeMs * 1000.0; // 微秒
        double avgTimeNs = avgTimeUs * 1000.0; // 纳秒

        _output.WriteLine($"[压测统计] 检索次数: {iterations:N0} 次");
        _output.WriteLine($"[压测统计] 总耗时: {totalMs:F3} ms");
        _output.WriteLine($"[压测统计] 单次查询平均耗时: {avgTimeMs:F6} ms ({avgTimeUs:F3} μs / {avgTimeNs:F1} ns)");
        _output.WriteLine($"[压测统计] 循环总堆内存分配: {totalAllocatedBytes} bytes (每千次分配: {(double)totalAllocatedBytes / (iterations / 1000.0):F2} bytes)");

        // 验收标准：单次平均耗时 <= 0.1 ms (100 微秒)
        Assert.True(avgTimeMs <= 0.1, $"单次查询平均耗时 {avgTimeMs:F6} ms 超出 0.1 ms 验收红线！");

        // 零 GC 验证：在紧凑热点查询中，堆内存分配应严格为 0
        Assert.Equal(0, totalAllocatedBytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int RunQueryLoop(IHearthDbAdapter adapter, string[] querySet, int count)
    {
        int querySetLength = querySet.Length;
        int hitCount = 0;
        for (int i = 0; i < count; i++)
        {
            string id = querySet[i % querySetLength];
            if (adapter.TryGetCard(id, out var meta) && meta != null)
            {
                hitCount++;
            }
        }
        return hitCount;
    }
}
