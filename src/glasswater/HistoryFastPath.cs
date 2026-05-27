using System.Reflection;

namespace glasswater;

public static class HistoryFastPath
{
    private static readonly object Sync = new();
    private static MethodInfo? _getHistoryItemsMethod;
    private static PropertyInfo? _commandLineProperty;
    private static bool _lookupFailed;

    public static bool ShouldDeferToHistory(string linePrefix, string lineSuffix)
    {
        if (!string.IsNullOrEmpty(lineSuffix) || string.IsNullOrWhiteSpace(linePrefix))
        {
            return false;
        }

        return HasHistoryPrefixSuggestion(linePrefix);
    }

    private static bool HasHistoryPrefixSuggestion(string prefix)
    {
        if (!TryGetHistoryItems(out Array? items) || items is null || items.Length == 0)
        {
            return false;
        }

        PropertyInfo? commandLineProperty = GetCommandLineProperty();
        if (commandLineProperty is null)
        {
            return false;
        }

        for (int i = items.Length - 1; i >= 0; i--)
        {
            object? item = items.GetValue(i);
            if (item is null)
            {
                continue;
            }

            string? line = commandLineProperty.GetValue(item) as string;
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            line = line.TrimEnd();
            if (line.Length <= prefix.Length)
            {
                continue;
            }

            if (line.IndexOf('\n') >= 0)
            {
                continue;
            }

            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHistoryItems(out Array? items)
    {
        items = null;
        MethodInfo? method = GetHistoryItemsMethod();
        if (method is null)
        {
            return false;
        }

        try
        {
            object? result = method.Invoke(null, null);
            if (result is Array array)
            {
                items = array;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static MethodInfo? GetHistoryItemsMethod()
    {
        if (_lookupFailed)
        {
            return null;
        }

        if (_getHistoryItemsMethod is not null)
        {
            return _getHistoryItemsMethod;
        }

        lock (Sync)
        {
            if (_lookupFailed)
            {
                return null;
            }

            if (_getHistoryItemsMethod is not null)
            {
                return _getHistoryItemsMethod;
            }

            Type? readLineType = FindReadLineType();
            if (readLineType is null)
            {
                _lookupFailed = true;
                return null;
            }

            _getHistoryItemsMethod = readLineType.GetMethod(
                "GetHistoryItems",
                BindingFlags.Public | BindingFlags.Static);

            if (_getHistoryItemsMethod is null)
            {
                _lookupFailed = true;
                return null;
            }

            return _getHistoryItemsMethod;
        }
    }

    private static PropertyInfo? GetCommandLineProperty()
    {
        if (_commandLineProperty is not null)
        {
            return _commandLineProperty;
        }

        MethodInfo? method = GetHistoryItemsMethod();
        if (method is null)
        {
            return null;
        }

        Type? elementType = method.ReturnType.GetElementType();
        if (elementType is null)
        {
            return null;
        }

        lock (Sync)
        {
            _commandLineProperty ??= elementType.GetProperty(
                "CommandLine",
                BindingFlags.Public | BindingFlags.Instance);
        }

        return _commandLineProperty;
    }

    private static Type? FindReadLineType()
    {
        Type? type = Type.GetType(
            "Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine",
            throwOnError: false);

        if (type is not null)
        {
            return type;
        }

        try
        {
            Assembly psReadLine = Assembly.Load("Microsoft.PowerShell.PSReadLine");
            return psReadLine.GetType("Microsoft.PowerShell.PSConsoleReadLine", throwOnError: false);
        }
        catch
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("Microsoft.PowerShell.PSConsoleReadLine", throwOnError: false);
                if (type is not null)
                {
                    return type;
                }
            }
        }

        return null;
    }
}
