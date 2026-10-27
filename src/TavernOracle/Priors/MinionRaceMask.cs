namespace TavernOracle.Priors;

/// <summary>
/// Bitmask representation of Battlegrounds minion races (tribes).
/// Enables zero-allocation O(1) bitwise tribe synergy and matching queries.
/// </summary>
[Flags]
public enum MinionRaceMask
{
    None = 0,
    Neutral = 0,
    Beast = 1 << 0,       // 1
    Mech = 1 << 1,        // 2
    Murloc = 1 << 2,      // 4
    Undead = 1 << 3,      // 8
    Pirate = 1 << 4,      // 16
    Elemental = 1 << 5,   // 32
    Dragon = 1 << 6,      // 64
    Demon = 1 << 7,       // 128
    Naga = 1 << 8,        // 256
    Quilboar = 1 << 9,    // 512
    All = 1 << 10         // 1024 (Amalgam / All-tribes)
}

/// <summary>
/// High-performance extension and utility methods for <see cref="MinionRaceMask"/>.
/// </summary>
public static class MinionRaceMaskExtensions
{
    /// <summary>
    /// Checks whether the current race mask satisfies or intersects with another race mask.
    /// In Hearthstone Battlegrounds, 'All' matches any specific minion race.
    /// </summary>
    public static bool Intersects(this MinionRaceMask self, MinionRaceMask other)
    {
        if (self == MinionRaceMask.None || other == MinionRaceMask.None)
        {
            return false;
        }

        if ((self & MinionRaceMask.All) != 0 || (other & MinionRaceMask.All) != 0)
        {
            return true;
        }

        return (self & other) != 0;
    }

    /// <summary>
    /// Parses a standard tribe/race string into its corresponding <see cref="MinionRaceMask"/>.
    /// Handles case-insensitivity, aliases, and dual-tribe comma/pipe separators.
    /// </summary>
    public static MinionRaceMask Parse(string? raceString)
    {
        if (string.IsNullOrWhiteSpace(raceString))
        {
            return MinionRaceMask.None;
        }

        var parts = raceString.Split(new[] { ',', '|', '/', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var combined = MinionRaceMask.None;

        foreach (var part in parts)
        {
            combined |= ParseSingle(part.Trim());
        }

        return combined;
    }

    private static MinionRaceMask ParseSingle(string token)
    {
        var cleaned = token.Trim();
        return cleaned.ToLowerInvariant() switch
        {
            "beast" or "beasts" or "野兽" => MinionRaceMask.Beast,
            "mech" or "mechs" or "mechanical" or "机械" => MinionRaceMask.Mech,
            "murloc" or "murlocs" or "鱼人" => MinionRaceMask.Murloc,
            "undead" or "亡灵" => MinionRaceMask.Undead,
            "pirate" or "pirates" or "海盗" => MinionRaceMask.Pirate,
            "elemental" or "elementals" or "元素" => MinionRaceMask.Elemental,
            "dragon" or "dragons" or "龙" or "巨龙" => MinionRaceMask.Dragon,
            "demon" or "demons" or "恶魔" => MinionRaceMask.Demon,
            "naga" or "纳迦" => MinionRaceMask.Naga,
            "quilboar" or "quilboars" or "boar" or "野猪人" or "野猪" => MinionRaceMask.Quilboar,
            "all" or "amalgam" or "全部" or "融合怪" or "所有" => MinionRaceMask.All,
            "neutral" or "none" or "无" or "中立" => MinionRaceMask.None,
            _ => Enum.TryParse<MinionRaceMask>(cleaned, true, out var parsed) ? parsed : MinionRaceMask.None
        };
    }
}
