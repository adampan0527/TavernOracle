namespace TavernOracle.Decision.ActionGeneration;

/// <summary>
/// 酒馆战棋招募阶段原子操作类别枚举。
/// </summary>
public enum ActionType : byte
{
    /// <summary>
    /// 购买酒馆在售随从 (耗费铸币，移入玩家手牌)
    /// </summary>
    Buy = 1,

    /// <summary>
    /// 出售战场已有随从 (获得 1 铸币，释放战场卡位)
    /// </summary>
    Sell = 2,

    /// <summary>
    /// 刷新酒馆商店 (耗费 1 铸币，重新生成在售随从)
    /// </summary>
    Reroll = 3,

    /// <summary>
    /// 冻结酒馆商店 (0 铸币，保留当前在售随从至下一回合)
    /// </summary>
    Freeze = 4,

    /// <summary>
    /// 升级酒馆星级 (耗费升本铸币，提升酒馆等级)
    /// </summary>
    UpgradeTier = 5,

    /// <summary>
    /// 从手牌打出随从至战场指定位置 (耗费打出铸币，占用 1 个战场卡位)
    /// </summary>
    Play = 6,

    /// <summary>Attach a Magnetic minion to an existing friendly Mech.</summary>
    Magnetize = 7
}
