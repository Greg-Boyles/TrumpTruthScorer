using System.Text.Json.Serialization;

namespace ScraperFunction;

public class TruthPost
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("reblogs_count")]
    public int ReblogsCount { get; set; }

    [JsonPropertyName("favourites_count")]
    public int FavouritesCount { get; set; }

    [JsonPropertyName("replies_count")]
    public int RepliesCount { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("media_attachments")]
    public List<MediaAttachment>? MediaAttachments { get; set; }

    [JsonPropertyName("reblog")]
    public object? Reblog { get; set; }
}