# ============================================================================
# Tittle -- Portable single-file build (self-contained, no runtime needed)
# ============================================================================
# Produces ONE Tittle.exe in dist/ that runs on any Windows 10/11 machine
# of the target architecture WITHOUT an installed .NET runtime. This is the
# "portable" half of the distribution story; the installer pipeline (Velopack /
# NSIS + auto-update) is a later milestone.
#
# Why these flags:
#   --self-contained                 bundle the .NET runtime -> no install needed
#   PublishSingleFile                collapse managed assemblies into one .exe
#   IncludeNativeLibrariesForSelf... fold Avalonia/Skia native .dll into the .exe
#                                    (without this they sit loose next to it)
#   PublishReadyToRun                AOT-precompile the IL so the UI stack isn't JIT'd
#                                    on the UI thread at first render (default ON)
#   PublishTrimmed (TrimMode=partial)  strip the .NET runtime + Avalonia (IsTrimmable) while
#                                    keeping our code + reflection-heavy libs whole (default ON)
#   DebugType/DebugSymbols=none      keep .pdb out of the shipped file
# Trimming is PARTIAL (see Tittle.csproj): only IsTrimmable assemblies (the .NET runtime + Avalonia)
# are trimmed; our assemblies and the reflection-heavy viewer libs are kept whole, so XAML reflection
# bindings / settings JSON never break. FULL trim is intentionally avoided (saves ~17MB more but would
# strip those reflection paths). Validated end-to-end render in plans/trim-qa.
#
# Cold start is the priority over file size (a viewer is launched repeatedly).
# Measured on win-x64 (median of 6, after warmup): the shipped default
# (R2R on, no compression) reaches input-idle in ~740ms vs ~1470ms for the old
# compressed/no-R2R bundle — 2x faster. Two factors: removing single-file
# compression saves the per-launch decompress (~440ms), R2R removes first-render
# JIT (~290ms). Size with partial trimming: ~121MB (vs ~171MB untrimmed, ~53MB
# old compressed). Use -Compress to trade size for speed, -NoTrim to skip trimming.
#
# Usage:
#   .\build.ps1                 # Release, win-x64, R2R, uncompressed, partial-trimmed
#   .\build.ps1 -Rid win-arm64  # build for ARM64 instead
#   .\build.ps1 -NoReadyToRun   # skip R2R (smaller, slower first render)
#   .\build.ps1 -NoTrim         # skip trimming (bigger ~171MB, zero trim risk)
#   .\build.ps1 -Compress       # re-enable single-file compression (smaller, slower start)
#   .\build.ps1 -NoOpen         # don't open Explorer when finished
#   .\build.ps1 -OutDir dist\win-x64   # alternate output dir (build_all puts each RID
#                                       in its own subfolder so phases don't clobber)
# ============================================================================

param(
    [string]$Rid = 'win-x64',
    [switch]$NoReadyToRun,   # opt OUT of R2R (R2R is on by default for faster cold start)
    [switch]$NoTrim,         # opt OUT of trimming (trimming is on by default, partial mode)
    [switch]$Compress,       # opt IN to single-file compression (smaller file, slower start)
    [switch]$NoOpen,
    [string]$OutDir = 'dist'
)

$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

# Shared console UI + helpers (UTF-8 console, box glyphs, Write-Banner/Step/Ok/...,
# Get-FileHashSHA256, Show-Notification). Keep ScriptKit.ps1 identical across repos.
. (Join-Path $root 'ScriptKit.ps1')

$project = Join-Path $root 'src\Tittle\Tittle.csproj'
$outDir  = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $root $OutDir }
$start   = Get-Date

Write-Banner "Tittle -- portable single-file" "Release  $Rid  single-file  self-contained"

# Fresh output every time so a stale exe can never be mistaken for a new build.
if (Test-Path -LiteralPath $outDir) { Remove-Item -LiteralPath $outDir -Recurse -Force }

$publishArgs = @(
    '-c', 'Release',
    '-r', $Rid,
    '--self-contained', 'true',
    '-o', $outDir,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    "-p:EnableCompressionInSingleFile=$($Compress.IsPresent.ToString().ToLower())",
    "-p:PublishReadyToRun=$((-not $NoReadyToRun).ToString().ToLower())",
    "-p:PublishTrimmed=$((-not $NoTrim).ToString().ToLower())",
    '-p:DebugType=none',
    '-p:DebugSymbols=false'
)

Write-Info 'Publishing (first run restores the runtime pack -- can take a minute)...'
dotnet publish $project @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Fail 'BUILD FAILED'
    Show-Notification -Title 'Tittle build' -Body 'Publish failed' -IsError
    exit $LASTEXITCODE
}

# Drop any stray .pdb the single-file bundler may have left behind.
Get-ChildItem -LiteralPath $outDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

$exe = Join-Path $outDir 'Tittle.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Fail "expected $exe was not produced."
    Show-Notification -Title 'Tittle build' -Body 'Publish produced no exe' -IsError
    exit 1
}

$sizeMB = '{0:0.0} MB' -f ((Get-Item -LiteralPath $exe).Length / 1MB)
$sha    = Get-FileHashSHA256 -Path $exe
$dur    = (Get-Date) - $start
$time   = '{0}:{1:D2}' -f [math]::Floor($dur.TotalMinutes), $dur.Seconds

# Anything besides the exe means the single-file packing didn't fully fold in.
$extra = Get-ChildItem -LiteralPath $outDir -File | Where-Object { $_.Name -ne 'Tittle.exe' }

Write-Host ''
Write-Ok "DONE  --  $time"
Write-Host ("    " + $SK_TM + " " + $exe)                 -ForegroundColor White
Write-Host ("    " + $SK_TM + " size    " + $sizeMB)      -ForegroundColor Gray
Write-Host ("    " + $SK_TM + " sha256  " + $sha)         -ForegroundColor DarkGray
if ($extra) {
    Write-Warn 'these files sit alongside the exe (not folded in):'
    $extra | ForEach-Object { Write-Info $_.Name }
}
Write-Host ''

Show-Notification -Title 'Tittle build' -Body "Done in $time -- $sizeMB" -IconPath $exe

if (-not $NoOpen) {
    Start-Process explorer.exe -ArgumentList "/select,`"$exe`""
}
