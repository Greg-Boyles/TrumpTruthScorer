using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System.Text.Json;
using System.Collections.Generic;
using Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ApiFunction;

public class Function
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IDynamoDBContext _dbContext;
    private readonly string _postsTable;

    public Function()
    {
        _dynamoClient = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(_dynamoClient);
        _postsTable = Environment.GetEnvironmentVariable("POSTS_TABLE") ?? "TruthScorer-Posts";
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing {request.HttpMethod} {request.Path}");

        try
        {
            var path = request.Path?.TrimEnd('/') ?? "";
            var pathParams = request.PathParameters ?? new Dictionary<string, string>();

            // Route handling
            if (path == "/posts" && request.HttpMethod == "GET")
            {
                return await GetRecentPosts(request, context);
            }
            else if (path.StartsWith("/posts/") && request.HttpMethod == "GET")
            {
                pathParams.TryGetValue("date", out var dateParam);
                var date = dateParam ?? path.Split('/').Last();
                return await GetPostsByDate(date, context);
            }
            else if (path.StartsWith("/summary/") && request.HttpMethod == "GET")
            {
                pathParams.TryGetValue("date", out var dateParam);
                var date = dateParam ?? path.Split('/').Last();
                return await GetDailySummary(date, context);
            }
            else if (path == "/trends" && request.HttpMethod == "GET")
            {
                return await GetTrends(request, context);
            }

            return CreateResponse(404, new { error = "Not found" });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return CreateResponse(500, new { error = "Internal server error" });
        }
    }

    private async Task<APIGatewayProxyResponse> GetRecentPosts(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? limitStr = null;
        request.QueryStringParameters?.TryGetValue("limit", out limitStr);
        limitStr ??= "20";
        var limit = int.TryParse(limitStr, out var l) ? Math.Min(l, 100) : 20;

        // Get today's date for recent posts
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var posts = await GetPostsForDateWithAnalysis(today, limit);

        // If no posts today, try yesterday
        if (posts.Count() == 0)
        {
            var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            posts = await GetPostsForDateWithAnalysis(yesterday, limit);
        }

        return CreateResponse(200, new { posts, date = today });
    }

    private async Task<APIGatewayProxyResponse> GetPostsByDate(string date, ILambdaContext context)
    {
        var posts = await GetPostsForDateWithAnalysis(date, 100);
        return CreateResponse(200, new { posts, date });
    }

    private async Task<List<PostWithAnalysis>> GetPostsForDateWithAnalysis(string date, int limit)
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
            },
            Limit = limit,
            BackwardSearch = true // Most recent first
        };

        var search = table.Query(queryConfig);
        var results = new List<PostWithAnalysis>();

        var documents = await search.GetNextSetAsync();
        foreach (var doc in documents)
        {
            var post = new Post
            {
                PostId = doc["postId"].AsString(),
                CreatedAt = doc["createdAt"].AsString(),
                DatePartition = doc["datePartition"].AsString(),
                Content = doc.ContainsKey("content") ? doc["content"].AsString() : "",
                ReblogsCount = doc.ContainsKey("reblogsCount") ? doc["reblogsCount"].AsInt() : 0,
                FavouritesCount = doc.ContainsKey("favouritesCount") ? doc["favouritesCount"].AsInt() : 0,
                RepliesCount = doc.ContainsKey("repliesCount") ? doc["repliesCount"].AsInt() : 0,
                Url = doc.ContainsKey("url") ? doc["url"].AsString() : ""
            };

            Analysis? analysis = null;
            try
            {
                analysis = await _dbContext.LoadAsync<Analysis>(post.PostId);
            }
            catch { }

            results.Add(new PostWithAnalysis { Post = post, Analysis = analysis });
        }

        return results.OrderByDescending(p => p.Post.CreatedAt).ToList();
    }

    private async Task<APIGatewayProxyResponse> GetDailySummary(string date, ILambdaContext context)
    {
        try
        {
            var summary = await _dbContext.LoadAsync<DailySummary>(date);
            
            if (summary == null)
            {
                // Generate on-the-fly summary if not cached
                var posts = await GetPostsForDateWithAnalysis(date, 200);
                
                if (posts.Count == 0)
                {
                    return CreateResponse(404, new { error = "No data for this date" });
                }

                // Calculate basic stats
                var analyses = posts.Where(p => p.Analysis != null).Select(p => p.Analysis!).ToList();
                var avgMental = analyses.Count > 0 ? analyses.Average(a => a.MentalScore) : 0;
                var avgMoral = analyses.Count > 0 ? analyses.Average(a => a.MoralScore) : 0;
                var postingHours = posts
                    .Select(p => DateTime.Parse(p.Post.CreatedAt).Hour)
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();
                var quietHours = CalculateQuietHours(postingHours);
                var topThemes = analyses
                    .SelectMany(a => a.KeyThemes ?? new List<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .GroupBy(t => t.ToLowerInvariant())
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();

                return CreateResponse(200, new
                {
                    date,
                    totalPosts = posts.Count,
                    avgMentalScore = Math.Round(avgMental, 1),
                    avgMoralScore = Math.Round(avgMoral, 1),
                    overallScore = Math.Round((avgMental + avgMoral) / 2, 1),
                    topThemes,
                    summaryText = string.Empty,
                    postingHours,
                    quietHoursStart = quietHours.start,
                    quietHoursEnd = quietHours.end,
                    createdAt = DateTime.UtcNow.ToString("O"),
                    cached = false
                });
            }

            return CreateResponse(200, summary);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error getting summary: {ex.Message}");
            return CreateResponse(404, new { error = "Summary not found" });
        }
    }

    private async Task<APIGatewayProxyResponse> GetTrends(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? daysStr = null;
        request.QueryStringParameters?.TryGetValue("days", out daysStr);
        daysStr ??= "7";
        var days = int.TryParse(daysStr, out var d) ? Math.Min(d, 30) : 7;

        var trends = new List<TrendData>();

        for (int i = 0; i < days; i++)
        {
            var date = DateTime.UtcNow.AddDays(-i).ToString("yyyy-MM-dd");
            
            try
            {
                var summary = await _dbContext.LoadAsync<DailySummary>(date);
                if (summary != null)
                {
                    trends.Add(new TrendData
                    {
                        Date = date,
                        AvgMentalScore = summary.AvgMentalScore,
                        AvgMoralScore = summary.AvgMoralScore,
                        PostCount = summary.TotalPosts
                    });
                }
            }
            catch { }
        }

        return CreateResponse(200, new { trends = trends.OrderBy(t => t.Date).ToList() });
    }

    private static (int? start, int? end) CalculateQuietHours(List<int> postingHours)
    {
        if (postingHours.Count < 2) return (null, null);

        var quietHours = Enumerable.Range(0, 24).Except(postingHours).ToList();
        if (quietHours.Count == 0) return (null, null);

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

        return (longestStart, (longestStart + longestLength - 1) % 24);
    }

    private APIGatewayProxyResponse CreateResponse(int statusCode, object body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" },
                { "Access-Control-Allow-Methods", "GET, OPTIONS" },
                { "Access-Control-Allow-Headers", "Content-Type" }
            },
            Body = JsonSerializer.Serialize(body, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            })
        };
    }
}
