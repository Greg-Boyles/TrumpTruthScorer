using System.Text.Json.Serialization;

namespace ScraperFunction;

public class MediaAttachment
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}