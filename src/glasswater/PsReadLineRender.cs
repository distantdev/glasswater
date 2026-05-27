using System.Reflection;
using System.Text;

namespace glasswater;

/// <summary>
/// Re-query inline predictions and redraw on the PSReadLine read loop thread.
/// </summary>
public static class PsReadLineRender
{
    private static readonly object Sync = new();
    private static Type? _readLineType;
    private static FieldInfo? _singletonField;
    private static MethodInfo? _renderInstanceMethod;
    private static bool _lookupFailed;

    public static void TryRefresh()
    {
        ForceRender();
    }

    public static void ForceRender()
    {
        if (!EnsureReflection())
        {
            return;
        }

        try
        {
            object? singleton = _singletonField!.GetValue(null);
            if (singleton is null)
            {
                return;
            }

            _renderInstanceMethod!.Invoke(singleton, new object[] { true });
        }
        catch
        {
            // Not on the read loop or PSReadLine is not in a renderable state.
        }
    }

    internal static Type? FindReadLineType()
    {
        if (_readLineType is not null)
        {
            return _readLineType;
        }

        const string typeName = "Microsoft.PowerShell.PSConsoleReadLine";

        Type? type = Type.GetType($"{typeName}, Microsoft.PowerShell.PSReadLine", throwOnError: false);
        if (type is not null)
        {
            return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, "Microsoft.PowerShell.PSReadLine", StringComparison.OrdinalIgnoreCase))
            {
                type = assembly.GetType(typeName, throwOnError: false);
                if (type is not null)
                {
                    return type;
                }
            }
        }

        try
        {
            Assembly psReadLine = Assembly.Load("Microsoft.PowerShell.PSReadLine");
            return psReadLine.GetType(typeName, throwOnError: false);
        }
        catch
        {
            return null;
        }
    }

    private static bool EnsureReflection()
    {
        if (_lookupFailed)
        {
            return false;
        }

        if (_renderInstanceMethod is not null)
        {
            return true;
        }

        lock (Sync)
        {
            if (_lookupFailed)
            {
                return false;
            }

            if (_renderInstanceMethod is not null)
            {
                return true;
            }

            _readLineType = FindReadLineType();
            if (_readLineType is null)
            {
                _lookupFailed = true;
                return false;
            }

            _singletonField = _readLineType.GetField(
                "_singleton",
                BindingFlags.NonPublic | BindingFlags.Static);

            _renderInstanceMethod = _readLineType.GetMethod(
                "Render",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);

            if (_singletonField is null || _renderInstanceMethod is null)
            {
                _lookupFailed = true;
                return false;
            }

            return true;
        }
    }
}
