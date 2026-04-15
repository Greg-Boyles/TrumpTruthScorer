using System.Text.Json.Serialization;

namespace AnalysisFunction;

public class AnalysisResponse
{
    [JsonPropertyName("mental_score")]
    public int MentalScore { get; set; } = 5;

    [JsonPropertyName("moral_score")]
    public int MoralScore { get; set; } = 5;

    [JsonPropertyName("emotional_state")]
    public string? EmotionalState { get; set; }

    [JsonPropertyName("key_themes")]
    public List<string>? KeyThemes { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}