using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AnalysisFunction;

public class Function
{
    private readonly IDynamoDBContext _dbContext;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private const string ModelId = "anthropic.claude-3-haiku-20240307-v1:0";

    public Function()
    {
        var dynamoClient = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(dynamoClient);
        _bedrockClient = new AmazonBedrockRuntimeClient();
    }

    public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing {dynamoEvent.Records.Count} DynamoDB stream records");

        foreach (var record in dynamoEvent.Records)
        {
            if (record.EventName != "INSERT")
            {
                continue;
            }

            try
            {
                var post = ParsePost(record.Dynamodb.NewImage);
                
                if (post == null || string.IsNullOrWhiteSpace(post.Content))
                {
                    context.Logger.LogWarning("Skipping record with no content");
                    continue;
                }

                context.Logger.LogInformation($"Analyzing post {post.PostId}");
                
                var analysis = await AnalyzePost(post, context);
                await _dbContext.SaveAsync(analysis);
                
                context.Logger.LogInformation($"Saved analysis for post {post.PostId}");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing record: {ex.Message}");
            }
        }
    }

    private Post? ParsePost(Dictionary<string, Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue> image)
    {
        return new Post
        {
            PostId = image.GetValueOrDefault("postId")?.S ?? "",
            CreatedAt = image.GetValueOrDefault("createdAt")?.S ?? "",
            DatePartition = image.GetValueOrDefault("datePartition")?.S ?? "",
            Content = image.GetValueOrDefault("content")?.S ?? ""
        };
    }

    private async Task<Analysis> AnalyzePost(Post post, ILambdaContext context)
    {
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

        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestBody)))
        };

        var response = await _bedrockClient.InvokeModelAsync(request);
        var responseBody = await new StreamReader(response.Body).ReadToEndAsync();
        
        context.Logger.LogInformation($"Bedrock response: {responseBody}");

        var bedrockResponse = JsonSerializer.Deserialize<BedrockResponse>(responseBody);
        var analysisJson = bedrockResponse?.Content?.FirstOrDefault()?.Text ?? "{}";
        
        // Clean up JSON if wrapped in markdown code blocks
        analysisJson = analysisJson.Trim();
        if (analysisJson.StartsWith("```json"))
        {
            analysisJson = analysisJson.Substring(7);
        }
        if (analysisJson.StartsWith("```"))
        {
            analysisJson = analysisJson.Substring(3);
        }
        if (analysisJson.EndsWith("```"))
        {
            analysisJson = analysisJson.Substring(0, analysisJson.Length - 3);
        }
        analysisJson = analysisJson.Trim();

        var parsed = JsonSerializer.Deserialize<AnalysisResponse>(analysisJson) ?? new AnalysisResponse();

        return new Analysis
        {
            PostId = post.PostId,
            MentalScore = Math.Clamp(parsed.MentalScore, 1, 10),
            MoralScore = Math.Clamp(parsed.MoralScore, 1, 10),
            EmotionalState = parsed.EmotionalState ?? "unknown",
            KeyThemes = parsed.KeyThemes ?? new List<string>(),
            Summary = parsed.Summary ?? "",
            AnalyzedAt = DateTime.UtcNow.ToString("O")
        };
    }
}

// Bedrock response models
public class BedrockResponse
{
    [JsonPropertyName("content")]
    public List<BedrockContent>? Content { get; set; }
}

public class BedrockContent
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

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
