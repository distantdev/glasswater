namespace glasswater;

public static class GlasswaterSession
{
    public static void OnNaturalLanguageAccepted(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        GlasswaterModule.Instance?.Coordinator.OnNaturalLanguageAccepted(command.Trim());
    }
}
