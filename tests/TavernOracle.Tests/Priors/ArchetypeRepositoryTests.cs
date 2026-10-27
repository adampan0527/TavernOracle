using System.Diagnostics;
using TavernOracle.Priors;
using Xunit;
using Xunit.Abstractions;

namespace TavernOracle.Tests.Priors;

public sealed class ArchetypeRepositoryTests
{
    private readonly ITestOutputHelper _output;
    private readonly IArchetypeRepository _repo;

    public ArchetypeRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
        _repo = ArchetypeRepository.Instance;
    }

    [Fact]
    public void FromJson_LoadsValidArchetypes_All10ArchetypesPresent()
    {
        // Assert: 10 major mainstream archetypes defined in specification
        var archetypes = _repo.GetAllArchetypes();
        Assert.NotNull(archetypes);
        Assert.Equal(10, archetypes.Count);

        var expectedIds = new[]
        {
            "Beasts_Leapfrogger",
            "Mechs_Deflecto",
            "Murlocs_Handbuff",
            "Undead_Reborn",
            "Pirates_APM",
            "Elementals_Nomi",
            "Dragons_Kalecgos",
            "Demons_SelfDamage",
            "Naga_Spellcraft",
            "Quilboar_BloodGem"
        };

        foreach (var expectedId in expectedIds)
        {
            var arch = _repo.GetArchetypeById(expectedId);
            Assert.NotNull(arch);
            Assert.False(string.IsNullOrWhiteSpace(arch.Name));
            Assert.False(string.IsNullOrWhiteSpace(arch.NameCn));
            Assert.False(string.IsNullOrWhiteSpace(arch.Description));
            Assert.NotEmpty(arch.KeyMechanics);
            Assert.NotEmpty(arch.TransitionSignposts);
            Assert.NotEmpty(arch.CoreMinions);
            _output.WriteLine($"[Archetype Validated] {arch.Id} - {arch.NameCn} ({arch.CoreMinions.Count} minions, primary={arch.PrimaryRace})");
        }
    }

    [Fact]
    public void Archetype_CoreMinions_AreProperlyDefined_WithValidAttributes()
    {
        // 1. Beasts: Leapfrogger (BG21_000), Titus Rivendare (BG25_352), Goldrinn (BGS_018)
        var beasts = _repo.GetArchetypeById("Beasts_Leapfrogger")!;
        Assert.Contains(beasts.CoreMinions, m => m.CardId == "BG21_000" && m.IsCoreEngine);
        Assert.Contains(beasts.CoreMinions, m => m.CardId == "BG25_352" && m.IsCoreEngine);
        Assert.Contains(beasts.CoreMinions, m => m.CardId == "BGS_018" && m.IsCoreEngine);

        // 2. Mechs: Deflect-o-Bot (BGS_071), Annoy-o-Module (BG_BOT_911), Iron Sensei (GVG_100)
        var mechs = _repo.GetArchetypeById("Mechs_Deflecto")!;
        Assert.Contains(mechs.CoreMinions, m => m.CardId == "BGS_071" && m.IsCoreEngine);
        Assert.Contains(mechs.CoreMinions, m => m.CardId == "BG_BOT_911");
        Assert.Contains(mechs.CoreMinions, m => m.CardId == "GVG_100" && m.IsCoreEngine);

        // 3. Murlocs: Brann Bronzebeard (BG_LOE_077), Bream Counter (BG26_354), King Bagurgle (BGS_030)
        var murlocs = _repo.GetArchetypeById("Murlocs_Handbuff")!;
        Assert.Contains(murlocs.CoreMinions, m => m.CardId == "BG_LOE_077" && m.IsCoreEngine);
        Assert.Contains(murlocs.CoreMinions, m => m.CardId == "BG26_354" && m.IsCoreEngine);
        Assert.Contains(murlocs.CoreMinions, m => m.CardId == "BGS_030");

        // 4. Undead: Eternal Knight (BG25_008), Titus Rivendare (BG25_352), Drakkari (BG26_ICC_029)
        var undead = _repo.GetArchetypeById("Undead_Reborn")!;
        Assert.Contains(undead.CoreMinions, m => m.CardId == "BG25_008" && m.IsCoreEngine);
        Assert.Contains(undead.CoreMinions, m => m.CardId == "BG25_352" && m.IsCoreEngine);
        Assert.Contains(undead.CoreMinions, m => m.CardId == "BG26_ICC_029");

        // 5. Pirates: Cap'n Hoggarr (BGS_046), Peggy Brittlebone (BG21_016)
        var pirates = _repo.GetArchetypeById("Pirates_APM")!;
        Assert.Contains(pirates.CoreMinions, m => m.CardId == "BGS_046" && m.IsCoreEngine);
        Assert.Contains(pirates.CoreMinions, m => m.CardId == "BG21_016" && m.IsCoreEngine);

        // 6. Elementals: Nomi (BGS_104), Wildfire Elemental (BAR_074), Master of Realities (BG21_036)
        var elementals = _repo.GetArchetypeById("Elementals_Nomi")!;
        Assert.Contains(elementals.CoreMinions, m => m.CardId == "BGS_104" && m.IsCoreEngine);
        Assert.Contains(elementals.CoreMinions, m => m.CardId == "BAR_074" && m.IsCoreEngine);
        Assert.Contains(elementals.CoreMinions, m => m.CardId == "BG21_036" && m.IsCoreEngine);

        // 7. Dragons: Kalecgos (BGS_045), Tarecgosa (BG21_015), Persistent Poet (BG29_811)
        var dragons = _repo.GetArchetypeById("Dragons_Kalecgos")!;
        Assert.Contains(dragons.CoreMinions, m => m.CardId == "BGS_045" && m.IsCoreEngine);
        Assert.Contains(dragons.CoreMinions, m => m.CardId == "BG21_015" && m.IsCoreEngine);
        Assert.Contains(dragons.CoreMinions, m => m.CardId == "BG29_811" && m.IsCoreEngine);

        // 8. Demons: Wrath Weaver (BGS_004), Soul Juggler (BGS_002), Tichondrius (BG26_523)
        var demons = _repo.GetArchetypeById("Demons_SelfDamage")!;
        Assert.Contains(demons.CoreMinions, m => m.CardId == "BGS_004" && m.IsCoreEngine);
        Assert.Contains(demons.CoreMinions, m => m.CardId == "BGS_002" && m.IsCoreEngine);
        Assert.Contains(demons.CoreMinions, m => m.CardId == "BG26_523" && m.IsCoreEngine);

        // 9. Naga: Deep-Sea Angler (BG23_004), Lady Athissa (BG23_318)
        var naga = _repo.GetArchetypeById("Naga_Spellcraft")!;
        Assert.Contains(naga.CoreMinions, m => m.CardId == "BG23_004" && m.IsCoreEngine);
        Assert.Contains(naga.CoreMinions, m => m.CardId == "BG23_318" && m.IsCoreEngine);

        // 10. Quilboar: Prophet of the Boar (BG20_203), Bristleback Knight (BG20_204), Charlga (BG20_303)
        var quilboar = _repo.GetArchetypeById("Quilboar_BloodGem")!;
        Assert.Contains(quilboar.CoreMinions, m => m.CardId == "BG20_203" && m.IsCoreEngine);
        Assert.Contains(quilboar.CoreMinions, m => m.CardId == "BG20_204" && m.IsCoreEngine);
        Assert.Contains(quilboar.CoreMinions, m => m.CardId == "BG20_303" && m.IsCoreEngine);

        // All minions must have valid positive tiers, names, and non-zero synergy weights
        var allMinions = _repo.GetAllEngineMinions();
        Assert.NotEmpty(allMinions);
        foreach (var m in allMinions)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.CardId));
            Assert.False(string.IsNullOrWhiteSpace(m.Name));
            Assert.False(string.IsNullOrWhiteSpace(m.NameCn));
            Assert.InRange(m.Tier, 1, 6);
            Assert.True(m.SynergyWeight > 0.0);
        }
    }

    [Fact]
    public void MinionRaceMask_BitmaskCalculations_AreExactAndO1()
    {
        // Verify bit values exact match to prompt specifications
        Assert.Equal(0, (int)MinionRaceMask.None);
        Assert.Equal(1 << 0, (int)MinionRaceMask.Beast);       // 1
        Assert.Equal(1 << 1, (int)MinionRaceMask.Mech);        // 2
        Assert.Equal(1 << 2, (int)MinionRaceMask.Murloc);      // 4
        Assert.Equal(1 << 3, (int)MinionRaceMask.Undead);      // 8
        Assert.Equal(1 << 4, (int)MinionRaceMask.Pirate);      // 16
        Assert.Equal(1 << 5, (int)MinionRaceMask.Elemental);   // 32
        Assert.Equal(1 << 6, (int)MinionRaceMask.Dragon);      // 64
        Assert.Equal(1 << 7, (int)MinionRaceMask.Demon);       // 128
        Assert.Equal(1 << 8, (int)MinionRaceMask.Naga);        // 256
        Assert.Equal(1 << 9, (int)MinionRaceMask.Quilboar);    // 512
        Assert.Equal(1 << 10, (int)MinionRaceMask.All);        // 1024

        // Dual tribe combination test
        var dualTribe = MinionRaceMask.Mech | MinionRaceMask.Demon;
        Assert.True(dualTribe.HasFlag(MinionRaceMask.Mech));
        Assert.True(dualTribe.HasFlag(MinionRaceMask.Demon));
        Assert.False(dualTribe.HasFlag(MinionRaceMask.Beast));

        // Intersection tests
        Assert.True(MinionRaceMask.Beast.Intersects(MinionRaceMask.Beast));
        Assert.False(MinionRaceMask.Beast.Intersects(MinionRaceMask.Mech));
        Assert.True(dualTribe.Intersects(MinionRaceMask.Mech));
        Assert.True(dualTribe.Intersects(MinionRaceMask.Demon));
        Assert.False(dualTribe.Intersects(MinionRaceMask.Dragon));

        // 'All' (Amalgam) intersects all tribes
        Assert.True(MinionRaceMask.All.Intersects(MinionRaceMask.Pirate));
        Assert.True(MinionRaceMask.Pirate.Intersects(MinionRaceMask.All));
        Assert.False(MinionRaceMask.None.Intersects(MinionRaceMask.All));

        // Parsing tests
        Assert.Equal(MinionRaceMask.Beast, MinionRaceMaskExtensions.Parse("Beast"));
        Assert.Equal(MinionRaceMask.Mech, MinionRaceMaskExtensions.Parse("机械"));
        Assert.Equal(MinionRaceMask.Murloc, MinionRaceMaskExtensions.Parse("murloc"));
        Assert.Equal(MinionRaceMask.Undead, MinionRaceMaskExtensions.Parse("亡灵"));
        Assert.Equal(MinionRaceMask.All, MinionRaceMaskExtensions.Parse("融合怪"));
        Assert.Equal(MinionRaceMask.Mech | MinionRaceMask.Beast, MinionRaceMaskExtensions.Parse("Mech, Beast"));
    }

    [Fact]
    public void ArchetypeRepository_FastRaceQueries_O1_ReturnsExpectedArchetypes()
    {
        var beastArchetypes = _repo.GetArchetypesByRace(MinionRaceMask.Beast);
        Assert.Contains(beastArchetypes, a => a.Id == "Beasts_Leapfrogger");

        var mechArchetypes = _repo.GetArchetypesByRace(MinionRaceMask.Mech);
        Assert.Contains(mechArchetypes, a => a.Id == "Mechs_Deflecto");

        var dragonArchetypes = _repo.GetArchetypesByRace(MinionRaceMask.Dragon);
        Assert.Contains(dragonArchetypes, a => a.Id == "Dragons_Kalecgos");

        var emptyArchetypes = _repo.GetArchetypesByRace(MinionRaceMask.None);
        Assert.Empty(emptyArchetypes);
    }

    [Fact]
    public void ArchetypeRepository_MinionLookups_AndSynergyWeights()
    {
        // Leapfrogger
        var leapfroggerArch = _repo.GetPrimaryArchetypeForMinion("BG21_000");
        Assert.NotNull(leapfroggerArch);
        Assert.Equal("Beasts_Leapfrogger", leapfroggerArch.Id);
        Assert.True(_repo.IsCoreMinion("BG21_000"));

        var weight = _repo.GetSynergyWeight("Beasts_Leapfrogger", "BG21_000");
        Assert.Equal(2.5, weight);

        // Deflect-o-Bot
        var deflectoArch = _repo.GetPrimaryArchetypeForMinion("BGS_071");
        Assert.NotNull(deflectoArch);
        Assert.Equal("Mechs_Deflecto", deflectoArch.Id);
        Assert.True(_repo.IsCoreMinion("BGS_071"));

        // Irrelevant minion weight is 0.0
        var irrelevantWeight = _repo.GetSynergyWeight("Mechs_Deflecto", "BG21_000");
        Assert.Equal(0.0, irrelevantWeight);
    }

    [Fact]
    public void SynergyMatrix_PairSynergies_AreSymmetricAndExact()
    {
        // Leapfrogger (BG21_000) + Titus Rivendare (BG25_352) = 2.5
        var pairSynergyForward = _repo.GetPairSynergy("BG21_000", "BG25_352");
        var pairSynergyReverse = _repo.GetPairSynergy("BG25_352", "BG21_000");
        Assert.Equal(2.5, pairSynergyForward);
        Assert.Equal(2.5, pairSynergyReverse);

        // Charlga (BG20_303) + Drakkari (BG26_ICC_029) = 2.5
        var charlgaDrakkari = _repo.GetPairSynergy("BG20_303", "BG26_ICC_029");
        Assert.Equal(2.5, charlgaDrakkari);

        // Unrelated cards default to 1.0 baseline
        var baseline = _repo.GetPairSynergy("UNKNOWN_CARD_1", "UNKNOWN_CARD_2");
        Assert.Equal(1.0, baseline);
    }

    [Fact]
    public void Performance_HighThroughput_QueryLatency_IsSubPointOneMilliseconds()
    {
        // Warmup JIT
        for (int i = 0; i < 1000; i++)
        {
            _ = _repo.GetPrimaryArchetypeForMinion("BG21_000");
            _ = _repo.GetSynergyWeight("Beasts_Leapfrogger", "BG21_000");
            _ = _repo.GetPairSynergy("BG21_000", "BG25_352");
            _ = _repo.GetArchetypesByRace(MinionRaceMask.Beast);
        }

        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            _ = _repo.GetPrimaryArchetypeForMinion("BG21_000");
            _ = _repo.GetSynergyWeight("Beasts_Leapfrogger", "BG21_000");
            _ = _repo.GetPairSynergy("BG21_000", "BG25_352");
            _ = _repo.GetArchetypesByRace(MinionRaceMask.Beast);
        }

        sw.Stop();

        // 4 operations per loop => total 400,000 queries
        int totalQueries = iterations * 4;
        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgPerQueryMs = totalMs / totalQueries;
        double avgPerQueryUs = avgPerQueryMs * 1000.0;

        _output.WriteLine($"[Benchmark Result] Completed {totalQueries:N0} queries in {totalMs:F2} ms.");
        _output.WriteLine($"[Latency Metric] Average query latency: {avgPerQueryUs:F4} microseconds ({avgPerQueryMs:F6} ms).");
        _output.WriteLine($"[Throughput Metric] Throughput: {(totalQueries / (totalMs / 1000.0)):N0} queries/second.");

        // Verification requirement: query performance <= 0.1 ms (100 microseconds)
        Assert.True(avgPerQueryMs <= 0.1, $"Query latency ({avgPerQueryMs:F6} ms) exceeded 0.1 ms threshold!");
        // In practice, this should be orders of magnitude faster (<= 0.005 ms)
        Assert.True(avgPerQueryMs <= 0.01, $"Expected latency to easily stay below 0.01 ms, got {avgPerQueryMs:F6} ms.");
    }

    [Fact]
    public void ArchetypeRepository_CrossReferences_AllSignpostsAndPairs_ResolveToDefinedMinions()
    {
        var allMinions = _repo.GetAllEngineMinions();
        var minionIds = new HashSet<string>(allMinions.Select(m => m.CardId), StringComparer.OrdinalIgnoreCase);

        // Verify transition signposts
        foreach (var arch in _repo.GetAllArchetypes())
        {
            Assert.NotEmpty(arch.TransitionSignposts);
            foreach (var signpost in arch.TransitionSignposts)
            {
                Assert.True(minionIds.Contains(signpost), $"Transition signpost '{signpost}' in archetype '{arch.Id}' does not resolve to any defined minion.");
            }
        }

        // Verify synergy pairs
        var pairs = _repo.SynergyMatrix.AllPairs;
        Assert.NotEmpty(pairs);
        foreach (var pair in pairs)
        {
            Assert.True(minionIds.Contains(pair.CardIdA), $"Synergy pair CardIdA '{pair.CardIdA}' does not resolve to any defined minion.");
            Assert.True(minionIds.Contains(pair.CardIdB), $"Synergy pair CardIdB '{pair.CardIdB}' does not resolve to any defined minion.");
            Assert.True(pair.SynergyScore > 1.0, $"Synergy pair {pair.CardIdA} + {pair.CardIdB} should have a positive synergy multiplier > 1.0.");
            Assert.False(string.IsNullOrWhiteSpace(pair.Reason), $"Synergy pair {pair.CardIdA} + {pair.CardIdB} must have a descriptive reason.");
        }
    }

    [Fact]
    public void ArchetypeRepository_EmbeddedLoading_MatchesInstance()
    {
        var embeddedRepo = ArchetypeRepository.FromEmbedded();
        Assert.NotNull(embeddedRepo);
        Assert.Equal(_repo.GetAllArchetypes().Count, embeddedRepo.GetAllArchetypes().Count);

        foreach (var arch in _repo.GetAllArchetypes())
        {
            var loaded = embeddedRepo.GetArchetypeById(arch.Id);
            Assert.NotNull(loaded);
            Assert.Equal(arch.Name, loaded.Name);
            Assert.Equal(arch.CoreMinions.Count, loaded.CoreMinions.Count);
        }
    }
}
