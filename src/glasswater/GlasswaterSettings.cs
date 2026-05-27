namespace glasswater;

public sealed class GlasswaterSettings
{
    public const string DefaultModel = "qwen2.5-coder:1.5b-base";
    public const string DefaultOllamaEndpoint = "http://127.0.0.1:11434";
    public const int DefaultDebounceMs = 250;
    public const int DefaultRequestTimeoutMs = 5000;
    public const int DefaultNaturalLanguageMaxTokens = 96;

    public string OllamaEndpoint { get; init; } = DefaultOllamaEndpoint;
    public string Model { get; init; } = DefaultModel;
    public int DebounceMs { get; init; } = DefaultDebounceMs;
    public int RequestTimeoutMs { get; init; } = DefaultRequestTimeoutMs;
    public bool NaturalLanguageEnabled { get; init; } = true;
    public int NaturalLanguageMaxTokens { get; init; } = DefaultNaturalLanguageMaxTokens;

    public Uri GenerateUri =>
        new(new Uri(OllamaEndpoint.TrimEnd('/') + "/"), "api/generate");
}
