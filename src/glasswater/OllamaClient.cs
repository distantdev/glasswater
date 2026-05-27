using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace glasswater;

public sealed class OllamaClient : IDisposable
{
    private static readonly string[] FimStopTokens =
    {
        "\n",
        "<|fim_prefix|>",
        "<|fim_suffix|>",
        "<|fim_middle|>",
    };

    private static readonly string[] CommandStopTokens =
    {
        "\n",
        "\r",
        "```",
    };

    private readonly HttpClient _httpClient;
    private readonly GlasswaterSettings _settings;
    private readonly bool _ownsHttpClient;

    public OllamaClient(GlasswaterSettings settings, HttpClient? httpClient = null)
    {
        _settings = settings;
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(settings.RequestTimeoutMs),
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
    }

    public async Task<string?> GenerateFimAsync(
        LineContext context,
        CancellationToken cancellationToken)
    {
        var request = new OllamaGenerateRequest
        {
            Model = _settings.Model,
            Prompt = context.LinePrefix,
            Suffix = context.LineSuffix,
            Stream = false,
            Options = new OllamaGenerateOptions
            {
                Temperature = 0.0,
                TopP = 1.0,
                NumPredict = 32,
                Stop = FimStopTokens,
            },
        };

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(_settings.GenerateUri, request, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        OllamaGenerateResponse? body = await response.Content
            .ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return SanitizeMiddle(body?.Response, context.LineSuffix);
    }

    public async Task<string?> GenerateCommandAsync(string request, CancellationToken cancellationToken)
    {
        string prompt =
            "Convert the request into exactly one PowerShell 7 command. " +
            "Output only the command. No explanation, markdown, quotes, or backticks.\n\n" +
            "Request: " + request.Trim() + "\n" +
            "Command:";

        var generateRequest = new OllamaGenerateRequest
        {
            Model = _settings.Model,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaGenerateOptions
            {
                Temperature = 0.0,
                TopP = 1.0,
                NumPredict = _settings.NaturalLanguageMaxTokens,
                Stop = CommandStopTokens,
            },
        };

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(_settings.GenerateUri, generateRequest, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        OllamaGenerateResponse? body = await response.Content
            .ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return SanitizeCommand(body?.Response);
    }

    public static string? SanitizeCommand(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string text = raw.Trim();
        foreach (string stop in CommandStopTokens)
        {
            int index = text.IndexOf(stop, StringComparison.Ordinal);
            if (index >= 0)
            {
                text = text[..index];
            }
        }

        text = text.Trim().Trim('`', '"', '\'');
        if (text.StartsWith("Command:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["Command:".Length..].Trim();
        }

        int newline = text.IndexOf('\n');
        if (newline >= 0)
        {
            text = text[..newline].Trim();
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static string? SanitizeMiddle(string? raw, string lineSuffix = "")
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        string text = raw;
        foreach (string stop in FimStopTokens)
        {
            int index = text.IndexOf(stop, StringComparison.Ordinal);
            if (index >= 0)
            {
                text = text[..index];
            }
        }

        if (!string.IsNullOrEmpty(lineSuffix))
        {
            int suffixIndex = text.IndexOf(lineSuffix, StringComparison.Ordinal);
            if (suffixIndex >= 0)
            {
                text = text[..suffixIndex];
            }
        }

        text = text.Trim('\r', '\n', ' ');
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("suffix")]
        public string? Suffix { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaGenerateOptions Options { get; set; } = new();
    }

    private sealed class OllamaGenerateOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }

        [JsonPropertyName("stop")]
        public string[] Stop { get; set; } = Array.Empty<string>();
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
