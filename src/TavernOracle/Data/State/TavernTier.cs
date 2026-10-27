using System;

namespace TavernOracle.Data.State;

/// <summary>
/// 酒馆战棋星级等级轻量值类型 (1 ~ 7 星)。
/// 支持与 int 无缝隐式转换。
/// </summary>
public readonly record struct TavernTier(int Value) : IComparable<TavernTier>, IComparable<int>
{
    public const int MinTier = 1;
    public const int MaxTier = 7;

    public static implicit operator int(TavernTier tier) => tier.Value;
    public static implicit operator TavernTier(int value) => new(value);

    public int CompareTo(TavernTier other) => Value.CompareTo(other.Value);
    public int CompareTo(int other) => Value.CompareTo(other);

    public override string ToString() => $"Tier {Value}";
}
