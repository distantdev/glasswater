using System.Reflection;

namespace glasswater;

/// <summary>
/// Runs prediction refresh on PSReadLine's read loop (via the 300ms idle hook).
/// </summary>
public static class PsReadLineIdleHook
{
    private static readonly object Sync = new();
    private static Action<CancellationToken>? _priorHandler;
    private static CompletionCoordinator? _coordinator;
    private static FieldInfo? _idleOverrideField;
    private static bool _installed;

    public static void Install(CompletionCoordinator coordinator)
    {
        lock (Sync)
        {
            _coordinator = coordinator;
            if (_installed)
            {
                return;
            }

            Type? readLineType = PsReadLineRender.FindReadLineType();
            if (readLineType is null)
            {
                return;
            }

            _idleOverrideField = readLineType.GetField(
                "_handleIdleOverride",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (_idleOverrideField is null)
            {
                return;
            }

            _priorHandler = _idleOverrideField.GetValue(null) as Action<CancellationToken>;
            _idleOverrideField.SetValue(null, (Action<CancellationToken>)OnIdle);
            _installed = true;
        }
    }

    public static void Uninstall()
    {
        lock (Sync)
        {
            if (!_installed || _idleOverrideField is null)
            {
                return;
            }

            _idleOverrideField.SetValue(null, _priorHandler);
            _priorHandler = null;
            _coordinator = null;
            _installed = false;
        }
    }

    private static void OnIdle(CancellationToken cancellationToken)
    {
        try
        {
            _priorHandler?.Invoke(cancellationToken);
        }
        catch
        {
            // Preserve prior handler behavior.
        }

        CompletionCoordinator? coordinator = _coordinator;
        if (coordinator is null)
        {
            return;
        }

        if (!BufferContextParser.TryParse(out LineContext? context) || context is null)
        {
            return;
        }

        if (!coordinator.TryGetCachedSuggestion(context, out _))
        {
            return;
        }

        PsReadLineRender.ForceRender();
    }
}
