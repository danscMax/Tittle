# ============================================================================
# Tittle -- Combined Build (tests + portable for every architecture)
# ============================================================================
# One command produces the full distribution set: the test suite gates the
# build, then build.ps1 runs once per RID into its own dist subfolder
# (dist\win-x64\Tittle.exe, dist\win-arm64\Tittle.exe).
#
# Usage:
#   .\build_all.ps1                # tests + win-x64 + win-arm64
#   .\build_all.ps1 -SkipTests     # skip the test gate (CI already ran it)
#   .\build_all.ps1 -ReadyToRun    # AOT-precompile both exes (faster cold start)
#   .\build_all.ps1 -Rids win-x64  # build a subset of architectures
#   .\build_all.ps1 -NoOpen        # suppress the Explorer window (CI/headless)
#
# Outputs:
#   dist\<rid>\Tittle.exe  (one per RID)
#   build-manifest.json         (version, sha256 per exe, duration)
# ============================================================================

param(
    [switch]$SkipTests,
    [switch]$ReadyToRun,
    [switch]$NoOpen,
    [string[]]$Rids = @('win-x64', 'win-arm64')
)

chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

# Box glyphs ($SK_*), Write-Banner/Info/Ok/Fail/Warn, Show-Notification and
# Get-FileHashSHA256 come from ScriptKit.ps1 (keep it identical across repos).
. (Join-Path $root 'ScriptKit.ps1')

$totalStart = Get-Date

# Section banner -- local: yellow phase heading with an optional dimmed note,
# distinct from the kit's cyan title Write-Banner (same shape as SweetWhisper).
function Write-SectionBanner {
    param([string]$Text, [string]$Note = '', [string]$Color = 'Yellow')
    $bar = $SK_H * 58
    Write-Host "  $SK_TL$bar$SK_TR" -ForegroundColor $Color
    Write-Host "  $SK_V  $Text" -ForegroundColor $Color
    if ($Note) { Write-Host "  $SK_V  $Note" -ForegroundColor DarkGray }
    Write-Host "  $SK_BL$bar$SK_BR" -ForegroundColor $Color
}

function Invoke-PreFlight {
    Write-Host '  Pre-flight checks...' -ForegroundColor Cyan

    # 1. .NET 9 SDK on PATH
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) {
        Write-Host '    [FAIL] dotnet not found on PATH.' -ForegroundColor Red
        Show-Notification -Title 'Tittle Build FAILED' -Body 'Pre-flight: dotnet missing.' -IsError
        exit 1
    }
    $sdk9 = (& dotnet --list-sdks) | Where-Object { $_ -match '^9\.' } | Select-Object -First 1
    if ($sdk9) {
        Write-Host "    [OK] .NET SDK $($sdk9 -replace '\s.*$','') found" -ForegroundColor Green
    } else {
        Write-Host '    [FAIL] .NET 9 SDK not found (dotnet --list-sdks has no 9.x entry).' -ForegroundColor Red
        Show-Notification -Title 'Tittle Build FAILED' -Body 'Pre-flight: .NET 9 SDK missing.' -IsError
        exit 1
    }

    # 2. Free disk space on this drive (two self-contained bundles + runtime packs)
    $driveLetter = $root[0]
    try {
        $freeBytes = (Get-PSDrive $driveLetter -ErrorAction Stop).Free
        $freeGB    = [math]::Round($freeBytes / 1GB, 1)
        if ($freeBytes -lt 2GB) {
            Write-Host "    [FAIL] Only ${freeGB} GB free on ${driveLetter}: -- need at least 2 GB." -ForegroundColor Red
            exit 1
        } else {
            Write-Host "    [OK] ${freeGB} GB free on ${driveLetter}:" -ForegroundColor Green
        }
    } catch {
        Write-Host "    [WARN] Could not check disk space on ${driveLetter}: ($($_.Exception.Message))" -ForegroundColor Yellow
    }

    Write-Host ''
}

$phaseCount = $Rids.Count + $(if ($SkipTests) { 0 } else { 1 })
$phase      = 0

Write-Host ''
Write-Banner 'Tittle -- combined build' "tests + portable single-file for: $($Rids -join ', ')"
Write-Host ''

Invoke-PreFlight

# -- Phase 1: test gate -------------------------------------------------------
if (-not $SkipTests) {
    $phase++
    Write-SectionBanner "$phase / $phaseCount -- Tests  (unit + Headless UI)"
    Write-Host ''

    dotnet test (Join-Path $root 'Tittle.sln') -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host '  Tests failed -- aborting (no artifacts produced).' -ForegroundColor Red
        Show-Notification -Title 'Tittle Build FAILED' -Body 'Tests failed. Check the terminal.' -IsError
        exit $LASTEXITCODE
    }
}

# -- Phases 2..N: one portable build per RID ----------------------------------
# dist/ is recreated as a whole so a stale exe can never be mistaken for fresh;
# each RID then publishes into its own subfolder (build.ps1 cleans only it).
$distRoot = Join-Path $root 'dist'
if (Test-Path -LiteralPath $distRoot) { Remove-Item -LiteralPath $distRoot -Recurse -Force }

foreach ($rid in $Rids) {
    $phase++
    Write-Host ''
    Write-SectionBanner "$phase / $phaseCount -- Portable  ($rid)"
    Write-Host ''

    # Named parameters, not array splatting: under Windows PowerShell 5.1 the
    # splatted "-Rid" bound POSITIONALLY into $Rid and broke the publish.
    & (Join-Path $root 'build.ps1') -Rid $rid -OutDir (Join-Path 'dist' $rid) `
        -NoOpen -ReadyToRun:$ReadyToRun
    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host "  Portable build for $rid failed -- aborting." -ForegroundColor Red
        Show-Notification -Title 'Tittle Build FAILED' -Body "Portable $rid failed. Check the terminal." -IsError
        exit $LASTEXITCODE
    }
}

# -- Final summary -------------------------------------------------------------
$totalDur  = (Get-Date) - $totalStart
$totalTime = '{0}:{1:D2}' -f [math]::Floor($totalDur.TotalMinutes), $totalDur.Seconds

# Version from the first produced exe (no <Version> in MSBuild props yet).
# NB: Windows PowerShell 5.1 runs this (the .bat uses powershell.exe) -- no PS7-only syntax.
$firstExe = Join-Path $distRoot (Join-Path $Rids[0] 'Tittle.exe')
$version  = '?'
if (Test-Path -LiteralPath $firstExe) {
    $pv = (Get-Item -LiteralPath $firstExe).VersionInfo.ProductVersion
    if ($pv) { $version = $pv }
}

$bar = $SK_H * 58
Write-Host ''
Write-Host "  $SK_TL$bar$SK_TR" -ForegroundColor Green
Write-Host "  $SK_V  ALL DONE  --  v$version  --  $totalTime" -ForegroundColor Green
Write-Host "  $SK_TM$bar" -ForegroundColor Green

$exes = [ordered]@{}
foreach ($rid in $Rids) {
    $exe = Join-Path $distRoot (Join-Path $rid 'Tittle.exe')
    if (Test-Path -LiteralPath $exe) {
        $sizeMB = '{0:0.0} MB' -f ((Get-Item -LiteralPath $exe).Length / 1MB)
        $exes[$rid] = [ordered]@{ path = $exe; sha256 = (Get-FileHashSHA256 -Path $exe); size = $sizeMB }
        Write-Host ("  $SK_V  {0,-10} {1}" -f $rid, $sizeMB) -ForegroundColor White
        Write-Host "  $SK_V             $exe" -ForegroundColor DarkGray
    } else {
        Write-Host "  $SK_V  $rid  [!!] exe missing" -ForegroundColor Red
    }
}
Write-Host "  $SK_BL$bar$SK_BR" -ForegroundColor Green

Show-Notification -Title 'Tittle Ready' `
                  -Body  "v$version -- $($Rids -join ' + ') -- $totalTime"

# Build manifest beside the script (same shape as SweetWhisper's).
$manifest = [ordered]@{
    version          = $version
    date             = (Get-Date -Format 'o')
    rids             = $Rids
    tests            = -not $SkipTests
    ready_to_run     = [bool]$ReadyToRun
    duration_seconds = [math]::Round($totalDur.TotalSeconds)
    artifacts        = $exes
}
$manifestPath = Join-Path $root 'build-manifest.json'
[System.IO.File]::WriteAllText($manifestPath,
    ($manifest | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
Write-Host '  build-manifest.json written' -ForegroundColor DarkGray

if (-not $NoOpen) {
    if (Test-Path -LiteralPath $firstExe) {
        Start-Process explorer.exe -ArgumentList "/select,`"$firstExe`""
    } elseif (Test-Path -LiteralPath $distRoot) {
        Start-Process explorer.exe -ArgumentList "`"$distRoot`""
    }
}
