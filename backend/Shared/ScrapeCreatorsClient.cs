using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

public class ScrapeCreatorsClient
{
    private readonly HttpClient _httpClient;

    public ScrapeCreatorsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ScrapeCreatorsPage> FetchPostsPageAsync(string apiKey, string handle, string? nextMaxId = null)
    {
        var url = $"https://api.scrapecreators.com/v1/truthsocial/user/posts?handle={Uri.EscapeDataString(handle)}";
        if (!string.IsNullOrWhiteSpace(nextMaxId))
        {
            url += $"&next_max_id={Uri.EscapeDataString(nextMaxId)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKey);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ScrapeCreatorsResponse>(json);
        var sourcePosts = apiResponse?.Posts ?? apiResponse?.Data ?? new List<TruthPost>();

        return new ScrapeCreatorsPage
        {
            Posts = sourcePosts.Select(MapPost).ToList(),
            NextMaxId = apiResponse?.NextMaxId
        };
    }

    private static Post MapPost(TruthPost item)
    {
        var createdAt = DateTimeOffset.Parse(item.CreatedAt).UtcDateTime;
        var content = StripHtml(item.Content);
        if (string.IsNullOrWhiteSpace(content))
        {
            content = item.Text?.Trim() ?? string.Empty;
        }

        return new Post
        {
            PostId = item.Id,
            CreatedAt = createdAt.ToString("O"),
            DatePartition = createdAt.ToString("yyyy-MM-dd"),
            Content = content,
            ReblogsCount = item.ReblogsCount,
            FavouritesCount = item.FavouritesCount,
            RepliesCount = item.RepliesCount,
            Url = item.Url,
            MediaUrls = item.MediaAttachments?.Select(m => m.Url).Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>(),
            IsRetruth = item.Reblog != null
        };
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
}

public class ScrapeCreatorsPage
{
    public List<Post> Posts { get; set; } = new();
    public string? NextMaxId { get; set; }
}

public class ScrapeCreatorsResponse
{
    [JsonPropertyName("posts")]
    public List<TruthPost>? Posts { get; set; }

    [JsonPropertyName("data")]
    public List<TruthPost>? Data { get; set; }

    [JsonPropertyName("next_max_id")]
    public string? NextMaxId { get; set; }
}

public class TruthPost
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

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
