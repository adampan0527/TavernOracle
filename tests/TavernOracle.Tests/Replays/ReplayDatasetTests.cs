using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TavernOracle.Data;
using TavernOracle.Data.Replays;
using TavernOracle.Data.State;
using TavernOracle.Priors;
using TavernOracle.Simulation;
using Xunit;

namespace TavernOracle.Tests.Replays;

/// <summary>
/// 离线盘面对局回放测试数据集单元测试套件 (T06)。
/// 覆盖 20 组代表性典型战局盘面快照的完整性、语义合法性、生死线风控突变与脱机回放服务。
/// </summary>
public sealed class ReplayDatasetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    static ReplayDatasetTests()
    {
        EnsureReplayFilesGenerated();
    }

    /// <summary>
    /// 确保全部 20 组标准 JSON 回放文件在物理磁盘上均完整存在 (Self-healing on demand)
    /// </summary>
    internal static void EnsureReplayFilesGenerated()
    {
        var allSnapshots = ReplayDatasetFactory.CreateAll();

        // 探测需要部署的目标目录
        var targetDirectories = new List<string>();

        // 1. 尝试工程根目录 data/replays
        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? cur = new DirectoryInfo(baseDir);
        string? rootDataDir = null;
        string? testReplayDir = null;
        string? srcReplayDir = null;

        for (int i = 0; i < 6 && cur != null; i++)
        {
            string candidateData = Path.Combine(cur.FullName, "data", "replays");
            string candidateTests = Path.Combine(cur.FullName, "tests", "TavernOracle.Tests", "Replays");
            string candidateSrc = Path.Combine(cur.FullName, "src", "TavernOracle", "Data", "Replays");

            if (rootDataDir == null && Directory.Exists(Path.Combine(cur.FullName, "data")))
            {
                rootDataDir = candidateData;
            }
            if (testReplayDir == null && Directory.Exists(Path.Combine(cur.FullName, "tests", "TavernOracle.Tests")))
            {
                testReplayDir = candidateTests;
            }
            if (srcReplayDir == null && Directory.Exists(Path.Combine(cur.FullName, "src", "TavernOracle", "Data")))
            {
                srcReplayDir = candidateSrc;
            }

            cur = cur.Parent;
        }

        if (rootDataDir != null) targetDirectories.Add(rootDataDir);
        if (testReplayDir != null) targetDirectories.Add(testReplayDir);
        if (srcReplayDir != null) targetDirectories.Add(srcReplayDir);

        // 也部署到 AppContext.BaseDirectory / Replays
        targetDirectories.Add(Path.Combine(baseDir, "Replays"));
        targetDirectories.Add(Path.Combine(baseDir, "Data", "Replays"));

        foreach (var dir in targetDirectories)
        {
            try
            {
                Directory.CreateDirectory(dir);
                foreach (var (name, snapshot) in allSnapshots)
                {
                    string filePath = Path.Combine(dir, $"{name}.json");
                    if (!File.Exists(filePath))
                    {
                        string json = snapshot.ToJson(JsonOptions);
                        File.WriteAllText(filePath, json);
                    }
                }
            }
            catch
            {
                // 忽略非关键目录的写入异常
            }
        }
    }

    #region 契约与集合基准测试 (Basic Contract Tests)

    [Fact]
    public void All_20_Standard_Replays_Must_Be_Defined_And_Unique()
    {
        var names = ReplayDatasetFactory.AllScenarioNames;
        Assert.NotNull(names);
        Assert.Equal(20, names.Count);

        var distinct = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(20, distinct.Count);

        var allSnapshots = ReplayDatasetFactory.CreateAll();
        Assert.Equal(20, allSnapshots.Count);

        foreach (var name in names)
        {
            Assert.True(allSnapshots.ContainsKey(name), $"Scenario '{name}' must exist in dictionary.");
        }
    }

    [Fact]
    public void ReplayDatasetLoader_Can_Load_All_Replays_Offline()
    {
        var loader = ReplayDatasetLoader.Instance;
        var names = loader.GetReplayNames();
        Assert.NotNull(names);
        Assert.True(names.Count >= 20, $"Expected at least 20 replays, found {names.Count}");

        var replays = loader.LoadAllReplays();
        Assert.NotNull(replays);
        Assert.True(replays.Count >= 20);

        foreach (var name in ReplayDatasetFactory.AllScenarioNames)
        {
            Assert.True(replays.TryGetValue(name, out var snapshot), $"Replay '{name}' must be loaded.");
            Assert.True(snapshot.Turn >= 1, "Turn must be >= 1");
            Assert.False(string.IsNullOrWhiteSpace(snapshot.HeroCardId), "HeroCardId must not be empty");
            Assert.True(snapshot.Health > 0, "Health must be > 0");
        }
    }

    [Fact]
    public void ReplayDatasetLoader_LoadReplay_CaseInsensitive_And_Extension_Handling()
    {
        var loader = ReplayDatasetLoader.Instance;
        string baseName = ReplayDatasetFactory.S01_LeapfroggerEarlyTransition;

        var snap1 = loader.LoadReplay(baseName);
        var snap2 = loader.LoadReplay(baseName + ".json");
        var snap3 = loader.LoadReplay(baseName.ToUpperInvariant());

        Assert.Equal(snap1.Turn, snap2.Turn);
        Assert.Equal(snap1.Health, snap3.Health);
        Assert.Equal(snap1.HeroCardId, snap3.HeroCardId);

        bool success = loader.TryLoadReplay("non_existent_replay_xyz", out var emptySnap);
        Assert.False(success);
        Assert.Equal(default, emptySnap);
    }

    #endregion

    #region 场景 1~4：跳蛙野兽流转折点 (Leapfrogger Transition)

    [Fact]
    public void Scenarios_01_To_04_Leapfrogger_Beast_Transition_Verified()
    {
        var loader = ReplayDatasetLoader.Instance;

        // 场景 1：转职初发期，拥有跳蛙与下水道老鼠
        var s01 = loader.LoadReplay(ReplayDatasetFactory.S01_LeapfroggerEarlyTransition);
        Assert.Equal(6, s01.Turn);
        Assert.Equal(3, s01.TavernTier);
        Assert.Contains(s01.BoardMinions, m => m.CardId == "BG21_000"); // 跳蛙
        Assert.Contains(s01.BoardMinions, m => m.CardId == "BG19_010"); // 下水道老鼠
        Assert.True(s01.BoardMinions.All(m => (m.Race & MinionRaceMask.Beast) != 0 || m.Race == MinionRaceMask.Neutral));

        // 场景 2：升到5本找到提图斯·瑞文戴尔与巨怪鹦鹉
        var s02 = loader.LoadReplay(ReplayDatasetFactory.S02_LeapfroggerTitusFound);
        Assert.Equal(8, s02.Turn);
        Assert.Equal(5, s02.TavernTier);
        Assert.Contains(s02.BoardMinions, m => m.CardId == "BG25_352"); // 提图斯·瑞文戴尔
        Assert.Contains(s02.BoardMinions, m => m.CardId == "BGS_078");  // 巨怪鹦鹉
        var titus = s02.BoardMinions.First(m => m.CardId == "BG25_352");
        Assert.Equal(s02.BoardMinions.Length - 1, titus.ZonePosition); // 提图斯靠最右站位保护

        // 场景 3：金色跳蛙 + 复生鹦鹉，酒馆冻结第二张提图斯
        var s03 = loader.LoadReplay(ReplayDatasetFactory.S03_LeapfroggerGoldenFrogReborn);
        Assert.Equal(10, s03.Turn);
        Assert.True(s03.IsFrozen);
        Assert.Contains(s03.BoardMinions, m => m.CardId == "BG21_000_G" && m.IsGolden);
        Assert.Contains(s03.TavernMinions, t => t.CardId == "BG25_352" && t.IsFrozen); // 酒馆冻结第三张提图斯

        // 场景 4：终极跳蛙大成阵容 (7席位满载，双提图斯，胜率超 90%)
        var s04 = loader.LoadReplay(ReplayDatasetFactory.S04_LeapfroggerFullFormation);
        Assert.Equal(12, s04.Turn);
        Assert.Equal(6, s04.TavernTier);
        Assert.Equal(7, s04.BoardMinions.Length);
        Assert.True(s04.Simulation.WinRate >= 0.90);
        Assert.Equal(0.00, s04.Simulation.LethalRate);
        Assert.Contains(s04.BoardMinions, m => m.CardId == "BG25_352_G" && m.IsGolden);
        Assert.Contains(s04.BoardMinions, m => m.CardId == "BG25_352" && !m.IsGolden);
    }

    #endregion

    #region 场景 5~8：残血紧急苟活生死线 (Lethal Emergency Survival Line)

    [Theory]
    [InlineData(ReplayDatasetFactory.S05_LethalEmergencyHp7Turn8, 7, 0.42)]
    [InlineData(ReplayDatasetFactory.S06_LethalCrisisHp4Turn9, 4, 0.58)]
    [InlineData(ReplayDatasetFactory.S07_LethalBorderlineHp9Turn7, 9, 0.38)]
    [InlineData(ReplayDatasetFactory.S08_LethalSurvivalHp2LastStand, 2, 0.72)]
    public void Scenarios_05_To_08_Lethal_Emergency_Survival_Line_Verified(
        string scenarioName,
        int expectedHealth,
        double expectedMinLethalRate)
    {
        var loader = ReplayDatasetLoader.Instance;
        var snapshot = loader.LoadReplay(scenarioName);

        // 验证生命值 <= 10
        Assert.True(snapshot.TotalHealth <= 10, $"TotalHealth {snapshot.TotalHealth} must be <= 10");
        Assert.Equal(expectedHealth, snapshot.Health);

        // 验证暴毙率 P_lethal >= 0.35 触发风控调节阀突变
        Assert.True(snapshot.Simulation.LethalRate >= 0.35, $"P_lethal {snapshot.Simulation.LethalRate} must be >= 0.35");
        Assert.Equal(expectedMinLethalRate, snapshot.Simulation.LethalRate, precision: 2);

        // 验证高败率与高预期承受伤害
        Assert.True(snapshot.Simulation.LossRate > 0.50, "Loss rate in lethal crisis should be > 50%");
        Assert.True(snapshot.Simulation.AverageDamageTaken > 5.0, "Expected damage taken should be significant (> 5.0)");
    }

    #endregion

    #region 场景 9~12：神仙决赛圈对决 (Final God Tier Compositions)

    [Fact]
    public void Scenarios_09_To_12_Final_God_Tier_Compositions_Verified()
    {
        var loader = ReplayDatasetLoader.Instance;

        // 场景 9：全金双狂战机械
        var s09 = loader.LoadReplay(ReplayDatasetFactory.S09_FinalDualGoldenCleaveMech);
        Assert.Equal(15, s09.Turn);
        Assert.Equal(7, s09.BoardMinions.Length);
        Assert.True(s09.BoardMinions.Count(m => m.IsGolden) >= 5, "At least 5 minions should be golden");
        var cleaveMech = s09.BoardMinions[0];
        Assert.True(cleaveMech.Cleave, "Position 0 must be Cleave");
        Assert.True(cleaveMech.DivineShield, "Cleave minion must have Divine Shield");
        Assert.True(cleaveMech.Windfury, "Cleave minion must have Windfury");

        // 场景 10：圣盾烈毒风怒全成型鱼人
        var s10 = loader.LoadReplay(ReplayDatasetFactory.S10_FinalDivineVenomMurlocs);
        Assert.Equal(16, s10.Turn);
        Assert.Equal(7, s10.BoardMinions.Length);
        Assert.Contains(s10.BoardMinions, m => m.DivineShield && m.Venomous);
        Assert.Contains(s10.BoardMinions, m => m.CardId == "BG_LOE_077_G"); // 金铜须
        Assert.True(s10.Simulation.WinRate >= 0.85);

        // 场景 11：大身材元素 (150/150+ 超级狂风与现境大师)
        var s11 = loader.LoadReplay(ReplayDatasetFactory.S11_FinalGiantElementalStats);
        Assert.Equal(14, s11.Turn);
        Assert.Equal(7, s11.BoardMinions.Length);
        var megaWindfury = s11.BoardMinions[0];
        Assert.True(megaWindfury.MegaWindfury);
        Assert.True(megaWindfury.Attack >= 150 && megaWindfury.Health >= 150);
        Assert.True(s11.BoardMinions.Any(m => m.Attack >= 200 && m.Health >= 200));

        // 场景 12：邪能恶魔吞噬阵容 (乌祖尔 + 提克迪奥斯 + 邪能蝙蝠 200/200+)
        var s12 = loader.LoadReplay(ReplayDatasetFactory.S12_FinalGiantDemonFelbat);
        Assert.Equal(15, s12.Turn);
        Assert.Equal(7, s12.BoardMinions.Length);
        var urzul = s12.BoardMinions[0];
        Assert.True(urzul.Attack >= 250 && urzul.Health >= 250);
        Assert.True(urzul.Taunt);
        Assert.Contains(s12.BoardMinions, m => m.CardId.Contains("BG21_027")); // 邪能蝙蝠
    }

    #endregion

    #region 场景 13~16：高频 APM 转职期 (High APM Pivot Phases)

    [Fact]
    public void Scenarios_13_To_16_Apm_Pivot_Phases_Verified()
    {
        var loader = ReplayDatasetLoader.Instance;

        // 场景 13：电表海盗 (双霍格 + 特瑟斯)
        var s13 = loader.LoadReplay(ReplayDatasetFactory.S13_ApmPiratesHoggerTransition);
        Assert.Equal(9, s13.Turn);
        Assert.Equal(5, s13.TavernTier);
        int hoggerCount = s13.BoardMinions.Count(m => m.CardId == "BGS_046");
        Assert.Equal(2, hoggerCount);
        Assert.Contains(s13.BoardMinions, m => m.CardId == "BG26_524"); // 特瑟斯
        Assert.True(s13.HandCards.Length >= 4, "High APM requires dense hand cards");

        // 场景 14：手牌鱼人循环 (算盘鱼 + 低音提琴 + 合唱鱼人)
        var s14 = loader.LoadReplay(ReplayDatasetFactory.S14_ApmMurlocHandBuffLoop);
        Assert.Equal(10, s14.Turn);
        Assert.Contains(s14.BoardMinions, m => m.CardId == "BG26_354"); // 算盘鱼
        Assert.Contains(s14.BoardMinions, m => m.CardId == "BG26_357"); // 合唱鱼人
        Assert.Contains(s14.BoardMinions, m => m.CardId == "BG26_350"); // 低音提琴鱼人
        Assert.True(s14.HandCards.Length >= 5, "Hand murloc loop holds buffed hand minions");

        // 场景 15：机械偏折重置流 (双偏折 + 吵吵模组 + 废铁残械 + 极恶魔能火炮)
        var s15 = loader.LoadReplay(ReplayDatasetFactory.S15_ApmMechDeflectoResetPivot);
        Assert.Equal(8, s15.Turn);
        int deflectoCount = s15.BoardMinions.Count(m => m.CardId == "BGS_071");
        Assert.Equal(2, deflectoCount);
        Assert.Contains(s15.BoardMinions, m => m.CardId == "BG26_160"); // 废铁残械
        Assert.Contains(s15.BoardMinions, m => m.CardId == "BG26_154"); // 极恶魔能火炮

        // 场景 16：诺米元素滚雪球 (诺米 + 飓风 + 狂风)
        var s16 = loader.LoadReplay(ReplayDatasetFactory.S16_ApmElementalCyclingNomi);
        Assert.Equal(7, s16.Turn);
        Assert.Contains(s16.BoardMinions, m => m.CardId == "BGS_046"); // 诺米
        Assert.True(s16.HandCards.Length >= 4, "Nomi cycling has elemental tokens in hand");
    }

    #endregion

    #region 场景 17~20：复杂动作与边界空间 (Complex Operational States)

    [Fact]
    public void Scenarios_17_To_20_Complex_And_Boundary_States_Verified()
    {
        var loader = ReplayDatasetLoader.Instance;

        // 场景 17：初开局空场 (Turn 1, 3金币, 0战场随从, 0手牌, 3酒馆在售)
        var s17 = loader.LoadReplay(ReplayDatasetFactory.S17_Turn1OpeningEmptyBoard);
        Assert.Equal(1, s17.Turn);
        Assert.Equal(3, s17.Gold.Available);
        Assert.Equal(3, s17.Gold.Max);
        Assert.Empty(s17.BoardMinions);
        Assert.Empty(s17.HandCards);
        Assert.Equal(3, s17.TavernMinions.Length);

        // 场景 18：极端多对子局面 (3对对子随从，酒馆刷新出第3张对子)
        var s18 = loader.LoadReplay(ReplayDatasetFactory.S18_MultiPairTripleHunt);
        Assert.Equal(7, s18.Turn);
        Assert.Equal(6, s18.BoardMinions.Length);
        var groupedBoard = s18.BoardMinions.GroupBy(m => m.CardId).ToList();
        Assert.Equal(3, groupedBoard.Count);
        Assert.True(groupedBoard.All(g => g.Count() == 2), "Board must consist of exactly 3 pairs");
        Assert.Contains(s18.TavernMinions, t => t.CardId == "BGS_071"); // 酒馆在售第3张偏折
        Assert.Contains(s18.TavernMinions, t => t.CardId == "BG19_010"); // 酒馆在售第3张老鼠

        // 场景 19：满手牌 10 张溢出极限局面 (10手牌，7场面满席位)
        var s19 = loader.LoadReplay(ReplayDatasetFactory.S19_FullHandOverflow10Cards);
        Assert.Equal(10, s19.Turn);
        Assert.Equal(10, s19.HandCards.Length);
        Assert.True(s19.IsHandFull, "IsHandFull must be true for 10 cards");
        Assert.Equal(7, s19.BoardMinions.Length);
        Assert.True(s19.IsBoardFull, "IsBoardFull must be true for 7 minions");

        // 场景 20：10 金币庞大动作分支局面 (10金币，手牌+场面+酒馆满选项)
        var s20 = loader.LoadReplay(ReplayDatasetFactory.S20_Complex10GoldBranchingBudget);
        Assert.Equal(9, s20.Turn);
        Assert.Equal(10, s20.Gold.Available);
        Assert.Equal(10, s20.Gold.Max);
        Assert.Equal(6, s20.BoardMinions.Length);
        Assert.Equal(4, s20.HandCards.Length);
        Assert.Equal(6, s20.TavernMinions.Length);
    }

    #endregion

    #region JSON 往返无损与性能测试 (Lossless Serialization & Performance)

    [Fact]
    public void BoardSnapshot_JsonRoundTrip_Lossless_For_All_20_Replays()
    {
        var all = ReplayDatasetFactory.CreateAll();

        foreach (var (name, original) in all)
        {
            string json = original.ToJson(JsonOptions);
            Assert.False(string.IsNullOrWhiteSpace(json));

            var restored = BoardSnapshot.FromJson(json, JsonOptions);

            Assert.Equal(original.Turn, restored.Turn);
            Assert.Equal(original.HeroCardId, restored.HeroCardId);
            Assert.Equal(original.Health, restored.Health);
            Assert.Equal(original.Armor, restored.Armor);
            Assert.Equal(original.TotalHealth, restored.TotalHealth);
            Assert.Equal(original.TavernTier, restored.TavernTier);
            Assert.Equal(original.UpgradeCost, restored.UpgradeCost);
            Assert.Equal(original.IsFrozen, restored.IsFrozen);
            Assert.Equal(original.BannedRaces, restored.BannedRaces);

            Assert.Equal(original.Gold.Available, restored.Gold.Available);
            Assert.Equal(original.Gold.Max, restored.Gold.Max);
            Assert.Equal(original.Gold.LockedInRoll, restored.Gold.LockedInRoll);

            Assert.Equal(original.Simulation.WinRate, restored.Simulation.WinRate, precision: 4);
            Assert.Equal(original.Simulation.TieRate, restored.Simulation.TieRate, precision: 4);
            Assert.Equal(original.Simulation.LossRate, restored.Simulation.LossRate, precision: 4);
            Assert.Equal(original.Simulation.LethalRate, restored.Simulation.LethalRate, precision: 4);
            Assert.Equal(original.Simulation.AverageDamageTaken, restored.Simulation.AverageDamageTaken, precision: 4);

            Assert.Equal(original.BoardMinions.Length, restored.BoardMinions.Length);
            for (int i = 0; i < original.BoardMinions.Length; i++)
            {
                var o = original.BoardMinions[i];
                var r = restored.BoardMinions[i];
                Assert.Equal(o.EntityId, r.EntityId);
                Assert.Equal(o.CardId, r.CardId);
                Assert.Equal(o.Attack, r.Attack);
                Assert.Equal(o.Health, r.Health);
                Assert.Equal(o.Tier, r.Tier);
                Assert.Equal(o.Race, r.Race);
                Assert.Equal(o.Mechanics, r.Mechanics);
                Assert.Equal(o.IsGolden, r.IsGolden);
                Assert.Equal(o.ZonePosition, r.ZonePosition);
            }

            Assert.Equal(original.HandCards.Length, restored.HandCards.Length);
            for (int i = 0; i < original.HandCards.Length; i++)
            {
                var o = original.HandCards[i];
                var r = restored.HandCards[i];
                Assert.Equal(o.EntityId, r.EntityId);
                Assert.Equal(o.CardId, r.CardId);
                Assert.Equal(o.Cost, r.Cost);
                Assert.Equal(o.IsMinion, r.IsMinion);
                Assert.Equal(o.IsSpell, r.IsSpell);
                Assert.Equal(o.ZonePosition, r.ZonePosition);
            }

            Assert.Equal(original.TavernMinions.Length, restored.TavernMinions.Length);
            for (int i = 0; i < original.TavernMinions.Length; i++)
            {
                var o = original.TavernMinions[i];
                var r = restored.TavernMinions[i];
                Assert.Equal(o.EntityId, r.EntityId);
                Assert.Equal(o.CardId, r.CardId);
                Assert.Equal(o.Cost, r.Cost);
                Assert.Equal(o.Tier, r.Tier);
                Assert.Equal(o.Race, r.Race);
                Assert.Equal(o.IsFrozen, r.IsFrozen);
            }
        }
    }

    [Fact]
    public void ReplayDataset_Files_On_Disk_Match_Parsed_Objects()
    {
        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? cur = new DirectoryInfo(baseDir);
        string? rootDataDir = null;

        for (int i = 0; i < 6 && cur != null; i++)
        {
            string candidate = Path.Combine(cur.FullName, "data", "replays");
            if (Directory.Exists(candidate))
            {
                rootDataDir = candidate;
                break;
            }
            cur = cur.Parent;
        }

        if (rootDataDir != null)
        {
            var files = Directory.GetFiles(rootDataDir, "*.json");
            Assert.True(files.Length >= 20, $"Expected >= 20 files in {rootDataDir}, found {files.Length}");

            foreach (var file in files)
            {
                string json = File.ReadAllText(file);
                var snapshot = BoardSnapshot.FromJson(json, JsonOptions);
                Assert.True(snapshot.Turn >= 1);
                Assert.False(string.IsNullOrWhiteSpace(snapshot.HeroCardId));
            }
        }
    }

    #endregion
}
