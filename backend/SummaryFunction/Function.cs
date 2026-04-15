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
            var summary = CalculateDailySummary(targetDate, posts, analyses);
            
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

    private DailySummary CalculateDailySummary(string date, List<Post> posts, List<Analysis> analyses)
    {
        var avgMental = analyses.Count > 0 ? analyses.Average(a => a.MentalScore) : 0;
        var avgMoral = analyses.Count > 0 ? analyses.Average(a => a.MoralScore) : 0;
        var overallScore = (avgMental + avgMoral) / 2;

        // Get posting hours (in UTC)
        var postingHours = posts
            .Select(p => DateTime.Parse(p.CreatedAt).Hour)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        // Calculate quiet hours (longest gap without posts)
        var (quietStart, quietEnd) = CalculateQuietHours(postingHours);

        // Aggregate themes
        var allThemes = analyses
            .SelectMany(a => a.KeyThemes)
            .GroupBy(t => t.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return new DailySummary
        {
            Date = date,
            TotalPosts = posts.Count,
            AvgMentalScore = Math.Round(avgMental, 1),
            AvgMoralScore = Math.Round(avgMoral, 1),
            OverallScore = Math.Round(overallScore, 1),
            TopThemes = allThemes,
            PostingHours = postingHours,
            QuietHoursStart = quietStart,
            QuietHoursEnd = quietEnd,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
    }

    private (int? start, int? end) CalculateQuietHours(List<int> postingHours)
    {
        if (postingHours.Count < 2) return (null, null);

        var allHours = Enumerable.Range(0, 24).ToList();
        var quietHours = allHours.Except(postingHours).ToList();

        if (quietHours.Count == 0) return (null, null);

        // Find longest consecutive quiet period
        var longestStart = quietHours[0];
        var longestLength = 1;
        var currentStart = quietHours[0];
        var currentLength = 1;

        for (int i = 1; i < quietHours.Count; i++)
        {
            if (quietHours[i] == quietHours[i - 1] + 1 || (quietHours[i - 1] == 23 && quietHours[i] == 0))
            {
                currentLength++;
            }
            else
            {
                if (currentLength > longestLength)
                {
                    longestStart = currentStart;
                    longestLength = currentLength;
                }
                currentStart = quietHours[i];
                currentLength = 1;
            }
        }

        if (currentLength > longestLength)
        {
            longestStart = currentStart;
            longestLength = currentLength;
        }

        var longestEnd = (longestStart + longestLength - 1) % 24;
        
        return (longestStart, longestEnd);
    }
}
