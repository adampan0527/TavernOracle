using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using TavernOracle.Data;
using TavernOracle.Data.State;

namespace TavernOracle.Decision.Positioning;

/// <summary>
/// 随从站位排列启发式排序器。
/// 严格遵循暴雪“纸笔准则”与零 GC 极低时延规范 (&lt; 0.5ms)。
/// 基于天梯万分级选手站位法则与决策引擎规范 (docs/decision_engine.md)：
/// 1. 先手主力输出位 (1~2 位)：圣盾狂战斧 (死神4000型、洞穴多头蛇) 或高攻破盾怪/风怒怪排在首位，最大化先手白吃与破盾收益；
/// 2. 常规战斗随从位 (3~4 位)：非关键亡语随从、中等战力随从；
/// 3. 亡语发动机位 (5 位)：产怪亡语随从，留出右侧空间接收衍生物；
/// 4. 防狂战与嘲讽隔离位 (6~7 位)：
///    - 嘲讽圣盾怪靠右侧部署 (将伤害吸引至边角，防狂战斧溅射波及两翼核心)；
///    - 核心发动机 (瑞文戴尔 Titus/Baron、布莱恩·铜须、达卡莱等) 置于最右侧末位 (第 7 席位)，防狂战波及，确保持续生效；
///    - 嘲讽随从与核心发动机之间必须有防狂战隔离墙 (低价值随从垫底)。
/// </summary>
public sealed class MinionPositionOptimizer : IMinionPositionOptimizer
{
    /// <summary>
    /// 全局默认单例实例 (便于零开销直接调用)。
    /// </summary>
    public static MinionPositionOptimizer Instance { get; } = new();

    #region 静态卡牌特征图谱 (FrozenSet O(1) 检索，零堆内存分配)

    private static readonly FrozenSet<string> s_cleaveSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "GVG_113", "TB_BaconUps_153", "BG_GVG_113",  // 死神4000型 (Foe Reaper 4000)
        "LOOT_078", "TB_BaconUps_043", "BG_LOOT_078", // 洞穴多头蛇 (Cave Hydra)
        "BG26_817", "BG26_817_G",                    // 刃式收集者 (Blade Collector)
        "BG23_362", "BG23_362_G"                     // 艾卓曼德斯 (Atramedes)
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_engineSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BG25_354", "BG25_354_G",                    // 泰特斯·瑞文戴尔 (Titus Rivendare)
        "FP1_031",                                   // 经典瑞文戴尔 (Baron Rivendare)
        "BG_LOE_077", "LOE_077", "TB_BaconUps_045",  // 布莱恩·铜须 (Brann Bronzebeard)
        "ICC_901", "BG_ICC_901", "TB_BaconUps_105",  // 达卡莱附魔师 (Drakkari Enchanter)
        "TTN_714", "BG_TTN_714",                     // 莫伊拉·铜须 (Moira Bronzebeard)
        "BGS_038", "TB_BaconUps_041"                 // 卡德加 (Khadgar)
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_buffDeathrattleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BG21_000", "BG21_000_G",                    // 跳蛙骑士 (Leapfrogger)
        "BGS_039", "TB_BaconUps_056",                // 恩佐斯的子嗣 (Spawn of N'Zoth)
        "CS2_032", "BG24_708"                        // 无私的英雄 (Selfless Hero)
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_tokenSpawnerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BGS_014", "TB_BaconUps_113",                // 阴森巨蟒 (Ghastcoiler)
        "BOT_533", "TB_BaconUps_071",                // 机械蛋 (Mechano-Egg)
        "BG20_303",                                  // 下水道老鼠 (Sewer Rat)
        "CFM_315",                                   // 鼠群 (Rat Pack)
        "CS2_065",                                   // 虚空领主 (Voidlord)
        "BG21_013",                                  // 小鬼妈妈 (Imp Mama)
        "EX1_538",                                   // 萨凡纳高岭猎怪 (Savannah Highmane)
        "BG26_147"                                   // 狡黠迅猛龙 (Sly Raptor)
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 静态战术说明文案常量 (Interned String Literals, 零堆内存开销)

    public const string ReasonCleaveEngineWithBumper =
        "先手圣盾狂战抢攻，嘲讽靠右并由防狂战隔离墙护航右侧核心发动机";

    public const string ReasonCleaveTauntCorner =
        "先手圣盾狂战抢攻，嘲讽靠右沉底吸引伤害以最小化狂战溅射";

    public const string ReasonCleaveEngineProtected =
        "先手圣盾狂战抢攻破盾，后排核心发动机沉底最右侧确保持续生效";

    public const string ReasonCleavePriority =
        "先手圣盾狂战排在首位，最大化先手白吃与破盾收益";

    public const string ReasonEngineTauntWithBumper =
        "先手主力抢攻破盾，嘲讽靠右并由防狂战隔离墙护航右侧核心发动机";

    public const string ReasonEngineProtected =
        "后排核心发动机沉底置于最右侧末位，确保持续生效";

    public const string ReasonTauntCorner =
        "嘲讽随从靠右侧部署，将伤害吸引至边角防狂战斧波及两翼";

    public const string ReasonStandardHeuristic =
        "基于先手主力、风怒输出、中坚战力与亡语腾位的综合启发式站位";

    public const string ReasonSingleMinion =
        "单随从自动置于首位";

    #endregion

    /// <inheritdoc />
    public PositioningResult OptimizePosition(ImmutableArray<MinionEntity> minions)
    {
        if (minions.IsDefaultOrEmpty)
        {
            return PositioningResult.Empty;
        }

        int count = Math.Min(minions.Length, BoardSnapshot.MaxBoardMinions);
        if (count == 1)
        {
            var single = minions[0];
            if (single.ZonePosition == 0)
            {
                return new PositioningResult(minions, PositioningResult.GetIdentityIndices(1), ReasonSingleMinion);
            }

            var orderedSingle = ImmutableArray.Create(single with { ZonePosition = 0 });
            return new PositioningResult(orderedSingle, PositioningResult.GetIdentityIndices(1), ReasonSingleMinion);
        }

        InlineMinionBuffer orderedBuffer = default;
        Span<MinionEntity> orderedSpan = ((Span<MinionEntity>)orderedBuffer).Slice(0, count);
        Span<int> optimalSlotsSpan = stackalloc int[count];
        Span<int> orderedOriginalSpan = stackalloc int[count];

        string reason = SolvePositioning(
            minions.AsSpan().Slice(0, count),
            orderedSpan,
            optimalSlotsSpan,
            orderedOriginalSpan);

        // 判定是否与原站位完全恒等
        bool isIdentity = true;
        for (int i = 0; i < count; i++)
        {
            if (optimalSlotsSpan[i] != i || minions[i].ZonePosition != i)
            {
                isIdentity = false;
                break;
            }
        }

        if (isIdentity)
        {
            InlineMinionBuffer inlineMinions = default;
            InlineIndexBuffer inlineSlots = default;
            InlineIndexBuffer inlineOriginal = default;
            for (int i = 0; i < count; i++)
            {
                inlineMinions[i] = orderedSpan[i];
                inlineSlots[i] = i;
                inlineOriginal[i] = i;
            }

            return new PositioningResult(
                inlineMinions,
                inlineSlots,
                inlineOriginal,
                count,
                reason,
                optionalPrebuiltOrdered: minions,
                optionalPrebuiltSlots: PositioningResult.GetIdentityIndices(count),
                optionalPrebuiltOriginal: PositioningResult.GetIdentityIndices(count));
        }

        InlineMinionBuffer resultMinions = default;
        InlineIndexBuffer resultSlots = default;
        InlineIndexBuffer resultOriginal = default;
        for (int i = 0; i < count; i++)
        {
            resultMinions[i] = orderedSpan[i];
            resultSlots[i] = optimalSlotsSpan[i];
            resultOriginal[i] = orderedOriginalSpan[i];
        }

        return new PositioningResult(resultMinions, resultSlots, resultOriginal, count, reason);
    }

    /// <inheritdoc />
    public PositioningResult OptimizePosition(in BoardSnapshot snapshot)
    {
        return OptimizePosition(snapshot.BoardMinions);
    }

    /// <inheritdoc />
    public int OptimizePositionSpan(
        ReadOnlySpan<MinionEntity> inputMinions,
        Span<MinionEntity> outputOrderedMinions,
        Span<int> outputSlotIndices)
    {
        int count = Math.Min(inputMinions.Length, BoardSnapshot.MaxBoardMinions);
        if (count == 0) return 0;

        if (outputOrderedMinions.Length < count || outputSlotIndices.Length < count)
        {
            throw new ArgumentException("Output spans must be at least as large as input minions count.");
        }

        if (count == 1)
        {
            outputOrderedMinions[0] = inputMinions[0] with { ZonePosition = 0 };
            outputSlotIndices[0] = 0;
            return 1;
        }

        Span<int> orderedOriginalSpan = stackalloc int[count];
        SolvePositioning(
            inputMinions.Slice(0, count),
            outputOrderedMinions.Slice(0, count),
            outputSlotIndices.Slice(0, count),
            orderedOriginalSpan);

        return count;
    }

    #region 核心启发式算法求解器 (纯栈分配，零堆内存分配)

    private static string SolvePositioning(
        ReadOnlySpan<MinionEntity> input,
        Span<MinionEntity> outputOrdered,
        Span<int> optimalSlots,
        Span<int> orderedOriginal)
    {
        int count = input.Length;

        Span<int> engineIndices = stackalloc int[count];
        int engineCount = 0;

        Span<int> tauntIndices = stackalloc int[count];
        int tauntCount = 0;

        Span<int> otherIndices = stackalloc int[count];
        int otherCount = 0;

        int cleaveCount = 0;

        // 1. 随从战术角色分类
        for (int i = 0; i < count; i++)
        {
            ref readonly var m = ref input[i];
            if (IsCleave(m))
            {
                cleaveCount++;
            }

            if (IsCoreEngine(m))
            {
                engineIndices[engineCount++] = i;
            }
            else if (m.Taunt)
            {
                tauntIndices[tauntCount++] = i;
            }
            else
            {
                otherIndices[otherCount++] = i;
            }
        }

        // 2. 槽位分配与防狂战隔离策略推导
        // 目标：将 input[0..count-1] 映射到 orderedOriginal[0..count-1]
        // orderedOriginal[slot] = originalIndex
        orderedOriginal.Fill(-1);

        bool usedBumper = false;

        // --- 核心发动机排布 (置于最右侧末位) ---
        if (engineCount > 0)
        {
            // 多个核心发动机时，按重要度与血量排序，最关键/脆皮置于最右侧末位
            SortEngines(engineIndices.Slice(0, engineCount), input);

            int engineStartSlot = count - engineCount;
            for (int e = 0; e < engineCount; e++)
            {
                orderedOriginal[engineStartSlot + e] = engineIndices[e];
            }
        }

        // --- 嘲讽随从与防狂战隔离墙排布 ---
        if (engineCount > 0 && tauntCount > 0)
        {
            // 既有嘲讽又有核心发动机：必须有隔离墙防狂战溅射！
            if (otherCount > 0)
            {
                // 从非嘲讽、非核心发动机随从中挑选最弱/最低价值的随从作为隔离墙 (Bumper)
                int bestBumperPosInOthers = PickBestBumperIndex(otherIndices.Slice(0, otherCount), input);
                int bumperOriginalIndex = otherIndices[bestBumperPosInOthers];

                // 将该随从从 others 中移除
                for (int o = bestBumperPosInOthers; o < otherCount - 1; o++)
                {
                    otherIndices[o] = otherIndices[o + 1];
                }
                otherCount--;

                // 隔离墙紧邻核心发动机左侧
                int bumperSlot = count - engineCount - 1;
                orderedOriginal[bumperSlot] = bumperOriginalIndex;
                usedBumper = true;

                // 嘲讽随从置于隔离墙左侧
                SortTaunts(tauntIndices.Slice(0, tauntCount), input);
                int tauntStartSlot = bumperSlot - tauntCount;
                for (int t = 0; t < tauntCount; t++)
                {
                    orderedOriginal[tauntStartSlot + t] = tauntIndices[t];
                }
            }
            else
            {
                // 无隔离随从可用时，嘲讽排在发动机左侧
                SortTaunts(tauntIndices.Slice(0, tauntCount), input);
                int tauntStartSlot = count - engineCount - tauntCount;
                for (int t = 0; t < tauntCount; t++)
                {
                    orderedOriginal[tauntStartSlot + t] = tauntIndices[t];
                }
            }
        }
        else if (tauntCount > 0)
        {
            // 无核心发动机，但有嘲讽随从：嘲讽靠右侧末位部署，吸收伤害至边角
            SortTaunts(tauntIndices.Slice(0, tauntCount), input);
            int tauntStartSlot = count - tauntCount;
            for (int t = 0; t < tauntCount; t++)
            {
                orderedOriginal[tauntStartSlot + t] = tauntIndices[t];
            }
        }

        // --- 剩余攻击与战斗随从排布 (填充左侧先手与中坚槽位) ---
        if (otherCount > 0)
        {
            // 按先手输出与战力评分降序排列 (圣盾狂战 > 普通狂战 > Buff亡语 > 风怒/圣盾 > 常规主力 > 亡语产怪)
            SortAttackers(otherIndices.Slice(0, otherCount), input);

            int currentSlot = 0;
            for (int o = 0; o < otherCount; o++)
            {
                // 找到下一个未被占用的槽位
                while (currentSlot < count && orderedOriginal[currentSlot] != -1)
                {
                    currentSlot++;
                }

                if (currentSlot < count)
                {
                    orderedOriginal[currentSlot] = otherIndices[o];
                    currentSlot++;
                }
            }
        }

        // 3. 构建输出数组与最优槽位映射
        for (int slot = 0; slot < count; slot++)
        {
            int origIdx = orderedOriginal[slot];
            if (origIdx >= 0 && origIdx < count)
            {
                optimalSlots[origIdx] = slot;
                outputOrdered[slot] = input[origIdx] with { ZonePosition = slot };
            }
        }

        // 4. 理由解释推导
        if (cleaveCount > 0 && engineCount > 0 && tauntCount > 0 && usedBumper)
        {
            return ReasonCleaveEngineWithBumper;
        }

        if (cleaveCount > 0 && tauntCount > 0)
        {
            return ReasonCleaveTauntCorner;
        }

        if (cleaveCount > 0 && engineCount > 0)
        {
            return ReasonCleaveEngineProtected;
        }

        if (cleaveCount > 0)
        {
            return ReasonCleavePriority;
        }

        if (engineCount > 0 && tauntCount > 0 && usedBumper)
        {
            return ReasonEngineTauntWithBumper;
        }

        if (engineCount > 0)
        {
            return ReasonEngineProtected;
        }

        if (tauntCount > 0)
        {
            return ReasonTauntCorner;
        }

        return ReasonStandardHeuristic;
    }

    #endregion

    #region 特征识别辅助方法 (零 GC 位运算与快速匹配)

    /// <summary>
    /// 判断随从是否为狂战斧 / 顺劈随从。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCleave(in MinionEntity minion)
    {
        if (minion.Cleave) return true;
        if (s_cleaveSet.Contains(minion.CardId)) return true;

        string id = minion.CardId;
        return id.Contains("Cleave", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Hydra", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Reaper", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Collector", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Atramedes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断随从是否为后排核心发动机 (如瑞文戴尔、铜须、达卡莱、莫伊拉等)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCoreEngine(in MinionEntity minion)
    {
        if (s_engineSet.Contains(minion.CardId)) return true;

        string id = minion.CardId;
        return id.Contains("Rivendare", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Baron", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Titus", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Brann", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Drakkari", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Moira", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Khadgar", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Engine", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断随从是否为增益型亡语 (先死给全场提供 Buff，希望尽快攻击或送死)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBuffDeathrattle(in MinionEntity minion)
    {
        if (!minion.Deathrattle) return false;
        if (s_buffDeathrattleSet.Contains(minion.CardId)) return true;

        string id = minion.CardId;
        return id.Contains("Leapfrogger", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Frog", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Spawn", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Selfless", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断随从是否为产怪型亡语随从 (需要空闲格子容纳衍生生物，不宜过早送掉造成吞怪)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTokenSpawnerDeathrattle(in MinionEntity minion)
    {
        if (!minion.Deathrattle) return false;
        if (IsBuffDeathrattle(minion) || IsCoreEngine(minion)) return false;
        if (s_tokenSpawnerSet.Contains(minion.CardId)) return true;

        string id = minion.CardId;
        if (id.Contains("Coiler", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Egg", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Rat", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Mama", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Voidlord", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Highmane", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Raptor", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Spawner", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 默认非增益、非发动机亡语归为产怪/效果亡语
        return true;
    }

    #endregion

    #region 评分与排序启发式规则

    /// <summary>
    /// 评估随从作为防狂战隔离墙 (Bumper) 的适合度。
    /// 分数越低越适合作为牺牲隔离墙垫底 (白板怪、低攻、无关键异能)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateBumperScore(in MinionEntity minion)
    {
        int score = 0;

        // 绝不牺牲狂战斧或风怒作为隔离垫子
        if (IsCleave(minion)) score += 1_000_000;
        if (minion.Windfury) score += 500_000;
        if (minion.HasPoisonOrVenom) score += 300_000;
        if (minion.DivineShield) score += 200_000;
        if (minion.Deathrattle) score += 100_000;

        // 身材与星级加权
        score += minion.Attack * 10;
        score += minion.Health;
        score += minion.Tier * 100;

        return score;
    }

    /// <summary>
    /// 评估随从作为先手攻击者的价值评分 (分数越高排在越靠左侧)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateOpenerScore(in MinionEntity minion)
    {
        int score = 0;

        bool cleave = IsCleave(minion);
        if (cleave)
        {
            // 圣盾狂战斧为顶级先手 (对标死神4000型、多头蛇)
            score += minion.DivineShield ? 10_000_000 : 8_000_000;
            score += minion.Attack * 100 + minion.Health;
            return score;
        }

        if (IsBuffDeathrattle(minion))
        {
            // 跳蛙、无私英雄等增益亡语，优先攻击送死触发全场BUFF
            score += 7_000_000 + minion.Attack * 10;
            return score;
        }

        if (minion.DivineShield && minion.HasPoisonOrVenom)
        {
            // 圣盾剧毒：稳定换取敌方高威胁大怪
            score += 6_000_000 + minion.Attack * 10;
            return score;
        }

        if (minion.MegaWindfury)
        {
            score += 5_500_000 + minion.Attack * 20;
            return score;
        }

        if (minion.Windfury)
        {
            score += 5_000_000 + minion.Attack * 10;
            return score;
        }

        if (minion.DivineShield)
        {
            // 圣盾随从先手破盾白吃
            score += 4_000_000 + minion.Attack * 10;
            return score;
        }

        if (minion.HasPoisonOrVenom)
        {
            score += 3_000_000 + minion.Attack * 10;
            return score;
        }

        if (IsTokenSpawnerDeathrattle(minion))
        {
            // 产怪亡语排在中右侧，等待左侧先手随从腾出格子
            score += 1_000_000 + minion.Attack * 10;
            return score;
        }

        // 常规战力随从：高攻优先打出伤害
        score += 2_000_000 + minion.Attack * 10 + minion.Health;
        return score;
    }

    /// <summary>
    /// 评估嘲讽随从的防守排布优先级。
    /// 圣盾嘲讽随从优先靠右邻接隔离墙或边角，最大化吸收首发伤害。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateTauntDefenseScore(in MinionEntity minion)
    {
        int score = 0;
        if (minion.DivineShield) score += 1_000_000;
        if (minion.Reborn) score += 500_000;
        score += minion.Health * 10 + minion.Attack;
        return score;
    }

    /// <summary>
    /// 评估核心发动机的保护重要度。
    /// 瑞文戴尔最关键，置于最末位。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateEngineImportance(in MinionEntity minion)
    {
        int score = 0;
        string id = minion.CardId;

        if (id.Contains("Rivendare", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("Baron", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("Titus", StringComparison.OrdinalIgnoreCase) ||
            id == "BG25_354" || id == "BG25_354_G" || id == "FP1_031")
        {
            score += 1_000_000;
        }
        else if (id.Contains("Moira", StringComparison.OrdinalIgnoreCase) || id == "TTN_714" || id == "BG_TTN_714")
        {
            score += 800_000;
        }
        else if (id.Contains("Drakkari", StringComparison.OrdinalIgnoreCase) || id == "ICC_901" || id == "BG_ICC_901")
        {
            score += 600_000;
        }
        else
        {
            score += 400_000;
        }

        // 血量较低的越脆弱，越需要靠右受保护
        score += Math.Max(0, 100 - minion.Health);
        return score;
    }

    private static int PickBestBumperIndex(ReadOnlySpan<int> candidateIndices, ReadOnlySpan<MinionEntity> input)
    {
        int bestIdx = 0;
        int lowestScore = int.MaxValue;

        for (int i = 0; i < candidateIndices.Length; i++)
        {
            int origIdx = candidateIndices[i];
            int score = CalculateBumperScore(input[origIdx]);
            if (score < lowestScore)
            {
                lowestScore = score;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    private static void SortAttackers(Span<int> indices, ReadOnlySpan<MinionEntity> input)
    {
        // 简单插入排序 (最大 7 个元素，栈上零分配，极度高效)
        for (int i = 1; i < indices.Length; i++)
        {
            int key = indices[i];
            int keyScore = CalculateOpenerScore(input[key]);
            int j = i - 1;

            while (j >= 0 && CalculateOpenerScore(input[indices[j]]) < keyScore)
            {
                indices[j + 1] = indices[j];
                j--;
            }
            indices[j + 1] = key;
        }
    }

    private static void SortTaunts(Span<int> indices, ReadOnlySpan<MinionEntity> input)
    {
        // 嘲讽排布：防御力越强 (如圣盾嘲讽) 越靠右部署 (升序：防御高的排在后面槽位)
        for (int i = 1; i < indices.Length; i++)
        {
            int key = indices[i];
            int keyScore = CalculateTauntDefenseScore(input[key]);
            int j = i - 1;

            while (j >= 0 && CalculateTauntDefenseScore(input[indices[j]]) > keyScore)
            {
                indices[j + 1] = indices[j];
                j--;
            }
            indices[j + 1] = key;
        }
    }

    private static void SortEngines(Span<int> indices, ReadOnlySpan<MinionEntity> input)
    {
        // 核心发动机排布：重要性越高的排在越右侧 (升序：最重要的排在后面槽位)
        for (int i = 1; i < indices.Length; i++)
        {
            int key = indices[i];
            int keyScore = CalculateEngineImportance(input[key]);
            int j = i - 1;

            while (j >= 0 && CalculateEngineImportance(input[indices[j]]) > keyScore)
            {
                indices[j + 1] = indices[j];
                j--;
            }
            indices[j + 1] = key;
        }
    }

    #endregion
}
