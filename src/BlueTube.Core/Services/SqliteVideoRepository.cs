using BlueTube.Core.Models;
using SQLite;

namespace BlueTube.Core.Services;

/// <summary>sqlite-net does not accept tokens. Each method checks the token before the call.</summary>
public sealed class SqliteVideoRepository(string dbPath) : IVideoRepository, IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _db = new(dbPath);

    public async Task InitAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _db.CreateTableAsync<Channel>();
        await _db.CreateTableAsync<Video>();
    }

    public async Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _db.Table<Channel>().OrderBy(c => c.Title).ToListAsync();
    }

    public async Task<Channel?> GetChannelAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _db.FindAsync<Channel>(id);
    }

    public async Task UpsertChannelAsync(Channel channel, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _db.InsertOrReplaceAsync(channel);
    }

    public async Task DeleteChannelAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _db.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM Video WHERE ChannelId = ?", id);
            conn.Delete<Channel>(id);
        });
    }

    public async Task<Video?> GetVideoAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _db.FindAsync<Video>(id);
    }

    public async Task<int> InsertNewVideosAsync(IEnumerable<Video> videos, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // "OR IGNORE": SQLite skips rows with a known key. User flags and scores stay.
        return await _db.InsertAllAsync(videos, "OR IGNORE");
    }

    public async Task UpdateVideoAsync(Video video, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _db.UpdateAsync(video);
    }

    public async Task<IReadOnlyList<Video>> GetVideosAsync(VideoQuery query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var q = _db.Table<Video>().Where(v => v.IsShort == query.IsShort);
        if (query.Watched is { } watched) q = q.Where(v => v.Watched == watched);
        if (query.WatchLater is { } later) q = q.Where(v => v.WatchLater == later);
        if (query.Favorite is { } fav) q = q.Where(v => v.Favorite == fav);
        return await q.OrderByDescending(v => v.PublishedAt).Take(query.Limit).ToListAsync();
    }

    public async Task<IReadOnlyList<Video>> GetUnscoredVideosAsync(int limit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _db.Table<Video>()
            .Where(v => v.QualityScore == null)
            .OrderByDescending(v => v.PublishedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<DateTime?> GetLatestPublishedAtAsync(string channelId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var latest = await _db.Table<Video>()
            .Where(v => v.ChannelId == channelId)
            .OrderByDescending(v => v.PublishedAt)
            .FirstOrDefaultAsync();
        return latest?.PublishedAt;
    }

    public ValueTask DisposeAsync() => new(_db.CloseAsync());
}
