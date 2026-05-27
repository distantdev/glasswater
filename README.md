# glasswater

Inline ghost-text completions for PowerShell via Ollama and Qwen2.5-Coder FIM.

## Install from PSGallery

```powershell
Install-Module glasswater -AllowPrerelease -Scope CurrentUser
Import-Module glasswater
Initialize-Glasswater

# One-time Ollama model setup (bundled with the module):
& (Join-Path (Split-Path (Get-Module glasswater).Path) 'scripts\Install-OllamaModel.ps1')
```

## Development quick start

```powershell
./Install-OllamaModel.ps1
./Start-GlasswaterDev.ps1
```

Or build without locking issues (outputs to `artifacts/`, not `bin/`):

```powershell
./Build-Glasswater.ps1
Import-Module ./artifacts/glasswater-dev/glasswater.dll -Force
Initialize-Glasswater
```

## Gallery publish (maintainers)

Stage a gallery-ready module (multi-TFM layout + manifest validation):

```powershell
./Publish-Glasswater.ps1
```

Optionally refresh `dist/glasswater` for local folder import testing:

```powershell
./Publish-Glasswater.ps1 -UpdateDist
Import-Module ./dist/glasswater
```

Push a version tag to trigger GitHub Actions publish (requires `PSGALLERY_API_KEY` repo secret).
The tag must match `ModuleVersion` plus `PrivateData.PSData.Prerelease` in `src/glasswater/glasswater.psd1`:

```powershell
git tag v0.1.0-preview1
git push origin v0.1.0-preview1
```

### First-time PSGallery setup

1. Create an account at [powershellgallery.com](https://www.powershellgallery.com).
2. Profile -> **API Keys** -> create a key scoped to `glasswater`.
3. GitHub repo **Settings -> Secrets and variables -> Actions** -> add `PSGALLERY_API_KEY`.

Type a partial command, pause briefly (~250ms), and inline ghost text should appear. Prior commands
use PSReadLine history when the prefix matches; novel mid-line edits and natural-language requests
use local Ollama (FIM or command generation).

## Requirements

- PowerShell 7.4+ (7.5 / 7.6 use matching .NET TFMs automatically)
- PSReadLine 2.2.2+
- [Ollama](https://ollama.com) with `qwen2.5-coder:1.5b-base`

For lower latency, keep the model loaded:

```powershell
$env:OLLAMA_KEEP_ALIVE = '-1'
```

See [SPEC.md](SPEC.md) for architecture and verification details.
