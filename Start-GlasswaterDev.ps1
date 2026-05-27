#Requires -Version 7.4
<#
.SYNOPSIS
    Builds glasswater to artifacts/ (avoids locked bin/) and opens a fresh pwsh with the module.
#>
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Get-GlasswaterDevTfm {
    $minor = $PSVersionTable.PSVersion.Minor
    if ($minor -ge 6) {
        return 'net10.0'
    }
    if ($minor -ge 5) {
        return 'net9.0'
    }
    return 'net8.0'
}

$root = $PSScriptRoot
$csproj = Join-Path $root "src\glasswater\glasswater.csproj"
$outDir = Join-Path $root "artifacts\glasswater-dev"
$dll = Join-Path $outDir "glasswater.dll"
$tfm = Get-GlasswaterDevTfm

if (-not $SkipBuild) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    dotnet build $csproj -c Release -f $tfm -o $outDir
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $dll)) {
    Write-Error @"
DLL not found: $dll
Run without -SkipBuild, or close pwsh windows and run: dotnet build $csproj -c Release -f $tfm -o $outDir
"@
}

$dllEscaped = $dll.Replace("'", "''")
$rootEscaped = $root.Replace("'", "''")

$init = @"
Set-Location '$rootEscaped'
Import-Module '$dllEscaped' -Force
Initialize-Glasswater
Write-Host "glasswater loaded from: $dllEscaped" -ForegroundColor Green
Write-Host 'Type a partial command (e.g. Get-Child) and pause ~0.5s for ghost text.' -ForegroundColor Green
"@

Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoExit', '-Command', $init)
