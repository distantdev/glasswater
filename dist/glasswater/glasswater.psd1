@{
    RootModule        = 'glasswater.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'e1352e80-60b1-4f32-8f1b-5e6f3b7d8d21'
    Author            = 'Levi Sluder'
    CompanyName       = 'Distant Nebula'
    Copyright         = '(c) 2026 Distant Nebula. All rights reserved.'
    Description       = 'Inline ghost-text completions for PSReadLine via local Ollama FIM.'
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
            ReleaseNotes             = 'Initial prerelease: inline ghost text via Ollama FIM and PSReadLine plugin.'
            Prerelease               = 'preview1'
            RequireLicenseAcceptance = $false
        }
    }
}
