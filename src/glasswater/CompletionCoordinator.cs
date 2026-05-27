using System.Collections.Generic;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;
using System.Threading.Tasks;

namespace glasswater;

public sealed class CompletionCoordinator : IDisposable
{
    private readonly GlasswaterSettings _settings;
    private readonly OllamaClient _ollamaClient;
    private readonly object _sync = new();
    private string? _lastError;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _requestCts;
    private int _generation;

    private string? _cachedFingerprint;
    private string? _cachedSuggestedBuffer;
    private string? _cachedReplacementCommand;
    private string? _suppressFimForLine;
    private uint _sessionCounter;

    public CompletionCoordinator(GlasswaterSettings settings, OllamaClient? ollamaClient = null)
    {
        _settings = settings;
        _ollamaClient = ollamaClient ?? new OllamaClient(settings);
    }

    public void RequestCompletion(LineContext context)
    {
        if (context.IsEmpty)
        {
            ClearCache();
            return;
        }

        if (context.Kind == CompletionKind.NaturalLanguage && !_settings.NaturalLanguageEnabled)
        {
            ClearCache();
            return;
        }

        if (context.Kind == CompletionKind.CodeFim && ShouldSuppressFim(context))
        {
            ClearCache();
            return;
        }

        lock (_sync)
        {
            CancelDebounceLocked();
            CancelRequestLocked();

            _debounceCts = new CancellationTokenSource();
            int generation = ++_generation;
            var debounceToken = _debounceCts.Token;
            int debounceMs = context.Kind == CompletionKind.NaturalLanguage
                ? Math.Max(_settings.DebounceMs, 300)
                : _settings.DebounceMs;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(debounceMs, debounceToken).ConfigureAwait(false);
                    await FetchAndCacheAsync(context, generation, debounceToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }, CancellationToken.None);
        }
    }

    public bool TryGetCachedSuggestion(LineContext context, out SuggestionPackage package)
    {
        package = default;

        if (context.Kind == CompletionKind.CodeFim && ShouldSuppressFim(context))
        {
            GlasswaterAccept.ClearPendingReplacement();
            return false;
        }

        lock (_sync)
        {
            if (_cachedFingerprint is null ||
                _cachedSuggestedBuffer is null ||
                !string.Equals(_cachedFingerprint, context.Fingerprint, StringComparison.Ordinal))
            {
                GlasswaterAccept.ClearPendingReplacement();
                return false;
            }

            if (context.Kind == CompletionKind.NaturalLanguage && _cachedReplacementCommand is not null)
            {
                GlasswaterAccept.SetPendingReplacement(_cachedReplacementCommand);
            }
            else
            {
                GlasswaterAccept.ClearPendingReplacement();
            }

            uint session = ++_sessionCounter;
            package = new SuggestionPackage(
                session,
                new List<PredictiveSuggestion>
                {
                    new PredictiveSuggestion(_cachedSuggestedBuffer),
                });

            return true;
        }
    }

    public void ClearCache()
    {
        lock (_sync)
        {
            _cachedFingerprint = null;
            _cachedSuggestedBuffer = null;
            _cachedReplacementCommand = null;
        }

        GlasswaterAccept.ClearPendingReplacement();
    }

    public void CancelPending()
    {
        lock (_sync)
        {
            CancelDebounceLocked();
            CancelRequestLocked();
            _generation++;
            _cachedFingerprint = null;
            _cachedSuggestedBuffer = null;
            _cachedReplacementCommand = null;
        }

        GlasswaterAccept.ClearPendingReplacement();
    }

    public void OnNaturalLanguageAccepted(string command)
    {
        CancelPending();
        lock (_sync)
        {
            _suppressFimForLine = command;
        }

        PsReadLineRender.TryRefresh();
    }

    public string? GetLastError()
    {
        lock (_sync)
        {
            return _lastError;
        }
    }

    private async Task FetchAndCacheAsync(
        LineContext context,
        int generation,
        CancellationToken debounceToken)
    {
        CancellationTokenSource requestCts;
        lock (_sync)
        {
            if (generation != _generation)
            {
                return;
            }

            CancelRequestLocked();
            requestCts = CancellationTokenSource.CreateLinkedTokenSource(debounceToken);
            _requestCts = requestCts;
        }

        try
        {
            string? suggestedBuffer;
            string? replacementCommand = null;

            if (context.Kind == CompletionKind.NaturalLanguage)
            {
                string line = context.LinePrefix + context.LineSuffix;
                string? command = await _ollamaClient
                    .GenerateCommandAsync(line, requestCts.Token)
                    .ConfigureAwait(false);

                if (command is null)
                {
                    ClearCacheIfGenerationMatches(context, generation);
                    return;
                }

                replacementCommand = command;
                suggestedBuffer = context.BuildNaturalLanguageDisplay(command);
            }
            else
            {
                string? middle = await _ollamaClient
                    .GenerateFimAsync(context, requestCts.Token)
                    .ConfigureAwait(false);

                if (middle is null || FimCompletionFilter.IsLowQuality(middle))
                {
                    ClearCacheIfGenerationMatches(context, generation);
                    return;
                }

                suggestedBuffer = context.BuildSuggestedBuffer(middle);
            }

            lock (_sync)
            {
                if (generation != _generation)
                {
                    return;
                }

                _cachedFingerprint = context.Fingerprint;
                _cachedSuggestedBuffer = suggestedBuffer;
                _cachedReplacementCommand = replacementCommand;
                _lastError = null;
            }

            PsReadLineRender.TryRefresh();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _lastError = ex.Message;
            }
        }
    }

    private void ClearCacheIfGenerationMatches(LineContext context, int generation)
    {
        lock (_sync)
        {
            if (generation == _generation &&
                string.Equals(_cachedFingerprint, context.Fingerprint, StringComparison.Ordinal))
            {
                _cachedFingerprint = null;
                _cachedSuggestedBuffer = null;
                _cachedReplacementCommand = null;
            }
        }
    }

    private bool ShouldSuppressFim(LineContext context)
    {
        lock (_sync)
        {
            if (string.IsNullOrEmpty(_suppressFimForLine))
            {
                return false;
            }

            string line = (context.LinePrefix + context.LineSuffix).Trim();
            if (string.Equals(line, _suppressFimForLine, StringComparison.Ordinal))
            {
                return true;
            }

            _suppressFimForLine = null;
            return false;
        }
    }

    private void CancelDebounceLocked()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    private void CancelRequestLocked()
    {
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            CancelDebounceLocked();
            CancelRequestLocked();
        }

        GlasswaterAccept.ClearPendingReplacement();
        _ollamaClient.Dispose();
    }
}
