using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ScraperFunction;

public class Function
{
    private readonly IDynamoDBContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly string _postsTable;
    private readonly string _apiKey;

    public Function()
    {
        var client = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(client);
        _httpClient = new HttpClient();
        _postsTable = Environment.GetEnvironmentVariable("POSTS_TABLE") ?? "TruthScorer-Posts";
        _apiKey = Environment.GetEnvironmentVariable("SCRAPECREATORS_API_KEY") ?? "";
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
        var url = "https://api.scrapecreators.com/v1/truthsocial/user/posts?handle=realDonaldTrump";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _apiKey);
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ScrapeCreatorsResponse>(json);
        
        if (apiResponse?.Data == null)
        {
            return new List<Post>();
        }

        var posts = new List<Post>();
        
        foreach (var item in apiResponse.Data)
        {
            var createdAt = DateTime.Parse(item.CreatedAt);
            
            posts.Add(new Post
            {
                PostId = item.Id,
                CreatedAt = createdAt.ToString("O"),
                DatePartition = createdAt.ToString("yyyy-MM-dd"),
                Content = StripHtml(item.Content),
                ReblogsCount = item.ReblogsCount,
                FavouritesCount = item.FavouritesCount,
                RepliesCount = item.RepliesCount,
                Url = item.Url,
                MediaUrls = item.MediaAttachments?.Select(m => m.Url).ToList() ?? new List<string>(),
                IsRetruth = item.Reblog != null
            });
        }

        context.Logger.LogInformation($"Fetched {posts.Count} posts from API.");
        return posts;
    }

    private async Task<int> SavePosts(List<Post> posts, ILambdaContext context)
    {
        var savedCount = 0;
        
        foreach (var post in posts)
        {
            try
            {
                // Check if post already exists
                var existing = await _dbContext.LoadAsync<Post>(post.PostId, post.CreatedAt);
                
                if (existing == null)
                {
                    await _dbContext.SaveAsync(post);
                    savedCount++;
                    context.Logger.LogInformation($"Saved post {post.PostId}");
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning($"Error saving post {post.PostId}: {ex.Message}");
            }
        }

        return savedCount;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        
        // Simple HTML strip - remove tags
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
}

// ScrapeCreators API response models
public class ScrapeCreatorsResponse
{
    [JsonPropertyName("data")]
    public List<TruthPost>? Data { get; set; }
}

public class TruthPost
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("reblogs_count")]
    public int ReblogsCount { get; set; }

    [JsonPropertyName("favourites_count")]
    public int FavouritesCount { get; set; }

    [JsonPropertyName("replies_count")]
    public int RepliesCount { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("media_attachments")]
    public List<MediaAttachment>? MediaAttachments { get; set; }

    [JsonPropertyName("reblog")]
    public object? Reblog { get; set; }
}

public class MediaAttachment
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
