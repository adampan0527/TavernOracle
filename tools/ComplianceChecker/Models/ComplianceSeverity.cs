namespace ComplianceChecker.Models;

/// <summary>
/// 合规审查严重级别
/// </summary>
public enum ComplianceSeverity
{
    /// <summary>
    /// 提示信息 / 建议
    /// </summary>
    Info,

    /// <summary>
    /// 警告项（潜在非规范行为，需人工确认）
    /// </summary>
    Warning,

    /// <summary>
    /// 绝对红线违规（触碰暴雪反作弊底线，必须阻止构建）
    /// </summary>
    Error
}
