using System;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// 脱机模拟 HDT 游戏事件源 (Mock Game Event Source)。
/// 用于单元测试、离线回放与自动化测试注入，完全脱离 HDT 宿主运行。
/// </summary>
public sealed class MockGameEventSource : IHdtGameEventSource
{
    private bool _isInGame;
    private bool _disposed;

    /// <inheritdoc />
    public event Action? GameStarted;

    /// <inheritdoc />
    public event Action? GameEnded;

    /// <inheritdoc />
    public event Action<int>? TurnStarted;

    /// <inheritdoc />
    public event Action<HdtEntityDto>? EntityCreated;

    /// <inheritdoc />
    public event Action<HdtEntityDto>? EntityUpdated;

    /// <inheritdoc />
    public event Action<HdtZoneChangeDto>? ZoneChanged;

    /// <inheritdoc />
    public event Action<HdtTagChangeDto>? TagChanged;

    /// <inheritdoc />
    public event Action? BoardChanged;

    /// <inheritdoc />
    public bool IsInGame => _isInGame;

    public void TriggerGameStart()
    {
        if (_disposed) return;
        _isInGame = true;
        GameStarted?.Invoke();
    }

    public void TriggerGameEnd()
    {
        if (_disposed) return;
        _isInGame = false;
        GameEnded?.Invoke();
    }

    public void TriggerTurnStart(int turn)
    {
        if (_disposed) return;
        TurnStarted?.Invoke(turn);
    }

    public void TriggerEntityCreated(HdtEntityDto entity)
    {
        if (_disposed) return;
        EntityCreated?.Invoke(entity);
    }

    public void TriggerEntityUpdated(HdtEntityDto entity)
    {
        if (_disposed) return;
        EntityUpdated?.Invoke(entity);
    }

    public void TriggerZoneChanged(HdtZoneChangeDto zoneChange)
    {
        if (_disposed) return;
        ZoneChanged?.Invoke(zoneChange);
    }

    public void TriggerTagChanged(HdtTagChangeDto tagChange)
    {
        if (_disposed) return;
        TagChanged?.Invoke(tagChange);
    }

    public void TriggerBoardChanged()
    {
        if (_disposed) return;
        BoardChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Attach(object? hdtGame)
    {
        // Mock 环境下保持空实现或记录状态
    }

    /// <inheritdoc />
    public void Detach()
    {
        _isInGame = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        GameStarted = null;
        GameEnded = null;
        TurnStarted = null;
        EntityCreated = null;
        EntityUpdated = null;
        ZoneChanged = null;
        TagChanged = null;
        BoardChanged = null;
    }
}
