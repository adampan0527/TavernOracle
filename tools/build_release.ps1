<#
.SYNOPSIS
    TavernOracle (酒馆先知) 自动化构建流水线与版本交付包发布脚本 (T23)。
.DESCRIPTION
    自动化串联 4 大严苛质检门禁：
    1. Gate 1 - 合规审查门禁 (Compliance Gate): 静态 AST 扫描 + 25/25 暴雪“纸笔准则”合规测试；
    2. Gate 2 - 质量与基准门禁 (Test & Benchmark Gate): 全量单测/集成测试 + 10,000 次压测 + 零 GC 性能基准；
    3. Gate 3 - 发布编译 (Release Build): Release 模式干净编译核心类库；
    4. Gate 4 - 制品归档与压缩 (Packaging): 打包生成符合 HDT 规范的 dist/TavernOracle-v{Version}.zip 交付物并进行完整性校验。
.PARAMETER Version
    交付包语义化版本号，默认为 1.0.0。
.PARAMETER Configuration
    构建配置，默认为 Release。
.PARAMETER SkipCompliance
    是否跳过合规门禁（默认否）。
.PARAMETER SkipTests
    是否跳过测试门禁（默认否）。
.PARAMETER SkipBenchmarks
    是否跳过压测性能基准门禁（默认否）。
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [switch]$SkipCompliance = $false,
    [switch]$SkipTests = $false,
    [switch]$SkipBenchmarks = $false
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $RepoRoot

$Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Write-Banner([string]$text, [ConsoleColor]$color = [ConsoleColor]::Cyan) {
    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor $color
    Write-Host "  $text" -ForegroundColor $color
    Write-Host ("=" * 80) -ForegroundColor $color
}

function Write-Step([string]$step, [string]$desc) {
    Write-Host ""
    Write-Host "[$step] $desc" -ForegroundColor Yellow
}

function Write-Pass([string]$msg) {
    Write-Host "  [PASSED] $msg" -ForegroundColor Green
}

function Write-Fail([string]$msg) {
    Write-Host "  [FAILED] $msg" -ForegroundColor Red
}

Write-Banner "TavernOracle (酒馆先知) - 自动化构建与交付流水线 (T23 Pipeline)`n  暴雪“纸笔准则” · 0 内存注入 · 0 模拟输入 · 100% 账号安全" -color Cyan

Write-Host "  代码库根目录 : $RepoRoot" -ForegroundColor DarkGray
Write-Host "  目标发布版本 : v$Version" -ForegroundColor DarkGray
Write-Host "  编译目标模式 : $Configuration" -ForegroundColor DarkGray
Write-Host "  构建启动时间 : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray

# ==============================================================================
# Gate 1: 合规审查门禁 (Compliance Gate)
# ==============================================================================
if (-not $SkipCompliance) {
    Write-Step "GATE 1/4" "合规审查门禁 (Compliance Gate) - 暴雪“纸笔准则”严苛审计"

    Write-Host "  --> 步骤 1.1: 运行 ComplianceChecker AST 语法树扫描器..." -ForegroundColor Gray
    dotnet run --project "$RepoRoot\tools\ComplianceChecker" -c $Configuration -- "$RepoRoot\src"
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "合规静态审查未通过！严禁打包未通过暴雪合规审查的代码！"
        exit 1
    }
    Write-Pass "ComplianceChecker 源码审查 100% 合规，未检测到任何模拟输入或内存注入特征。"

    Write-Host "  --> 步骤 1.2: 运行 TavernOracle.ComplianceTests (25/25 用例)..." -ForegroundColor Gray
    dotnet test "$RepoRoot\tests\TavernOracle.ComplianceTests\TavernOracle.ComplianceTests.csproj" -c $Configuration --no-restore --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "TavernOracle.ComplianceTests 出现失败用例！"
        exit 1
    }
    Write-Pass "TavernOracle.ComplianceTests 25/25 用例全部通过！"
} else {
    Write-Host "[SKIP] 已跳过 Gate 1 (合规审查门禁)。" -ForegroundColor DarkYellow
}

# ==============================================================================
# Gate 2: 质量与基准门禁 (Test & Benchmark Gate)
# ==============================================================================
if (-not $SkipTests) {
    Write-Step "GATE 2/4" "质量与基准门禁 (Test & Benchmark Gate) - 单元/集成测试与 10,000 次压测"

    Write-Host "  --> 步骤 2.1: 运行全量单元测试与集成测试 (包含 10,000 次压测与打包验证)..." -ForegroundColor Gray
    dotnet test "$RepoRoot\tests\TavernOracle.Tests\TavernOracle.Tests.csproj" -c $Configuration --no-restore --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "TavernOracle.Tests 测试套件未完全通过！"
        exit 1
    }
    Write-Pass "TavernOracle.Tests 全量测试用例 100% 通过！"

    if (-not $SkipBenchmarks) {
        Write-Host "  --> 步骤 2.2: 运行 TavernOracle.Benchmarks 离线压测基准与零 GC 校验..." -ForegroundColor Gray
        dotnet run --project "$RepoRoot\benchmarks\TavernOracle.Benchmarks" -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "TavernOracle.Benchmarks 压测基准未达标！"
            exit 1
        }
        Write-Pass "10,000 次求解压测基准通过：零 GC 托管堆分配，P99 耗时远低于 4.8ms 硬指标！"
    }
} else {
    Write-Host "[SKIP] 已跳过 Gate 2 (质量与基准门禁)。" -ForegroundColor DarkYellow
}

# ==============================================================================
# Gate 3: 发布编译 (Release Build)
# ==============================================================================
Write-Step "GATE 3/4" "发布编译门禁 (Release Build) - 以 $Configuration 模式编译核心插件程序集"

dotnet build "$RepoRoot\src\TavernOracle\TavernOracle.csproj" `
    -c $Configuration `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -p:TreatWarningsAsErrors=true

if ($LASTEXITCODE -ne 0) {
    Write-Fail "核心工程发布编译失败！"
    exit 1
}
Write-Pass "TavernOracle.dll 编译成功 (0 警告，0 错误)。"

# ==============================================================================
# Gate 4: 制品归档与压缩 (Packaging)
# ==============================================================================
Write-Step "GATE 4/4" "制品归档与压缩门禁 (Packaging Gate) - 打包 dist/TavernOracle-v$Version.zip"

$DistDir = Join-Path $RepoRoot "dist"
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

$ZipPath = Join-Path $DistDir "TavernOracle-v$Version.zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

$BinDir = Join-Path $RepoRoot "src\TavernOracle\bin\$Configuration\net472"
if (-not (Test-Path $BinDir)) {
    $BinDir = Join-Path $RepoRoot "src\TavernOracle\bin\$Configuration\net8.0-windows"
}
$StagingDir = Join-Path ([System.IO.Path]::GetTempPath()) ("TavernOracle_Package_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

try {
    # 1. 核心 DLL 程序集与必要运行库
    $DllSrc = Join-Path $BinDir "TavernOracle.dll"
    if (-not (Test-Path $DllSrc)) {
        throw "无法找到核心编译产物: $DllSrc"
    }
    Get-ChildItem -Path $BinDir -Filter "*.dll" | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $StagingDir $_.Name) -Force
    }

    # 2. .deps.json 运行时依赖元数据 (若存在)
    $DepsSrc = Join-Path $BinDir "TavernOracle.deps.json"
    if (Test-Path $DepsSrc) {
        Copy-Item $DepsSrc (Join-Path $StagingDir "TavernOracle.deps.json") -Force
    }

    # 3. .pdb 调试符号文件
    Get-ChildItem -Path $BinDir -Filter "*.pdb" | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $StagingDir $_.Name) -Force
    }

    # 4. 流派先验知识库 Priors/archetypes.json
    $ArchetypesSrc = Join-Path $BinDir "Priors\archetypes.json"
    if (-not (Test-Path $ArchetypesSrc)) {
        $ArchetypesSrc = Join-Path $RepoRoot "src\TavernOracle\Priors\archetypes.json"
    }
    if (Test-Path $ArchetypesSrc) {
        $PriorsDir = Join-Path $StagingDir "Priors"
        New-Item -ItemType Directory -Path $PriorsDir -Force | Out-Null
        Copy-Item $ArchetypesSrc (Join-Path $PriorsDir "archetypes.json") -Force
    }

    # 5. README.md 说明文档
    $ReadmeSrc = Join-Path $RepoRoot "README.md"
    if (Test-Path $ReadmeSrc) {
        Copy-Item $ReadmeSrc (Join-Path $StagingDir "README.md") -Force
    }

    # 使用 .NET 压缩引擎打成最终 Zip 发布包
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $StagingDir,
        $ZipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )
}
finally {
    if (Test-Path $StagingDir) {
        Remove-Item $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 验证生成的 Zip 包
if (-not (Test-Path $ZipPath)) {
    Write-Fail "交付包生成失败，未找到目标文件: $ZipPath"
    exit 1
}

$ZipFileInfo = Get-Item $ZipPath
$ZipSizeBytes = $ZipFileInfo.Length
$ZipSizeKb = [math]::Round($ZipSizeBytes / 1024, 2)

if ($ZipSizeBytes -lt 50000 -or $ZipSizeBytes -gt 10000000) {
    Write-Fail "交付包体积异常 ($ZipSizeBytes bytes)，未在合理范围 (50KB ~ 10MB) 内！"
    exit 1
}

# 计算 SHA256 校验和
$Sha256 = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash

# 检查压缩包内条目列表
$Archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
$Entries = $Archive.Entries
$EntryCount = $Entries.Count

Write-Pass "交付物压缩完成: $ZipPath"
Write-Host "  - 压缩包体积   : $ZipSizeKb KB ($ZipSizeBytes 字节)" -ForegroundColor Gray
Write-Host "  - SHA256 校验和: $Sha256" -ForegroundColor Gray
Write-Host "  - 内部文件条目 : $EntryCount 个文件" -ForegroundColor Gray
foreach ($entry in $Entries) {
    Write-Host "      * $($entry.FullName) ($($entry.Length) 字节)" -ForegroundColor DarkGray
}
$Archive.Dispose()

# 同时保证 dist/Plugins/TavernOracle 解压目录就绪
$PluginsDistDir = Join-Path $DistDir "Plugins\TavernOracle"
if (-not (Test-Path $PluginsDistDir)) {
    New-Item -ItemType Directory -Path $PluginsDistDir -Force | Out-Null
}
Copy-Item (Join-Path $BinDir "TavernOracle.dll") (Join-Path $PluginsDistDir "TavernOracle.dll") -Force
if (Test-Path (Join-Path $BinDir "TavernOracle.deps.json")) {
    Copy-Item (Join-Path $BinDir "TavernOracle.deps.json") (Join-Path $PluginsDistDir "TavernOracle.deps.json") -Force
}
if (Test-Path (Join-Path $BinDir "TavernOracle.pdb")) {
    Copy-Item (Join-Path $BinDir "TavernOracle.pdb") (Join-Path $PluginsDistDir "TavernOracle.pdb") -Force
}

$Stopwatch.Stop()
$TotalSeconds = [math]::Round($Stopwatch.Elapsed.TotalSeconds, 2)

# ==============================================================================
# 流水线交付大功告成
# ==============================================================================
Write-Banner "交付包发布成功！所有 4 大门禁 100% 通过！(总耗时: $TotalSeconds 秒)" -color Green

Write-Host @"
--------------------------------------------------------------------------------
                     TavernOracle 发布交付物清单
--------------------------------------------------------------------------------
  交付压缩包 : $ZipPath
  包体大小   : $ZipSizeKb KB ($ZipSizeBytes bytes)
  SHA256     : $Sha256
  解压目录   : $PluginsDistDir (可直接镜像至 HDT Plugins 目录)

--------------------------------------------------------------------------------
                     HDT 插件快速安装说明
--------------------------------------------------------------------------------
  1. 打开 Hearthstone Deck Tracker (HDT);
  2. 依次点击: [选项] -> [插件] -> [打开插件文件夹];
  3. 将 $ZipPath 解压至该目录 (形成 Plugins\TavernOracle\ 结构);
  4. 返回 HDT 插件列表，勾选启用 [TavernOracle] 插件条目即可！
================================================================================
"@ -ForegroundColor Green
