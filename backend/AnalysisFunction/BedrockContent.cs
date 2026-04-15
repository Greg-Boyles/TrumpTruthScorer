using System.Text.Json.Serialization;

namespace AnalysisFunction;

public class BedrockContent
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}