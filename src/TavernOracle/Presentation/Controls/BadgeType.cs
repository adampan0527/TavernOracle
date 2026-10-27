namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 酒馆战棋随从卡面头顶推荐微标分类。
/// 对齐 docs/dev_specifications.md 第 3.2 节微标规范。
/// </summary>
public enum BadgeType
{
    /// <summary>
    /// 🔥 三连对子 (TriplePair)：手牌或场上已有 1 张，购买即凑齐三连。金色渐变外框，附带发光呼吸动效。
    /// </summary>
    TriplePair = 0,

    /// <summary>
    /// ★ 流派质变怪 (KeyEngine)：大幅提升当前阵容核心流派战斗力。魔能紫徽标，标注流派协同得分。
    /// </summary>
    KeyEngine = 1,

    /// <summary>
    /// 🛡️ 即时保命单卡 (TempoFix)：即插即用保命战力，高亮圣盾/剧毒单卡。翡翠绿盾牌徽标。
    /// </summary>
    TempoFix = 2,

    /// <summary>
    /// ⛔ 陷阱预警 (TrapWarning)：低收益或高风险陷阱卡预警。半透明灰色下沉或红色细斜杠警示。
    /// </summary>
    TrapWarning = 3
}
