using SQLite;

namespace BlueTube.Core.Models;

public class Channel
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";

    /// <summary>User tags, comma-separated. Use <see cref="TagList"/>.</summary>
    public string Tags { get; set; } = "";

    public DateTime? LastFetchedAt { get; set; }

    [Ignore]
    public IReadOnlyList<string> TagList
    {
        get => TagHelper.Parse(Tags);
        set => Tags = TagHelper.Join(value);
    }
}
