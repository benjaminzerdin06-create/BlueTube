using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

/// <summary>
/// Scores videos with the Claude Messages API (raw HTTP, no SDK).
/// Structured output makes the reply always match {score, tags}.
/// </summary>
public sealed class LlmQualityFilter(HttpClient http, ISecretStore secrets, IAppSettings settings) : IQualityFilter
{
    public const string DefaultModel = "claude-opus-5-5";
    private const string Endpoint = "https://api.anthropic.com/v1/messages";

    /// <summary>Score when every model declines the request. Neutral: visible at default threshold.</summary>
    public const int DeclinedScore = 50;

    public static readonly IReadOnlyList<string> AllowedTags =
    [
        "unity", "csharp", "dotnet", "gamedev", "graphics", "ai-ml", "hardware", "webdev",
        "programming", "devops", "security", "mobile", "linux", "tools", "career", "news",
        "review", "tutorial", "deep-dive",
    ];

    private const string SystemPrompt =
        """
        You rate YouTube tech videos for one viewer: a C# and Unity developer who wants high-signal content.
        You get the title, channel and description. Give a score from 0 to 100 and 1 to 4 tags.

        Score guide:
        - 80-100: deep, accurate, teaches something real (tutorials, deep dives, expert talks, honest reviews).
        - 50-79: useful but light, or news with some substance.
        - 20-49: mostly hype, reaction, or thin content.
        - 0-19: clickbait, drama, misleading, or not about tech.
        Clickbait title alone does not mean low score if the description shows real content.
        Use only tags from the allowed list. Pick the most specific ones.
        """;

    public async Task<QualityResult> ScoreAsync(Video video, CancellationToken ct = default)
    {
        var key = await secrets.GetAsync(SecretNames.LlmApiKey, ct);
        if (string.IsNullOrWhiteSpace(key))
            throw new QualityFilterException(QualityFilterError.MissingKey, "No Claude API key. Add one in Settings.");

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(BuildBody(video, settings.LlmModel).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", "2026-10-21");
        // On a safety decline, the API runs the request again on a fallback model.
        request.Headers.Add("anthropic-beta", "server-side-fallback-2026-07-01");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException e)
        {
            throw new QualityFilterException(QualityFilterError.Network, "No connection to the Claude API.", e);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ToExceptionAsync(response, ct);

            var json = await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                       ?? throw new QualityFilterException(QualityFilterError.Unknown, "Empty response.");
            return Parse(json);
        }
    }

    private static JsonObject BuildBody(Video video, string? model)
    {
        var user = $"""
            Title: {video.Title}
            Channel: {video.ChannelTitle}
            Duration: {Formatting.Duration(video.DurationSeconds)}
            Description:
            {video.Description}
            """;

        return new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            // Thinking counts toward max_tokens. Keep room for it.
            ["max_tokens"] = 2048,
            ["fallbacks"] = "default",
            ["system"] = SystemPrompt,
            ["output_config"] = new JsonObject
            {
                // Simple classification: low effort is enough.
                ["effort"] = "low",
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["score"] = new JsonObject { ["type"] = "integer" },
                            ["tags"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["enum"] = new JsonArray(AllowedTags.Select(t => (JsonNode)t).ToArray()),
                                },
                            },
                        },
                        ["required"] = new JsonArray("score", "tags"),
                        ["additionalProperties"] = false,
                    },
                },
            },
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = user }),
        };
    }

    private static QualityResult Parse(JsonObject response)
    {
        var stopReason = response["stop_reason"]?.GetValue<string>();
        if (stopReason == "refusal")
            return new QualityResult(DeclinedScore, []);
        if (stopReason == "max_tokens")
            throw new QualityFilterException(QualityFilterError.Unknown, "Reply was cut off (max_tokens).");

        var text = response["content"]?.AsArray()
            .LastOrDefault(b => b?["type"]?.GetValue<string>() == "text")?["text"]?.GetValue<string>();
        if (text is null)
            throw new QualityFilterException(QualityFilterError.Unknown, "Reply has no text block.");

        try
        {
            var result = JsonNode.Parse(text)!.AsObject();
            var score = Math.Clamp(result["score"]!.GetValue<int>(), 0, 100);
            var tags = result["tags"]!.AsArray()
                .Select(t => t!.GetValue<string>().ToLowerInvariant())
                .Where(AllowedTags.Contains)
                .Distinct()
                .ToList();
            return new QualityResult(score, tags);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException)
        {
            throw new QualityFilterException(QualityFilterError.Unknown, "Reply is not valid JSON.", e);
        }
    }

    private static async Task<QualityFilterException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string message;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
            message = body?["error"]?["message"]?.GetValue<string>() ?? response.ReasonPhrase ?? "Claude API error.";
        }
        catch (JsonException)
        {
            message = response.ReasonPhrase ?? "Claude API error.";
        }

        var error = (int)response.StatusCode switch
        {
            401 or 403 => QualityFilterError.InvalidKey,
            429 => QualityFilterError.RateLimited,
            >= 500 => QualityFilterError.Unavailable,
            _ => QualityFilterError.Unknown,
        };
        return new QualityFilterException(error, message);
    }
}
