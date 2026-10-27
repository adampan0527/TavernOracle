[CmdletBinding()]
param([string]$HdtDirectory = 'C:\Users\Adam\Desktop\game\HDT')

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source = Join-Path $repoRoot 'dist\Plugins\TavernOracle'
if (-not (Test-Path -LiteralPath (Join-Path $source 'TavernOracle.dll'))) {
    throw 'Build the net472 plugin before installation.'
}
if (-not (Test-Path -LiteralPath (Join-Path $HdtDirectory 'HearthstoneDeckTracker.exe'))) {
    throw "HDT installation not found: $HdtDirectory"
}
if (Get-Process | Where-Object { $_.ProcessName -like 'HearthstoneDeckTracker*' }) {
    throw 'Please exit HDT normally, then run this installer again. HDT is holding the old plugin DLL open.'
}
$targets = @(
    (Join-Path $HdtDirectory 'Plugins\TavernOracle'),
    (Join-Path $env:APPDATA 'HearthstoneDeckTracker\Plugins\TavernOracle')
)
$backupRoot = Join-Path $repoRoot ('dist\plugin-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
for ($i = 0; $i -lt $targets.Count; $i++) {
    $target = $targets[$i]
    if (Test-Path -LiteralPath $target) {
        $backup = Join-Path $backupRoot $i
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Get-ChildItem -LiteralPath $target -File | Copy-Item -Destination $backup
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $target -Force
    $expected = (Get-FileHash -LiteralPath (Join-Path $source 'TavernOracle.dll')).Hash
    $actual = (Get-FileHash -LiteralPath (Join-Path $target 'TavernOracle.dll')).Hash
    if ($expected -ne $actual) { throw "Plugin verification failed: $target" }
    Write-Output "Installed and verified: $target"
}
Write-Output 'Start HDT and ensure TavernOracle is enabled. The banner should show a data status or a shopping recommendation.'
