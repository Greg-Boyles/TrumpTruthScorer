using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.BedrockRuntime;
using Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AnalysisFunction;

public class Function
{
    private readonly IDynamoDBContext _dbContext;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly PostAnalysisService _postAnalysisService;

    public Function()
    {
        var dynamoClient = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(dynamoClient);
        _bedrockClient = new AmazonBedrockRuntimeClient();
        _postAnalysisService = new PostAnalysisService();
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

                var analysis = await _postAnalysisService.AnalyzePostAsync(post, _bedrockClient, message => context.Logger.LogInformation(message));
                if (analysis == null)
                {
                    context.Logger.LogWarning($"Skipping post {post.PostId} because no analysis was produced.");
                    continue;
                }
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
}