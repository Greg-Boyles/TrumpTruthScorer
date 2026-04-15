using System.Text.Json.Serialization;

namespace ScraperFunction;

public class ScrapeCreatorsResponse
{
    [JsonPropertyName("posts")]
    public List<TruthPost>? Posts { get; set; }
    [JsonPropertyName("data")]
    public List<TruthPost>? Data { get; set; }
}