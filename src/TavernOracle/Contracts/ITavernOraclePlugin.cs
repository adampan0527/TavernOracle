using System;
using TavernOracle.Presentation.Settings;

namespace TavernOracle.Contracts
{
    /// <summary>
    /// Abstraction contract for TavernOracle core plugin lifecycle, state tracking, and decoupled orchestration.
    /// </summary>
    public interface ITavernOraclePlugin
    {
        /// <summary>
        /// Whether the plugin is currently active and loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// The UTC timestamp of the most recent update tick.
        /// </summary>
        DateTime? LastUpdateTime { get; }

        /// <summary>
        /// Total number of update ticks processed since load.
        /// </summary>
        ulong UpdateTickCount { get; }

        /// <summary>
        /// 玩家个性化配置与风控参数快照。
        /// </summary>
        PluginSettings Settings { get; }

        /// <summary>
        /// 嵌入 HDT 插件选项界面的快捷控制面板控件。
        /// </summary>
        SettingsView SettingsControl { get; }

        /// <summary>
        /// Event fired when the plugin is loaded and initialized.
        /// </summary>
        event Action? Loaded;

        /// <summary>
        /// Event fired when the plugin is unloaded and shut down.
        /// </summary>
        event Action? Unloaded;

        /// <summary>
        /// Event fired when an update tick occurs.
        /// </summary>
        event Action? Updated;

        /// <summary>
        /// Event fired when the settings button is pressed.
        /// </summary>
        event Action? ButtonPressed;
    }
}
