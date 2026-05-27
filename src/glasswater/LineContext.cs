namespace glasswater;

/// <summary>
/// Cursor-scoped context on the current line for FIM completion.
/// </summary>
public sealed class LineContext
{
    public required string Buffer { get; init; }
    public required int Cursor { get; init; }
    public required string LinePrefix { get; init; }
    public required string LineSuffix { get; init; }
    public required string Fingerprint { get; init; }

    public CompletionKind Kind { get; init; } = CompletionKind.CodeFim;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(LinePrefix) && string.IsNullOrWhiteSpace(LineSuffix);

    public string BuildSuggestedBuffer(string middle)
    {
        if (string.IsNullOrEmpty(middle))
        {
            return Buffer;
        }

        return Buffer[..Cursor] + middle + Buffer[Cursor..];
    }

    public string BuildNaturalLanguageDisplay(string command)
    {
        return Buffer + " \u2190 " + command;
    }
}
