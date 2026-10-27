using System;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// HDT 游戏事件抽象适配接口。
/// 杜绝状态机与 HDT 宿主进程事件产生强编码耦合，
/// 支持在线 HDT 挂载与离线 Mock 模拟事件源的双向自由插拔。
/// </summary>
public interface IHdtGameEventSource : IDisposable
{
    /// <summary>
    /// 对局开始事件
    /// </summary>
    event Action? GameStarted;

    /// <summary>
    /// 对局结束事件
    /// </summary>
    event Action? GameEnded;

    /// <summary>
    /// 回合更替开始事件 (参数为当前回合数)
    /// </summary>
    event Action<int>? TurnStarted;

    /// <summary>
    /// 新实体创建事件
    /// </summary>
    event Action<HdtEntityDto>? EntityCreated;

    /// <summary>
    /// 实体状态更新事件
    /// </summary>
    event Action<HdtEntityDto>? EntityUpdated;

    /// <summary>
    /// 实体区域流转变更事件
    /// </summary>
    event Action<HdtZoneChangeDto>? ZoneChanged;

    /// <summary>
    /// 实体游戏标签 (Tag) 变更事件
    /// </summary>
    event Action<HdtTagChangeDto>? TagChanged;

    /// <summary>
    /// 盘面综合变动通知事件
    /// </summary>
    event Action? BoardChanged;

    /// <summary>
    /// 当前是否处于战棋对局中
    /// </summary>
    bool IsInGame { get; }

    /// <summary>
    /// 挂载底层 HDT 游戏实例 (如 Core.Game)
    /// </summary>
    void Attach(object? hdtGame);

    /// <summary>
    /// 解除挂载并注销事件监听
    /// </summary>
    void Detach();
}
