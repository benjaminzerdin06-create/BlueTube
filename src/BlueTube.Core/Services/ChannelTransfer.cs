using System.Text.Json;
using System.Text.Json.Serialization;
using BlueTube.Core.Models;

namespace BlueTube.Core.Services;

/// <summary>
/// Copy the channel list between devices (phone and PC) as JSON text.
/// Contains channel ID, title, thumbnail and tags only. No videos, no keys.
/// </summary>
public static class ChannelTransfer
{
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Export(IEnumerable<Channel> channels) =>
        JsonSerializer.Serialize(new ExportFile(
            FormatVersion,
            channels.Select(c => new ExportChannel(c.Id, c.Title, c.ThumbnailUrl, c.TagList.ToList())).ToList()),
            Options);

    /// <summary>Parses export text. Skips entries without a valid "UC..." ID.</summary>
    /// <exception cref="FormatException">Text is not a BlueTube export.</exception>
    public static IReadOnlyList<Channel> Import(string json)
    {
        ExportFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ExportFile>(json, Options);
        }
        catch (JsonException e)
        {
            throw new FormatException("Text is not a BlueTube channel export.", e);
        }
        if (file?.Channels is null)
            throw new FormatException("Text is not a BlueTube channel export.");

        return file.Channels
            .Where(c => c.Id is { Length: 24 } && c.Id.StartsWith("UC", StringComparison.Ordinal))
            .DistinctBy(c => c.Id)
            .Select(c => new Channel
            {
                Id = c.Id,
                Title = c.Title ?? c.Id,
                ThumbnailUrl = c.ThumbnailUrl ?? "",
                TagList = c.Tags ?? [],
            })
            .ToList();
    }

    private sealed record ExportFile(
        [property: JsonPropertyName("bluetube_channels")] int Version,
        [property: JsonPropertyName("channels")] List<ExportChannel>? Channels);

    private sealed record ExportChannel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("thumbnail")] string? ThumbnailUrl,
        [property: JsonPropertyName("tags")] List<string>? Tags);
}
