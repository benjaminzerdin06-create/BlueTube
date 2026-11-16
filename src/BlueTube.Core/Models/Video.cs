using SQLite;

namespace BlueTube.Core.Models;

public class Video
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    [Indexed]
    public string ChannelId { get; set; } = "";

    /// <summary>Copy of the channel title. Cards and the filter do not need a join.</summary>
    public string ChannelTitle { get; set; } = "";

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";

    [Indexed]
    public DateTime PublishedAt { get; set; }

    public int DurationSeconds { get; set; }

    [Indexed]
    public bool IsShort { get; set; }

    /// <summary>0-100. Null until the quality filter scores the video.</summary>
    public int? QualityScore { get; set; }

    /// <summary>Quality filter tags, comma-separated. Use <see cref="AiTagList"/>.</summary>
    public string AiTags { get; set; } = "";

    public bool Watched { get; set; }
    public bool WatchLater { get; set; }
    public bool Favorite { get; set; }

    [Ignore]
    public IReadOnlyList<string> AiTagList
    {
        get => TagHelper.Parse(AiTags);
        set => AiTags = TagHelper.Join(value);
    }
}
