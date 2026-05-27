# glasswater: PowerShell Inline Autocomplete (Ollama FIM)

## Objective

glasswater intercepts PSReadLine input, debounces keystrokes, and fetches deterministic inline completions from a local Ollama instance using Qwen2.5-Coder Fill-in-the-Middle (FIM).

### Key metrics

- Target latency: under 250ms from keystroke pause to ghost-text rendering (debounce + inference)
- Memory: under 2GB VRAM/RAM for model execution (Ollama-managed)
- Shell responsiveness: `GetSuggestion` returns in under 20ms (cached); HTTP runs off the hot path

## Architecture

```
PowerShell Session
  PSReadLine (PredictionSource HistoryAndPlugin)
    -> History (native prefix match on prior commands)
    -> ICommandPredictor (glasswater)
         -> HistoryFastPath (defer when history would win)
         -> BufferContextParser (prefix/suffix on current line)
         -> NaturalLanguageDetector
         -> CompletionCoordinator (250ms debounce, cancellation)
         -> OllamaClient (POST /api/generate)
Ollama (qwen2.5-coder:1.5b-base)
```

When the current line prefix matches PSReadLine history (cursor at end of line, not natural language),
glasswater returns no plugin suggestion and cancels in-flight Ollama work so native history ghost text
is shown. Mid-line edits (non-empty suffix) and natural-language requests still use Ollama.

## Model and FIM

- Model: `qwen2.5-coder:1.5b-base`
- Ollama applies the Qwen FIM template from `prompt` + `suffix` fields
- Generation options: `temperature=0`, `top_p=1`, `num_predict=32`, stop on newline and FIM tokens

## Usage

```powershell
# Gallery install
Install-Module glasswater -AllowPrerelease -Scope CurrentUser
Import-Module glasswater
& (Join-Path (Split-Path (Get-Module glasswater).Path) 'scripts/Install-OllamaModel.ps1')
Initialize-Glasswater

# Development
./Install-OllamaModel.ps1
./Publish-Glasswater.ps1 -UpdateDist
Import-Module ./dist/glasswater
Initialize-Glasswater
```

Requires PowerShell 7.4+ (module ships net8.0 / net9.0 / net10.0 builds).

### Initialize-Glasswater parameters

| Parameter | Default | Description |
| --- | --- | --- |
| OllamaEndpoint | http://127.0.0.1:11434 | Ollama base URL |
| Model | qwen2.5-coder:1.5b-base | Model tag |
| DebounceMs | 250 | Idle delay before HTTP request |
| RequestTimeoutMs | 5000 | HTTP client timeout |
| SkipPsReadLineSetup | false | Skip `Set-PSReadLineOption` |
| DisableNaturalLanguage | false | Skip NL command generation |
| PluginOnly | false | Use `PredictionSource Plugin` only (no native history) |

## Verification

1. **No typing jank**: Type continuously; prior HTTP requests should cancel (no stalls).
2. **History**: Run a command twice; type a prefix of the prior line at end of line; history ghost text appears without waiting on Ollama.
3. **FIM**: `Get-Service | Where-Object { $_.Status -eq ` with cursor before `}`; pause; expect a middle fragment such as `'Running'`.
4. **Single-line**: Completions must not include newlines.
5. **Stale cache**: After a pause, change input; old ghost text must not apply.
6. **Natural language**: Type `find all open ports`; pause; expect a ` <- ` hint such as `Get-NetTCPConnection -State Established | Where-Object { $_.LocalPort -ne 0 }`; Right Arrow replaces the line with the generated command.
7. **Ollama down**: Empty suggestion; init may warn if the server is unreachable.

## Implementation notes

- Uses `ICommandPredictor`, not a custom key handler, for ghost text.
- Default `PredictionSource` is `HistoryAndPlugin`; use `-PluginOnly` to restore plugin-only behavior.
- `HistoryFastPath` mirrors PSReadLine `GetOneHistorySuggestion` via `GetHistoryItems` (reflection).
- `PSConsoleReadLine.GetBufferState` (reflection) supplies true cursor position for suffix-aware FIM.
- After async completion, `PsReadLineRender` invokes PSReadLine `Render()` when available; otherwise ghost text appears on the next keypress.
