using System.Text.Json.Serialization;

namespace BlueTube.Core.Services;

// DTOs for the YouTube Data API v3 responses. Only the fields we use.

internal sealed record ListResponse<T>(
    [property: JsonPropertyName("items")] List<T>? Items,
    [property: JsonPropertyName("nextPageToken")] string? NextPageToken);

internal sealed record PlaylistItemDto(
    [property: JsonPropertyName("contentDetails")] PlaylistItemDetailsDto ContentDetails);

internal sealed record PlaylistItemDetailsDto(
    [property: JsonPropertyName("videoId")] string VideoId,
    [property: JsonPropertyName("videoPublishedAt")] DateTimeOffset? VideoPublishedAt);

internal sealed record VideoDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("snippet")] SnippetDto Snippet,
    [property: JsonPropertyName("contentDetails")] VideoDetailsDto? ContentDetails,
    [property: JsonPropertyName("player")] PlayerDto? Player);

internal sealed record ChannelDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("snippet")] SnippetDto Snippet);

internal sealed record SnippetDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("channelId")] string? ChannelId,
    [property: JsonPropertyName("channelTitle")] string? ChannelTitle,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset PublishedAt,
    [property: JsonPropertyName("thumbnails")] Dictionary<string, ThumbnailDto>? Thumbnails);

internal sealed record ThumbnailDto(
    [property: JsonPropertyName("url")] string Url);

internal sealed record VideoDetailsDto(
    [property: JsonPropertyName("duration")] string? Duration);

internal sealed record PlayerDto(
    [property: JsonPropertyName("embedWidth")] long? EmbedWidth,
    [property: JsonPropertyName("embedHeight")] long? EmbedHeight);

internal sealed record ErrorResponse(
    [property: JsonPropertyName("error")] ErrorBody? Error);

internal sealed record ErrorBody(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("errors")] List<ErrorItem>? Errors);

internal sealed record ErrorItem(
    [property: JsonPropertyName("reason")] string? Reason);
