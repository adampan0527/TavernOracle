#if !HAS_HDT_REFERENCE
namespace Hearthstone_Deck_Tracker.Plugins
{
    /// <summary>
    /// Contract matching Hearthstone Deck Tracker (HDT) plugin interface.
    /// Provides decoupled offline compilation and mockable testing.
    /// When hosted within HDT, mirrors the exact signatures loaded by HDT's PluginManager reflection mechanism.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Display name of the plugin in HDT's plugin list.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Detailed description of the plugin functionality.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Label text for the settings button in HDT's options menu.
        /// </summary>
        string ButtonText { get; }

        /// <summary>
        /// Author / organization developing the plugin.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Semantic version of the plugin.
        /// </summary>
        System.Version Version { get; }

        /// <summary>
        /// Optional top menu item added to HDT's main window menu bar.
        /// </summary>
        System.Windows.Controls.MenuItem? MenuItem { get; }

        /// <summary>
        /// Lifecycle callback invoked when the plugin is enabled / loaded by HDT.
        /// </summary>
        void OnLoad();

        /// <summary>
        /// Lifecycle callback invoked when the plugin is disabled / unloaded by HDT.
        /// </summary>
        void OnUnload();

        /// <summary>
        /// Callback invoked when the user clicks the plugin button in HDT's options menu.
        /// </summary>
        void OnButtonPress();

        /// <summary>
        /// Periodic tick callback invoked by HDT (approx. every 100ms).
        /// </summary>
        void OnUpdate();
    }
}
#endif
