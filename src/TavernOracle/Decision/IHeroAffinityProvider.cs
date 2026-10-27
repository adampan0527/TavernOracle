namespace TavernOracle.Decision;

using TavernOracle.Priors;

/// <summary>
/// 英雄流派与种族亲和先验提供器契约。
/// </summary>
public interface IHeroAffinityProvider
{
    /// <summary>
    /// 查询指定英雄标识符的先验亲和配置。
    /// </summary>
    /// <param name="heroCardIdOrName">英雄 CardID 或名称/别名</param>
    /// <param name="affinity">匹配到的亲和配置</param>
    /// <returns>若存在亲和偏置返回 true，否则返回 false</returns>
    bool TryGetAffinity(string? heroCardIdOrName, out HeroAffinity affinity);

    /// <summary>
    /// 注册或覆盖英雄的亲和配置。
    /// </summary>
    void RegisterAffinity(HeroAffinity affinity);

    /// <summary>
    /// 获取全量已注册的英雄亲和配置集合。
    /// </summary>
    IReadOnlyCollection<HeroAffinity> GetAllAffinities();
}
