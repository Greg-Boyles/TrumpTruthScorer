using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

public class PostAnalysisService
{
    private const string ModelId = "anthropic.claude-3-haiku-20240307-v1:0";

    public async Task<Analysis?> AnalyzePostAsync(Post post, IAmazonBedrockRuntime bedrockClient, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(post.Content))
        {
            log?.Invoke($"Skipping post {post.PostId} because it has no analyzable content.");
            return null;
        }

        var prompt = $@"Analyze this Truth Social post from Donald Trump and provide a JSON response with the following fields:
- mental_score: Integer 1-10 (10 = calm/focused, 1 = erratic/stressed)
- moral_score: Integer 1-10 (10 = truthful/ethical rhetoric, 1 = misleading/aggressive)
- emotional_state: One word describing the emotional tone (e.g., ""agitated"", ""triumphant"", ""defensive"")
- key_themes: Array of 2-4 main themes/topics
- summary: One sentence summary of the post's message

Post content:
{post.Content}

Respond ONLY with valid JSON, no other text.";

        var requestBody = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 500,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        string responseBody = string.Empty;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var request = new InvokeModelRequest
                {
                    ModelId = ModelId,
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestBody)))
                };

                var response = await bedrockClient.InvokeModelAsync(request);
                responseBody = await new StreamReader(response.Body).ReadToEndAsync();
                log?.Invoke($"Bedrock response for post {post.PostId}: {responseBody}");
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                log?.Invoke($"Bedrock attempt {attempt} failed for post {post.PostId}: {ex.Message}");
                if (attempt == 3)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }

        if (string.IsNullOrWhiteSpace(responseBody) && lastException != null)
        {
            throw lastException;
        }

        var bedrockResponse = JsonSerializer.Deserialize<BedrockResponse>(responseBody);
        var analysisJson = bedrockResponse?.Content?.FirstOrDefault()?.Text ?? "{}";

        analysisJson = analysisJson.Trim();
        if (analysisJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            analysisJson = analysisJson[7..];
        }
        if (analysisJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            analysisJson = analysisJson[3..];
        }
        if (analysisJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            analysisJson = analysisJson[..^3];
        }

        var parsed = JsonSerializer.Deserialize<AnalysisResponse>(analysisJson.Trim()) ?? new AnalysisResponse();

        return new Analysis
        {
            PostId = post.PostId,
            MentalScore = Math.Clamp(parsed.MentalScore, 1, 10),
            MoralScore = Math.Clamp(parsed.MoralScore, 1, 10),
            EmotionalState = parsed.EmotionalState ?? "unknown",
            KeyThemes = parsed.KeyThemes ?? new List<string>(),
            Summary = parsed.Summary ?? string.Empty,
            AnalyzedAt = DateTime.UtcNow.ToString("O")
        };
    }

    private sealed class BedrockResponse
    {
        [JsonPropertyName("content")]
        public List<BedrockContent>? Content { get; set; }
    }

    private sealed class BedrockContent
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class AnalysisResponse
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
}
