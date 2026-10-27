using System;
using System.Reflection;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// 基于反射解耦的 HDT 原生 GameEvents 监听适配器 (Reflection HDT Game Event Source)。
/// 在不强引用 HDT 实体库的前提下，能够跨版本安全反射监听 HDT 内部 GameEvents 并转换为统一事件。
/// 脱机或 HDT 未就绪时优雅静默降级，杜绝崩溃。
/// </summary>
public sealed class ReflectionHdtGameEventSource : IHdtGameEventSource
{
    private readonly object _syncLock = new();
    private bool _isInGame;
    private bool _disposed;
    private object? _attachedGame;

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

    #region 事件派发辅助方法

    public void RaiseGameStarted() => GameStarted?.Invoke();
    public void RaiseGameEnded() => GameEnded?.Invoke();
    public void RaiseTurnStarted(int turn) => TurnStarted?.Invoke(turn);
    public void RaiseEntityCreated(HdtEntityDto entity) => EntityCreated?.Invoke(entity);
    public void RaiseEntityUpdated(HdtEntityDto entity) => EntityUpdated?.Invoke(entity);
    public void RaiseZoneChanged(HdtZoneChangeDto zoneChange) => ZoneChanged?.Invoke(zoneChange);
    public void RaiseTagChanged(HdtTagChangeDto tagChange) => TagChanged?.Invoke(tagChange);
    public void RaiseBoardChanged() => BoardChanged?.Invoke();

    #endregion

    public ReflectionHdtGameEventSource(object? hdtGame = null)
    {
        if (hdtGame != null)
        {
            Attach(hdtGame);
        }
        else
        {
            TryAttachHdtEvents();
        }
    }

    /// <summary>
    /// 尝试通过反射发现并订阅 HDT 的静态 GameEvents
    /// </summary>
    public bool TryAttachHdtEvents()
    {
        lock (_syncLock)
        {
            if (_disposed) return false;

            try
            {
                var eventsType = Type.GetType("Hearthstone_Deck_Tracker.API.GameEvents, HearthstoneDeckTracker")
                                 ?? Type.GetType("Hearthstone_Deck_Tracker.API.GameEvents")
                                 ?? Type.GetType("HearthstoneDeckTracker.API.GameEvents, HearthstoneDeckTracker")
                                 ?? Type.GetType("HearthstoneDeckTracker.API.GameEvents");

                if (eventsType == null)
                {
                    return false;
                }

                RegisterActionList(eventsType, "OnGameStart", () =>
                {
                    _isInGame = true;
                    GameStarted?.Invoke();
                });

                RegisterActionList(eventsType, "OnGameEnd", () =>
                {
                    _isInGame = false;
                    GameEnded?.Invoke();
                });

                return true;
            }
            catch
            {
                // 静默降级，确保环境缺失时不影响运行
                return false;
            }
        }
    }

    private static void RegisterActionList(Type eventsType, string propName, Action callback)
    {
        try
        {
            var prop = eventsType.GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                var actionListObj = prop.GetValue(null);
                if (actionListObj != null)
                {
                    var addMethod = actionListObj.GetType().GetMethod("Add", new[] { typeof(Action) });
                    addMethod?.Invoke(actionListObj, new object[] { callback });
                }
            }
        }
        catch
        {
            // 忽略注册错误
        }
    }

    /// <inheritdoc />
    public void Attach(object? hdtGame)
    {
        lock (_syncLock)
        {
            if (_disposed) return;
            Detach();
            _attachedGame = hdtGame;
            _isInGame = hdtGame != null;
        }
    }

    /// <inheritdoc />
    public void Detach()
    {
        lock (_syncLock)
        {
            _attachedGame = null;
            _isInGame = false;
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
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
}
