# ============================================================================
# SeriousView -- quick developer run (from source, no publish step)
# ============================================================================
# Launches the Debug build straight from source via `dotnet run`. This is the
# fast inner-loop launcher -- it incrementally rebuilds only what changed, so
# it's "no [full] build" in the sense that there's no 48 MB single-file publish.
# Debug includes Avalonia DevTools (F12). Pass a file to open it on startup.
#
#   .\run.ps1                 # launch (restores last session, else welcome)
#   .\run.ps1 README.md       # launch and open a file
#   .\run.ps1 -Release        # run the Release config instead of Debug
# ============================================================================

param(
    [switch]$Release,
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$FileArgs
)

chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

$root    = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$project = Join-Path $root 'src\SeriousView\SeriousView.csproj'
$config  = if ($Release) { 'Release' } else { 'Debug' }

$runArgs = @('--project', $project, '-c', $config)
# Everything after `--` is forwarded to the app (so a path lands in args[0]).
if ($FileArgs) { $runArgs += '--'; $runArgs += $FileArgs }

Write-Host "  dotnet run ($config)..." -ForegroundColor Cyan
dotnet run @runArgs
