@{
    RootModule        = 'glasswater.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'e1352e80-60b1-4f32-8f1b-5e6f3b7d8d21'
    Author            = 'Levi Sluder'
    CompanyName       = 'Distant Nebula'
    Copyright         = '(c) 2026 Distant Nebula. All rights reserved.'
    Description       = 'Use natural language in PowerShell with private local-LLM command replacements.'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')

    CmdletsToExport   = @(
        'Initialize-Glasswater'
        'Test-GlasswaterCompletion'
        'Test-GlasswaterNaturalLanguage'
    )

    RequiredModules   = @(
        @{ ModuleName = 'PSReadLine'; ModuleVersion = '2.2.2' }
    )

    PrivateData = @{
        PSData = @{
            Tags                     = @('PSReadLine', 'Completion', 'Ollama', 'AI', 'FIM', 'Prediction')
            LicenseUri               = 'https://github.com/distantdev/glasswater/blob/main/LICENSE'
            ProjectUri               = 'https://github.com/distantdev/glasswater'
            ReleaseNotes             = 'Natural-language command quote sanitization fix; README demo GIF and docs updates.'
            Prerelease               = 'preview3'
            RequireLicenseAcceptance = $false
        }
    }
}
