using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TavernOracle.Presentation.Native;

namespace TavernOracle.Presentation.Overlay;

/// <summary>
/// WPF 透明点击穿透覆盖层基类。
/// 严格遵循暴雪“纸笔准则（Pencil & Paper Rule）”及 COMP005 规范。
/// 支持两种运行形态：
/// 1. 独立运行形态：独立置顶透明 WPF 窗体，Win32 扩展样式挂载 WS_EX_TRANSPARENT | WS_EX_LAYERED；
/// 2. HDT 宿主形态：自动或手动附加挂载至 HDT 的 Core.OverlayCanvas 容器中。
/// </summary>
public class TransparentOverlayBase : Window, ITransparentOverlay
{
    private readonly List<UIElement> _children = [];
    private readonly Canvas _containerCanvas;
    private Canvas? _hostCanvas;
    private IntPtr _windowHandle = IntPtr.Zero;
    private bool _isDisposed;

    #region Constructors

    /// <summary>
    /// 初始化透明点击穿透覆盖层基类
    /// </summary>
    public TransparentOverlayBase()
    {
        // 核心 WPF 悬浮穿透窗口属性配置
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        Focusable = false;

        // 初始化根容器 Canvas（穿透命中测试）
        _containerCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };

        Content = _containerCanvas;
    }

    #endregion

    #region Properties

    /// <summary>
    /// 承载所有 HUD 子视觉元素（微标、横条、仪表盘）的根 Canvas 容器
    /// </summary>
    public Canvas OverlayContainer => _containerCanvas;

    /// <summary>
    /// 当前挂载的所有子视觉元素只读集合
    /// </summary>
    public IReadOnlyList<UIElement> Children => _children.AsReadOnly();

    /// <summary>
    /// 当前是否已被挂载到外部 Canvas (例如 HDT 的 Core.OverlayCanvas)
    /// </summary>
    public bool IsAttachedToHostCanvas => _hostCanvas != null;

    /// <summary>
    /// 挂载的外部宿主 Canvas 实例 (未挂载时为 null)
    /// </summary>
    public Canvas? HostCanvas => _hostCanvas;

    /// <summary>
    /// 宿主原生窗口句柄 (HWND)。在独立窗口形态下有效，若为纯外部 Canvas 挂载或未就绪时为 IntPtr.Zero。
    /// </summary>
    public IntPtr WindowHandle
    {
        get
        {
            if (_windowHandle == IntPtr.Zero && CheckAccess())
            {
                var helper = new WindowInteropHelper(this);
                _windowHandle = helper.Handle;
            }
            return _windowHandle;
        }
    }

    /// <summary>
    /// 当前覆盖层是否处于可见状态
    /// </summary>
    public new bool IsVisible
    {
        get
        {
            if (IsAttachedToHostCanvas)
            {
                return _containerCanvas.Visibility == Visibility.Visible;
            }

            return base.IsVisible;
        }
    }

    /// <summary>
    /// 当前宿主窗口是否已启用 Win32 点击穿透样式
    /// </summary>
    public bool IsClickThrough
    {
        get
        {
            if (WindowHandle == IntPtr.Zero)
            {
                return false;
            }

            return WindowStyleHelper.IsClickThroughEnabled(WindowHandle);
        }
    }

    #endregion

    #region Lifecycle & Window Handle

    /// <summary>
    /// 窗口句柄初始化时自动应用 Win32 点击穿透与分层样式
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyClickThrough();
    }

    /// <summary>
    /// 确保 Win32 原生窗口句柄已创建并返回，支持在脱机/测试环境下无需 Show() 即可预先初始化句柄
    /// </summary>
    /// <returns>窗口 HWND</returns>
    public IntPtr EnsureHandle()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.EnsureHandle();
            ApplyClickThrough();
        }

        return _windowHandle;
    }

    /// <summary>
    /// 显式为当前窗口应用 WS_EX_TRANSPARENT | WS_EX_LAYERED 等点击穿透样式。
    /// 严格遵循 COMP005 合规检查规则。
    /// </summary>
    public void ApplyClickThrough()
    {
        IntPtr hwnd = WindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int currentStyle = WindowStyleHelper.GetWindowLongPtr(hwnd, WindowStyleHelper.GWL_EXSTYLE).ToInt32();
        // 严格遵循 COMP005: 必须显式包含 WS_EX_TRANSPARENT (0x20) 与 WS_EX_LAYERED (0x80000)
        WindowStyleHelper.SetWindowLongPtr(
            hwnd,
            WindowStyleHelper.GWL_EXSTYLE,
            (IntPtr)(currentStyle | WindowStyleHelper.WS_EX_TRANSPARENT | WindowStyleHelper.WS_EX_LAYERED | WindowStyleHelper.WS_EX_TOOLWINDOW | WindowStyleHelper.WS_EX_NOACTIVATE));
    }

    #endregion

    #region Host Canvas Management (HDT Integration)

    /// <summary>
    /// 挂载/附加至外部指定的 Canvas (例如 HDT 的 Core.OverlayCanvas)
    /// </summary>
    /// <param name="hostCanvas">外部 Canvas 容器</param>
    public void AttachToHostCanvas(Canvas hostCanvas)
    {
        if (hostCanvas == null) throw new ArgumentNullException(nameof(hostCanvas));

        if (ReferenceEquals(_hostCanvas, hostCanvas))
        {
            return;
        }

        DetachFromHostCanvas();

        // 将容器从独立窗体 Content 中移出，挂载至外部 Canvas
        Content = null;
        _hostCanvas = hostCanvas;
        _hostCanvas.Children.Add(_containerCanvas);

        // 在附加模式下，独立顶级 Window 保持隐藏
        if (base.IsVisible)
        {
            Hide();
        }
    }

    /// <summary>
    /// 从外部宿主 Canvas 分离，恢复独立顶级 Window 管理
    /// </summary>
    public void DetachFromHostCanvas()
    {
        if (_hostCanvas == null)
        {
            return;
        }

        _hostCanvas.Children.Remove(_containerCanvas);
        _hostCanvas = null;

        // 重新恢复作为本窗体的 Content
        Content = _containerCanvas;
    }

    /// <summary>
    /// 尝试通过反射动态探测并挂载至 HDT 的 Core.OverlayCanvas。
    /// 若 HDT 运行环境未就绪或未找到 Core.OverlayCanvas，则保持独立 Window 模式并返回 false。
    /// </summary>
    /// <returns>是否成功发现并挂载至 HDT 覆盖画布</returns>
    public bool TryAttachToHdtOverlayCanvas()
    {
        try
        {
            var coreType = Type.GetType("Hearthstone_Deck_Tracker.API.Core, HearthstoneDeckTracker")
                           ?? Type.GetType("HearthstoneDeckTracker.API.Core, HearthstoneDeckTracker")
                           ?? AppDomain.CurrentDomain.GetAssemblies()
                               .Select(a => a.GetType("Hearthstone_Deck_Tracker.API.Core") ?? a.GetType("HearthstoneDeckTracker.API.Core"))
                               .FirstOrDefault(t => t != null);

            if (coreType != null)
            {
                var prop = coreType.GetProperty("OverlayCanvas", BindingFlags.Public | BindingFlags.Static);
                if (prop?.GetValue(null) is Canvas hdtCanvas)
                {
                    AttachToHostCanvas(hdtCanvas);
                    return true;
                }
            }
        }
        catch
        {
            // 异常兜底，降级保持独立窗口
        }

        return false;
    }

    #endregion

    #region Display & Bounds Control

    /// <summary>
    /// 显示覆盖层
    /// </summary>
    public void ShowOverlay()
    {
        if (IsAttachedToHostCanvas)
        {
            _containerCanvas.Visibility = Visibility.Visible;
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// 隐藏覆盖层
    /// </summary>
    public void HideOverlay()
    {
        if (IsAttachedToHostCanvas)
        {
            _containerCanvas.Visibility = Visibility.Collapsed;
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// 调整覆盖层的位置与尺寸 (支持 T19 窗口吸附与分辨率适配)
    /// </summary>
    public void SetBounds(double left, double top, double width, double height)
    {
        if (width >= 0)
        {
            _containerCanvas.Width = width;
            if (!IsAttachedToHostCanvas)
            {
                Width = width;
            }
        }

        if (height >= 0)
        {
            _containerCanvas.Height = height;
            if (!IsAttachedToHostCanvas)
            {
                Height = height;
            }
        }

        if (!IsAttachedToHostCanvas)
        {
            Left = left;
            Top = top;
        }
        else
        {
            Canvas.SetLeft(_containerCanvas, left);
            Canvas.SetTop(_containerCanvas, top);
        }
    }

    #endregion

    #region Child Visual Elements Management

    /// <summary>
    /// 添加一个子视觉元素（微标、横条、仪表盘等）
    /// </summary>
    public void AddChild(UIElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        if (!_children.Contains(element))
        {
            _children.Add(element);
            _containerCanvas.Children.Add(element);
        }
    }

    /// <summary>
    /// 在指定坐标位置添加并布局子视觉元素
    /// </summary>
    public void AddChild(UIElement element, double left, double top, int zIndex = 0)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        Canvas.SetZIndex(element, zIndex);

        AddChild(element);
    }

    /// <summary>
    /// 移除指定的子视觉元素
    /// </summary>
    public bool RemoveChild(UIElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        bool removed = _children.Remove(element);
        if (removed)
        {
            _containerCanvas.Children.Remove(element);
        }

        return removed;
    }

    /// <summary>
    /// 清空所有子视觉元素
    /// </summary>
    public void ClearChildren()
    {
        _children.Clear();
        _containerCanvas.Children.Clear();
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// 释放覆盖层资源，从宿主 Canvas 分离并安全关闭窗口
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            DetachFromHostCanvas();
            ClearChildren();

            try
            {
                Close();
            }
            catch
            {
                // 窗口若已关闭则忽略
            }
        }

        _isDisposed = true;
    }

    #endregion
}
