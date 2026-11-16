using BlueTube.Core.Models;
using BlueTube.Core.Services;

namespace BlueTube.Tests.Fakes;

public sealed class FakeQualityFilter : IQualityFilter
{
    /// <summary>Score per video ID. Default 70.</summary>
    public Dictionary<string, QualityResult> Results { get; } = [];
    /// <summary>Throw this for every call when set.</summary>
    public QualityFilterException? ThrowAll { get; set; }
    /// <summary>Throw this for the given video ID.</summary>
    public Dictionary<string, QualityFilterException> Errors { get; } = [];
    public List<string> Calls { get; } = [];

    public Task<QualityResult> ScoreAsync(Video video, CancellationToken ct = default)
    {
        lock (Calls) Calls.Add(video.Id);
        if (ThrowAll is not null) throw ThrowAll;
        if (Errors.TryGetValue(video.Id, out var e)) throw e;
        return Task.FromResult(Results.GetValueOrDefault(video.Id, new QualityResult(70, ["programming"])));
    }
}

public sealed class FakeAppSettings : IAppSettings
{
    public int MinScore { get; set; }
    public string LlmModel { get; set; } = "test-model";
}
