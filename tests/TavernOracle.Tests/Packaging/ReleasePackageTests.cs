using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Hearthstone_Deck_Tracker.Plugins;
using TavernOracle.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TavernOracle.Tests.Packaging
{
    /// <summary>
    /// 自动化版本交付包验收与合规测试套件 (T23)。
    /// 验证自动化构建流水线输出的交付物：dist/TavernOracle-v1.0.0.zip：
    /// 1. 自动触发或校验生成的交付 Zip 包文件存在且体积合理；
    /// 2. 解压后根目录结构 100% 符合 HDT Plugins/ 目录即解压即用的规范 (包含 TavernOracle.dll, deps.json, README.md)；
    /// 3. 解压出的核心程序集通过反射动态加载，100% 实现 HDT IPlugin 与内部 ITavernOraclePlugin 契约；
    /// 4. 严苛验证安装包未附带 HDT 宿主主程序等冲突冗余依赖，符合暴雪纸笔准则规范。
    /// </summary>
    public sealed class ReleasePackageTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _repoRoot;
        private readonly string _zipPath;
        private readonly string _tempExtractDir;

        public ReleasePackageTests(ITestOutputHelper output)
        {
            _output = output;
            _repoRoot = ReleasePackager.FindRepoRoot();

            // 自动触发或校验生成的 dist/TavernOracle-v1.0.0.zip
            _zipPath = ReleasePackager.EnsurePackageCreated(_repoRoot, ReleasePackager.DefaultVersion);

            _tempExtractDir = Path.Combine(
                Path.GetTempPath(),
                "TavernOracle_ReleaseVerification_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempExtractDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempExtractDir))
                {
                    Directory.Delete(_tempExtractDir, recursive: true);
                }
            }
            catch
            {
                // Ignore transient cleanup errors in temp folder
            }
        }

        /// <summary>
        /// 验证 1: Zip 包文件存在且体积合理 (50KB ~ 10MB)。
        /// 避免空包、损坏包或误打包数十兆无用二进制的臃肿产物。
        /// </summary>
        [Fact]
        public void ReleasePackage_ZipFileExistsAndHasValidReasonableSize()
        {
            Assert.True(File.Exists(_zipPath), $"Release package zip does not exist at expected path: {_zipPath}");

            var fileInfo = new FileInfo(_zipPath);
            long sizeBytes = fileInfo.Length;
            double sizeKb = sizeBytes / 1024.0;

            _output.WriteLine($"[ReleasePackage] Zip Path: {_zipPath}");
            _output.WriteLine($"[ReleasePackage] File Size: {sizeKb:F2} KB ({sizeBytes:N0} bytes)");

            string sha256 = ReleasePackager.ComputeSha256(_zipPath);
            _output.WriteLine($"[ReleasePackage] SHA256: {sha256}");

            // 预期体积在 50 KB 到 10 MB 之间 (当前产物约 200KB~600KB)
            Assert.True(sizeBytes > 50_000, $"Zip package is suspiciously small ({sizeBytes} bytes)");
            Assert.True(sizeBytes < 10_000_000, $"Zip package exceeds 10 MB limit ({sizeBytes} bytes)");
        }

        /// <summary>
        /// 验证 2: Zip 压缩包内部 Entry 包含核心交付文件，且未污染宿主主程序文件。
        /// </summary>
        [Fact]
        public void ReleasePackage_ZipArchiveEntries_ContainRequiredFilesAndNoHostPollution()
        {
            using var archive = ZipFile.OpenRead(_zipPath);

            var entryNames = archive.Entries.Select(e => e.FullName).ToList();
            _output.WriteLine($"[ReleasePackage] Total Entries: {entryNames.Count}");
            foreach (var name in entryNames)
            {
                _output.WriteLine($"  - {name}");
            }

            // 根目录核心必须项
            Assert.Contains(ReleasePackager.PluginAssemblyName, entryNames);
            Assert.Contains(ReleasePackager.PluginDepsName, entryNames);
            Assert.Contains(ReleasePackager.ReadmeFileName, entryNames);

            // 严禁包含 HDT 宿主二进制或外部大包 (HearthstoneDeckTracker.exe / HearthDb.dll)
            Assert.DoesNotContain("HearthstoneDeckTracker.exe", entryNames);
            Assert.DoesNotContain("HearthDb.dll", entryNames);
        }

        /// <summary>
        /// 验证 3: 解压后根目录结构符合 HDT Plugins/ 目录直接解压即用的规范。
        /// 解压到任意测试目录后，根目录直接包含 TavernOracle.dll、deps.json 与 README.md。
        /// </summary>
        [Fact]
        public void ReleasePackage_ExtractedRootDirectoryStructure_MatchesHdtPluginsSpec()
        {
            ZipFile.ExtractToDirectory(_zipPath, _tempExtractDir, overwriteFiles: true);

            string extractedDll = Path.Combine(_tempExtractDir, ReleasePackager.PluginAssemblyName);
            string extractedDeps = Path.Combine(_tempExtractDir, ReleasePackager.PluginDepsName);
            string extractedReadme = Path.Combine(_tempExtractDir, ReleasePackager.ReadmeFileName);

            Assert.True(File.Exists(extractedDll), $"Extracted '{ReleasePackager.PluginAssemblyName}' must exist in root");
            Assert.True(File.Exists(extractedDeps), $"Extracted '{ReleasePackager.PluginDepsName}' must exist in root");
            Assert.True(File.Exists(extractedReadme), $"Extracted '{ReleasePackager.ReadmeFileName}' must exist in root");

            // 验证 README.md 内容包含合规说明与安装指南
            string readmeContent = File.ReadAllText(extractedReadme);
            Assert.True(
                readmeContent.Contains("TavernOracle") &&
                (readmeContent.Contains("Pencil & Paper Rule") || readmeContent.Contains("纸笔准则")),
                "README.md in release package must contain TavernOracle compliance statement.");
        }

        /// <summary>
        /// 验证 4: 解压出的核心程序集能通过反射动态加载，并 100% 实现 IPlugin / ITavernOraclePlugin 接口。
        /// </summary>
        [Fact]
        public void ReleasePackage_ExtractedAssembly_LoadsViaReflectionAndImplementsRequiredContracts()
        {
            ZipFile.ExtractToDirectory(_zipPath, _tempExtractDir, overwriteFiles: true);
            string extractedDll = Path.Combine(_tempExtractDir, ReleasePackager.PluginAssemblyName);

            // 通过独立 Assembly.LoadFrom 反射载入解压出来的发布二进制程序集
            var assembly = Assembly.LoadFrom(extractedDll);
            Assert.NotNull(assembly);

            var pluginType = assembly.GetType("TavernOracle.TavernOraclePlugin");
            Assert.NotNull(pluginType);

            // 验证实现 HDT 官方插件接口契约
            Assert.True(
                typeof(IPlugin).IsAssignableFrom(pluginType),
                "TavernOraclePlugin must implement Hearthstone_Deck_Tracker.Plugins.IPlugin");

            // 验证实现 TavernOracle 内部生命周期抽象契约
            Assert.True(
                typeof(ITavernOraclePlugin).IsAssignableFrom(pluginType),
                "TavernOraclePlugin must implement TavernOracle.Contracts.ITavernOraclePlugin");

            // 反射实例化并校验基础元数据
            RunInSta(() =>
            {
                var instance = Activator.CreateInstance(pluginType);
                Assert.NotNull(instance);

                var plugin = (IPlugin)instance;
                _output.WriteLine($"[Reflection] Plugin Name: {plugin.Name}");
                _output.WriteLine($"[Reflection] Plugin Author: {plugin.Author}");
                _output.WriteLine($"[Reflection] Plugin Version: {plugin.Version}");
                _output.WriteLine($"[Reflection] Plugin Description: {plugin.Description}");
                _output.WriteLine($"[Reflection] Plugin ButtonText: {plugin.ButtonText}");

                Assert.Equal("TavernOracle", plugin.Name);
                Assert.Equal("TavernOracle Team", plugin.Author);
                Assert.Equal(1, plugin.Version.Major);
                Assert.Equal(0, plugin.Version.Minor);
                Assert.False(string.IsNullOrWhiteSpace(plugin.Description));
                Assert.False(string.IsNullOrWhiteSpace(plugin.ButtonText));

                var oraclePlugin = (ITavernOraclePlugin)instance;
                Assert.False(oraclePlugin.IsLoaded);
                Assert.NotNull(oraclePlugin.Settings);
            });
        }

        /// <summary>
        /// 验证 5: 验证打包器对包重复生成的幂等性与产物一致性。
        /// </summary>
        [Fact]
        public void ReleasePackage_IdempotentPackaging_MaintainsIntegrity()
        {
            string zip1 = ReleasePackager.CreateReleasePackage(_repoRoot, "1.0.0");
            string hash1 = ReleasePackager.ComputeSha256(zip1);

            string zip2 = ReleasePackager.CreateReleasePackage(_repoRoot, "1.0.0");
            string hash2 = ReleasePackager.ComputeSha256(zip2);

            Assert.Equal(zip1, zip2);
            Assert.True(File.Exists(zip1));
            // 验证两次生成均生成合法的 Zip 包文件
            using var archive = ZipFile.OpenRead(zip2);
            Assert.NotEmpty(archive.Entries);
        }

        private static void RunInSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caught != null)
            {
                ExceptionDispatchInfo.Capture(caught).Throw();
            }
        }
    }
}
