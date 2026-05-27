using System.Text.RegularExpressions;

namespace glasswater;

public static partial class NaturalLanguageDetector
{
    private static readonly string[] IntentWords =
    {
        "list", "show", "display", "print", "all", "the", "dir", "directory", "folder",
        "files", "file", "find", "search", "delete", "remove", "copy", "move", "create",
        "make", "run", "execute", "start", "stop", "kill", "process", "service", "please",
        "want", "need", "every", "each", "from", "into", "with", "where", "whose",
    };

    public static bool IsNaturalLanguage(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string text = line.Trim();
        if (text.Length < 8 || !text.Contains(' '))
        {
            return false;
        }

        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        if (text.Contains('|') ||
            text.Contains('{') ||
            text.Contains('}') ||
            text.Contains("$(") ||
            text.Contains("$_"))
        {
            return false;
        }

        if (CmdletPrefix().IsMatch(text))
        {
            return false;
        }

        if (!HasIntentWord(text))
        {
            return false;
        }

        return true;
    }

    [GeneratedRegex(
        @"^(Get|Set|New|Remove|Start|Stop|Test|Invoke|foreach|if|while|switch|cd|ls|dir|cat|echo|pwsh|git|docker)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CmdletPrefix();

    private static bool HasIntentWord(string text)
    {
        foreach (string word in IntentWords)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
