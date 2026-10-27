using TavernOracle.Data.State;

namespace TavernOracle.Data.Replays;

/// <summary>
/// 离线盘面对局回放数据加载服务契约 (T06)。
/// 为离线回放、测试套件以及后续决策算法与基准测试提供脱机盘面快照加载支持。
/// </summary>
public interface IReplayDatasetLoader
{
    /// <summary>
    /// 获取所有可用回放场景名称列表 (不带 .json 扩展名)。
    /// </summary>
    IReadOnlyList<string> GetReplayNames();

    /// <summary>
    /// 加载指定名称的盘面快照 (支持带或不带 .json 后缀)。
    /// </summary>
    /// <param name="name">回放场景名称，例如 "01_leapfrogger_early_transition"</param>
    /// <returns>反序列化后的 BoardSnapshot 强类型结构体</returns>
    BoardSnapshot LoadReplay(string name);

    /// <summary>
    /// 尝试加载指定名称的盘面快照。
    /// </summary>
    bool TryLoadReplay(string name, out BoardSnapshot snapshot);

    /// <summary>
    /// 批量加载全部离线盘面快照集合。
    /// </summary>
    /// <returns>键为回放名称，值为对应快照的不可变只读字典</returns>
    IReadOnlyDictionary<string, BoardSnapshot> LoadAllReplays();
}
