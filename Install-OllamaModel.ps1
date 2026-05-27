#Requires -Version 7.0
<#
.SYNOPSIS
    Pulls the default Ollama model for glasswater and verifies the server is reachable.
#>
param(
    [string]$Model = "qwen2.5-coder:1.5b-base",
    [string]$OllamaEndpoint = "http://127.0.0.1:11434"
)

$ErrorActionPreference = "Stop"

function Test-OllamaServer {
    param([string]$BaseUrl)
    try {
        $tagsUri = ($BaseUrl.TrimEnd("/")) + "/api/tags"
        $null = Invoke-RestMethod -Uri $tagsUri -Method Get -TimeoutSec 5
        return $true
    }
    catch {
        return $false
    }
}

if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) {
    Write-Error "ollama CLI not found. Install from https://ollama.com and ensure 'ollama serve' is running."
}

if (-not (Test-OllamaServer -BaseUrl $OllamaEndpoint)) {
    Write-Warning "Ollama API not reachable at $OllamaEndpoint. Start the server with: ollama serve"
}

Write-Host "Pulling model: $Model"
& ollama pull $Model

if (Test-OllamaServer -BaseUrl $OllamaEndpoint) {
    Write-Host "Ollama is reachable at $OllamaEndpoint"
}
else {
    Write-Warning "Model pulled but API still unreachable at $OllamaEndpoint"
}

Write-Host @"

glasswater is ready to use:

  Import-Module glasswater
  Initialize-Glasswater

If installed from the gallery, the model script lives at:

  Join-Path (Split-Path (Get-Module glasswater).Path) 'scripts/Install-OllamaModel.ps1'

Optional: keep the model loaded for lower latency:

  `$env:OLLAMA_KEEP_ALIVE = '-1'

"@
