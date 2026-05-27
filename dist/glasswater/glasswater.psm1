$ErrorActionPreference = 'Stop'

$minor = $PSVersionTable.PSVersion.Minor
$tfm = if ($minor -ge 6) {
    'net10.0'
}
elseif ($minor -ge 5) {
    'net9.0'
}
else {
    'net8.0'
}

$dllPath = Join-Path $PSScriptRoot (Join-Path $tfm 'glasswater.dll')
if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "glasswater binary not found for PowerShell $($PSVersionTable.PSVersion) (expected '$dllPath')."
}

Import-Module -Name $dllPath -Scope Local -DisableNameChecking

Export-ModuleMember -Cmdlet @(
    'Initialize-Glasswater'
    'Test-GlasswaterCompletion'
    'Test-GlasswaterNaturalLanguage'
)
