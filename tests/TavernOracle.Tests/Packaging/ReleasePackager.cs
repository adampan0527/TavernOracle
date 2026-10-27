using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TavernOracle.Tests.Packaging
{
    /// <summary>
    /// Utility helper for generating, locating, and inspecting HDT Release packages (T23).
    /// Creates the standard distribution zip package (e.g. dist/TavernOracle-v1.0.0.zip)
    /// adhering to HDT plugin deployment guidelines.
    /// </summary>
    public static class ReleasePackager
    {
        public const string DefaultVersion = "1.0.0";
        public const string PluginAssemblyName = "TavernOracle.dll";
        public const string PluginDepsName = "TavernOracle.deps.json";
        public const string PluginSymbolsName = "TavernOracle.pdb";
        public const string ReadmeFileName = "README.md";
        public const string ArchetypesFileName = "Priors/archetypes.json";

        /// <summary>
        /// Locates the root repository directory containing TavernOracle.sln.
        /// </summary>
        public static string FindRepoRoot()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(currentDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TavernOracle.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                // Fallback to CurrentDirectory
                dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TavernOracle.sln")))
                {
                    dir = dir.Parent;
                }
            }

            if (dir == null)
            {
                throw new DirectoryNotFoundException("Could not locate repository root directory containing TavernOracle.sln.");
            }

            return dir.FullName;
        }

        /// <summary>
        /// Returns the standard path to the release package zip file: dist/TavernOracle-v{version}.zip.
        /// </summary>
        public static string GetPackageZipPath(string repoRoot, string version = DefaultVersion)
        {
            return Path.Combine(repoRoot, "dist", $"TavernOracle-v{version}.zip");
        }

        /// <summary>
        /// Ensures that the release package zip file exists and is valid.
        /// If it doesn't exist, builds and creates it automatically.
        /// </summary>
        public static string EnsurePackageCreated(string? repoRoot = null, string version = DefaultVersion, string configuration = "Release")
        {
            repoRoot ??= FindRepoRoot();
            string zipPath = GetPackageZipPath(repoRoot, version);

            if (File.Exists(zipPath))
            {
                var fileInfo = new FileInfo(zipPath);
                if (fileInfo.Length > 10_000)
                {
                    return zipPath;
                }
            }

            return CreateReleasePackage(repoRoot, version, configuration);
        }

        /// <summary>
        /// Packages Release build artifacts and documentation into the distribution zip file.
        /// </summary>
        public static string CreateReleasePackage(string repoRoot, string version = DefaultVersion, string configuration = "Release")
        {
            string distDir = Path.Combine(repoRoot, "dist");
            Directory.CreateDirectory(distDir);

            string zipPath = GetPackageZipPath(repoRoot, version);

            // Locate compiled assembly from src or dist
            string primaryBinDir = Path.Combine(repoRoot, "src", "TavernOracle", "bin", configuration, "net8.0-windows");
            string fallbackBinDir = Path.Combine(repoRoot, "src", "TavernOracle", "bin", "Debug", "net8.0-windows");
            string distPluginsDir = Path.Combine(repoRoot, "dist", "Plugins", "TavernOracle");

            string binDir = primaryBinDir;
            if (!File.Exists(Path.Combine(binDir, PluginAssemblyName)))
            {
                if (File.Exists(Path.Combine(distPluginsDir, PluginAssemblyName)))
                {
                    binDir = distPluginsDir;
                }
                else if (File.Exists(Path.Combine(fallbackBinDir, PluginAssemblyName)))
                {
                    binDir = fallbackBinDir;
                }
                else
                {
                    throw new FileNotFoundException(
                        $"Could not find compiled '{PluginAssemblyName}' in '{primaryBinDir}' or '{distPluginsDir}'. Please build the project first.");
                }
            }

            string dllPath = Path.Combine(binDir, PluginAssemblyName);
            string depsPath = Path.Combine(binDir, PluginDepsName);
            string pdbPath = Path.Combine(binDir, PluginSymbolsName);
            string archetypesPath = Path.Combine(binDir, "Priors", "archetypes.json");
            if (!File.Exists(archetypesPath))
            {
                archetypesPath = Path.Combine(repoRoot, "src", "TavernOracle", "Priors", "archetypes.json");
            }

            string readmePath = Path.Combine(repoRoot, ReadmeFileName);

            // Remove existing zip if present
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // Create temporary staging folder
            string stageDir = Path.Combine(Path.GetTempPath(), "TavernOracle_Package_Stage_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stageDir);

            try
            {
                // Copy core plugin assembly
                File.Copy(dllPath, Path.Combine(stageDir, PluginAssemblyName), true);

                // Copy deps.json if present
                if (File.Exists(depsPath))
                {
                    File.Copy(depsPath, Path.Combine(stageDir, PluginDepsName), true);
                }

                // Copy pdb symbols if present
                if (File.Exists(pdbPath))
                {
                    File.Copy(pdbPath, Path.Combine(stageDir, PluginSymbolsName), true);
                }

                // Copy readme documentation
                if (File.Exists(readmePath))
                {
                    File.Copy(readmePath, Path.Combine(stageDir, ReadmeFileName), true);
                }

                // Copy Prior archetypes if available
                if (File.Exists(archetypesPath))
                {
                    string priorsTargetDir = Path.Combine(stageDir, "Priors");
                    Directory.CreateDirectory(priorsTargetDir);
                    File.Copy(archetypesPath, Path.Combine(priorsTargetDir, "archetypes.json"), true);
                }

                // Compress stage directory into zip package
                ZipFile.CreateFromDirectory(stageDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stageDir))
                    {
                        Directory.Delete(stageDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup failure in temp directory
                }
            }

            return zipPath;
        }

        /// <summary>
        /// Computes the SHA256 hex string of a file.
        /// </summary>
        public static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
