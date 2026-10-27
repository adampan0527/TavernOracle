using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using TavernOracle.Data.State;
using TavernOracle.Priors;
using TavernOracle.Simulation;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// HDT 游戏事件与底层实体转换流水线 (HDT Event Converter)。
/// 负责将 HDT 原始实体或解耦 DTO 极速转换为内部统一的只读值类型：
/// <see cref="MinionEntity"/>, <see cref="HandEntity"/>, <see cref="TavernMinion"/> 及 <see cref="BoardSnapshot"/>。
/// 兼具 0 GC 高性能装配与跨版本动态反射兼容能力。
/// </summary>
public static class HdtEventConverter
{
    #region 炉石官方 GameTag 常量映射 (解耦避免直接引用可能缺失的枚举)

    public const int TAG_ZONE = 49;
    public const int TAG_CONTROLLER = 50;
    public const int TAG_ZONE_POSITION = 263;
    public const int TAG_ATK = 47;
    public const int TAG_HEALTH = 45;
    public const int TAG_DAMAGE = 44;
    public const int TAG_ARMOR = 292;
    public const int TAG_COST = 48;
    public const int TAG_TAUNT = 190;
    public const int TAG_DIVINE_SHIELD = 194;
    public const int TAG_POISONOUS = 363;
    public const int TAG_VENOMOUS = 2178;
    public const int TAG_REBORN = 1085;
    public const int TAG_MODULAR = 849;
    public const int TAG_MAGNETIC = 1039;
    public const int TAG_WINDFURY = 189;
    public const int TAG_MEGA_WINDFURY = 1207;
    public const int TAG_DEATHRATTLE = 217;
    public const int TAG_STEALTH = 191;
    public const int TAG_SPELLCRAFT = 1946;
    public const int TAG_TECH_LEVEL = 1440;
    public const int TAG_PLAYER_TECH_LEVEL = 1377;
    public const int TAG_PREMIUM = 12;
    public const int TAG_FROZEN = 260;
    public const int TAG_CARDRACE = 200;
    public const int TAG_RESOURCES = 26;
    public const int TAG_RESOURCES_USED = 25;
    public const int TAG_TURN = 20;

    #endregion

    #region 单体实体转换 (DTO -> 内部只读结构体)

    /// <summary>
    /// 将原始 DTO 转换为战场在场随从实体 <see cref="MinionEntity"/>。
    /// 结合 HearthDb 元数据补全缺失星级、种族与机制标签。
    /// </summary>
    public static MinionEntity ConvertToMinionEntity(
        HdtEntityDto raw,
        IHearthDbAdapter? dbAdapter = null)
    {
        int entityId = raw.EntityId;
        string cardId = raw.CardId ?? string.Empty;
        int attack = Math.Max(0, raw.Attack);
        int health = Math.Max(0, raw.Health);
        int tier = raw.Tier;
        MinionRaceMask race = raw.Race;
        CardMechanicsMask mechanics = raw.Mechanics;
        bool isGolden = raw.IsGolden;
        int zonePos = Math.Max(0, raw.ZonePosition);

        dbAdapter ??= HearthDbAdapter.Instance;
        if (dbAdapter != null && !string.IsNullOrEmpty(cardId) && dbAdapter.TryGetCard(cardId, out var meta))
        {
            if (tier <= 0) tier = meta.Tier;
            if (race == MinionRaceMask.None) race = meta.Race;
            mechanics |= meta.Mechanics;
            if (!isGolden) isGolden = meta.IsGolden;
            if (attack == 0 && health == 0)
            {
                attack = meta.BaseAttack;
                health = meta.BaseHealth;
            }
        }

        return new MinionEntity(
            entityId: entityId,
            cardId: cardId,
            attack: attack,
            health: health,
            tier: MathEx.Clamp(tier, 1, 7),
            race: race,
            mechanics: mechanics,
            isGolden: isGolden,
            zonePosition: zonePos
        );
    }

    /// <summary>
    /// 将原始 DTO 转换为玩家手牌实体 <see cref="HandEntity"/>。
    /// </summary>
    public static HandEntity ConvertToHandEntity(
        HdtEntityDto raw,
        IHearthDbAdapter? dbAdapter = null)
    {
        int entityId = raw.EntityId;
        string cardId = raw.CardId ?? string.Empty;
        int cost = Math.Max(0, raw.Cost);
        int zonePos = Math.Max(0, raw.ZonePosition);

        bool isMinion = true;
        bool isSpell = false;

        dbAdapter ??= HearthDbAdapter.Instance;
        if (dbAdapter != null && !string.IsNullOrEmpty(cardId) && dbAdapter.TryGetCard(cardId, out var meta))
        {
            if (meta.Tier > 0 || meta.BaseAttack > 0 || meta.BaseHealth > 0)
            {
                isMinion = true;
                isSpell = false;
            }
            else
            {
                // 酒馆法术判定 (如 BGS_、BG_Spell 等)
                isSpell = true;
                isMinion = false;
            }
        }

        if (raw.IsMinionCard.HasValue)
        {
            isMinion = raw.IsMinionCard.Value;
            isSpell = !isMinion;
        }
        return new HandEntity(
            entityId: entityId,
            cardId: cardId,
            cost: cost,
            isMinion: isMinion,
            isSpell: isSpell,
            zonePosition: zonePos
        ) { HasLiveStats = raw.Health > 0, Attack = raw.Attack, Health = raw.Health, Race = raw.Race, Mechanics = raw.Mechanics, DisplayName = raw.DisplayName };
    }

    /// <summary>
    /// 将原始 DTO 转换为酒馆在售随从实体 <see cref="TavernMinion"/>。
    /// </summary>
    public static TavernMinion ConvertToTavernMinion(
        HdtEntityDto raw,
        IHearthDbAdapter? dbAdapter = null)
    {
        int entityId = raw.EntityId;
        string cardId = raw.CardId ?? string.Empty;
        int cost = raw.Cost > 0 ? raw.Cost : 3; // 酒馆战棋随从标准售价为 3
        int tier = raw.Tier;
        MinionRaceMask race = raw.Race;
        bool isFrozen = raw.IsFrozen;

        dbAdapter ??= HearthDbAdapter.Instance;
        if (dbAdapter != null && !string.IsNullOrEmpty(cardId) && dbAdapter.TryGetCard(cardId, out var meta))
        {
            if (tier <= 0) tier = meta.Tier;
            if (race == MinionRaceMask.None) race = meta.Race;
        }

        return new TavernMinion(
            entityId: entityId,
            cardId: cardId,
            cost: cost,
            tier: MathEx.Clamp(tier, 1, 7),
            race: race,
            isFrozen: isFrozen
        ) { IsTavernSpell = raw.IsTavernSpell, HasLiveStats = raw.Health > 0, Attack = raw.Attack, Health = raw.Health, Mechanics = raw.Mechanics, DisplayName = raw.DisplayName };
    }

    #endregion

    #region 全局盘面快照装配 (流水线核心)

    /// <summary>
    /// 从原始盘面状态容器组装不可变盘面快照 <see cref="BoardSnapshot"/>。
    /// 严格控制在 &lt;= 0.1ms 预算内，且避免不必要的堆垃圾回收。
    /// </summary>
    public static BoardSnapshot BuildSnapshot(
        HdtBoardRawState rawState,
        IHearthDbAdapter? dbAdapter = null,
        CombatSimulationResult? simulation = null)
    {
        if (rawState == null) throw new ArgumentNullException(nameof(rawState));
        dbAdapter ??= HearthDbAdapter.Instance;

        // 1. 构建战场随从不可变切片 (最大 7 席位)
        int boardCount = Math.Min(rawState.PlayerBoard.Count, BoardSnapshot.MaxBoardMinions);
        var boardBuilder = ImmutableArray.CreateBuilder<MinionEntity>(boardCount);
        for (int i = 0; i < boardCount; i++)
        {
            var raw = rawState.PlayerBoard[i];
            var minion = ConvertToMinionEntity(raw with { ZonePosition = i }, dbAdapter);
            boardBuilder.Add(minion);
        }

        // 2. 构建手牌不可变切片 (最大 10 张)
        int handCount = Math.Min(rawState.PlayerHand.Count, BoardSnapshot.MaxHandCards);
        var handBuilder = ImmutableArray.CreateBuilder<HandEntity>(handCount);
        for (int i = 0; i < handCount; i++)
        {
            var raw = rawState.PlayerHand[i];
            var hand = ConvertToHandEntity(raw with { ZonePosition = i }, dbAdapter);
            handBuilder.Add(hand);
        }

        // 3. 构建酒馆在售随从不可变切片 (最大 7 席位)
        int tavernCount = Math.Min(rawState.TavernMinions.Count, BoardSnapshot.MaxTavernMinions);
        var tavernBuilder = ImmutableArray.CreateBuilder<TavernMinion>(tavernCount);
        for (int i = 0; i < tavernCount; i++)
        {
            var raw = rawState.TavernMinions[i];
            // 若全局酒馆处于冻结，传递冻结标识
            bool frozen = raw.IsFrozen || rawState.IsFrozen;
            var tavern = ConvertToTavernMinion(raw with { IsFrozen = frozen }, dbAdapter);
            tavernBuilder.Add(tavern);
        }

        // 4. 组装铸币预算与战力仿真
        var gold = new GoldBudget(rawState.GoldAvailable, rawState.GoldMax, rawState.GoldLocked);
        var effectiveSim = simulation ?? rawState.Simulation;

        return new BoardSnapshot(
            turn: Math.Max(1, rawState.Turn),
            heroCardId: rawState.HeroCardId,
            health: Math.Max(0, rawState.Health),
            armor: Math.Max(0, rawState.Armor),
            tavernTier: MathEx.Clamp(rawState.TavernTier, 1, 7),
            upgradeCost: Math.Max(0, rawState.UpgradeCost),
            isFrozen: rawState.IsFrozen,
            bannedRaces: rawState.BannedRaces,
            gold: gold,
            simulation: effectiveSim,
            boardMinions: boardBuilder.MoveToImmutable(),
            handCards: handBuilder.MoveToImmutable(),
            tavernMinions: tavernBuilder.MoveToImmutable()
        );
    }

    #endregion

    #region HDT 原生实体反射转换 (安全解耦，杜绝硬编码依赖)

    /// <summary>
    /// 从 HDT 原生实体对象（HearthstoneDeckTracker.Hearthstone.Entities.Entity）提取转换为 DTO。
    /// 无论原生类库版本如何演进，均通过反射与 Duck Typing 弹性适配。
    /// </summary>
    public static HdtEntityDto ConvertFromNativeEntity(
        object? nativeEntity,
        IHearthDbAdapter? dbAdapter = null)
    {
        if (nativeEntity == null)
        {
            return default;
        }

        try
        {
            var entityType = nativeEntity.GetType();

            int id = GetPropertyOrFieldValue<int>(nativeEntity, entityType, "Id");
            string cardId = GetPropertyOrFieldValue<string>(nativeEntity, entityType, "CardId") ?? string.Empty;

            // 提取基础身材与费用
            int attack = GetTagOrProperty(nativeEntity, entityType, TAG_ATK, "Attack");
            int health = GetTagOrProperty(nativeEntity, entityType, TAG_HEALTH, "Health");
            int cost = GetTagOrProperty(nativeEntity, entityType, TAG_COST, "Cost");
            int tier = GetTagOrProperty(nativeEntity, entityType, TAG_TECH_LEVEL, "Tier");
            int controller = GetTagOrProperty(nativeEntity, entityType, TAG_CONTROLLER, "Controller");
            int zonePos = GetTagOrProperty(nativeEntity, entityType, TAG_ZONE_POSITION, "ZonePosition");
            bool isFrozen = GetTagValue(nativeEntity, entityType, TAG_FROZEN) > 0;
            bool isGolden = GetTagValue(nativeEntity, entityType, TAG_PREMIUM) > 0 || cardId.EndsWith("_G", StringComparison.OrdinalIgnoreCase);

            // 区域解析
            HdtZone zone = ExtractZone(nativeEntity, entityType);

            // 机制与种族提取
            var mechanics = ExtractMechanicsFromEntity(nativeEntity, entityType);
            var race = ExtractRaceFromEntity(nativeEntity, entityType);

            return new HdtEntityDto(
                EntityId: id,
                CardId: cardId,
                Controller: controller,
                Zone: zone,
                ZonePosition: zonePos,
                Attack: attack,
                Health: health,
                Cost: cost,
                Tier: tier,
                Race: race,
                Mechanics: mechanics,
                IsGolden: isGolden,
                IsFrozen: isFrozen,
                IsTavernSpell: GetPropertyOrFieldValue<bool>(nativeEntity, entityType, "IsBattlegroundsSpell"),
                DisplayName: GetPropertyOrFieldValue<string>(nativeEntity, entityType, "LocalizedName"),
                IsMinionCard: entityType.GetProperty("IsMinion") != null
                    ? GetPropertyOrFieldValue<bool>(nativeEntity, entityType, "IsMinion") : (bool?)null
            );
        }
        catch
        {
            // 发生异常时优雅降级返回安全默认值
            return default;
        }
    }

    /// <summary>
    /// 从 HDT 原生 Game 实例（如 Core.Game / GameV2）提取完整局势快照。
    /// </summary>
    public static BoardSnapshot ConvertFromHdtGame(
        object? hdtGame,
        IHearthDbAdapter? dbAdapter = null,
        CombatSimulationResult? simulation = null)
    {
        if (hdtGame == null)
        {
            return new BoardSnapshot();
        }

        try
        {
            var gameType = hdtGame.GetType();
            var rawState = new HdtBoardRawState();

            // 1. 读取 Player 实体集合与英雄数据
            var playerObj = GetPropertyOrFieldValue<object>(hdtGame, gameType, "Player");
            if (playerObj != null)
            {
                var playerType = playerObj.GetType();

                // 英雄属性
                var heroObj = GetPropertyOrFieldValue<object>(playerObj, playerType, "Hero");
                if (heroObj != null)
                {
                    var heroType = heroObj.GetType();
                    rawState.HeroCardId = GetPropertyOrFieldValue<string>(heroObj, heroType, "CardId") ?? string.Empty;
                    int hp = GetTagOrProperty(heroObj, heroType, TAG_HEALTH, "Health");
                    int dmg = GetTagValue(heroObj, heroType, TAG_DAMAGE);
                    int armor = GetTagOrProperty(heroObj, heroType, TAG_ARMOR, "Armor");
                    rawState.Health = Math.Max(0, hp - dmg);
                    rawState.Armor = Math.Max(0, armor);
                }

                // 战场与手牌
                ExtractEntityCollection(playerObj, playerType, "Board", rawState.PlayerBoard, dbAdapter, minionsOnly: true);
                ExtractEntityCollection(playerObj, playerType, "Hand", rawState.PlayerHand, dbAdapter);
                // Playing an already purchased minion is free in Battlegrounds.
                for (int i = 0; i < rawState.PlayerHand.Count; i++)
                    if (!rawState.PlayerHand[i].IsTavernSpell)
                        rawState.PlayerHand[i] = rawState.PlayerHand[i] with { Cost = 0 };
            }

            // 2. 读取 Opponent (战棋招募阶段 Opponent 为鲍勃酒馆在售货架)
            var opponentObj = GetPropertyOrFieldValue<object>(hdtGame, gameType, "Opponent");
            if (opponentObj != null)
            {
                var oppType = opponentObj.GetType();
                ExtractEntityCollection(opponentObj, oppType, "Board", rawState.TavernMinions, dbAdapter, minionsOnly: true, includeShopSpells: true);
                for (int i = 0; i < rawState.TavernMinions.Count; i++)
                    if (!rawState.TavernMinions[i].IsTavernSpell)
                        rawState.TavernMinions[i] = rawState.TavernMinions[i] with { Cost = 3 };
            }

            // 3. 读取酒馆等级、回合数与铸币
            int turn = GetPropertyOrFieldValue<int>(hdtGame, gameType, "Turn");
            if (turn <= 0 && gameType.GetMethod("GetTurnNumber", Type.EmptyTypes)?.Invoke(hdtGame, null) is int nativeTurn)
                turn = nativeTurn;
            if (turn <= 0)
            {
                turn = 1;
            }
            rawState.Turn = turn;

            // A zero tag is a real exhausted budget, never a reason to invent gold.
            var playerEntity = GetPropertyOrFieldValue<object>(hdtGame, gameType, "PlayerEntity");
            rawState.GoldMax = playerEntity == null ? 0 : GetTagValue(playerEntity, playerEntity.GetType(), TAG_RESOURCES);
            int used = playerEntity == null ? 0 : GetTagValue(playerEntity, playerEntity.GetType(), TAG_RESOURCES_USED);
            int temporary = playerEntity == null ? 0 : GetTagValue(playerEntity, playerEntity.GetType(), 295);
            rawState.GoldAvailable = Math.Max(0, rawState.GoldMax + temporary - used);
            rawState.TavernTier = GetPropertyOrFieldValue<int>(hdtGame, gameType, "PlayerTechLevel");
            if (rawState.TavernTier <= 0 && playerEntity != null)
                rawState.TavernTier = GetTagValue(playerEntity, playerEntity.GetType(), TAG_PLAYER_TECH_LEVEL);
            // Power.log exposes a separate TB_BaconShopTechUp*_Button entity.
            // Unknown cost must not turn into a free tier-up recommendation.
            rawState.UpgradeCost = int.MaxValue;
            var owned = playerObj == null ? null : GetPropertyOrFieldValue<IEnumerable>(playerObj, playerObj.GetType(), "PlayerEntities");
            if (owned != null)
                foreach (var entity in owned)
                {
                    if (entity == null) continue;
                    var entityType = entity.GetType();
                    var cardId = GetPropertyOrFieldValue<string>(entity, entityType, "CardId") ?? string.Empty;
                    if (cardId.StartsWith("TB_BaconShopTechUp", StringComparison.Ordinal) &&
                        GetPropertyOrFieldValue<bool>(entity, entityType, "IsInPlay"))
                    {
                        rawState.UpgradeCost = GetTagValue(entity, entityType, TAG_COST);
                        break;
                    }
                }
            rawState.IsFrozen = rawState.TavernMinions.Exists(entity => entity.IsFrozen);

            return BuildSnapshot(rawState, dbAdapter, simulation);
        }
        catch
        {
            // 异常兜底：返回安全初始快照，绝不引发未捕获崩溃
            return new BoardSnapshot();
        }
    }

    #endregion

    #region 反射与标签解析底层辅助方法

    private static void ExtractEntityCollection(
        object owner,
        Type ownerType,
        string propertyName,
        List<HdtEntityDto> targetList,
        IHearthDbAdapter? dbAdapter,
        bool minionsOnly = false,
        bool includeShopSpells = false)
    {
        var collection = GetPropertyOrFieldValue<IEnumerable>(owner, ownerType, propertyName);
        if (collection == null) return;

        foreach (var item in collection)
        {
            if (item == null) continue;
            var type = item.GetType();
            if (minionsOnly && type.GetProperty("IsMinion") != null &&
                !GetPropertyOrFieldValue<bool>(item, type, "IsMinion") &&
                !(includeShopSpells && GetPropertyOrFieldValue<bool>(item, type, "IsBattlegroundsSpell"))) continue;
            var dto = ConvertFromNativeEntity(item, dbAdapter);
            if (dto.EntityId > 0 || !string.IsNullOrEmpty(dto.CardId))
            {
                targetList.Add(dto);
            }
        }
        targetList.Sort((left, right) => left.ZonePosition.CompareTo(right.ZonePosition));
    }

    private static int GetTagOrProperty(object target, Type targetType, int tagId, string propertyName)
    {
        int tagVal = GetTagValue(target, targetType, tagId);
        if (tagVal != 0) return tagVal;

        return GetPropertyOrFieldValue<int>(target, targetType, propertyName);
    }

    private static int GetTagValue(object target, Type targetType, int tagId)
    {
        try
        {
            var getTagMethod = targetType.GetMethod("GetTag", new[] { typeof(int) });
            if (getTagMethod != null)
            {
                var res = getTagMethod.Invoke(target, new object[] { tagId });
                if (res is int intVal) return intVal;
            }

            // 尝试通过枚举重载 GetTag(GameTag)
            var getTagEnumMethod = targetType.GetMethod("GetTag", BindingFlags.Public | BindingFlags.Instance);
            if (getTagEnumMethod != null)
            {
                var parameters = getTagEnumMethod.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsEnum)
                {
                    var enumVal = Enum.ToObject(parameters[0].ParameterType, tagId);
                    var res = getTagEnumMethod.Invoke(target, new[] { enumVal });
                    if (res is int intVal) return intVal;
                }
            }
        }
        catch
        {
            // 忽略反射调用错误
        }

        return 0;
    }

    private static T? GetPropertyOrFieldValue<T>(object target, Type targetType, string name)
    {
        try
        {
            var prop = targetType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanRead)
            {
                var val = prop.GetValue(target);
                if (val is T typedVal) return typedVal;
                if (val != null && typeof(T) == typeof(int) && int.TryParse(val.ToString(), out int intRes))
                {
                    return (T)(object)intRes;
                }
            }

            var field = targetType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var val = field.GetValue(target);
                if (val is T typedVal) return typedVal;
            }
        }
        catch
        {
            // 忽略反射错误
        }

        return default;
    }

    private static HdtZone ExtractZone(object nativeEntity, Type entityType)
    {
        int zoneTag = GetTagValue(nativeEntity, entityType, TAG_ZONE);
        if (zoneTag > 0)
        {
            return zoneTag switch
            {
                1 => HdtZone.Play,
                2 => HdtZone.Deck,
                3 => HdtZone.Hand,
                4 => HdtZone.Graveyard,
                5 => HdtZone.SetAside,
                6 => HdtZone.Secret,
                _ => HdtZone.Unknown
            };
        }

        // 尝试通过 IsInPlay / IsInHand / IsInSetAside 布尔属性判定
        if (GetPropertyOrFieldValue<bool>(nativeEntity, entityType, "IsInPlay")) return HdtZone.Play;
        if (GetPropertyOrFieldValue<bool>(nativeEntity, entityType, "IsInHand")) return HdtZone.Hand;
        if (GetPropertyOrFieldValue<bool>(nativeEntity, entityType, "IsInSetAside")) return HdtZone.SetAside;
        if (GetPropertyOrFieldValue<bool>(nativeEntity, entityType, "IsInGraveyard")) return HdtZone.Graveyard;

        return HdtZone.Unknown;
    }

    private static CardMechanicsMask ExtractMechanicsFromEntity(object nativeEntity, Type entityType)
    {
        var mask = CardMechanicsMask.None;

        if (GetTagValue(nativeEntity, entityType, TAG_TAUNT) > 0) mask |= CardMechanicsMask.Taunt;
        if (GetTagValue(nativeEntity, entityType, TAG_DIVINE_SHIELD) > 0) mask |= CardMechanicsMask.DivineShield;
        if (GetTagValue(nativeEntity, entityType, TAG_POISONOUS) > 0) mask |= CardMechanicsMask.Poisonous;
        if (GetTagValue(nativeEntity, entityType, TAG_VENOMOUS) > 0) mask |= CardMechanicsMask.Venomous;
        if (GetTagValue(nativeEntity, entityType, TAG_REBORN) > 0) mask |= CardMechanicsMask.Reborn;
        if (GetTagValue(nativeEntity, entityType, TAG_MODULAR) > 0 || GetTagValue(nativeEntity, entityType, TAG_MAGNETIC) > 0) mask |= CardMechanicsMask.Magnetic;
        if (GetTagValue(nativeEntity, entityType, TAG_MEGA_WINDFURY) > 0)
        {
            mask |= CardMechanicsMask.MegaWindfury | CardMechanicsMask.Windfury;
        }
        else if (GetTagValue(nativeEntity, entityType, TAG_WINDFURY) > 0)
        {
            mask |= CardMechanicsMask.Windfury;
        }
        if (GetTagValue(nativeEntity, entityType, TAG_DEATHRATTLE) > 0) mask |= CardMechanicsMask.Deathrattle;
        if (GetTagValue(nativeEntity, entityType, TAG_STEALTH) > 0) mask |= CardMechanicsMask.Stealth;
        if (GetTagValue(nativeEntity, entityType, TAG_SPELLCRAFT) > 0) mask |= CardMechanicsMask.Spellcraft;

        return mask;
    }

    private static MinionRaceMask ExtractRaceFromEntity(object nativeEntity, Type entityType)
    {
        int raceTag = GetTagValue(nativeEntity, entityType, TAG_CARDRACE);
        return MapRaceTag(raceTag);
    }

    public static MinionRaceMask MapRaceTag(int tag) => tag switch
    {
        20 => MinionRaceMask.Beast,       // Race.BEAST
        17 => MinionRaceMask.Mech,        // Race.MECHANICAL
        14 => MinionRaceMask.Murloc,      // Race.MURLOC
        11 => MinionRaceMask.Undead,      // Race.UNDEAD
        23 => MinionRaceMask.Pirate,      // Race.PIRATE
        21 => MinionRaceMask.Elemental,   // Race.ELEMENTAL
        24 => MinionRaceMask.Dragon,      // Race.DRAGON
        15 => MinionRaceMask.Demon,       // Race.DEMON
        92 => MinionRaceMask.Naga,        // Race.NAGA
        95 => MinionRaceMask.Quilboar,    // Race.QUILBOAR
        26 => MinionRaceMask.All,         // Race.ALL
        _ => MinionRaceMask.None
    };

    #endregion
}
