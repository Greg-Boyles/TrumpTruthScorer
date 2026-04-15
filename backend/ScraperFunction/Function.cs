using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ScraperFunction;

public class Function
{
    private readonly IDynamoDBContext _dbContext;
    private readonly IAmazonSimpleSystemsManagement _ssmClient;
    private readonly HttpClient _httpClient;
    private readonly ScrapeCreatorsClient _scrapeCreatorsClient;
    private readonly string _apiKeyParam;
    private string? _apiKeyCache;

    public Function()
    {
        var client = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(client);
        _ssmClient = new AmazonSimpleSystemsManagementClient();
        _httpClient = new HttpClient();
        _scrapeCreatorsClient = new ScrapeCreatorsClient(_httpClient);
        _apiKeyParam = Environment.GetEnvironmentVariable("SCRAPECREATORS_API_KEY_PARAM") ?? "/truthscorer/scrapecreators-api-key";
    }

    private async Task<string> GetApiKeyAsync()
    {
        if (_apiKeyCache != null) return _apiKeyCache;
        
        var response = await _ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = _apiKeyParam,
            WithDecryption = true
        });
        
        _apiKeyCache = response.Parameter.Value;
        return _apiKeyCache;
    }

    public async Task<string> FunctionHandler(object input, ILambdaContext context)
    {
        context.Logger.LogInformation("Starting Truth Social scraper...");

        try
        {
            // Fetch posts from ScrapeCreators API
            var posts = await FetchTruthSocialPosts(context);
            
            if (posts.Count == 0)
            {
                context.Logger.LogInformation("No new posts found.");
                return "No new posts";
            }

            // Store posts in DynamoDB
            var savedCount = await SavePosts(posts, context);
            
            context.Logger.LogInformation($"Successfully saved {savedCount} new posts.");
            return $"Saved {savedCount} posts";
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error in scraper: {ex.Message}");
            throw;
        }
    }

    private async Task<List<Post>> FetchTruthSocialPosts(ILambdaContext context)
    {
        var apiKey = await GetApiKeyAsync();
        var page = await _scrapeCreatorsClient.FetchPostsPageAsync(apiKey, "realDonaldTrump");

        if (page.Posts.Count == 0)
        {
            return new List<Post>();
        }

        context.Logger.LogInformation($"Fetched {page.Posts.Count} posts from API. Next cursor: {page.NextMaxId ?? "<none>"}");
        return page.Posts;
    }

    private async Task<int> SavePosts(List<Post> posts, ILambdaContext context)
    {
        var savedCount = 0;
        
        foreach (var post in posts)
        {
            try
            {
                var existing = await _dbContext.LoadAsync<Post>(post.PostId, post.CreatedAt);
                if (existing == null)
                {
                    await _dbContext.SaveAsync(post);
                    savedCount++;
                    context.Logger.LogInformation($"Saved post {post.PostId}");
                    continue;
                }

                var merged = new Post
                {
                    PostId = existing.PostId,
                    CreatedAt = existing.CreatedAt,
                    DatePartition = string.IsNullOrWhiteSpace(existing.DatePartition) ? post.DatePartition : existing.DatePartition,
                    Content = string.IsNullOrWhiteSpace(existing.Content) ? post.Content : existing.Content,
                    ReblogsCount = Math.Max(existing.ReblogsCount, post.ReblogsCount),
                    FavouritesCount = Math.Max(existing.FavouritesCount, post.FavouritesCount),
                    RepliesCount = Math.Max(existing.RepliesCount, post.RepliesCount),
                    Url = string.IsNullOrWhiteSpace(existing.Url) ? post.Url : existing.Url,
                    MediaUrls = existing.MediaUrls.Count > 0 ? existing.MediaUrls : post.MediaUrls,
                    IsRetruth = existing.IsRetruth || post.IsRetruth
                };

                if (!PostsEquivalent(existing, merged))
                {
                    await _dbContext.SaveAsync(merged);
                    context.Logger.LogInformation($"Updated post {post.PostId}");
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning($"Error saving post {post.PostId}: {ex.Message}");
            }
        }

        return savedCount;
    }

    private static bool PostsEquivalent(Post left, Post right) =>
        left.DatePartition == right.DatePartition &&
        left.Content == right.Content &&
        left.ReblogsCount == right.ReblogsCount &&
        left.FavouritesCount == right.FavouritesCount &&
        left.RepliesCount == right.RepliesCount &&
        left.Url == right.Url &&
        left.IsRetruth == right.IsRetruth &&
        left.MediaUrls.SequenceEqual(right.MediaUrls);
}