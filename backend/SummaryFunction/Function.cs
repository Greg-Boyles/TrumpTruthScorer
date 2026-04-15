using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SummaryFunction;

public class Function
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IDynamoDBContext _dbContext;
    private readonly string _postsTable;
    private readonly string _analysisTable;

    public Function()
    {
        _dynamoClient = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(_dynamoClient);
        _postsTable = Environment.GetEnvironmentVariable("POSTS_TABLE") ?? "TruthScorer-Posts";
        _analysisTable = Environment.GetEnvironmentVariable("ANALYSIS_TABLE") ?? "TruthScorer-Analysis";
    }

    public async Task<string> FunctionHandler(object input, ILambdaContext context)
    {
        // Generate summary for yesterday (since this runs at midnight)
        var targetDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        
        context.Logger.LogInformation($"Generating daily summary for {targetDate}");

        try
        {
            // Get all posts for the date
            var posts = await GetPostsForDate(targetDate);
            
            if (posts.Count == 0)
            {
                context.Logger.LogInformation($"No posts found for {targetDate}");
                return $"No posts for {targetDate}";
            }

            // Get analyses for those posts
            var analyses = await GetAnalysesForPosts(posts.Select(p => p.PostId).ToList());

            // Calculate aggregates
            var summary = DailySummaryService.CalculateDailySummary(targetDate, posts, analyses);
            
            // Save summary
            await _dbContext.SaveAsync(summary);
            
            context.Logger.LogInformation($"Saved daily summary for {targetDate}: {summary.TotalPosts} posts, avg mental: {summary.AvgMentalScore:F1}, avg moral: {summary.AvgMoralScore:F1}");
            
            return $"Summary generated for {targetDate}";
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error generating summary: {ex.Message}");
            throw;
        }
    }

    private async Task<List<Post>> GetPostsForDate(string date)
    {
        var table = Table.LoadTable(_dynamoClient, _postsTable);
        
        var queryConfig = new QueryOperationConfig
        {
            IndexName = "DateIndex",
            KeyExpression = new Expression
            {
                ExpressionStatement = "datePartition = :date",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":date", date }
                }
            }
        };

        var search = table.Query(queryConfig);
        var posts = new List<Post>();

        do
        {
            var documents = await search.GetNextSetAsync();
            foreach (var doc in documents)
            {
                posts.Add(new Post
                {
                    PostId = doc["postId"].AsString(),
                    CreatedAt = doc["createdAt"].AsString(),
                    DatePartition = doc["datePartition"].AsString(),
                    Content = doc.ContainsKey("content") ? doc["content"].AsString() : ""
                });
            }
        } while (!search.IsDone);

        return posts;
    }

    private async Task<List<Analysis>> GetAnalysesForPosts(List<string> postIds)
    {
        var analyses = new List<Analysis>();
        
        foreach (var postId in postIds)
        {
            try
            {
                var analysis = await _dbContext.LoadAsync<Analysis>(postId);
                if (analysis != null)
                {
                    analyses.Add(analysis);
                }
            }
            catch
            {
                // Skip posts without analysis
            }
        }

        return analyses;
    }
}
