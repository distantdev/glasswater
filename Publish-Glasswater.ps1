#Requires -Version 7.4
<#
.SYNOPSIS
    Stages the glasswater module for PSGallery publish (multi-TFM binary layout).
.DESCRIPTION
    Publishes net8.0, net9.0, and net10.0 builds into a gallery-ready folder,
    copies the module manifest and Install-OllamaModel.ps1, and validates
    with Test-ModuleManifest.
.PARAMETER Configuration
    dotnet build configuration (Release or Debug).
.PARAMETER OutputPath
    Staged module root relative to the repo root (default: publish/glasswater).
.PARAMETER SkipTestModuleManifest
    Skip Test-ModuleManifest validation.
.PARAMETER UpdateDist
    Also copy the staged module to dist/glasswater for local Import-Module testing.
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$OutputPath = 'publish/glasswater',

    [switch]$SkipTestModuleManifest,

    [switch]$UpdateDist
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$csproj = Join-Path $root 'src\glasswater\glasswater.csproj'
$manifestSource = Join-Path $root 'src\glasswater\glasswater.psd1'
$psm1Source = Join-Path $root 'src\glasswater\glasswater.psm1'
$ollamaScriptSource = Join-Path $root 'Install-OllamaModel.ps1'
$stageRoot = Join-Path $root $OutputPath

$tfms = @('net8.0', 'net9.0', 'net10.0')

$hostAssemblyNames = @(
    'System.Management.Automation'
    'Microsoft.PowerShell.ConsoleHost'
    'Microsoft.PowerShell.CoreCLR.Eventing'
    'Microsoft.PowerShell.Native'
    'Microsoft.PowerShell.Security'
    'Microsoft.PowerShell.Commands.Management'
    'Microsoft.PowerShell.Commands.Utility'
    'Microsoft.WSMan.Management'
    'Microsoft.WSMan.Runtime'
    'Microsoft.Management.Infrastructure'
    'Microsoft.Management.Infrastructure.Runtime.Unix'
    'Microsoft.Management.Infrastructure.Runtime.Win'
    'Microsoft.ApplicationInsights'
    'Microsoft.Security.Extensions'
    'Microsoft.Win32.Registry.AccessControl'
    'Newtonsoft.Json'
    'System.Configuration.ConfigurationManager'
    'System.Diagnostics.EventLog'
    'System.DirectoryServices'
    'System.Management'
    'System.Security.Cryptography.Pkcs'
    'System.Security.Cryptography.ProtectedData'
    'System.Security.Permissions'
    'System.Windows.Extensions'
    'System.CodeDom'
)

function Remove-StagedArtifact {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
}

function Remove-PublishNoise {
    param([string]$PublishDir)

    foreach ($pattern in @('*.pdb', '*.deps.json', '*.runtimeconfig.json', '*.xml')) {
        Get-ChildItem -LiteralPath $PublishDir -Filter $pattern -File -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    foreach ($name in $hostAssemblyNames) {
        Get-ChildItem -LiteralPath $PublishDir -Filter "$name.dll" -File -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    $runtimesDir = Join-Path $PublishDir 'runtimes'
    if (Test-Path -LiteralPath $runtimesDir) {
        Remove-Item -LiteralPath $runtimesDir -Recurse -Force
    }
}

function Copy-StagedTree {
    param(
        [string]$Source,
        [string]$Destination
    )

    Remove-StagedArtifact -Path $Destination
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

Remove-StagedArtifact -Path $stageRoot
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

foreach ($tfm in $tfms) {
    $tfmOut = Join-Path $stageRoot $tfm
    Write-Host "Publishing $tfm -> $tfmOut"

    dotnet publish $csproj -c $Configuration -f $tfm -o $tfmOut --no-self-contained
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $dllPath = Join-Path $tfmOut 'glasswater.dll'
    if (-not (Test-Path -LiteralPath $dllPath)) {
        throw "Expected output not found: $dllPath"
    }

    Remove-PublishNoise -PublishDir $tfmOut
}

Copy-Item -LiteralPath $manifestSource -Destination (Join-Path $stageRoot 'glasswater.psd1') -Force
Copy-Item -LiteralPath $psm1Source -Destination (Join-Path $stageRoot 'glasswater.psm1') -Force

$scriptsDir = Join-Path $stageRoot 'scripts'
New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null
Copy-Item -LiteralPath $ollamaScriptSource -Destination (Join-Path $scriptsDir 'Install-OllamaModel.ps1') -Force

if (-not $SkipTestModuleManifest) {
    $manifestPath = Join-Path $stageRoot 'glasswater.psd1'
    Write-Host "Validating manifest: $manifestPath"
    $null = Test-ModuleManifest -Path $manifestPath
}

if ($UpdateDist) {
    $distRoot = Join-Path $root 'dist\glasswater'
    Write-Host "Updating dist/glasswater"
    Copy-StagedTree -Source $stageRoot -Destination $distRoot
}

Write-Host ""
Write-Host "Staged module: $stageRoot" -ForegroundColor Green
Write-Host "Publish: Publish-Module -Path '$stageRoot' -Repository PSGallery -NuGetApiKey <key>" -ForegroundColor Green
