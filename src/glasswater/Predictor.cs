using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;

namespace glasswater;

public sealed class Predictor : ICommandPredictor, IDisposable
{
    public const string PredictorGuid = "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f";

    private readonly Guid _id;
    private readonly CompletionCoordinator _coordinator;

    public Predictor(GlasswaterSettings settings)
    {
        _id = new Guid(PredictorGuid);
        _coordinator = new CompletionCoordinator(settings);
    }

    internal Predictor(CompletionCoordinator coordinator)
    {
        _id = new Guid(PredictorGuid);
        _coordinator = coordinator;
    }

    public Guid Id => _id;
    public string Name => "glasswater";
    public string Description => "Ollama FIM ghost text for PowerShell";

    public SuggestionPackage GetSuggestion(
        PredictionClient client,
        PredictionContext context,
        CancellationToken cancellationToken)
    {
        LineContext? lineContext = null;
        string input = context.InputAst.Extent.Text;
        if (!BufferContextParser.TryParseFromInput(input, out lineContext) || lineContext is null)
        {
            if (!BufferContextParser.TryParse(out lineContext) || lineContext is null)
            {
                return default;
            }
        }

        if (lineContext.IsEmpty)
        {
            _coordinator.ClearCache();
            return default;
        }

        if (lineContext.Kind != CompletionKind.NaturalLanguage &&
            HistoryFastPath.ShouldDeferToHistory(lineContext.LinePrefix, lineContext.LineSuffix))
        {
            _coordinator.CancelPending();
            return default;
        }

        _coordinator.RequestCompletion(lineContext);

        if (_coordinator.TryGetCachedSuggestion(lineContext, out SuggestionPackage package))
        {
            return package;
        }

        return default;
    }

    public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedbackKind) => false;
    public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) { }
    public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) { }
    public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history) { }
    public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) { }

    internal CompletionCoordinator Coordinator => _coordinator;

    public void Dispose()
    {
        PsReadLineIdleHook.Uninstall();
        _coordinator.Dispose();
    }
}

[Cmdlet(VerbsDiagnostic.Test, "GlasswaterNaturalLanguage")]
public sealed class TestGlasswaterNaturalLanguageCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false, Position = 0)]
    public string Request { get; set; } = "find all open ports";

    protected override void ProcessRecord()
    {
        var settings = new GlasswaterSettings();
        using var client = new OllamaClient(settings);
        try
        {
            string? command = client.GenerateCommandAsync(Request, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            WriteObject(command);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GlasswaterOllamaError", ErrorCategory.NotSpecified, null));
        }
    }
}

[Cmdlet(VerbsDiagnostic.Test, "GlasswaterCompletion")]
public sealed class TestGlasswaterCompletionCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false, Position = 0)]
    public string Prompt { get; set; } = "Get-Child";

    [Parameter(Mandatory = false)]
    public string Suffix { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        if (GlasswaterModule.Instance is null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Run Initialize-Glasswater first."),
                "GlasswaterNotInitialized",
                ErrorCategory.InvalidOperation,
                targetObject: null));
            return;
        }

        var context = new LineContext
        {
            Buffer = Prompt + Suffix,
            Cursor = Prompt.Length,
            LinePrefix = Prompt,
            LineSuffix = Suffix,
            Fingerprint = "test",
        };

        var settings = new GlasswaterSettings();
        using var client = new OllamaClient(settings);
        try
        {
            string? middle = client.GenerateFimAsync(context, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            WriteObject(new
            {
                Middle = middle,
                SuggestedLine = context.BuildSuggestedBuffer(middle ?? string.Empty),
            });
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GlasswaterOllamaError", ErrorCategory.NotSpecified, null));
        }
    }
}

public static class GlasswaterModule
{
    public static Predictor? Instance { get; set; }
    public static readonly Guid ModuleGuid = new("e1352e80-60b1-4f32-8f1b-5e6f3b7d8d21");
}

public sealed class GlasswaterModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
    }

    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        if (GlasswaterModule.Instance is not null)
        {
            PsReadLineIdleHook.Uninstall();
            SubsystemManager.UnregisterSubsystem<ICommandPredictor>(GlasswaterModule.Instance.Id);
            GlasswaterModule.Instance.Dispose();
            GlasswaterModule.Instance = null;
        }
    }
}

[Cmdlet(VerbsData.Initialize, "glasswater")]
public sealed class InitializeGlasswaterCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false)]
    public string OllamaEndpoint { get; set; } = GlasswaterSettings.DefaultOllamaEndpoint;

    [Parameter(Mandatory = false)]
    public string Model { get; set; } = GlasswaterSettings.DefaultModel;

    [Parameter(Mandatory = false)]
    public int DebounceMs { get; set; } = GlasswaterSettings.DefaultDebounceMs;

    [Parameter(Mandatory = false)]
    public int RequestTimeoutMs { get; set; } = GlasswaterSettings.DefaultRequestTimeoutMs;

    [Parameter(Mandatory = false)]
    public SwitchParameter SkipPsReadLineSetup { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter DisableNaturalLanguage { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PluginOnly { get; set; }

    protected override void ProcessRecord()
    {
        if (GlasswaterModule.Instance is not null)
        {
            SubsystemManager.UnregisterSubsystem<ICommandPredictor>(GlasswaterModule.Instance.Id);
            GlasswaterModule.Instance.Dispose();
        }

        var settings = new GlasswaterSettings
        {
            OllamaEndpoint = OllamaEndpoint,
            Model = Model,
            DebounceMs = DebounceMs,
            RequestTimeoutMs = RequestTimeoutMs,
            NaturalLanguageEnabled = !DisableNaturalLanguage.IsPresent,
        };

        var predictor = new Predictor(settings);
        GlasswaterModule.Instance = predictor;
        PsReadLineIdleHook.Uninstall();
        PsReadLineIdleHook.Install(predictor.Coordinator);
        SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, predictor);

        if (!SkipPsReadLineSetup.IsPresent)
        {
            try
            {
                string predictionSource = PluginOnly.IsPresent ? "Plugin" : "HistoryAndPlugin";
                InvokeCommand.InvokeScript(
                    $"Set-PSReadLineOption -PredictionSource {predictionSource} -PredictionViewStyle InlineView");
                InvokeCommand.InvokeScript(PsReadLineKeyBindings.InstallScript);
            }
            catch (Exception ex)
            {
                WriteWarning($"Could not configure PSReadLine options: {ex.Message}");
            }
        }

        WriteObject("glasswater predictor registered (FIM + natural language).");
    }
}
