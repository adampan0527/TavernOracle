namespace TavernOracle.Tests.Decision;

using System.Diagnostics;
using TavernOracle.Decision;
using TavernOracle.Priors;
using Xunit;
using Xunit.Abstractions;

public sealed class BayesianInferenceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IBayesianArchetypeInference _inference;

    public BayesianInferenceTests(ITestOutputHelper output)
    {
        _output = output;
        _inference = new BayesianArchetypeInference(ArchetypeRepository.Instance);
    }

    [Fact]
    public void Leapfrogger_And_Rivendare_OnBoard_BeastsPosteriorExceeds80Percent()
    {
        // Arrange: 场上有跳蛙骑士 (BG21_000) 与提图斯·瑞文戴尔 (BG25_352)
        var board = new[] { "BG21_000", "BG25_352" };
        var hand = Array.Empty<string>();

        // Act
        var dist = _inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: MinionRaceMask.None,
            boardMinions: board,
            handMinions: hand);

        // Assert
        _output.WriteLine($"[Posterior] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");
        foreach (var entry in dist)
        {
            _output.WriteLine($"  - {entry.Archetype.NameCn,-10} ({entry.Archetype.Id}): {entry.Probability:P2}");
        }

        Assert.NotNull(dist.TopArchetype);
        Assert.Equal("Beasts_Leapfrogger", dist.TopArchetypeId);
        Assert.True(dist["Beasts_Leapfrogger"] >= 0.80,
            $"Expected Beasts posterior >= 80%, but was {dist["Beasts_Leapfrogger"]:P2}");
    }

    [Fact]
    public void Leapfrogger_InHand_And_Rivendare_OnBoard_BeastsPosteriorExceeds80Percent()
    {
        // Arrange: 场上有提图斯·瑞文戴尔 (BG25_352)，手牌有跳蛙骑士 (BG21_000)
        var board = new[] { "BG25_352" };
        var hand = new[] { "BG21_000" };

        // Act
        var dist = _inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: MinionRaceMask.None,
            boardMinions: board,
            handMinions: hand);

        _output.WriteLine($"[Posterior (Board Titus + Hand Leapfrogger)] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");

        Assert.Equal("Beasts_Leapfrogger", dist.TopArchetypeId);
        Assert.True(dist["Beasts_Leapfrogger"] >= 0.80,
            $"Expected Beasts posterior >= 80%, but was {dist["Beasts_Leapfrogger"]:P2}");
    }

    [Fact]
    public void Leapfrogger_And_Rivendare_BothInHand_BeastsPosteriorExceeds80Percent()
    {
        // Arrange: 跳蛙与提图斯均在手牌中
        var board = Array.Empty<string>();
        var hand = new[] { "BG21_000", "BG25_352" };

        // Act
        var dist = _inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: MinionRaceMask.None,
            boardMinions: board,
            handMinions: hand);

        _output.WriteLine($"[Posterior (Both in Hand)] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");

        Assert.Equal("Beasts_Leapfrogger", dist.TopArchetypeId);
        Assert.True(dist["Beasts_Leapfrogger"] >= 0.80,
            $"Expected Beasts posterior >= 80%, but was {dist["Beasts_Leapfrogger"]:P2}");
    }

    [Fact]
    public void Leapfrogger_With_BaronRivendare_FP1_031_BeastsPosteriorExceeds80Percent()
    {
        // Arrange: 跳蛙骑士与经典瑞文戴尔男爵 (FP1_031)
        var board = new[] { "BG21_000", "FP1_031" };

        // Act
        var dist = _inference.Infer(null, MinionRaceMask.None, board);

        _output.WriteLine($"[Posterior (Leapfrogger + Baron)] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");

        Assert.Equal("Beasts_Leapfrogger", dist.TopArchetypeId);
        Assert.True(dist["Beasts_Leapfrogger"] >= 0.80,
            $"Expected Beasts posterior >= 80%, but was {dist["Beasts_Leapfrogger"]:P2}");
    }

    [Fact]
    public void Leapfrogger_With_Rivendare_And_OtherMinions_BeastsPosteriorDominates()
    {
        // Arrange: 场上跳蛙+提图斯，外加中立/杂毛随从
        var board = new[] { "BG21_000", "BG25_352", "BGS_004" }; // BGS_004: 愤怒编织者

        // Act
        var dist = _inference.Infer(null, MinionRaceMask.None, board);

        _output.WriteLine($"[Posterior with mixed minions] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");

        Assert.Equal("Beasts_Leapfrogger", dist.TopArchetypeId);
        Assert.True(dist["Beasts_Leapfrogger"] >= 0.80,
            $"Expected Beasts posterior >= 80%, but was {dist["Beasts_Leapfrogger"]:P2}");
    }

    [Fact]
    public void BannedRaces_HardConstraint_MechanicsBanned_DeflectoMechProbabilityIsZero()
    {
        // Arrange: 对局禁选机械 (MinionRaceMask.Mech)，即使场上有偏折机器人 (BGS_071)
        var board = new[] { "BGS_071" };
        var banned = MinionRaceMask.Mech;

        // Act
        var dist = _inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: banned,
            boardMinions: board,
            handMinions: null);

        // Assert
        double mechProb = dist["Mechs_Deflecto"];
        _output.WriteLine($"[Mech Banned Verification] Mechs_Deflecto Probability: {mechProb:P2}");

        Assert.Equal(0.0, mechProb);
        Assert.NotEqual("Mechs_Deflecto", dist.TopArchetypeId);
    }

    [Fact]
    public void BannedRaces_HardConstraint_BeastsBanned_LeapfroggerProbabilityIsZero()
    {
        // Arrange: 对局禁选野兽 (MinionRaceMask.Beast)，即使场上有跳蛙+提图斯
        var board = new[] { "BG21_000", "BG25_352" };
        var banned = MinionRaceMask.Beast;

        // Act
        var dist = _inference.Infer(
            heroCardIdOrName: null,
            bannedRaces: banned,
            boardMinions: board,
            handMinions: null);

        // Assert: 野兽流先验归零，提图斯被亡灵复生流吸纳
        double beastsProb = dist["Beasts_Leapfrogger"];
        _output.WriteLine($"[Beast Banned Verification] Beasts_Leapfrogger: {beastsProb:P2}, Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");

        Assert.Equal(0.0, beastsProb);
        Assert.NotEqual("Beasts_Leapfrogger", dist.TopArchetypeId);
        // 提图斯也是亡灵流核心发动机
        Assert.Equal("Undead_Reborn", dist.TopArchetypeId);
    }

    [Theory]
    [InlineData("TB_BaconShop_HERO_78")]
    [InlineData("BG_DAL_575")]
    [InlineData("Chenvaala")]
    [InlineData("齐恩瓦拉")]
    public void HeroAffinity_Chenvaala_BoostsElementalsPriorOnEmptyBoard(string heroIdentifier)
    {
        // Arrange: 空盘面与手牌，英雄为齐恩瓦拉
        // Act
        var dist = _inference.Infer(
            heroCardIdOrName: heroIdentifier,
            bannedRaces: MinionRaceMask.None,
            boardMinions: null,
            handMinions: null);

        // Assert: 齐恩瓦拉对元素流有明显先验增益
        _output.WriteLine($"[Chenvaala Prior ({heroIdentifier})] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");
        foreach (var entry in dist)
        {
            _output.WriteLine($"  - {entry.Archetype.NameCn}: {entry.Probability:P2}");
        }

        Assert.Equal("Elementals_Nomi", dist.TopArchetypeId);
        double elemProb = dist["Elementals_Nomi"];
        double otherProb = dist["Beasts_Leapfrogger"];
        Assert.True(elemProb > otherProb * 2.0,
            $"Expected Elementals ({elemProb:P2}) to be > 2x other unbanned archetypes ({otherProb:P2})");
    }

    [Fact]
    public void HeroAffinity_Chenvaala_ElementalsBanned_OverridesAffinityToZero()
    {
        // Arrange: 齐恩瓦拉遭遇对局禁选元素
        var dist = _inference.Infer(
            heroCardIdOrName: "Chenvaala",
            bannedRaces: MinionRaceMask.Elemental,
            boardMinions: null,
            handMinions: null);

        // Assert: 即使英雄亲和，禁选硬拦截依然优先使元素流归零
        Assert.Equal(0.0, dist["Elementals_Nomi"]);
        Assert.NotEqual("Elementals_Nomi", dist.TopArchetypeId);
    }

    [Fact]
    public void HeroAffinity_Ysera_BoostsDragonsPrior()
    {
        // Arrange: 伊瑟拉亲和龙
        var dist = _inference.Infer("Ysera", MinionRaceMask.None, null, null);

        _output.WriteLine($"[Ysera Prior] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");
        Assert.Equal("Dragons_Kalecgos", dist.TopArchetypeId);
    }

    [Fact]
    public void HeroAffinity_Patches_BoostsPiratesPrior()
    {
        // Arrange: 帕奇斯亲和海盗
        var dist = _inference.Infer("Patches", MinionRaceMask.None, null, null);

        _output.WriteLine($"[Patches Prior] Top: {dist.TopArchetype?.NameCn} ({dist.TopProbability:P2})");
        Assert.Equal("Pirates_APM", dist.TopArchetypeId);
    }

    [Fact]
    public void NeutralHero_EmptyBoard_UniformPriorDistribution()
    {
        // Arrange: 普通中立英雄，空盘面，无禁选
        var dist = _inference.Infer(null, MinionRaceMask.None, null, null);

        // Assert: 10 个流派平权分布，各 10%
        Assert.Equal(10, dist.Count);
        double expected = 1.0 / 10.0;
        double sum = 0.0;

        foreach (var entry in dist)
        {
            Assert.InRange(entry.Probability, expected - 0.001, expected + 0.001);
            sum += entry.Probability;
        }

        Assert.InRange(sum, 0.999, 1.001);
    }

    [Fact]
    public void Performance_And_ZeroGC_20000Iterations_CompletesWithinBudgetAndZeroCollections()
    {
        // Arrange: 模拟实际对战盘面（7 张场上随从 + 3 张手牌随从）
        var board = new[] { "BG21_000", "BG25_352", "BGS_078", "BG26_355", "BG20_105", "BG26_147", "BG21_004" };
        var hand = new[] { "BG26_356", "FP1_031", "BG21_003" };
        var banned = MinionRaceMask.Mech | MinionRaceMask.Elemental;
        const string hero = "TB_BaconShop_HERO_78";

        ReadOnlySpan<string> boardSpan = board.AsSpan();
        ReadOnlySpan<string> handSpan = hand.AsSpan();

        // 预热 JIT
        for (int i = 0; i < 1000; i++)
        {
            var warmup = _inference.InferSpan(hero, banned, boardSpan, handSpan);
            if (warmup.TopProbability < 0) throw new InvalidOperationException();
        }

        // 清理并记录初始 GC 状态与内存基线
        var sw = new Stopwatch();

        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        int gen0Start = GC.CollectionCount(0);
        int gen1Start = GC.CollectionCount(1);
        int gen2Start = GC.CollectionCount(2);

        const int iterations = 20_000;
        long allocatedBytesStart = GC.GetAllocatedBytesForCurrentThread();
        sw.Start();

        double dummySum = 0.0;
        for (int i = 0; i < iterations; i++)
        {
            var result = _inference.InferSpan(hero, banned, boardSpan, handSpan);
            dummySum += result.TopProbability;
        }

        long allocatedBytesEnd = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();
        int gen0End = GC.CollectionCount(0);
        int gen1End = GC.CollectionCount(1);
        int gen2End = GC.CollectionCount(2);

        int gen0Delta = gen0End - gen0Start;
        int gen1Delta = gen1End - gen1Start;
        int gen2Delta = gen2End - gen2Start;
        long allocatedBytesDelta = allocatedBytesEnd - allocatedBytesStart;

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMs = totalMs / iterations;
        double avgUs = avgMs * 1000.0;

        _output.WriteLine($"[Zero GC Benchmark Results]");
        _output.WriteLine($"  - Total Iterations:     {iterations:N0}");
        _output.WriteLine($"  - Total Time:           {totalMs:F2} ms");
        _output.WriteLine($"  - Average Per Infer:    {avgMs:F5} ms ({avgUs:F2} \u03bcs)");
        _output.WriteLine($"  - Gen0 Collections:     {gen0Delta}");
        _output.WriteLine($"  - Gen1 Collections:     {gen1Delta}");
        _output.WriteLine($"  - Gen2 Collections:     {gen2Delta}");
        _output.WriteLine($"  - Allocated Heap Bytes: {allocatedBytesDelta} bytes");
        _output.WriteLine($"  - Dummy Check:          {dummySum:F2}");

        // Assert 1: 平均耗时严格 <= 0.5 ms (力争 < 0.05 ms)
        Assert.True(avgMs <= 0.5, $"Single inference took {avgMs:F5} ms, exceeding 0.5 ms budget!");
        Assert.True(avgMs < 0.05, $"Single inference took {avgMs:F5} ms, exceeding 0.05 ms ideal target!");

        // Assert 2: GC 回收增量为 0
        Assert.Equal(0, gen0Delta);
        Assert.Equal(0, gen1Delta);
        Assert.Equal(0, gen2Delta);

        // Assert 3: 堆分配增量严格为 0 字节
        Assert.Equal(0, allocatedBytesDelta);
    }

    [Fact]
    public void Distribution_Iteration_And_Indexers_WorkAccurately()
    {
        var board = new[] { "BG21_000", "BG25_352" };
        var dist = _inference.Infer(null, MinionRaceMask.None, board);

        // Enumerator loop test
        int counted = 0;
        double totalSum = 0.0;
        foreach (var entry in dist)
        {
            counted++;
            totalSum += entry.Probability;
            Assert.NotNull(entry.Archetype);
        }

        Assert.Equal(dist.Count, counted);
        Assert.InRange(totalSum, 0.999, 1.001);

        // Indexers
        double pById = dist["Beasts_Leapfrogger"];
        double pByObj = dist.GetProbability(dist.TopArchetype);
        Assert.Equal(pById, pByObj);

        // ToDictionary & ToArray
        var dict = dist.ToDictionary();
        Assert.Equal(10, dict.Count);
        Assert.True(dict.ContainsKey("Beasts_Leapfrogger"));

        var arr = dist.ToArray();
        Assert.Equal(10, arr.Length);
    }

    [Fact]
    public void EdgeCase_AllArchetypesBanned_HandledGracefully()
    {
        // 禁选全部种族
        var allTribes = MinionRaceMask.Beast | MinionRaceMask.Mech | MinionRaceMask.Murloc |
                        MinionRaceMask.Undead | MinionRaceMask.Pirate | MinionRaceMask.Elemental |
                        MinionRaceMask.Dragon | MinionRaceMask.Demon | MinionRaceMask.Naga |
                        MinionRaceMask.Quilboar;

        var dist = _inference.Infer(null, allTribes, new[] { "BG21_000" });

        Assert.Null(dist.TopArchetype);
        Assert.Equal(0.0, dist.TopProbability);
        Assert.Equal(0.0, dist["Beasts_Leapfrogger"]);
    }

    [Fact]
    public void EdgeCase_NullOrUnknownCardIds_HandledSafely()
    {
        var board = new[] { "UNKNOWN_CARD_XYZ", null!, "", "   ", "FP1_031" };
        var dist = _inference.Infer(null, MinionRaceMask.None, board);

        // 不崩溃且能识别出 FP1_031 (瑞文戴尔男爵)
        Assert.NotNull(dist.TopArchetype);
        Assert.True(dist.TopProbability > 0.0);
    }
}
