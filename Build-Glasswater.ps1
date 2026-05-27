#Requires -Version 7.0
<#
.SYNOPSIS
    Builds glasswater to artifacts/glasswater-dev for the current PowerShell host TFM.
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

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
$csproj = Join-Path $root 'src\glasswater\glasswater.csproj'
$outDir = Join-Path $root 'artifacts\glasswater-dev'
$tfm = Get-GlasswaterDevTfm

New-Item -ItemType Directory -Path $outDir -Force | Out-Null
dotnet build $csproj -c $Configuration -f $tfm -o $outDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built ($tfm): $(Join-Path $outDir 'glasswater.dll')"
Write-Host "Import: Import-Module '$outDir\glasswater.dll' -Force; Initialize-Glasswater"
