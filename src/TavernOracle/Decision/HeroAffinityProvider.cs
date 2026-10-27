namespace TavernOracle.Decision;

using System.Collections.Concurrent;
using TavernOracle.Priors;

/// <summary>
/// 默认英雄亲和提供器实现。
/// 内置标准酒馆战棋核心亲和英雄（齐恩瓦拉、伊瑟拉、帕奇斯、米尔菲丝等）并支持线程安全动态注册扩展。
/// </summary>
public sealed class HeroAffinityProvider : IHeroAffinityProvider
{
    private static readonly Lazy<HeroAffinityProvider> _defaultInstance = new(() => new HeroAffinityProvider());

    /// <summary>
    /// 全局默认共享实例。
    /// </summary>
    public static HeroAffinityProvider Default => _defaultInstance.Value;

    private readonly ConcurrentDictionary<string, HeroAffinity> _affinityMap;

    public HeroAffinityProvider()
    {
        _affinityMap = new ConcurrentDictionary<string, HeroAffinity>(StringComparer.OrdinalIgnoreCase);
        RegisterDefaultAffinities();
    }

    public bool TryGetAffinity(string? heroCardIdOrName, out HeroAffinity affinity)
    {
        if (string.IsNullOrWhiteSpace(heroCardIdOrName))
        {
            affinity = default;
            return false;
        }

        return _affinityMap.TryGetValue(heroCardIdOrName.Trim(), out affinity);
    }

    public void RegisterAffinity(HeroAffinity affinity)
    {
        if (string.IsNullOrWhiteSpace(affinity.HeroIdentifier))
        {
            return;
        }

        _affinityMap[affinity.HeroIdentifier.Trim()] = affinity;
    }

    public void RegisterAffinityWithAliases(HeroAffinity affinity, params string[] aliases)
    {
        RegisterAffinity(affinity);
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                _affinityMap[alias.Trim()] = new HeroAffinity(
                    alias.Trim(),
                    affinity.RaceAffinity,
                    affinity.ArchetypeIdAffinity,
                    affinity.WeightMultiplier);
            }
        }
    }

    public IReadOnlyCollection<HeroAffinity> GetAllAffinities() => _affinityMap.Values.ToArray();

    private void RegisterDefaultAffinities()
    {
        // 齐恩瓦拉 (Chenvaala) -> 元素
        var chenvaala = new HeroAffinity("TB_BaconShop_HERO_78", MinionRaceMask.Elemental, "Elementals_Nomi", 3.5);
        RegisterAffinityWithAliases(chenvaala, "BG_DAL_575", "Chenvaala", "齐恩瓦拉");

        // 伊瑟拉 (Ysera) -> 龙
        var ysera = new HeroAffinity("TB_BaconShop_HERO_53", MinionRaceMask.Dragon, "Dragons_Kalecgos", 3.5);
        RegisterAffinityWithAliases(ysera, "BG_DRG_205", "Ysera", "伊瑟拉");

        // 帕奇斯 (Patches the Pirate) -> 海盗
        var patches = new HeroAffinity("TB_BaconShop_HERO_18", MinionRaceMask.Pirate, "Pirates_APM", 3.5);
        RegisterAffinityWithAliases(patches, "Patches", "Patches the Pirate", "海盗帕奇斯", "帕奇斯");

        // 米尔菲丝·法力风暴 (Millificent Manastorm) -> 机械
        var millificent = new HeroAffinity("TB_BaconShop_HERO_17", MinionRaceMask.Mech, "Mechs_Deflecto", 3.5);
        RegisterAffinityWithAliases(millificent, "Millificent", "Millificent Manastorm", "米尔菲丝", "米尔菲丝·法力风暴");

        // 菌菇术士弗戈尔 (Fungmancer Flurgl) -> 鱼人
        var flurgl = new HeroAffinity("TB_BaconShop_HERO_55", MinionRaceMask.Murloc, "Murlocs_Handbuff", 3.5);
        RegisterAffinityWithAliases(flurgl, "Flurgl", "Fungmancer Flurgl", "菌菇术士", "菌菇术士弗戈尔");

        // 加拉克苏斯大王 (Lord Jaraxxus) -> 恶魔
        var jaraxxus = new HeroAffinity("TB_BaconShop_HERO_21", MinionRaceMask.Demon, "Demons_SelfDamage", 3.5);
        RegisterAffinityWithAliases(jaraxxus, "Jaraxxus", "Lord Jaraxxus", "加拉克苏斯", "加拉克苏斯大王");

        // 亡语者布莱克松 (Death Speaker Blackthorn) -> 野猪人
        var blackthorn = new HeroAffinity("TB_BaconShop_HERO_103", MinionRaceMask.Quilboar, "Quilboar_BloodGem", 3.5);
        RegisterAffinityWithAliases(blackthorn, "Blackthorn", "Death Speaker Blackthorn", "布莱克松", "亡语者布莱克松");

        // 艾萨拉女王 (Queen Azshara) -> 纳迦
        var azshara = new HeroAffinity("TB_BaconShop_HERO_90", MinionRaceMask.Naga, "Naga_Spellcraft", 3.0);
        RegisterAffinityWithAliases(azshara, "Azshara", "Queen Azshara", "艾萨拉", "艾萨拉女王");

        // 格雷布 (Greybough) -> 野兽与亡语
        var greybough = new HeroAffinity("TB_BaconShop_HERO_95", MinionRaceMask.Beast | MinionRaceMask.Undead, null, 2.5);
        RegisterAffinityWithAliases(greybough, "Greybough", "格雷布");

        // 穆克拉 (King Mukla) -> 野兽
        var mukla = new HeroAffinity("TB_BaconShop_HERO_38", MinionRaceMask.Beast, "Beasts_Leapfrogger", 2.5);
        RegisterAffinityWithAliases(mukla, "Mukla", "King Mukla", "穆克拉");
    }
}
