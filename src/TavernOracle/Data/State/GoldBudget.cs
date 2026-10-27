using System;
using System.Text.Json.Serialization;

namespace TavernOracle.Data.State;

/// <summary>
/// 铸币经济预算轻量 DTO (Zero GC 结构体)。
/// 封装当前招募回合的可用铸币、铸币上限与刷新预留锁定铸币。
/// 具备不可变语义与纯值类型特性。
/// </summary>
public readonly record struct GoldBudget
{
    /// <summary>
    /// 当前实时可用铸币 (Available Gold)
    /// </summary>
    public int Available { get; init; }

    /// <summary>
    /// 当前回合铸币上限 (Max Gold，初始 3，每回合 +1，上限通常 10，部分英雄或效果可突破)
    /// </summary>
    public int Max { get; init; }

    /// <summary>
    /// 锁定在刷新或预定操作中的铸币数量 (LockedInRoll)
    /// </summary>
    public int LockedInRoll { get; init; }

    /// <summary>
    /// 构造铸币预算状态
    /// </summary>
    [JsonConstructor]
    public GoldBudget(int available, int max = 10, int lockedInRoll = 0)
    {
        Available = Math.Max(0, available);
        Max = Math.Max(0, max);
        LockedInRoll = Math.Max(0, lockedInRoll);
    }

    /// <summary>
    /// 扣除锁定预留后实际可直接支配的净铸币
    /// </summary>
    [JsonIgnore]
    public int NetSpendable => Math.Max(0, Available - LockedInRoll);

    /// <summary>
    /// 是否有足够铸币进行刷新 (通常 1 铸币)
    /// </summary>
    [JsonIgnore]
    public bool CanAffordRoll => Available >= 1;

    /// <summary>
    /// 是否有足够铸币购买标准随从 (通常 3 铸币)
    /// </summary>
    [JsonIgnore]
    public bool CanAffordMinion => Available >= 3;

    /// <summary>
    /// 判定是否能承受指定数额的花费
    /// </summary>
    public bool CanAfford(int cost) => Available >= cost;

    public override string ToString() => $"[Gold: {Available}/{Max} (Locked:{LockedInRoll})]";
}
