namespace glasswater;

/// <summary>
/// Holds the PowerShell command pending accept for a natural-language line.
/// Replacement runs in the PSReadLine key handler (see <see cref="PsReadLineKeyBindings"/>).
/// </summary>
public static class GlasswaterAccept
{
    private static readonly object Sync = new();
    private static string? _pendingReplacement;

    public static void SetPendingReplacement(string? command)
    {
        lock (Sync)
        {
            _pendingReplacement = command;
        }
    }

    public static void ClearPendingReplacement()
    {
        lock (Sync)
        {
            _pendingReplacement = null;
        }
    }

    /// <summary>
    /// Returns and clears the pending command, or null if none.
    /// </summary>
    public static string? TakePendingReplacement()
    {
        lock (Sync)
        {
            string? command = _pendingReplacement;
            _pendingReplacement = null;
            return command;
        }
    }
}
