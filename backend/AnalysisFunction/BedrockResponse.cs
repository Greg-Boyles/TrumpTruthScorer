using System.Text.Json.Serialization;

namespace AnalysisFunction;

public class BedrockResponse
{
    [JsonPropertyName("content")]
    public List<BedrockContent>? Content { get; set; }
}