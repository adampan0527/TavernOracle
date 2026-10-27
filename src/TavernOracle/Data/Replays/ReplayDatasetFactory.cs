using System.Collections.Immutable;
using TavernOracle.Data.State;
using TavernOracle.Priors;
using TavernOracle.Simulation;

namespace TavernOracle.Data.Replays;

/// <summary>
/// 离线盘面对局回放测试数据集标准工厂 (T06)。
/// 构建包含 20 组酒馆战棋典型战略场景的标准 BoardSnapshot 强类型对象集合：
/// 1. 场景 1~4：跳蛙野兽流转折点 (Leapfrogger Beast Transition)
/// 2. 场景 5~8：残血紧急苟活生死线 (Lethal Emergency Survival Line, HP <= 10, P_lethal >= 0.35)
/// 3. 场景 9~12：神仙决赛圈对决 (End-game God Tier Compositions, 全金狂战/圣盾烈毒/超大身材)
/// 4. 场景 13~16：高频 APM 转职期 (High APM Pivot Phases, 电表海盗/手牌鱼人/机械重置/诺米元素)
/// 5. 场景 17~20：复杂动作与边界空间 (Complex Operational States, 空场开局/多对子/10手牌溢出/10金币动作分支)
/// </summary>
public static class ReplayDatasetFactory
{
    public const string S01_LeapfroggerEarlyTransition = "01_leapfrogger_early_transition";
    public const string S02_LeapfroggerTitusFound = "02_leapfrogger_titus_found";
    public const string S03_LeapfroggerGoldenFrogReborn = "03_leapfrogger_golden_frog_reborn";
    public const string S04_LeapfroggerFullFormation = "04_leapfrogger_full_formation";

    public const string S05_LethalEmergencyHp7Turn8 = "05_lethal_emergency_hp7_turn8";
    public const string S06_LethalCrisisHp4Turn9 = "06_lethal_crisis_hp4_turn9";
    public const string S07_LethalBorderlineHp9Turn7 = "07_lethal_borderline_hp9_turn7";
    public const string S08_LethalSurvivalHp2LastStand = "08_lethal_survival_hp2_last_stand";

    public const string S09_FinalDualGoldenCleaveMech = "09_final_dual_golden_cleave_mech";
    public const string S10_FinalDivineVenomMurlocs = "10_final_divine_venom_murlocs";
    public const string S11_FinalGiantElementalStats = "11_final_giant_elemental_stats";
    public const string S12_FinalGiantDemonFelbat = "12_final_giant_demon_felbat";

    public const string S13_ApmPiratesHoggerTransition = "13_apm_pirates_hogger_transition";
    public const string S14_ApmMurlocHandBuffLoop = "14_apm_murloc_hand_buff_loop";
    public const string S15_ApmMechDeflectoResetPivot = "15_apm_mech_deflecto_reset_pivot";
    public const string S16_ApmElementalCyclingNomi = "16_apm_elemental_cycling_nomi";

    public const string S17_Turn1OpeningEmptyBoard = "17_turn1_opening_empty_board";
    public const string S18_MultiPairTripleHunt = "18_multi_pair_triple_hunt";
    public const string S19_FullHandOverflow10Cards = "19_full_hand_overflow_10_cards";
    public const string S20_Complex10GoldBranchingBudget = "20_complex_10_gold_branching_budget";

    /// <summary>
    /// 获取全部 20 组标准回放场景的有序名称列表
    /// </summary>
    public static readonly IReadOnlyList<string> AllScenarioNames = new[]
    {
        S01_LeapfroggerEarlyTransition,
        S02_LeapfroggerTitusFound,
        S03_LeapfroggerGoldenFrogReborn,
        S04_LeapfroggerFullFormation,
        S05_LethalEmergencyHp7Turn8,
        S06_LethalCrisisHp4Turn9,
        S07_LethalBorderlineHp9Turn7,
        S08_LethalSurvivalHp2LastStand,
        S09_FinalDualGoldenCleaveMech,
        S10_FinalDivineVenomMurlocs,
        S11_FinalGiantElementalStats,
        S12_FinalGiantDemonFelbat,
        S13_ApmPiratesHoggerTransition,
        S14_ApmMurlocHandBuffLoop,
        S15_ApmMechDeflectoResetPivot,
        S16_ApmElementalCyclingNomi,
        S17_Turn1OpeningEmptyBoard,
        S18_MultiPairTripleHunt,
        S19_FullHandOverflow10Cards,
        S20_Complex10GoldBranchingBudget
    };

    /// <summary>
    /// 构建全部 20 组标准回放场景的键值字典
    /// </summary>
    public static Dictionary<string, BoardSnapshot> CreateAll()
    {
        return new Dictionary<string, BoardSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            [S01_LeapfroggerEarlyTransition] = Create01LeapfroggerEarlyTransition(),
            [S02_LeapfroggerTitusFound] = Create02LeapfroggerTitusFound(),
            [S03_LeapfroggerGoldenFrogReborn] = Create03LeapfroggerGoldenFrogReborn(),
            [S04_LeapfroggerFullFormation] = Create04LeapfroggerFullFormation(),

            [S05_LethalEmergencyHp7Turn8] = Create05LethalEmergencyHp7Turn8(),
            [S06_LethalCrisisHp4Turn9] = Create06LethalCrisisHp4Turn9(),
            [S07_LethalBorderlineHp9Turn7] = Create07LethalBorderlineHp9Turn7(),
            [S08_LethalSurvivalHp2LastStand] = Create08LethalSurvivalHp2LastStand(),

            [S09_FinalDualGoldenCleaveMech] = Create09FinalDualGoldenCleaveMech(),
            [S10_FinalDivineVenomMurlocs] = Create10FinalDivineVenomMurlocs(),
            [S11_FinalGiantElementalStats] = Create11FinalGiantElementalStats(),
            [S12_FinalGiantDemonFelbat] = Create12FinalGiantDemonFelbat(),

            [S13_ApmPiratesHoggerTransition] = Create13ApmPiratesHoggerTransition(),
            [S14_ApmMurlocHandBuffLoop] = Create14ApmMurlocHandBuffLoop(),
            [S15_ApmMechDeflectoResetPivot] = Create15ApmMechDeflectoResetPivot(),
            [S16_ApmElementalCyclingNomi] = Create16ApmElementalCyclingNomi(),

            [S17_Turn1OpeningEmptyBoard] = Create17Turn1OpeningEmptyBoard(),
            [S18_MultiPairTripleHunt] = Create18MultiPairTripleHunt(),
            [S19_FullHandOverflow10Cards] = Create19FullHandOverflow10Cards(),
            [S20_Complex10GoldBranchingBudget] = Create20Complex10GoldBranchingBudget()
        };
    }

    #region 场景 1~4：跳蛙野兽流转折点

    public static BoardSnapshot Create01LeapfroggerEarlyTransition()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 0),
            new MinionEntity(102, "BG19_010", 1, 9, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 1),
            new MinionEntity(103, "BG26_805", 4, 1, 3, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 2),
            new MinionEntity(104, "BG20_001", 3, 4, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 3)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG21_000", 0, true, false, 0),
            new HandEntity(202, "BG_SPELL_01", 1, false, true, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BGS_078", 3, 4, MinionRaceMask.Beast, false),
            new TavernMinion(302, "BG21_010", 3, 3, MinionRaceMask.Beast, false),
            new TavernMinion(303, "BGS_071", 3, 3, MinionRaceMask.Mech, false),
            new TavernMinion(304, "BG21_012", 3, 2, MinionRaceMask.Pirate, false)
        );

        return new BoardSnapshot(
            turn: 6,
            heroCardId: "BG_HERO_102",
            health: 28,
            armor: 4,
            tavernTier: 3,
            upgradeCost: 6,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Naga | MinionRaceMask.Undead,
            gold: new GoldBudget(8, 8, 0),
            simulation: new CombatSimulationResult(0.55, 0.10, 0.35, 0.05, 2.1, 5000, false, "Transitioning into beasts"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create02LeapfroggerTitusFound()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_078", 5, 3, 4, MinionRaceMask.Beast, CardMechanicsMask.Reborn, false, 0),
            new MinionEntity(102, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Reborn | CardMechanicsMask.Deathrattle, false, 1),
            new MinionEntity(103, "BG19_010", 2, 10, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 2),
            new MinionEntity(104, "BG26_805", 5, 2, 3, MinionRaceMask.Beast, CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(105, "BG25_352", 1, 7, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 4)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG21_000", 0, true, false, 0),
            new HandEntity(202, "BG_SPELL_BUFF", 2, false, true, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BGS_078", 3, 4, MinionRaceMask.Beast, false),
            new TavernMinion(302, "BG26_355", 3, 4, MinionRaceMask.Beast, false),
            new TavernMinion(303, "BG21_016", 3, 4, MinionRaceMask.Pirate, false),
            new TavernMinion(304, "BG20_003", 3, 5, MinionRaceMask.Demon, false),
            new TavernMinion(305, "BG20_004", 3, 4, MinionRaceMask.Elemental, false),
            new TavernMinion(306, "BG20_005", 3, 3, MinionRaceMask.Murloc, false)
        );

        return new BoardSnapshot(
            turn: 8,
            heroCardId: "BG_HERO_102",
            health: 22,
            armor: 3,
            tavernTier: 5,
            upgradeCost: 9,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Elemental | MinionRaceMask.Pirate,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.65, 0.10, 0.25, 0.02, -3.2, 5000, false, "Titus Rivendare secured"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create03LeapfroggerGoldenFrogReborn()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_078", 7, 5, 4, MinionRaceMask.Beast, CardMechanicsMask.Reborn, false, 0),
            new MinionEntity(102, "BG21_000_G", 6, 6, 2, MinionRaceMask.Beast, CardMechanicsMask.Reborn | CardMechanicsMask.Deathrattle, true, 1),
            new MinionEntity(103, "BGS_078", 5, 3, 4, MinionRaceMask.Beast, CardMechanicsMask.Windfury, false, 2),
            new MinionEntity(104, "BG19_010", 2, 10, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(105, "BG26_355", 3, 6, 4, MinionRaceMask.Beast, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG25_352", 1, 7, 5, MinionRaceMask.Neutral, CardMechanicsMask.Stealth, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG25_352", 0, true, false, 0)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG25_352", 3, 5, MinionRaceMask.Neutral, true),
            new TavernMinion(302, "BGS_078", 3, 4, MinionRaceMask.Beast, true),
            new TavernMinion(303, "BG26_355", 3, 4, MinionRaceMask.Beast, true),
            new TavernMinion(304, "BG20_006", 3, 5, MinionRaceMask.Dragon, true),
            new TavernMinion(305, "BG20_007", 3, 4, MinionRaceMask.Mech, true),
            new TavernMinion(306, "BG20_008", 3, 3, MinionRaceMask.Undead, true)
        );

        return new BoardSnapshot(
            turn: 10,
            heroCardId: "BG_HERO_102",
            health: 18,
            armor: 2,
            tavernTier: 5,
            upgradeCost: 8,
            isFrozen: true,
            bannedRaces: MinionRaceMask.Dragon | MinionRaceMask.Quilboar,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.78, 0.07, 0.15, 0.01, -6.5, 5000, false, "Golden frog and frozen 2nd Titus in shop"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create04LeapfroggerFullFormation()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_078_G", 10, 6, 4, MinionRaceMask.Beast, CardMechanicsMask.Reborn, true, 0),
            new MinionEntity(102, "BG21_000_G", 8, 8, 2, MinionRaceMask.Beast, CardMechanicsMask.Reborn | CardMechanicsMask.Deathrattle, true, 1),
            new MinionEntity(103, "BGS_078", 7, 5, 4, MinionRaceMask.Beast, CardMechanicsMask.Reborn, false, 2),
            new MinionEntity(104, "BG19_010", 4, 12, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(105, "BG26_355_G", 6, 12, 4, MinionRaceMask.Beast, CardMechanicsMask.None, true, 4),
            new MinionEntity(106, "BG25_352_G", 2, 14, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, true, 5),
            new MinionEntity(107, "BG25_352", 1, 7, 5, MinionRaceMask.Neutral, CardMechanicsMask.Stealth, false, 6)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_SHIELD", 2, false, true, 0),
            new HandEntity(202, "BG_SPELL_STATS", 1, false, true, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG20_010", 3, 6, MinionRaceMask.Beast, false),
            new TavernMinion(302, "BG20_011", 3, 6, MinionRaceMask.Neutral, false),
            new TavernMinion(303, "BG20_012", 3, 5, MinionRaceMask.Mech, false),
            new TavernMinion(304, "BG20_013", 3, 5, MinionRaceMask.Pirate, false),
            new TavernMinion(305, "BG20_014", 3, 6, MinionRaceMask.Dragon, false),
            new TavernMinion(306, "BG20_015", 3, 6, MinionRaceMask.Elemental, false)
        );

        return new BoardSnapshot(
            turn: 12,
            heroCardId: "BG_HERO_102",
            health: 15,
            armor: 0,
            tavernTier: 6,
            upgradeCost: 0,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Demon | MinionRaceMask.Murloc,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.90, 0.05, 0.05, 0.00, -12.4, 8000, false, "Full formation leapfrogger beast juggernaut"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    #endregion

    #region 场景 5~8：残血紧急苟活生死线 (HP <= 10, P_lethal >= 0.35)

    public static BoardSnapshot Create05LethalEmergencyHp7Turn8()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_BOT_01", 6, 5, 3, MinionRaceMask.Mech, CardMechanicsMask.None, false, 0),
            new MinionEntity(102, "BG_PIR_01", 7, 6, 3, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 1),
            new MinionEntity(103, "BG_DEM_01", 8, 8, 3, MinionRaceMask.Demon, CardMechanicsMask.Taunt, false, 2),
            new MinionEntity(104, "BG_ELE_01", 5, 5, 2, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_BEA_01", 4, 4, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_MUR_01", 3, 3, 1, MinionRaceMask.Murloc, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_COIN", 1, false, true, 0)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "GVG_100", 3, 4, MinionRaceMask.Mech, false),
            new TavernMinion(302, "BGS_071", 3, 3, MinionRaceMask.Mech, false),
            new TavernMinion(303, "BG20_021", 3, 4, MinionRaceMask.Demon, false),
            new TavernMinion(304, "BG20_022", 3, 3, MinionRaceMask.Pirate, false),
            new TavernMinion(305, "BG20_023", 3, 4, MinionRaceMask.Beast, false),
            new TavernMinion(306, "BG20_024", 3, 2, MinionRaceMask.Neutral, false)
        );

        return new BoardSnapshot(
            turn: 8,
            heroCardId: "BG_HERO_105",
            health: 7,
            armor: 0,
            tavernTier: 4,
            upgradeCost: 7,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Naga | MinionRaceMask.Quilboar,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.25, 0.15, 0.60, 0.42, 7.2, 5000, false, "Emergency: HP 7, P_lethal 0.42 >= 0.35, cut tier-up greed"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create06LethalCrisisHp4Turn9()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_BOT_02", 7, 6, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BG_PIR_02", 8, 7, 3, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 1),
            new MinionEntity(103, "BG_DEM_02", 6, 9, 3, MinionRaceMask.Demon, CardMechanicsMask.Taunt, false, 2),
            new MinionEntity(104, "BG_ELE_02", 4, 4, 2, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_ECON_TOKEN", 2, 2, 1, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_BEA_02", 5, 4, 2, MinionRaceMask.Beast, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_PIR_SWABBY", 0, true, false, 0),
            new HandEntity(202, "BG_SPELL_ARMOR", 2, false, true, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_MUR_VENOM", 3, 4, MinionRaceMask.Murloc, false),
            new TavernMinion(302, "BG_TAUNT_DIVINE", 3, 4, MinionRaceMask.Neutral, false),
            new TavernMinion(303, "BG20_031", 3, 4, MinionRaceMask.Mech, false),
            new TavernMinion(304, "BG20_032", 3, 3, MinionRaceMask.Pirate, false),
            new TavernMinion(305, "BG20_033", 3, 4, MinionRaceMask.Beast, false),
            new TavernMinion(306, "BG20_034", 3, 2, MinionRaceMask.Quilboar, false)
        );

        return new BoardSnapshot(
            turn: 9,
            heroCardId: "BG_HERO_106",
            health: 4,
            armor: 0,
            tavernTier: 4,
            upgradeCost: 6,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Elemental | MinionRaceMask.Dragon,
            gold: new GoldBudget(8, 10, 0),
            simulation: new CombatSimulationResult(0.18, 0.12, 0.70, 0.58, 9.5, 5000, false, "Critical Crisis: HP 4, P_lethal 0.58, sell econ and buy survival"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create07LethalBorderlineHp9Turn7()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_TEMPO_1", 5, 4, 2, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BG_TEMPO_2", 4, 6, 2, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 1),
            new MinionEntity(103, "BG_TEMPO_3", 6, 5, 3, MinionRaceMask.Quilboar, CardMechanicsMask.Taunt, false, 2),
            new MinionEntity(104, "BG_TEMPO_4", 3, 3, 1, MinionRaceMask.Demon, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_TEMPO_5", 4, 3, 2, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 4)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_BUFF_TEMPO", 1, false, true, 0)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BGS_071", 3, 3, MinionRaceMask.Mech, false),
            new TavernMinion(302, "BG19_010", 3, 3, MinionRaceMask.Beast, false),
            new TavernMinion(303, "BG20_041", 3, 3, MinionRaceMask.Pirate, false),
            new TavernMinion(304, "BG20_042", 3, 2, MinionRaceMask.Murloc, false),
            new TavernMinion(305, "BG20_043", 3, 2, MinionRaceMask.Quilboar, false)
        );

        return new BoardSnapshot(
            turn: 7,
            heroCardId: "BG_HERO_107",
            health: 9,
            armor: 0,
            tavernTier: 3,
            upgradeCost: 5,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Beast | MinionRaceMask.Undead,
            gold: new GoldBudget(9, 9, 0),
            simulation: new CombatSimulationResult(0.30, 0.15, 0.55, 0.38, 6.8, 5000, false, "Borderline survival: HP 9, P_lethal 0.38, upgrade vs roll survival"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create08LethalSurvivalHp2LastStand()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_STAND_1", 10, 8, 4, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BG_STAND_2", 8, 12, 4, MinionRaceMask.Demon, CardMechanicsMask.Taunt, false, 1),
            new MinionEntity(103, "BG_STAND_3", 7, 7, 3, MinionRaceMask.Pirate, CardMechanicsMask.Windfury, false, 2),
            new MinionEntity(104, "BG_STAND_4", 4, 4, 2, MinionRaceMask.Murloc, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_STAND_5", 3, 3, 1, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_STAND_6", 5, 6, 3, MinionRaceMask.Beast, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_STAND_TOKEN", 0, true, false, 0)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_VENOM_CLEAVE", 3, 5, MinionRaceMask.Neutral, false),
            new TavernMinion(302, "BG_DIVINE_SHIELD_TAUNT", 3, 5, MinionRaceMask.Mech, false),
            new TavernMinion(303, "BG20_051", 3, 5, MinionRaceMask.Demon, false),
            new TavernMinion(304, "BG20_052", 3, 5, MinionRaceMask.Dragon, false),
            new TavernMinion(305, "BG20_053", 3, 4, MinionRaceMask.Elemental, false),
            new TavernMinion(306, "BG20_054", 3, 4, MinionRaceMask.Quilboar, false)
        );

        return new BoardSnapshot(
            turn: 11,
            heroCardId: "BG_HERO_108",
            health: 2,
            armor: 0,
            tavernTier: 5,
            upgradeCost: 9,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Mech | MinionRaceMask.Pirate,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.12, 0.08, 0.80, 0.72, 12.0, 8000, false, "Last Stand: HP 2, P_lethal 0.72 >= 0.35, all-in tempo survival"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    #endregion

    #region 场景 9~12：神仙决赛圈对决

    public static BoardSnapshot Create09FinalDualGoldenCleaveMech()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG21_001_G", 65, 65, 6, MinionRaceMask.Mech, CardMechanicsMask.Cleave | CardMechanicsMask.Windfury | CardMechanicsMask.DivineShield, true, 0),
            new MinionEntity(102, "BGS_071_G", 50, 40, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, true, 1),
            new MinionEntity(103, "BG_BOT_911_G", 80, 80, 4, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, true, 2),
            new MinionEntity(104, "BGS_043_G", 30, 30, 3, MinionRaceMask.Mech, CardMechanicsMask.Reborn | CardMechanicsMask.Deathrattle, true, 3),
            new MinionEntity(105, "BG26_160_G", 45, 45, 4, MinionRaceMask.Mech, CardMechanicsMask.Deathrattle, true, 4),
            new MinionEntity(106, "BG26_ICC_029_G", 15, 30, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, true, 5),
            new MinionEntity(107, "BG25_352_G", 2, 14, 5, MinionRaceMask.Neutral, CardMechanicsMask.Stealth, true, 6)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_BOT_MAGNETIC", 0, true, false, 0),
            new HandEntity(202, "BG_SPELL_BUFF_MECH", 2, false, true, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG20_061", 3, 6, MinionRaceMask.Mech, false),
            new TavernMinion(302, "BG20_062", 3, 6, MinionRaceMask.Neutral, false),
            new TavernMinion(303, "BG20_063", 3, 5, MinionRaceMask.Dragon, false),
            new TavernMinion(304, "BG20_064", 3, 5, MinionRaceMask.Pirate, false),
            new TavernMinion(305, "BG20_065", 3, 6, MinionRaceMask.Quilboar, false),
            new TavernMinion(306, "BG20_066", 3, 6, MinionRaceMask.Beast, false)
        );

        return new BoardSnapshot(
            turn: 15,
            heroCardId: "BG_HERO_109",
            health: 32,
            armor: 5,
            tavernTier: 6,
            upgradeCost: 0,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Murloc | MinionRaceMask.Elemental,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.85, 0.10, 0.05, 0.00, -14.0, 10000, false, "Finals: Full Golden Cleave Mechs"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create10FinalDivineVenomMurlocs()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_MUR_VENOM_1", 45, 45, 5, MinionRaceMask.Murloc, CardMechanicsMask.DivineShield | CardMechanicsMask.Venomous | CardMechanicsMask.Windfury, false, 0),
            new MinionEntity(102, "BG_MUR_VENOM_2", 30, 30, 4, MinionRaceMask.Murloc, CardMechanicsMask.DivineShield | CardMechanicsMask.Venomous, false, 1),
            new MinionEntity(103, "BG26_354", 55, 60, 4, MinionRaceMask.Murloc, CardMechanicsMask.Venomous, false, 2),
            new MinionEntity(104, "BG26_357_G", 95, 95, 5, MinionRaceMask.Murloc, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield, true, 3),
            new MinionEntity(105, "BG26_350_G", 40, 40, 4, MinionRaceMask.Murloc, CardMechanicsMask.Deathrattle, true, 4),
            new MinionEntity(106, "BG_LOE_077_G", 4, 8, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, true, 5),
            new MinionEntity(107, "BG_MUR_VENOM_TAUNT", 35, 35, 4, MinionRaceMask.Murloc, CardMechanicsMask.Venomous | CardMechanicsMask.Taunt, false, 6)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_MUR_HAND_BUFFED_1", 0, true, false, 0),
            new HandEntity(202, "BG_MUR_HAND_BUFFED_2", 0, true, false, 1),
            new HandEntity(203, "BG_MUR_HAND_BUFFED_3", 0, true, false, 2)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG20_071", 3, 6, MinionRaceMask.Murloc, false),
            new TavernMinion(302, "BG20_072", 3, 6, MinionRaceMask.Neutral, false),
            new TavernMinion(303, "BG20_073", 3, 5, MinionRaceMask.Dragon, false),
            new TavernMinion(304, "BG20_074", 3, 5, MinionRaceMask.Beast, false),
            new TavernMinion(305, "BG20_075", 3, 6, MinionRaceMask.Pirate, false),
            new TavernMinion(306, "BG20_076", 3, 6, MinionRaceMask.Elemental, false)
        );

        return new BoardSnapshot(
            turn: 16,
            heroCardId: "BG_HERO_110",
            health: 28,
            armor: 0,
            tavernTier: 6,
            upgradeCost: 0,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Undead | MinionRaceMask.Demon,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.88, 0.08, 0.04, 0.00, -15.5, 10000, false, "Finals: Divine Shield Venomous Murlocs"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create11FinalGiantElementalStats()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_DAL_742_G", 180, 180, 3, MinionRaceMask.Elemental, CardMechanicsMask.MegaWindfury | CardMechanicsMask.DivineShield, true, 0),
            new MinionEntity(102, "BG_BOT_451_G", 140, 140, 3, MinionRaceMask.Elemental, CardMechanicsMask.DivineShield | CardMechanicsMask.Windfury, true, 1),
            new MinionEntity(103, "BG21_016_G", 240, 240, 5, MinionRaceMask.Elemental, CardMechanicsMask.Taunt, true, 2),
            new MinionEntity(104, "BG26_166", 130, 130, 4, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BGS_046_G", 40, 40, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, true, 4),
            new MinionEntity(106, "BG21_016", 160, 160, 5, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 5),
            new MinionEntity(107, "BG_ELE_SHIELD_TAUNT", 170, 170, 4, MinionRaceMask.Elemental, CardMechanicsMask.DivineShield | CardMechanicsMask.Taunt, false, 6)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_ELE_BUFF", 2, false, true, 0),
            new HandEntity(202, "BG_ELE_MINION", 0, true, false, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG20_081", 3, 6, MinionRaceMask.Elemental, false),
            new TavernMinion(302, "BG20_082", 3, 6, MinionRaceMask.Elemental, false),
            new TavernMinion(303, "BG20_083", 3, 5, MinionRaceMask.Neutral, false),
            new TavernMinion(304, "BG20_084", 3, 5, MinionRaceMask.Mech, false),
            new TavernMinion(305, "BG20_085", 3, 6, MinionRaceMask.Pirate, false),
            new TavernMinion(306, "BG20_086", 3, 6, MinionRaceMask.Dragon, false)
        );

        return new BoardSnapshot(
            turn: 14,
            heroCardId: "BG_HERO_111",
            health: 24,
            armor: 0,
            tavernTier: 6,
            upgradeCost: 0,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Beast | MinionRaceMask.Naga,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.82, 0.12, 0.06, 0.00, -13.2, 10000, false, "Finals: 150/150+ Giant Elementals"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create12FinalGiantDemonFelbat()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG21_004_G", 280, 280, 5, MinionRaceMask.Demon, CardMechanicsMask.Taunt, true, 0),
            new MinionEntity(102, "BGS_004_G", 210, 210, 1, MinionRaceMask.Neutral, CardMechanicsMask.None, true, 1),
            new MinionEntity(103, "BG21_027_G", 130, 130, 6, MinionRaceMask.Demon, CardMechanicsMask.None, true, 2),
            new MinionEntity(104, "BG21_027", 85, 85, 6, MinionRaceMask.Demon, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG26_174", 65, 65, 5, MinionRaceMask.Demon, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG26_ICC_029_G", 15, 30, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, true, 5),
            new MinionEntity(107, "BG_DEM_VOIDLORD", 80, 130, 5, MinionRaceMask.Demon, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 6)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_DEMON_EAT", 2, false, true, 0),
            new HandEntity(202, "BG_DEM_MINION", 0, true, false, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG20_091", 3, 6, MinionRaceMask.Demon, false),
            new TavernMinion(302, "BG20_092", 3, 6, MinionRaceMask.Demon, false),
            new TavernMinion(303, "BG20_093", 3, 5, MinionRaceMask.Neutral, false),
            new TavernMinion(304, "BG20_094", 3, 5, MinionRaceMask.Beast, false),
            new TavernMinion(305, "BG20_095", 3, 6, MinionRaceMask.Mech, false),
            new TavernMinion(306, "BG20_096", 3, 6, MinionRaceMask.Murloc, false)
        );

        return new BoardSnapshot(
            turn: 15,
            heroCardId: "BG_HERO_112",
            health: 18,
            armor: 0,
            tavernTier: 6,
            upgradeCost: 0,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Dragon | MinionRaceMask.Pirate,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.84, 0.10, 0.06, 0.00, -13.8, 10000, false, "Finals: 200/200+ Giant Demons Felbat Devour"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    #endregion

    #region 场景 13~16：高频 APM 转职期

    public static BoardSnapshot Create13ApmPiratesHoggerTransition()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG26_525", 14, 10, 4, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 0),
            new MinionEntity(102, "BG21_016", 10, 12, 4, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 1),
            new MinionEntity(103, "BG26_524", 8, 14, 5, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 2),
            new MinionEntity(104, "BGS_046", 8, 8, 5, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BGS_046", 8, 8, 5, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_PIR_03", 6, 7, 3, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_PIR_HAND_1", 0, true, false, 0),
            new HandEntity(202, "BG_PIR_HAND_2", 0, true, false, 1),
            new HandEntity(203, "BG_PIR_HAND_3", 0, true, false, 2),
            new HandEntity(204, "BG_SPELL_GOLD", 1, false, true, 3)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_PIR_SHOP_1", 3, 2, MinionRaceMask.Pirate, false),
            new TavernMinion(302, "BG_PIR_SHOP_2", 3, 3, MinionRaceMask.Pirate, false),
            new TavernMinion(303, "BG_PIR_SHOP_3", 3, 4, MinionRaceMask.Pirate, false),
            new TavernMinion(304, "BG20_101", 3, 4, MinionRaceMask.Neutral, false),
            new TavernMinion(305, "BG20_102", 3, 5, MinionRaceMask.Dragon, false),
            new TavernMinion(306, "BG20_103", 3, 5, MinionRaceMask.Elemental, false)
        );

        return new BoardSnapshot(
            turn: 9,
            heroCardId: "BG_HERO_113",
            health: 24,
            armor: 2,
            tavernTier: 5,
            upgradeCost: 9,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Beast | MinionRaceMask.Murloc,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.58, 0.12, 0.30, 0.04, -2.0, 5000, false, "APM Pirates: Double Hogger infinite econ engine"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create14ApmMurlocHandBuffLoop()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_LOE_077", 2, 4, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 0),
            new MinionEntity(102, "BG26_354", 20, 22, 4, MinionRaceMask.Murloc, CardMechanicsMask.Venomous, false, 1),
            new MinionEntity(103, "BG26_357", 30, 30, 5, MinionRaceMask.Murloc, CardMechanicsMask.Taunt, false, 2),
            new MinionEntity(104, "BG26_350", 16, 18, 4, MinionRaceMask.Murloc, CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(105, "UNG_073", 14, 14, 4, MinionRaceMask.Murloc, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_MUR_BAGURGLE", 12, 12, 5, MinionRaceMask.Murloc, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_MUR_GIANT_HAND", 0, true, false, 0),
            new HandEntity(202, "BG_MUR_BATTLECRY_1", 0, true, false, 1),
            new HandEntity(203, "BG_MUR_BATTLECRY_2", 0, true, false, 2),
            new HandEntity(204, "BG_SPELL_DISCOVER", 2, false, true, 3),
            new HandEntity(205, "BG_SPELL_STATS", 1, false, true, 4)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "UNG_073", 3, 4, MinionRaceMask.Murloc, false),
            new TavernMinion(302, "BG26_354", 3, 4, MinionRaceMask.Murloc, false),
            new TavernMinion(303, "BG20_111", 3, 5, MinionRaceMask.Neutral, false),
            new TavernMinion(304, "BG20_112", 3, 5, MinionRaceMask.Pirate, false),
            new TavernMinion(305, "BG20_113", 3, 4, MinionRaceMask.Dragon, false),
            new TavernMinion(306, "BG20_114", 3, 3, MinionRaceMask.Elemental, false)
        );

        return new BoardSnapshot(
            turn: 10,
            heroCardId: "BG_HERO_114",
            health: 20,
            armor: 0,
            tavernTier: 5,
            upgradeCost: 8,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Demon | MinionRaceMask.Mech,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.62, 0.10, 0.28, 0.03, -3.5, 5000, false, "APM Murlocs: Hand buffing and summon engine"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create15ApmMechDeflectoResetPivot()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_071", 14, 8, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BGS_071", 12, 8, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 1),
            new MinionEntity(103, "BG26_160", 8, 8, 4, MinionRaceMask.Mech, CardMechanicsMask.Deathrattle, false, 2),
            new MinionEntity(104, "BG26_154", 4, 10, 4, MinionRaceMask.Mech, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_BOT_911", 6, 10, 4, MinionRaceMask.Mech, CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield | CardMechanicsMask.Magnetic, false, 4),
            new MinionEntity(106, "BGS_043", 6, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.Reborn | CardMechanicsMask.Deathrattle, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_BOT_911", 0, true, false, 0),
            new HandEntity(202, "BG_BOT_MODULE", 0, true, false, 1),
            new HandEntity(203, "BG_SPELL_GOLD", 1, false, true, 2)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_BOT_911", 3, 4, MinionRaceMask.Mech, false),
            new TavernMinion(302, "GVG_100", 3, 4, MinionRaceMask.Mech, false),
            new TavernMinion(303, "BGS_071", 3, 3, MinionRaceMask.Mech, false),
            new TavernMinion(304, "BG20_121", 3, 4, MinionRaceMask.Pirate, false),
            new TavernMinion(305, "BG20_122", 3, 3, MinionRaceMask.Quilboar, false)
        );

        return new BoardSnapshot(
            turn: 8,
            heroCardId: "BG_HERO_115",
            health: 26,
            armor: 3,
            tavernTier: 4,
            upgradeCost: 7,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Dragon | MinionRaceMask.Undead,
            gold: new GoldBudget(9, 10, 0),
            simulation: new CombatSimulationResult(0.66, 0.12, 0.22, 0.02, -4.0, 5000, false, "APM Mechs: Dual Deflect-o reset loop"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create16ApmElementalCyclingNomi()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_046", 4, 4, 5, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 0),
            new MinionEntity(102, "BG_BOT_451", 7, 4, 3, MinionRaceMask.Elemental, CardMechanicsMask.DivineShield | CardMechanicsMask.Windfury, false, 1),
            new MinionEntity(103, "BG_DAL_742", 5, 5, 3, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 2),
            new MinionEntity(104, "BG26_167", 4, 5, 2, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_TEMPO_MINION", 6, 6, 3, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 4)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_ELE_TOKEN_1", 0, true, false, 0),
            new HandEntity(202, "BG_ELE_TOKEN_2", 0, true, false, 1),
            new HandEntity(203, "BG_ELE_TOKEN_3", 0, true, false, 2),
            new HandEntity(204, "BG_SPELL_ROLL", 1, false, true, 3)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_ELE_SHOP_1", 3, 2, MinionRaceMask.Elemental, false),
            new TavernMinion(302, "BG_ELE_SHOP_2", 3, 3, MinionRaceMask.Elemental, false),
            new TavernMinion(303, "BG_ELE_SHOP_3", 3, 4, MinionRaceMask.Elemental, false),
            new TavernMinion(304, "BG20_131", 3, 4, MinionRaceMask.Mech, false),
            new TavernMinion(305, "BG20_132", 3, 3, MinionRaceMask.Demon, false)
        );

        return new BoardSnapshot(
            turn: 7,
            heroCardId: "BG_HERO_116",
            health: 25,
            armor: 4,
            tavernTier: 4,
            upgradeCost: 7,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Pirate | MinionRaceMask.Quilboar,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.50, 0.15, 0.35, 0.06, 0.5, 5000, false, "APM Elementals: Nomi cycling ramp phase"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    #endregion

    #region 场景 17~20：复杂动作与边界空间

    public static BoardSnapshot Create17Turn1OpeningEmptyBoard()
    {
        var board = ImmutableArray<MinionEntity>.Empty;
        var hand = ImmutableArray<HandEntity>.Empty;

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG_MUR_TIDEHUNTER", 3, 1, MinionRaceMask.Murloc, false),
            new TavernMinion(302, "BG_PIR_SWABBY", 3, 1, MinionRaceMask.Pirate, false),
            new TavernMinion(303, "BG_BEA_ALLEYCAT", 3, 1, MinionRaceMask.Beast, false)
        );

        return new BoardSnapshot(
            turn: 1,
            heroCardId: "BG_HERO_101",
            health: 30,
            armor: 8,
            tavernTier: 1,
            upgradeCost: 5,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Undead | MinionRaceMask.Naga,
            gold: new GoldBudget(3, 3, 0),
            simulation: CombatSimulationResult.CreateDefaultSafePrior(),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create18MultiPairTripleHunt()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_071", 3, 2, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BGS_071", 3, 2, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 1),
            new MinionEntity(103, "BG19_010", 1, 9, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 2),
            new MinionEntity(104, "BG19_010", 1, 9, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(105, "BGS_078", 5, 3, 4, MinionRaceMask.Beast, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BGS_078", 5, 3, 4, MinionRaceMask.Beast, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_COIN", 1, false, true, 0)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BGS_071", 3, 3, MinionRaceMask.Mech, false),
            new TavernMinion(302, "BG19_010", 3, 3, MinionRaceMask.Beast, false),
            new TavernMinion(303, "BG20_141", 3, 3, MinionRaceMask.Pirate, false),
            new TavernMinion(304, "BG20_142", 3, 3, MinionRaceMask.Demon, false),
            new TavernMinion(305, "BG20_143", 3, 2, MinionRaceMask.Neutral, false)
        );

        return new BoardSnapshot(
            turn: 7,
            heroCardId: "BG_HERO_102",
            health: 26,
            armor: 3,
            tavernTier: 3,
            upgradeCost: 6,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Demon | MinionRaceMask.Dragon,
            gold: new GoldBudget(9, 9, 0),
            simulation: new CombatSimulationResult(0.52, 0.15, 0.33, 0.04, -1.2, 5000, false, "3 pairs on board, triples in tavern!"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create19FullHandOverflow10Cards()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BG_BOARD_1", 12, 10, 4, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BG_BOARD_2", 8, 14, 4, MinionRaceMask.Demon, CardMechanicsMask.Taunt, false, 1),
            new MinionEntity(103, "BG_BOARD_3", 10, 10, 4, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 2),
            new MinionEntity(104, "BG_BOARD_4", 9, 9, 3, MinionRaceMask.Elemental, CardMechanicsMask.None, false, 3),
            new MinionEntity(105, "BG_BOARD_5", 7, 8, 3, MinionRaceMask.Beast, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_BOARD_6", 6, 6, 2, MinionRaceMask.Quilboar, CardMechanicsMask.None, false, 5),
            new MinionEntity(107, "BG_BOARD_7", 5, 5, 2, MinionRaceMask.Murloc, CardMechanicsMask.None, false, 6)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_01", 2, false, true, 0),
            new HandEntity(202, "BG_SPELL_02", 1, false, true, 1),
            new HandEntity(203, "BG_SPELL_03", 2, false, true, 2),
            new HandEntity(204, "BG_SPELL_BANANA_1", 0, false, true, 3),
            new HandEntity(205, "BG_SPELL_BANANA_2", 0, false, true, 4),
            new HandEntity(206, "BG_BOT_MAGNETIC_1", 0, true, false, 5),
            new HandEntity(207, "BG_BOT_MAGNETIC_2", 0, true, false, 6),
            new HandEntity(208, "BG_DISCOVER_TRIPLE", 0, false, true, 7),
            new HandEntity(209, "BG_BATTLECRY_01", 0, true, false, 8),
            new HandEntity(210, "BG_BATTLECRY_02", 0, true, false, 9)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BG20_151", 3, 5, MinionRaceMask.Mech, false),
            new TavernMinion(302, "BG20_152", 3, 5, MinionRaceMask.Demon, false),
            new TavernMinion(303, "BG20_153", 3, 5, MinionRaceMask.Pirate, false),
            new TavernMinion(304, "BG20_154", 3, 4, MinionRaceMask.Dragon, false),
            new TavernMinion(305, "BG20_155", 3, 4, MinionRaceMask.Elemental, false),
            new TavernMinion(306, "BG20_156", 3, 3, MinionRaceMask.Quilboar, false)
        );

        return new BoardSnapshot(
            turn: 10,
            heroCardId: "BG_HERO_103",
            health: 21,
            armor: 0,
            tavernTier: 5,
            upgradeCost: 8,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Naga | MinionRaceMask.Pirate,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.60, 0.10, 0.30, 0.03, -2.8, 5000, false, "Full hand 10/10 cards overflow constraint"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    public static BoardSnapshot Create20Complex10GoldBranchingBudget()
    {
        var board = ImmutableArray.Create(
            new MinionEntity(101, "BGS_071", 5, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 0),
            new MinionEntity(102, "BGS_071", 4, 3, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 1),
            new MinionEntity(103, "BG_PIR_SWABBY", 2, 2, 1, MinionRaceMask.Pirate, CardMechanicsMask.None, false, 2),
            new MinionEntity(104, "BG19_010", 1, 9, 3, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Deathrattle, false, 3),
            new MinionEntity(105, "BG_TEMPO_STRONG", 8, 7, 3, MinionRaceMask.Neutral, CardMechanicsMask.None, false, 4),
            new MinionEntity(106, "BG_QUIL_BATTLECRY", 4, 4, 2, MinionRaceMask.Quilboar, CardMechanicsMask.None, false, 5)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(201, "BG_SPELL_GOLD", 1, false, true, 0),
            new HandEntity(202, "BG_SIGNPOST_MINION", 0, true, false, 1),
            new HandEntity(203, "BG_BATTLECRY_BUFF", 0, true, false, 2),
            new HandEntity(204, "BG_TEMPO_PAIR", 0, true, false, 3)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(301, "BGS_071", 3, 3, MinionRaceMask.Mech, false),
            new TavernMinion(302, "GVG_100", 3, 4, MinionRaceMask.Mech, false),
            new TavernMinion(303, "BG26_355", 3, 4, MinionRaceMask.Beast, false),
            new TavernMinion(304, "BG20_161", 3, 4, MinionRaceMask.Demon, false),
            new TavernMinion(305, "BG20_162", 3, 3, MinionRaceMask.Dragon, false),
            new TavernMinion(306, "BG20_163", 3, 3, MinionRaceMask.Pirate, false)
        );

        return new BoardSnapshot(
            turn: 9,
            heroCardId: "BG_HERO_104",
            health: 24,
            armor: 2,
            tavernTier: 4,
            upgradeCost: 8,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Elemental | MinionRaceMask.Undead,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.48, 0.14, 0.38, 0.06, 0.2, 5000, false, "Massive 10 gold branching decision search space"),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );
    }

    #endregion
}
