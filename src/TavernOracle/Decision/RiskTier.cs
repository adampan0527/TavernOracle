namespace TavernOracle.Decision;

/// <summary>
/// 战力风险等级分类。
/// 依据对战暴毙率 P_lethal 划分不同的决策紧迫程度。
/// </summary>
public enum RiskTier
{
    /// <summary>
    /// 安全状态 (P_lethal &lt;= 0.05)：暴毙威胁极低，处于绝对安全区，可极致贪婪升本与长线运营。
    /// </summary>
    Safe = 0,

    /// <summary>
    /// 低风险状态 (0.05 &lt; P_lethal &lt;= 0.15)：轻微斩杀威胁，正常平衡即时战力与资源储备。
    /// </summary>
    Low = 1,

    /// <summary>
    /// 警告状态 (0.15 &lt; P_lethal &lt;= 0.30)：战力显著承压，压低贪婪权重，优先补强战力防暴毙。
    /// </summary>
    Warning = 2,

    /// <summary>
    /// 绝境突变状态 (P_lethal &gt; 0.30)：触发生死线突变，强制切断贪婪逻辑，全力即时战力苟活。
    /// </summary>
    Critical = 3,

    /// <summary>
    /// 绝境突变同义别名 (Lethal)
    /// </summary>
    Lethal = Critical
}
