using Amazon.BedrockRuntime;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Shared;
using System.Globalization;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BackfillFunction;

public class Function
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IDynamoDBContext _dbContext;
    private readonly IAmazonSimpleSystemsManagement _ssmClient;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ScrapeCreatorsClient _scrapeCreatorsClient;
    private readonly PostAnalysisService _postAnalysisService;
    private readonly string _postsTable;
    private readonly string _apiKeyParam;
    private string? _apiKeyCache;

    public Function()
    {
        _dynamoClient = new AmazonDynamoDBClient();
        _dbContext = new DynamoDBContext(_dynamoClient);
        _ssmClient = new AmazonSimpleSystemsManagementClient();
        _bedrockClient = new AmazonBedrockRuntimeClient();
        _scrapeCreatorsClient = new ScrapeCreatorsClient(new HttpClient());
        _postAnalysisService = new PostAnalysisService();
        _postsTable = Environment.GetEnvironmentVariable("POSTS_TABLE") ?? "TruthScorer-Posts";
        _apiKeyParam = Environment.GetEnvironmentVariable("SCRAPECREATORS_API_KEY_PARAM") ?? "/truthscorer/scrapecreators-api-key";
    }

    public async Task<BackfillResponse> FunctionHandler(BackfillRequest? input, ILambdaContext context)
    {
        var request = NormalizeRequest(input);
        var response = new BackfillResponse
        {
            Mode = request.Mode,
            Handle = request.Handle,
            StartDate = request.StartDate!,
            EndDate = request.EndDate!,
            NextCursor = request.ResumeCursor,
            ProcessedDates = EnumerateDates(request.StartDate!, request.EndDate!)
        };

        context.Logger.LogInformation($"Starting backfill workflow. Mode={request.Mode}, Handle={request.Handle}, StartDate={request.StartDate}, EndDate={request.EndDate}, MaxPages={request.MaxPages}, MaxAnalysisPosts={request.MaxAnalysisPosts}, ResumeCursor={request.ResumeCursor ?? "<none>"}");

        if (request.Mode is "full" or "ingest")
        {
            await RunIngestPhaseAsync(request, response, context);
        }

        if (request.Mode is "full" or "analyze")
        {
            await RunAnalysisPhaseAsync(request, response, context);
        }

        if (request.Mode is "full" or "summary")
        {
            response.RemainingAnalyzablePostsWithoutAnalysis = await CountRemainingAnalyzablePostsWithoutAnalysisAsync(request.StartDate!, request.EndDate!);
            if (request.Mode == "full" && response.RemainingAnalyzablePostsWithoutAnalysis > 0)
            {
                response.Errors.Add($"Skipped summary regeneration because {response.RemainingAnalyzablePostsWithoutAnalysis} analyzable posts in the requested range still do not have analysis.");
            }
            else
            {
                await RunSummaryPhaseAsync(request, response, context);
            }
        }

        return response;
    }

    private static BackfillRequest NormalizeRequest(BackfillRequest? input)
    {
        var today = DateTime.UtcNow.Date;
        var request = input ?? new BackfillRequest();
        request.Mode = string.IsNullOrWhiteSpace(request.Mode) ? "full" : request.Mode.Trim().ToLowerInvariant();
        request.Handle = string.IsNullOrWhiteSpace(request.Handle) ? "realDonaldTrump" : request.Handle.Trim();
        request.EndDate = NormalizeDateString(request.EndDate) ?? today.ToString("yyyy-MM-dd");
        request.StartDate = NormalizeDateString(request.StartDate) ?? today.AddDays(-1).ToString("yyyy-MM-dd");
        request.MaxPages = request.MaxPages <= 0 ? 20 : request.MaxPages;
        request.MaxAnalysisPosts = request.MaxAnalysisPosts <= 0 ? 100 : request.MaxAnalysisPosts;

        var startDate = DateOnly.ParseExact(request.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endDate = DateOnly.ParseExact(request.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (startDate > endDate)
        {
            throw new ArgumentException("startDate must be less than or equal to endDate.");
        }

        return request;
    }

    private static string? NormalizeDateString(string? date) =>
        string.IsNullOrWhiteSpace(date)
            ? null
            : DateOnly.ParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<string> EnumerateDates(string startDate, string endDate)
    {
        var dates = new List<string>();
        var current = DateOnly.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        while (current <= end)
        {
            dates.Add(current.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            current = current.AddDays(1);
        }

        return dates;
    }

    private async Task RunIngestPhaseAsync(BackfillRequest request, BackfillResponse response, ILambdaContext context)
    {
        var apiKey = await GetApiKeyAsync();
        var cursor = request.ResumeCursor;
        var startDate = DateOnly.ParseExact(request.StartDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endDate = DateOnly.ParseExact(request.EndDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        for (var pageIndex = 0; pageIndex < request.MaxPages; pageIndex++)
        {
            var page = await _scrapeCreatorsClient.FetchPostsPageAsync(apiKey, request.Handle, cursor);
            response.PagesFetched++;
            response.PostsFetched += page.Posts.Count;
            response.NextCursor = page.NextMaxId;

            if (page.Posts.Count == 0)
            {
                break;
            }

            var reachedLowerBound = false;
            foreach (var post in page.Posts)
            {
                var postDate = DateOnly.ParseExact(post.DatePartition, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (postDate > endDate)
                {
                    response.PostsOutsideRange++;
                    continue;
                }

                if (postDate < startDate)
                {
                    response.PostsOutsideRange++;
                    reachedLowerBound = true;
                    continue;
                }

                switch (await UpsertPostAsync(post))
                {
                    case PostSaveOutcome.Inserted:
                        response.PostsInserted++;
                        break;
                    case PostSaveOutcome.Updated:
                        response.PostsUpdated++;
                        break;
                    default:
                        response.PostsSkippedExisting++;
                        break;
                }
            }

            if (reachedLowerBound || string.IsNullOrWhiteSpace(page.NextMaxId))
            {
                break;
            }

            cursor = page.NextMaxId;
        }

        context.Logger.LogInformation($"Ingest phase complete. PagesFetched={response.PagesFetched}, PostsFetched={response.PostsFetched}, Inserted={response.PostsInserted}, Updated={response.PostsUpdated}, Existing={response.PostsSkippedExisting}, OutsideRange={response.PostsOutsideRange}, NextCursor={response.NextCursor ?? "<none>"}");
    }

    private async Task RunAnalysisPhaseAsync(BackfillRequest request, BackfillResponse response, ILambdaContext context)
    {
        var analyzedCount = 0;

        foreach (var date in EnumerateDates(request.StartDate!, request.EndDate!))
        {
            var posts = await GetPostsForDateAsync(date);
            foreach (var post in posts.OrderByDescending(p => p.CreatedAt))
            {
                var existingAnalysis = await _dbContext.LoadAsync<Analysis>(post.PostId);
                if (existingAnalysis != null)
                {
                    response.AnalysesSkippedExisting++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(post.Content))
                {
                    response.AnalysesSkippedEmpty++;
                    continue;
                }

                try
                {
                    var analysis = await _postAnalysisService.AnalyzePostAsync(post, _bedrockClient, message => context.Logger.LogInformation(message));
                    if (analysis == null)
                    {
                        response.AnalysesSkippedEmpty++;
                        continue;
                    }

                    await _dbContext.SaveAsync(analysis);
                    response.AnalysesCreated++;
                    analyzedCount++;

                    if (analyzedCount >= request.MaxAnalysisPosts)
                    {
                        context.Logger.LogInformation($"Analysis phase reached MaxAnalysisPosts={request.MaxAnalysisPosts}.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    response.Errors.Add($"analysis:{post.PostId}:{ex.Message}");
                    context.Logger.LogError($"Error analyzing post {post.PostId}: {ex.Message}");
                }
            }
        }
    }

    private async Task RunSummaryPhaseAsync(BackfillRequest request, BackfillResponse response, ILambdaContext context)
    {
        foreach (var date in EnumerateDates(request.StartDate!, request.EndDate!))
        {
            var posts = await GetPostsForDateAsync(date);
            if (posts.Count == 0)
            {
                continue;
            }

            var analyses = await GetAnalysesForPostsAsync(posts.Select(p => p.PostId).ToList());
            var summary = DailySummaryService.CalculateDailySummary(date, posts, analyses);
            await _dbContext.SaveAsync(summary);
            response.SummariesGenerated++;
            context.Logger.LogInformation($"Regenerated summary for {date} with {posts.Count} posts and {analyses.Count} analyses.");
        }
    }

    private async Task<List<Post>> GetPostsForDateAsync(string date)
    {
        var table = Table.LoadTable(_dynamoClient, _postsTable);
        var search = table.Query(new QueryOperationConfig
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
        });

        var posts = new List<Post>();
        do
        {
            var documents = await search.GetNextSetAsync();
            foreach (var doc in documents)
            {
                posts.Add(MapPostDocument(doc));
            }
        } while (!search.IsDone);

        return posts;
    }

    private async Task<List<Analysis>> GetAnalysesForPostsAsync(List<string> postIds)
    {
        var analyses = new List<Analysis>();
        foreach (var postId in postIds)
        {
            var analysis = await _dbContext.LoadAsync<Analysis>(postId);
            if (analysis != null)
            {
                analyses.Add(analysis);
            }
        }

        return analyses;
    }

    private async Task<int> CountRemainingAnalyzablePostsWithoutAnalysisAsync(string startDate, string endDate)
    {
        var remaining = 0;
        foreach (var date in EnumerateDates(startDate, endDate))
        {
            var posts = await GetPostsForDateAsync(date);
            foreach (var post in posts)
            {
                if (string.IsNullOrWhiteSpace(post.Content))
                {
                    continue;
                }

                var analysis = await _dbContext.LoadAsync<Analysis>(post.PostId);
                if (analysis == null)
                {
                    remaining++;
                }
            }
        }

        return remaining;
    }

    private async Task<PostSaveOutcome> UpsertPostAsync(Post incoming)
    {
        var existing = await _dbContext.LoadAsync<Post>(incoming.PostId, incoming.CreatedAt);
        if (existing == null)
        {
            await _dbContext.SaveAsync(incoming);
            return PostSaveOutcome.Inserted;
        }

        var merged = new Post
        {
            PostId = existing.PostId,
            CreatedAt = existing.CreatedAt,
            DatePartition = string.IsNullOrWhiteSpace(existing.DatePartition) ? incoming.DatePartition : existing.DatePartition,
            Content = string.IsNullOrWhiteSpace(existing.Content) ? incoming.Content : existing.Content,
            ReblogsCount = Math.Max(existing.ReblogsCount, incoming.ReblogsCount),
            FavouritesCount = Math.Max(existing.FavouritesCount, incoming.FavouritesCount),
            RepliesCount = Math.Max(existing.RepliesCount, incoming.RepliesCount),
            Url = string.IsNullOrWhiteSpace(existing.Url) ? incoming.Url : existing.Url,
            MediaUrls = existing.MediaUrls.Count > 0 ? existing.MediaUrls : incoming.MediaUrls,
            IsRetruth = existing.IsRetruth || incoming.IsRetruth
        };

        if (PostsEquivalent(existing, merged))
        {
            return PostSaveOutcome.Unchanged;
        }

        await _dbContext.SaveAsync(merged);
        return PostSaveOutcome.Updated;
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

    private static Post MapPostDocument(Document doc)
    {
        return new Post
        {
            PostId = doc.TryGetValue("postId", out var postId) ? postId.AsString() : string.Empty,
            CreatedAt = doc.TryGetValue("createdAt", out var createdAt) ? createdAt.AsString() : string.Empty,
            DatePartition = doc.TryGetValue("datePartition", out var datePartition) ? datePartition.AsString() : string.Empty,
            Content = doc.TryGetValue("content", out var content) ? content.AsString() : string.Empty,
            ReblogsCount = doc.TryGetValue("reblogsCount", out var reblogsCount) ? reblogsCount.AsInt() : 0,
            FavouritesCount = doc.TryGetValue("favouritesCount", out var favouritesCount) ? favouritesCount.AsInt() : 0,
            RepliesCount = doc.TryGetValue("repliesCount", out var repliesCount) ? repliesCount.AsInt() : 0,
            Url = doc.TryGetValue("url", out var url) ? url.AsString() : string.Empty,
            MediaUrls = new List<string>(),
            IsRetruth = doc.TryGetValue("isRetruth", out var isRetruth) && isRetruth.AsBoolean()
        };
    }

    private async Task<string> GetApiKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(_apiKeyCache))
        {
            return _apiKeyCache;
        }

        var response = await _ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = _apiKeyParam,
            WithDecryption = true
        });

        _apiKeyCache = response.Parameter.Value;
        return _apiKeyCache;
    }

    private enum PostSaveOutcome
    {
        Inserted,
        Updated,
        Unchanged
    }
}
