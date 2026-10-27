using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hearthstone_Deck_Tracker.Plugins;
using TavernOracle;
using TavernOracle.Contracts;
using Xunit;

namespace TavernOracle.Tests
{
    public class TavernOraclePluginTests
    {
        [Fact]
        public void Metadata_Properties_ShouldReturnExpectedValues()
        {
            var plugin = new TavernOraclePlugin();

            Assert.Equal("TavernOracle", plugin.Name);
            Assert.Contains("酒馆战棋", plugin.Description);
            Assert.Equal("设置", plugin.ButtonText);
            Assert.Equal("TavernOracle Team", plugin.Author);
            Assert.NotNull(plugin.Version);
            Assert.True(plugin.Version.Major >= 1, "Plugin major version should be at least 1");
            Assert.Null(plugin.MenuItem);
        }

        [Fact]
        public void InitialState_ShouldBeUnloaded()
        {
            var plugin = new TavernOraclePlugin();

            Assert.False(plugin.IsLoaded);
            Assert.Null(plugin.LastUpdateTime);
            Assert.Equal(0UL, plugin.UpdateTickCount);
        }

        [Fact]
        public void OnLoad_ShouldSetIsLoadedTrue_AndTriggerEvent()
        {
            var plugin = new TavernOraclePlugin();
            var loadedEventCount = 0;
            plugin.Loaded += () => loadedEventCount++;

            plugin.OnLoad();

            Assert.True(plugin.IsLoaded);
            Assert.Equal(1, loadedEventCount);

            // Repeated OnLoad should be idempotent and not fire event again
            plugin.OnLoad();
            Assert.True(plugin.IsLoaded);
            Assert.Equal(1, loadedEventCount);
        }

        [Fact]
        public void OnUpdate_WhenLoaded_ShouldIncrementTickCount_AndTriggerEvent()
        {
            var plugin = new TavernOraclePlugin();
            var updateEventCount = 0;
            plugin.Updated += () => updateEventCount++;

            plugin.OnLoad();
            var beforeUpdate = DateTime.UtcNow.AddSeconds(-1);

            plugin.OnUpdate();

            Assert.Equal(1UL, plugin.UpdateTickCount);
            Assert.NotNull(plugin.LastUpdateTime);
            Assert.True(plugin.LastUpdateTime >= beforeUpdate);
            Assert.Equal(1, updateEventCount);

            plugin.OnUpdate();
            Assert.Equal(2UL, plugin.UpdateTickCount);
            Assert.Equal(2, updateEventCount);
        }

        [Fact]
        public void OnUpdate_WhenNotLoaded_ShouldNotIncrementTickCount()
        {
            var plugin = new TavernOraclePlugin();
            var updateEventCount = 0;
            plugin.Updated += () => updateEventCount++;

            plugin.OnUpdate();

            Assert.Equal(0UL, plugin.UpdateTickCount);
            Assert.Null(plugin.LastUpdateTime);
            Assert.Equal(0, updateEventCount);
        }

        [Fact]
        public void OnButtonPress_ShouldTriggerButtonPressedEvent()
        {
            var plugin = new TavernOraclePlugin();
            var buttonPressCount = 0;
            plugin.ButtonPressed += () => buttonPressCount++;

            plugin.OnButtonPress();

            Assert.Equal(1, buttonPressCount);
        }

        [Fact]
        public void OnUnload_ShouldSetIsLoadedFalse_AndTriggerEvent()
        {
            var plugin = new TavernOraclePlugin();
            var unloadedEventCount = 0;
            plugin.Unloaded += () => unloadedEventCount++;

            plugin.OnLoad();
            Assert.True(plugin.IsLoaded);

            plugin.OnUnload();
            Assert.False(plugin.IsLoaded);
            Assert.Equal(1, unloadedEventCount);

            // Repeated OnUnload should be idempotent
            plugin.OnUnload();
            Assert.False(plugin.IsLoaded);
            Assert.Equal(1, unloadedEventCount);
        }

        [Fact]
        public void IPluginContract_ShouldBeAssignableAndImplementInterface()
        {
            Assert.True(typeof(IPlugin).IsAssignableFrom(typeof(TavernOraclePlugin)));
            Assert.True(typeof(ITavernOraclePlugin).IsAssignableFrom(typeof(TavernOraclePlugin)));

            IPlugin pluginInterface = new TavernOraclePlugin();
            Assert.Equal("TavernOracle", pluginInterface.Name);
            Assert.Equal("设置", pluginInterface.ButtonText);

            // Lifecycle invocation via IPlugin contract
            pluginInterface.OnLoad();
            pluginInterface.OnUpdate();
            pluginInterface.OnButtonPress();
            pluginInterface.OnUnload();
        }

        [Fact]
        public void ComplianceCheck_NoProhibitedPInvokeOrSendInput()
        {
            var assembly = typeof(TavernOraclePlugin).Assembly;
            var prohibitedApis = new[]
            {
                "SendInput",
                "mouse_event",
                "keybd_event",
                "SendKeys",
                "OpenProcess",
                "WriteProcessMemory"
            };

            foreach (var type in assembly.GetTypes())
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    var dllImport = method.GetCustomAttribute<DllImportAttribute>();
                    if (dllImport != null)
                    {
                        var entryPoint = dllImport.EntryPoint ?? method.Name;
                        foreach (var prohibited in prohibitedApis)
                        {
                            Assert.False(
                                string.Equals(entryPoint, prohibited, StringComparison.OrdinalIgnoreCase),
                                $"Compliance violation: {type.FullName}.{method.Name} imports prohibited API '{prohibited}'");
                        }
                    }
                }
            }
        }
    }
}
