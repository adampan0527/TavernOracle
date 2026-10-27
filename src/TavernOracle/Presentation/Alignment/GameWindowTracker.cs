using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using TavernOracle.Presentation.Overlay;
using DpiChangedEventArgs = TavernOracle.Presentation.Alignment.DpiChangedEventArgs;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 炉石游戏窗口追踪与自适应吸附对齐服务实现。
/// 严格遵守暴雪“纸笔准则（Pencil & Paper Rule）”，仅使用只读 API 查询目标窗口状态。
/// 支持窗口化、无边框全屏、21:9 超宽屏、16:10 以及高 DPI（100%~200%）无损坐标转换。
/// </summary>
public sealed class GameWindowTracker : IGameWindowTracker
{
    private readonly IWindowPositionProvider _positionProvider;
    private readonly List<ITransparentOverlay> _attachedOverlays = [];
    private readonly object _syncLock = new();

    private System.Threading.Timer? _pollTimer;
    private IntPtr _windowHandle = IntPtr.Zero;
    private bool _isTracking;
    private bool _isDisposed;

    private Rect _windowRect = Rect.Empty;
    private Rect _clientRect = Rect.Empty;
    private uint _dpi = 96;
    private double _dpiScale = 1.0;
    private GameViewport _viewport = GameViewport.Empty;

    #region Events

    /// <inheritdoc />
    public event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;

    /// <inheritdoc />
    public event EventHandler<DpiChangedEventArgs>? DpiChanged;

    /// <inheritdoc />
    public event EventHandler<GameViewportChangedEventArgs>? ViewportChanged;

    #endregion

    #region Constructors

    /// <summary>
    /// 初始化游戏窗口追踪服务实例
    /// </summary>
    /// <param name="positionProvider">可选的自定义窗口位置提供者（用于单元测试依赖注入）</param>
    public GameWindowTracker(IWindowPositionProvider? positionProvider = null)
    {
        _positionProvider = positionProvider ?? DefaultWindowPositionProvider.Instance;
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public IntPtr WindowHandle => _windowHandle;

    /// <inheritdoc />
    public bool IsTracking => _isTracking;

    /// <inheritdoc />
    public Rect WindowRect => _windowRect;

    /// <inheritdoc />
    public Rect ClientRect => _clientRect;

    /// <inheritdoc />
    public uint Dpi => _dpi;

    /// <inheritdoc />
    public double DpiScale => _dpiScale;

    /// <inheritdoc />
    public GameViewport Viewport => _viewport;

    /// <summary>
    /// 当前已绑定的透明覆盖层只读集合
    /// </summary>
    public IReadOnlyList<ITransparentOverlay> AttachedOverlays
    {
        get
        {
            lock (_syncLock)
            {
                return _attachedOverlays.ToArray();
            }
        }
    }

    #endregion

    #region Tracking Control

    /// <inheritdoc />
    public void StartTracking(IntPtr targetHandle = default, int pollIntervalMs = 50)
    {
        ThrowIfDisposed();

        lock (_syncLock)
        {
            if (targetHandle != IntPtr.Zero)
            {
                _windowHandle = targetHandle;
            }

            _isTracking = true;

            // 立即触发首次同步
            UpdateNowInternal();

            // 启动定时轮询
            int interval = Math.Max(10, pollIntervalMs);
            _pollTimer?.Dispose();
            _pollTimer = new System.Threading.Timer(OnPollTimerTick, null, interval, interval);
        }
    }

    /// <inheritdoc />
    public void StopTracking()
    {
        lock (_syncLock)
        {
            _isTracking = false;
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
    }

    /// <inheritdoc />
    public bool UpdateNow()
    {
        ThrowIfDisposed();

        lock (_syncLock)
        {
            return UpdateNowInternal();
        }
    }

    private void OnPollTimerTick(object? state)
    {
        if (_isDisposed || !_isTracking)
        {
            return;
        }

        try
        {
            UpdateNow();
        }
        catch
        {
            // 避免后台轮询线程未捕获异常导致宿主进程中断
        }
    }

    private bool UpdateNowInternal()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            _windowHandle = _positionProvider.FindHearthstoneWindow();
            if (_windowHandle == IntPtr.Zero)
            {
                return false;
            }
        }

        // 读取窗口外框矩形 (物理像素)
        if (!_positionProvider.GetWindowRect(_windowHandle, out RECT winRect))
        {
            // 目标窗口句柄可能已失效或关闭，尝试重新查找
            _windowHandle = _positionProvider.FindHearthstoneWindow();
            if (_windowHandle == IntPtr.Zero || !_positionProvider.GetWindowRect(_windowHandle, out winRect))
            {
                return false;
            }
        }

        // 读取窗口客户区矩形
        if (!_positionProvider.GetClientRect(_windowHandle, out RECT clientRectRaw))
        {
            return false;
        }

        // 将客户区原点转换为屏幕物理绝对坐标
        var clientOrigin = new POINT(0, 0);
        if (!_positionProvider.ClientToScreen(_windowHandle, ref clientOrigin))
        {
            return false;
        }

        uint newDpi = _positionProvider.GetDpiForWindow(_windowHandle);
        if (newDpi == 0)
        {
            newDpi = 96;
        }

        double newDpiScale = Math.Max(0.5, newDpi / 96.0);

        var newWindowRect = new Rect(winRect.Left, winRect.Top, winRect.Width, winRect.Height);
        var newClientRect = new Rect(clientOrigin.X, clientOrigin.Y, clientRectRaw.Width, clientRectRaw.Height);

        bool boundsChanged = _windowRect != newWindowRect || _clientRect != newClientRect;
        bool dpiChanged = _dpi != newDpi;

        Rect oldWinRect = _windowRect;
        Rect oldClientRect = _clientRect;
        uint oldDpi = _dpi;
        double oldDpiScale = _dpiScale;
        GameViewport oldViewport = _viewport;

        _windowRect = newWindowRect;
        _clientRect = newClientRect;
        _dpi = newDpi;
        _dpiScale = newDpiScale;

        // 计算 16:9 自适应活跃视口（支持 Letterbox / Pillarbox）
        _viewport = GameViewport.Calculate(newClientRect, newDpi);
        bool viewportChanged = oldViewport != _viewport;

        // 派发事件
        if (boundsChanged)
        {
            WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs(oldWinRect, newWindowRect, oldClientRect, newClientRect));
        }

        if (dpiChanged)
        {
            DpiChanged?.Invoke(this, new DpiChangedEventArgs(oldDpi, newDpi, oldDpiScale, newDpiScale));
        }

        if (viewportChanged)
        {
            ViewportChanged?.Invoke(this, new GameViewportChangedEventArgs(oldViewport, _viewport));
        }

        // 若几何状态发生变化，自动同步吸附所有绑定的透明覆盖层
        if (boundsChanged || dpiChanged || viewportChanged)
        {
            SynchronizeOverlays(_viewport);
        }

        return true;
    }

    #endregion

    #region Overlay Attachment & Auto-snapping

    /// <inheritdoc />
    public void AttachOverlay(ITransparentOverlay overlay)
    {
        if (overlay == null) throw new ArgumentNullException(nameof(overlay));
        ThrowIfDisposed();

        lock (_syncLock)
        {
            if (!_attachedOverlays.Contains(overlay))
            {
                _attachedOverlays.Add(overlay);
            }

            if (_viewport.IsValid)
            {
                SynchronizeSingleOverlay(overlay, _viewport);
            }
        }
    }

    /// <inheritdoc />
    public void DetachOverlay(ITransparentOverlay overlay)
    {
        if (overlay == null) throw new ArgumentNullException(nameof(overlay));

        lock (_syncLock)
        {
            _attachedOverlays.Remove(overlay);
        }
    }

    private void SynchronizeOverlays(GameViewport viewport)
    {
        if (!viewport.IsValid)
        {
            return;
        }

        List<ITransparentOverlay> copy;
        lock (_syncLock)
        {
            if (_attachedOverlays.Count == 0)
            {
                return;
            }
            copy = new List<ITransparentOverlay>(_attachedOverlays);
        }

        foreach (var overlay in copy)
        {
            SynchronizeSingleOverlay(overlay, viewport);
        }
    }

    private void SynchronizeSingleOverlay(ITransparentOverlay overlay, GameViewport viewport)
    {
        void ApplyBounds()
        {
            try
            {
                if (overlay.IsAttachedToHostCanvas)
                {
                    // 挂载至外部 HDT Canvas 时，容器坐标基准为客户区左上角
                    // 覆盖层直接覆盖全客户区（以 DIP 计量），子控件内部基于 Viewport.RelativeDipBounds 布局
                    double clientDipW = viewport.PhysicalClientBounds.Width / viewport.DpiScale;
                    double clientDipH = viewport.PhysicalClientBounds.Height / viewport.DpiScale;
                    overlay.SetBounds(0, 0, clientDipW, clientDipH);
                }
                else
                {
                    // 独立 WPF 顶层窗体形态：窗体尺寸与位置以屏幕 DIP 为单位吸附到游戏客户区
                    double screenDipX = viewport.PhysicalClientBounds.X / viewport.DpiScale;
                    double screenDipY = viewport.PhysicalClientBounds.Y / viewport.DpiScale;
                    double screenDipW = viewport.PhysicalClientBounds.Width / viewport.DpiScale;
                    double screenDipH = viewport.PhysicalClientBounds.Height / viewport.DpiScale;
                    overlay.SetBounds(screenDipX, screenDipY, screenDipW, screenDipH);
                }
            }
            catch
            {
                // 忽略被销毁或不可访问窗口的吸附更新异常
            }
        }

        if (overlay is DispatcherObject dispatcherObj && !dispatcherObj.CheckAccess())
        {
            dispatcherObj.Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)ApplyBounds);
        }
        else
        {
            ApplyBounds();
        }
    }

    #endregion

    #region Coordinate Conversions

    /// <inheritdoc />
    public Point PhysicalToDip(Point physicalPoint)
    {
        double scale = _dpiScale > 0 ? _dpiScale : 1.0;
        return new Point(physicalPoint.X / scale, physicalPoint.Y / scale);
    }

    /// <inheritdoc />
    public Point DipToPhysical(Point dipPoint)
    {
        double scale = _dpiScale > 0 ? _dpiScale : 1.0;
        return new Point(dipPoint.X * scale, dipPoint.Y * scale);
    }

    /// <inheritdoc />
    public Rect PhysicalToDip(Rect physicalRect)
    {
        if (physicalRect.IsEmpty)
        {
            return Rect.Empty;
        }

        double scale = _dpiScale > 0 ? _dpiScale : 1.0;
        return new Rect(
            physicalRect.X / scale,
            physicalRect.Y / scale,
            physicalRect.Width / scale,
            physicalRect.Height / scale);
    }

    /// <inheritdoc />
    public Rect DipToPhysical(Rect dipRect)
    {
        if (dipRect.IsEmpty)
        {
            return Rect.Empty;
        }

        double scale = _dpiScale > 0 ? _dpiScale : 1.0;
        return new Rect(
            dipRect.X * scale,
            dipRect.Y * scale,
            dipRect.Width * scale,
            dipRect.Height * scale);
    }

    #endregion

    #region IDisposable

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(GameWindowTracker));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopTracking();

        lock (_syncLock)
        {
            _attachedOverlays.Clear();
        }
    }

    #endregion
}
