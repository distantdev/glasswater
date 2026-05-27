# glasswater

Inline ghost-text completions for PowerShell, powered by local [Ollama](https://ollama.com) and Qwen2.5-Coder FIM.

`glasswater` adds inline command suggestions using a local LLM in Ollama. Type part of a command or plain English, pause briefly, and accept gray ghost text with the Right Arrow (same as native PSReadLine suggestions). History matches use PSReadLine's built-in history; everything else uses your local model. Suggestions stay on your machine.

![glasswater inline ghost-text completions in PowerShell](docs/glasswater_example.gif)

## What glasswater does

- If you type part of a command, it suggests the rest inline.
- If you type plain English, it suggests a command you can run.
- The experience is designed to feel native with PSReadLine ghost text.

## How it works in practice

When enabled, `glasswater` uses two suggestion sources:

1. **History first**: if your current text matches recent commands, PSReadLine history suggestions appear immediately.
2. **Local LLM fallback**: for new commands, mid-line edits, or natural language text, `glasswater` calls Ollama and returns a suggestion.

`glasswater` debounces input (default 250 ms) and cancels stale in-flight requests to keep typing responsive.

## Requirements

- PowerShell 7.4 or newer
- PSReadLine 2.2.2 or newer (installed automatically with `Install-Module`)
- [Ollama](https://ollama.com) installed and running locally
- Model: `qwen2.5-coder:1.5b-base`

## Install and first run

```powershell
Install-Module glasswater -AllowPrerelease -Scope CurrentUser
Import-Module glasswater
Initialize-Glasswater
```

Pull the model once (script ships inside the module):

```powershell
$root = Split-Path (Get-Module glasswater).Path
& "$root\scripts\Install-OllamaModel.ps1"
```

Optional: keep the model loaded for faster suggestions:

```powershell
$env:OLLAMA_KEEP_ALIVE = "-1"
```

## Core usage patterns

### 1) Partial command completion

Type:

```powershell
Get-Child
```

Pause briefly. You should see inline completion.

### 2) Mid-line completion

Type part of a pipeline and place the cursor in the middle. `glasswater` can fill the missing expression.

### 3) Natural language to command

Type a plain English instruction such as:

```text
find all open ports
```

Pause briefly. Gray ghost text shows the suggested command after ` <- `, for example:

```powershell
Get-NetTCPConnection -State Established | Where-Object { $_.LocalPort -ne 0 }
```

Press Right Arrow to accept the replacement and replace the whole line with that command.

## Initialization options

`Initialize-Glasswater` supports these options:

- `-OllamaEndpoint` (default `http://127.0.0.1:11434`)
- `-Model` (default `qwen2.5-coder:1.5b-base`)
- `-DebounceMs` (default `250`)
- `-RequestTimeoutMs` (default `5000`)
- `-DisableNaturalLanguage`
- `-PluginOnly`
- `-SkipPsReadLineSetup`

Examples:

```powershell
Initialize-Glasswater -DebounceMs 150 -RequestTimeoutMs 3000
Initialize-Glasswater -Model "qwen2.5-coder:7b"
Initialize-Glasswater -DisableNaturalLanguage
```

## Load automatically in every shell

Add to your PowerShell profile:

```powershell
Import-Module glasswater
Initialize-Glasswater
```

Open profile quickly:

```powershell
code $PROFILE
```

## Troubleshooting

### No suggestions appear

- Check module loaded: `Get-Module glasswater`
- Check predictor options: `Get-PSReadLineOption`
- Re-run init: `Initialize-Glasswater`

### Natural language does not replace line

- Ensure natural language is enabled (do not use `-DisableNaturalLanguage`)
- Press Right Arrow to accept replacement
- Re-run init to reapply keybinding

### Ollama errors or timeouts

- Verify server: `Invoke-RestMethod http://127.0.0.1:11434/api/tags`
- Ensure model exists: `ollama list`
- Increase timeout: `Initialize-Glasswater -RequestTimeoutMs 10000`

## Development

Clone the repo, install the Ollama model, and launch a dev session:

```powershell
./Install-OllamaModel.ps1
./Start-GlasswaterDev.ps1
```

That builds the module and opens a new PowerShell window with glasswater loaded.

To build and load manually:

```powershell
./Build-Glasswater.ps1
Import-Module ./artifacts/glasswater-dev/glasswater.dll -Force
Initialize-Glasswater
```

See [SPEC.md](SPEC.md) for architecture, parameters, and verification steps.
