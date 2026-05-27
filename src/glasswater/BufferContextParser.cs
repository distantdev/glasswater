using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace glasswater;

public static class BufferContextParser
{
    private static readonly object Sync = new();
    private static MethodInfo? _getBufferStateMethod;
    private static bool _lookupFailed;

    public static bool TryParse(out LineContext? context)
    {
        context = null;
        if (!TryGetBufferState(out string buffer, out int cursor))
        {
            return false;
        }

        return TryBuildContext(buffer, cursor, out context);
    }

    public static bool TryParseFromInput(string buffer, out LineContext? context)
    {
        context = null;
        if (string.IsNullOrEmpty(buffer))
        {
            return false;
        }

        return TryBuildContext(buffer, buffer.Length, out context);
    }

    private static bool TryBuildContext(string buffer, int cursor, out LineContext? context)
    {
        context = null;

        if (cursor < 0 || cursor > buffer.Length)
        {
            return false;
        }

        int lineStart = 0;
        for (int i = cursor - 1; i >= 0; i--)
        {
            if (buffer[i] == '\n')
            {
                lineStart = i + 1;
                break;
            }
        }

        int lineEnd = buffer.Length;
        for (int i = cursor; i < buffer.Length; i++)
        {
            if (buffer[i] == '\n')
            {
                lineEnd = i;
                break;
            }
        }

        int cursorInLine = cursor - lineStart;
        string line = buffer[lineStart..lineEnd];
        string prefix = cursorInLine > 0 ? line[..cursorInLine] : string.Empty;
        string suffix = cursorInLine < line.Length ? line[cursorInLine..] : string.Empty;

        CompletionKind kind = NaturalLanguageDetector.IsNaturalLanguage(line.Trim())
            ? CompletionKind.NaturalLanguage
            : CompletionKind.CodeFim;

        context = new LineContext
        {
            Buffer = buffer,
            Cursor = cursor,
            LinePrefix = prefix,
            LineSuffix = suffix,
            Fingerprint = ComputeFingerprint(buffer, cursor, prefix, suffix, kind),
            Kind = kind,
        };

        return true;
    }

    private static string ComputeFingerprint(
        string buffer,
        int cursor,
        string prefix,
        string suffix,
        CompletionKind kind)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{kind}\0{buffer}\0{cursor}\0{prefix}\0{suffix}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }

    private static bool TryGetBufferState(out string buffer, out int cursor)
    {
        buffer = string.Empty;
        cursor = 0;

        MethodInfo? method = GetBufferStateMethod();
        if (method is null)
        {
            return false;
        }

        try
        {
            object?[] args = { string.Empty, 0 };
            method.Invoke(null, args);
            buffer = (string)(args[0] ?? string.Empty);
            cursor = (int)(args[1] ?? 0);
            return cursor >= 0 && cursor <= buffer.Length;
        }
        catch
        {
            return false;
        }
    }

    private static MethodInfo? GetBufferStateMethod()
    {
        if (_lookupFailed)
        {
            return null;
        }

        if (_getBufferStateMethod is not null)
        {
            return _getBufferStateMethod;
        }

        lock (Sync)
        {
            if (_lookupFailed)
            {
                return null;
            }

            if (_getBufferStateMethod is not null)
            {
                return _getBufferStateMethod;
            }

            Type? type = Type.GetType(
                "Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine",
                throwOnError: false);

            if (type is null)
            {
                try
                {
                    Assembly psReadLine = Assembly.Load("Microsoft.PowerShell.PSReadLine");
                    type = psReadLine.GetType("Microsoft.PowerShell.PSConsoleReadLine", throwOnError: false);
                }
                catch
                {
                    type = null;
                }
            }

            if (type is null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType("Microsoft.PowerShell.PSConsoleReadLine", throwOnError: false);
                    if (type is not null)
                    {
                        break;
                    }
                }
            }

            if (type is not null)
            {
                foreach (MethodInfo candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!string.Equals(candidate.Name, "GetBufferState", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (parameters.Length == 2 &&
                        parameters[0].ParameterType.IsByRef &&
                        parameters[1].ParameterType.IsByRef)
                    {
                        _getBufferStateMethod = candidate;
                        return candidate;
                    }
                }
            }

            _lookupFailed = true;
            return null;
        }
    }
}

