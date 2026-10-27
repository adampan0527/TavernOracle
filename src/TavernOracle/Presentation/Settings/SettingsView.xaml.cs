using System;
using System.Windows;
using System.Windows.Controls;

namespace TavernOracle.Presentation.Settings;

/// <summary>
/// 嵌入 HDT 插件选项界面的 WPF UserControl (T22 SettingsView)。
/// 提供战棋深色调亚克力磨砂视觉风格，绑定 SettingsViewModel 实现实时响应与本地持久化。
/// 严格遵守暴雪“纸笔准则（Pencil &amp; Paper Rule）”。
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// 当前绑定的设置视图模型。
    /// </summary>
    public SettingsViewModel? ViewModel
    {
        get => DataContext as SettingsViewModel;
        set => DataContext = value;
    }

    /// <summary>
    /// 无参构造函数，支持 XAML 设计器与反射实例化。
    /// </summary>
    public SettingsView()
        : this(new SettingsViewModel())
    {
    }

    /// <summary>
    /// 支持依赖注入指定 ViewModel 的构造函数。
    /// </summary>
    /// <param name="viewModel">设置视图模型</param>
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// 响应“恢复默认设置”按钮点击。
    /// </summary>
    private void OnResetButtonClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel?.ResetDefaults();
        }
        catch
        {
            // Defensive degradation: Zero unhandled exceptions
        }
    }
}
