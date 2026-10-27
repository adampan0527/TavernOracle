using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TavernOracle.Priors;
#if HAS_HEARTHDB_REFERENCE
using HearthDb;
using HearthDb.Enums;
#endif

namespace TavernOracle.Data;

/// <summary>
/// HearthDb 元数据适配封装器实现。
/// 将 HearthDb 官方卡牌数据库高效适配为战棋专用元数据字典。
/// 基于 .NET 8 <see cref="FrozenDictionary{TKey,TValue}"/> 实现全内存 O(1) 检索，
/// 支撑 15ms 决策链路中 <= 0.1ms 的单次随从元数据提取预算。
/// </summary>
public sealed class HearthDbAdapter : IHearthDbAdapter
{
    private static readonly Lazy<HearthDbAdapter> s_instance = new(() => new HearthDbAdapter());

    /// <summary>
    /// 全局单例实例
    /// </summary>
    public static HearthDbAdapter Instance => s_instance.Value;

    private readonly FrozenDictionary<string, BattlegroundsCardMetadata> _cardsByCardId;
    private readonly FrozenDictionary<int, BattlegroundsCardMetadata> _cardsByDbfId;
    private readonly FrozenDictionary<string, string> _tripleCardIdMap;

    private readonly IReadOnlyList<BattlegroundsCardMetadata> _allMinions;
    private readonly IReadOnlyList<BattlegroundsCardMetadata> _allMinionsWithGolden;
    private readonly IReadOnlyList<BattlegroundsCardMetadata>[] _minionsByTier;
    private readonly IReadOnlyList<BattlegroundsCardMetadata>[] _minionsByTierWithGolden;

    /// <inheritdoc />
    public int CachedCardCount => _cardsByCardId.Count;

    /// <inheritdoc />
    public bool IsInitialized { get; }

    /// <summary>
    /// 默认构造函数：自动预热并载入 HearthDb 全量战棋卡牌。
    /// </summary>
    public HearthDbAdapter() : this(customCards: null, customTriples: null)
    {
    }

    /// <summary>
    /// 支持传入自定义卡牌或测试用离线数据集的高级构造函数。
    /// </summary>
    /// <param name="customCards">可选的自定义卡牌集合</param>
    /// <param name="customTriples">可选的自定义三连对应字典 (普通 -> 金色 或 双向)</param>
    public HearthDbAdapter(
        IEnumerable<BattlegroundsCardMetadata>? customCards = null,
        IDictionary<string, string>? customTriples = null)
    {
        var cardDict = new Dictionary<string, BattlegroundsCardMetadata>(StringComparer.OrdinalIgnoreCase);
        var dbfDict = new Dictionary<int, BattlegroundsCardMetadata>();
        var tripleDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (customCards != null)
        {
            // 采用显式传入的卡牌集
            foreach (var card in customCards)
            {
                if (string.IsNullOrEmpty(card.CardId)) continue;
                cardDict[card.CardId] = card;
                if (card.DbfId > 0)
                {
                    dbfDict[card.DbfId] = card;
                }
            }

            if (customTriples != null)
            {
                foreach (var kvp in customTriples)
                {
                    string k = kvp.Key;
                    string v = kvp.Value;
                    if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
                    tripleDict[k] = v;
                    tripleDict[v] = k;
                }
            }
        }
        else
        {
#if HAS_HEARTHDB_REFERENCE
            try
            {
                LoadFromHearthDb(cardDict, dbfDict, tripleDict);
            }
            catch
            {
                // 若底层反射或文件加载异常，降级加载内置核心应急数据集
                LoadFallbackDataset(cardDict, dbfDict, tripleDict);
            }
#else
            LoadFallbackDataset(cardDict, dbfDict, tripleDict);
#endif
        }

        // 补全已录入卡牌中的三连映射关联
        CompleteTripleLinkages(cardDict, tripleDict);

        // 冻结为只读极速检索字典
        _cardsByCardId = cardDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _cardsByDbfId = dbfDict.ToFrozenDictionary();
        _tripleCardIdMap = tripleDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        // 预计算随从列表与分星级缓存 (0 GC 查询保证)
        PrecomputeMinionLists(
            cardDict.Values,
            out _allMinions,
            out _allMinionsWithGolden,
            out _minionsByTier,
            out _minionsByTierWithGolden);

        IsInitialized = _cardsByCardId.Count > 0;
    }

#if HAS_HEARTHDB_REFERENCE
    private static void LoadFromHearthDb(
        Dictionary<string, BattlegroundsCardMetadata> cardDict,
        Dictionary<int, BattlegroundsCardMetadata> dbfDict,
        Dictionary<string, string> tripleDict)
    {
        // 1. 载入官方三连映射表 (双向打通)
        foreach (var kvp in Cards.NormalToTripleCardIds)
        {
            string normalId = kvp.Key;
            string goldenId = kvp.Value;
            if (string.IsNullOrEmpty(normalId) || string.IsNullOrEmpty(goldenId)) continue;
            tripleDict[normalId] = goldenId;
            tripleDict[goldenId] = normalId;
        }

        foreach (var kvp in Cards.TripleToNormalCardIds)
        {
            string goldenId = kvp.Key;
            string normalId = kvp.Value;
            if (string.IsNullOrEmpty(goldenId) || string.IsNullOrEmpty(normalId)) continue;
            tripleDict[goldenId] = normalId;
            tripleDict[normalId] = goldenId;
        }

        // 2. 遍历全量卡牌，筛选战棋关联卡牌
        foreach (var card in Cards.All.Values)
        {
            if (string.IsNullOrEmpty(card.Id)) continue;

            // 过滤判断是否属于酒馆战棋相关卡牌
            if (!IsBattlegroundsRelatedCard(card, tripleDict))
            {
                continue;
            }

            var metadata = ConvertHearthDbCard(card, tripleDict);
            cardDict[metadata.CardId] = metadata;

            if (metadata.DbfId > 0)
            {
                dbfDict[metadata.DbfId] = metadata;
            }
        }

        // 3. 补充关键经典随从 (如 Baron Rivendare / FP1_031) 的 Tier 等级修正 (若官方 TechLevel 为 0)
        ApplySpecialCardCorrections(cardDict);
    }

    private static bool IsBattlegroundsRelatedCard(Card card, Dictionary<string, string> tripleDict)
    {
        if (card.TechLevel > 0) return true;
        if (card.IsBaconPoolMinion || card.BaconBuddyMinion) return true;
        if (card.Id.StartsWith("BG", StringComparison.OrdinalIgnoreCase)) return true;
        if (card.Id.StartsWith("BGS_", StringComparison.OrdinalIgnoreCase)) return true;
        if (card.Id.StartsWith("TB_Bacon", StringComparison.OrdinalIgnoreCase)) return true;
        if (tripleDict.ContainsKey(card.Id)) return true;

        // 特殊关键发动机随从在天梯原始版本也有可能在战棋日志中出现 (如 FP1_031 瑞文戴尔, LOE_077 铜须)
        if (card.Id.Equals("FP1_031", StringComparison.OrdinalIgnoreCase) ||
            card.Id.Equals("LOE_077", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static BattlegroundsCardMetadata ConvertHearthDbCard(Card card, Dictionary<string, string> tripleDict)
    {
        // 判定金色卡牌：依据 Premium 属性或 ID 特征
        bool isGolden = card.Premium ||
                        card.Id.EndsWith("_G", StringComparison.OrdinalIgnoreCase) ||
                        card.Id.StartsWith("TB_BaconUps", StringComparison.OrdinalIgnoreCase);

        // 机制标签掩码
        var mechanics = ExtractMechanics(card);

        // 种族解析 (主种族 + 附带副种族)
        var raceMask = MapRace(card.Race) | MapRace(card.SecondaryRace);

        // 星级判定
        int tier = card.TechLevel;

        // 三连对应 ID
        string? tripleCardId = null;
        if (tripleDict.TryGetValue(card.Id, out var mappedTriple))
        {
            tripleCardId = mappedTriple;
        }
        else if (isGolden && card.Id.EndsWith("_G", StringComparison.OrdinalIgnoreCase))
        {
            tripleCardId = card.Id[..^2];
        }

        return new BattlegroundsCardMetadata
        {
            CardId = card.Id,
            DbfId = card.DbfId,
            Name = card.Name ?? string.Empty,
            LocalizedName = card.GetLocName(HearthDb.Enums.Locale.zhCN),
            Tier = tier,
            Race = raceMask,
            BaseAttack = card.Attack,
            BaseHealth = card.Health,
            IsGolden = isGolden,
            TripleCardId = tripleCardId,
            Mechanics = mechanics
        };
    }

    private static CardMechanicsMask ExtractMechanics(Card card)
    {
        var mask = CardMechanicsMask.None;

        if (card.Taunt || card.Entity.GetTag(GameTag.TAUNT) > 0)
        {
            mask |= CardMechanicsMask.Taunt;
        }

        if (card.DivineShield || card.Entity.GetTag(GameTag.DIVINE_SHIELD) > 0)
        {
            mask |= CardMechanicsMask.DivineShield;
        }

        if (card.Poisonous || card.Entity.GetTag(GameTag.POISONOUS) > 0)
        {
            mask |= CardMechanicsMask.Poisonous;
        }

        if (card.Venomous || card.Entity.GetTag(GameTag.VENOMOUS) > 0)
        {
            mask |= CardMechanicsMask.Venomous;
        }

        if (card.Reborn || card.Entity.GetTag(GameTag.REBORN) > 0)
        {
            mask |= CardMechanicsMask.Reborn;
        }

        if (card.Entity.GetTag(GameTag.MODULAR) > 0 || card.Entity.GetTag(GameTag.MAGNETIC) > 0)
        {
            mask |= CardMechanicsMask.Magnetic;
        }

        if (card.MegaWindfury || card.Entity.GetTag(GameTag.MEGA_WINDFURY) > 0)
        {
            mask |= CardMechanicsMask.MegaWindfury;
            mask |= CardMechanicsMask.Windfury;
        }
        else if (card.Windfury || card.Entity.GetTag(GameTag.WINDFURY) > 0)
        {
            mask |= CardMechanicsMask.Windfury;
        }

        if (card.Deathrattle || card.Entity.GetTag(GameTag.DEATHRATTLE) > 0)
        {
            mask |= CardMechanicsMask.Deathrattle;
        }

        if (card.Entity.GetTag(GameTag.STEALTH) > 0)
        {
            mask |= CardMechanicsMask.Stealth;
        }

        if (card.Entity.GetTag(GameTag.SPELLCRAFT) > 0)
        {
            mask |= CardMechanicsMask.Spellcraft;
        }

        return mask;
    }

    private static MinionRaceMask MapRace(Race race) => race switch
    {
        Race.BEAST => MinionRaceMask.Beast,
        Race.MECHANICAL => MinionRaceMask.Mech,
        Race.MURLOC => MinionRaceMask.Murloc,
        Race.UNDEAD => MinionRaceMask.Undead,
        Race.PIRATE => MinionRaceMask.Pirate,
        Race.ELEMENTAL => MinionRaceMask.Elemental,
        Race.DRAGON => MinionRaceMask.Dragon,
        Race.DEMON => MinionRaceMask.Demon,
        Race.NAGA => MinionRaceMask.Naga,
        Race.QUILBOAR => MinionRaceMask.Quilboar,
        Race.ALL => MinionRaceMask.All,
        _ => MinionRaceMask.None
    };

    private static void ApplySpecialCardCorrections(Dictionary<string, BattlegroundsCardMetadata> cardDict)
    {
        // 修正原版瑞文戴尔 (FP1_031) 在战棋中的 5 星属性与亡语机制
        if (cardDict.TryGetValue("FP1_031", out var rivendare) && rivendare.Tier == 0)
        {
            cardDict["FP1_031"] = rivendare with
            {
                Tier = 5,
                Race = MinionRaceMask.Undead,
                Mechanics = rivendare.Mechanics | CardMechanicsMask.Deathrattle
            };
        }

        // 修正原版布莱恩·铜须 (LOE_077) 在战棋中的 5 星属性
        if (cardDict.TryGetValue("LOE_077", out var brann) && brann.Tier == 0)
        {
            cardDict["LOE_077"] = brann with
            {
                Tier = 5
            };
        }
    }
#endif

    private static void CompleteTripleLinkages(
        Dictionary<string, BattlegroundsCardMetadata> cardDict,
        Dictionary<string, string> tripleDict)
    {
        foreach (var kvp in cardDict.ToArray())
        {
            string cardId = kvp.Key;
            var metadata = kvp.Value;
            if (string.IsNullOrEmpty(metadata.TripleCardId) && tripleDict.TryGetValue(cardId, out var mappedTriple))
            {
                cardDict[cardId] = metadata with { TripleCardId = mappedTriple };
            }
        }
    }

    private static void PrecomputeMinionLists(
        IEnumerable<BattlegroundsCardMetadata> cards,
        out IReadOnlyList<BattlegroundsCardMetadata> allMinions,
        out IReadOnlyList<BattlegroundsCardMetadata> allMinionsWithGolden,
        out IReadOnlyList<BattlegroundsCardMetadata>[] minionsByTier,
        out IReadOnlyList<BattlegroundsCardMetadata>[] minionsByTierWithGolden)
    {
        var normalList = new List<BattlegroundsCardMetadata>();
        var goldenList = new List<BattlegroundsCardMetadata>();

        var byTierNormal = new List<BattlegroundsCardMetadata>[8];
        var byTierGolden = new List<BattlegroundsCardMetadata>[8];
        for (int i = 0; i <= 7; i++)
        {
            byTierNormal[i] = [];
            byTierGolden[i] = [];
        }

        foreach (var card in cards)
        {
            // 战棋随从定义：拥有有效星级 (1~7) 或有效攻防身材
            if (card.Tier <= 0 && card.BaseAttack <= 0 && card.BaseHealth <= 0)
            {
                continue;
            }

            goldenList.Add(card);

            int tier = (card.Tier is >= 0 and <= 7) ? card.Tier : 0;
            byTierGolden[tier].Add(card);

            if (!card.IsGolden)
            {
                normalList.Add(card);
                byTierNormal[tier].Add(card);
            }
        }

        allMinions = normalList.AsReadOnly();
        allMinionsWithGolden = goldenList.AsReadOnly();

        minionsByTier = new IReadOnlyList<BattlegroundsCardMetadata>[8];
        minionsByTierWithGolden = new IReadOnlyList<BattlegroundsCardMetadata>[8];
        for (int i = 0; i <= 7; i++)
        {
            minionsByTier[i] = byTierNormal[i].AsReadOnly();
            minionsByTierWithGolden[i] = byTierGolden[i].AsReadOnly();
        }
    }

    /// <inheritdoc />
    public bool TryGetCard(string cardId, [NotNullWhen(true)] out BattlegroundsCardMetadata? metadata)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            metadata = null;
            return false;
        }

        return _cardsByCardId.TryGetValue(cardId, out metadata);
    }

    /// <inheritdoc />
    public bool TryGetCardByDbfId(int dbfId, [NotNullWhen(true)] out BattlegroundsCardMetadata? metadata)
    {
        if (dbfId <= 0)
        {
            metadata = null;
            return false;
        }

        return _cardsByDbfId.TryGetValue(dbfId, out metadata);
    }

    /// <inheritdoc />
    public string? GetTripleCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        // 1. 优先查阅三连映射表 (双向 O(1))
        if (_tripleCardIdMap.TryGetValue(cardId, out var tripleId))
        {
            return tripleId;
        }

        // 2. 查阅卡牌元数据内的 TripleCardId
        if (_cardsByCardId.TryGetValue(cardId, out var meta) && !string.IsNullOrEmpty(meta.TripleCardId))
        {
            return meta.TripleCardId;
        }

        // 3. 通用命名模式回退推测 (_G 后缀推断)
        if (cardId.EndsWith("_G", StringComparison.OrdinalIgnoreCase))
        {
            var baseId = cardId[..^2];
            if (_cardsByCardId.ContainsKey(baseId))
            {
                return baseId;
            }
        }
        else
        {
            var goldenId = cardId + "_G";
            if (_cardsByCardId.ContainsKey(goldenId))
            {
                return goldenId;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<BattlegroundsCardMetadata> GetAllBattlegroundsMinions(int? tier = null, bool includeGolden = false)
    {
        if (tier == null)
        {
            return includeGolden ? _allMinionsWithGolden : _allMinions;
        }

        int t = tier.Value;
        if (t is >= 0 and <= 7)
        {
            return includeGolden ? _minionsByTierWithGolden[t] : _minionsByTier[t];
        }

        return Array.Empty<BattlegroundsCardMetadata>();
    }

    /// <summary>
    /// 脱机或应急备用数据集 (覆盖典型核心流派随从)
    /// </summary>
    private static void LoadFallbackDataset(
        Dictionary<string, BattlegroundsCardMetadata> cardDict,
        Dictionary<int, BattlegroundsCardMetadata> dbfDict,
        Dictionary<string, string> tripleDict)
    {
        void AddCard(BattlegroundsCardMetadata normal, BattlegroundsCardMetadata golden)
        {
            cardDict[normal.CardId] = normal;
            cardDict[golden.CardId] = golden;
            if (normal.DbfId > 0) dbfDict[normal.DbfId] = normal;
            if (golden.DbfId > 0) dbfDict[golden.DbfId] = golden;

            tripleDict[normal.CardId] = golden.CardId;
            tripleDict[golden.CardId] = normal.CardId;
        }

        // 跳蛙骑士 (Leapfrogger)
        AddCard(
            new BattlegroundsCardMetadata
            {
                CardId = "BG21_000",
                DbfId = 72042,
                Name = "Leapfrogger",
                Tier = 2,
                Race = MinionRaceMask.Beast,
                BaseAttack = 3,
                BaseHealth = 3,
                IsGolden = false,
                TripleCardId = "BG21_000_G",
                Mechanics = CardMechanicsMask.Deathrattle
            },
            new BattlegroundsCardMetadata
            {
                CardId = "BG21_000_G",
                DbfId = 74974,
                Name = "Leapfrogger",
                Tier = 2,
                Race = MinionRaceMask.Beast,
                BaseAttack = 6,
                BaseHealth = 6,
                IsGolden = true,
                TripleCardId = "BG21_000",
                Mechanics = CardMechanicsMask.Deathrattle
            }
        );

        // 偏折机器人 (Deflect-o-Bot)
        AddCard(
            new BattlegroundsCardMetadata
            {
                CardId = "BGS_071",
                DbfId = 61930,
                Name = "Deflect-o-Bot",
                Tier = 3,
                Race = MinionRaceMask.Mech,
                BaseAttack = 3,
                BaseHealth = 2,
                IsGolden = false,
                TripleCardId = "TB_BaconUps_123",
                Mechanics = CardMechanicsMask.DivineShield
            },
            new BattlegroundsCardMetadata
            {
                CardId = "TB_BaconUps_123",
                DbfId = 61932,
                Name = "Deflect-o-Bot",
                Tier = 3,
                Race = MinionRaceMask.Mech,
                BaseAttack = 6,
                BaseHealth = 4,
                IsGolden = true,
                TripleCardId = "BGS_071",
                Mechanics = CardMechanicsMask.DivineShield
            }
        );

        // 泰特斯·瑞文戴尔 (Titus Rivendare)
        AddCard(
            new BattlegroundsCardMetadata
            {
                CardId = "BG25_354",
                DbfId = 97408,
                Name = "Titus Rivendare",
                Tier = 5,
                Race = MinionRaceMask.Undead,
                BaseAttack = 1,
                BaseHealth = 7,
                IsGolden = false,
                TripleCardId = "BG25_354_G",
                Mechanics = CardMechanicsMask.Deathrattle
            },
            new BattlegroundsCardMetadata
            {
                CardId = "BG25_354_G",
                DbfId = 97409,
                Name = "Titus Rivendare",
                Tier = 5,
                Race = MinionRaceMask.Undead,
                BaseAttack = 2,
                BaseHealth = 14,
                IsGolden = true,
                TripleCardId = "BG25_354",
                Mechanics = CardMechanicsMask.Deathrattle
            }
        );

        // 经典瑞文戴尔 (Baron Rivendare)
        var baron = new BattlegroundsCardMetadata
        {
            CardId = "FP1_031",
            DbfId = 1915,
            Name = "Baron Rivendare",
            Tier = 5,
            Race = MinionRaceMask.Undead,
            BaseAttack = 1,
            BaseHealth = 7,
            IsGolden = false,
            TripleCardId = "BG25_354_G",
            Mechanics = CardMechanicsMask.Deathrattle
        };
        cardDict["FP1_031"] = baron;
        dbfDict[1915] = baron;

        // 布莱恩·铜须 (Brann Bronzebeard)
        AddCard(
            new BattlegroundsCardMetadata
            {
                CardId = "BG_LOE_077",
                DbfId = 96786,
                Name = "Brann Bronzebeard",
                Tier = 5,
                Race = MinionRaceMask.None,
                BaseAttack = 2,
                BaseHealth = 4,
                IsGolden = false,
                TripleCardId = "TB_BaconUps_045",
                Mechanics = CardMechanicsMask.None
            },
            new BattlegroundsCardMetadata
            {
                CardId = "TB_BaconUps_045",
                DbfId = 58400,
                Name = "Brann Bronzebeard",
                Tier = 5,
                Race = MinionRaceMask.None,
                BaseAttack = 4,
                BaseHealth = 8,
                IsGolden = true,
                TripleCardId = "BG_LOE_077",
                Mechanics = CardMechanicsMask.None
            }
        );

        // 吵吵模组 (Annoy-o-Module, 磁力+嘲讽+圣盾)
        AddCard(
            new BattlegroundsCardMetadata
            {
                CardId = "BG_BOT_911",
                DbfId = 48500,
                Name = "Annoy-o-Module",
                Tier = 3,
                Race = MinionRaceMask.Mech,
                BaseAttack = 2,
                BaseHealth = 4,
                IsGolden = false,
                TripleCardId = "TB_BaconUps_099",
                Mechanics = CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield | CardMechanicsMask.Magnetic
            },
            new BattlegroundsCardMetadata
            {
                CardId = "TB_BaconUps_099",
                DbfId = 59844,
                Name = "Annoy-o-Module",
                Tier = 3,
                Race = MinionRaceMask.Mech,
                BaseAttack = 4,
                BaseHealth = 8,
                IsGolden = true,
                TripleCardId = "BG_BOT_911",
                Mechanics = CardMechanicsMask.Taunt | CardMechanicsMask.DivineShield | CardMechanicsMask.Magnetic
            }
        );
    }
}
