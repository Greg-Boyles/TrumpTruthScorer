using Amazon.DynamoDBv2.DataModel;

namespace Shared;

[DynamoDBTable("TruthScorer-Posts")]
public class Post
{
    [DynamoDBHashKey("postId")]
    public string PostId { get; set; } = string.Empty;

    [DynamoDBRangeKey("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [DynamoDBProperty("datePartition")]
    public string DatePartition { get; set; } = string.Empty;

    [DynamoDBProperty("content")]
    public string Content { get; set; } = string.Empty;

    [DynamoDBProperty("reblogsCount")]
    public int ReblogsCount { get; set; }

    [DynamoDBProperty("favouritesCount")]
    public int FavouritesCount { get; set; }

    [DynamoDBProperty("repliesCount")]
    public int RepliesCount { get; set; }

    [DynamoDBProperty("url")]
    public string Url { get; set; } = string.Empty;

    [DynamoDBProperty("mediaUrls")]
    public List<string> MediaUrls { get; set; } = new();

    [DynamoDBProperty("isRetruth")]
    public bool IsRetruth { get; set; }
}

[DynamoDBTable("TruthScorer-Analysis")]
public class Analysis
{
    [DynamoDBHashKey("postId")]
    public string PostId { get; set; } = string.Empty;

    [DynamoDBProperty("mentalScore")]
    public int MentalScore { get; set; }

    [DynamoDBProperty("moralScore")]
    public int MoralScore { get; set; }

    [DynamoDBProperty("emotionalState")]
    public string EmotionalState { get; set; } = string.Empty;

    [DynamoDBProperty("keyThemes")]
    public List<string> KeyThemes { get; set; } = new();

    [DynamoDBProperty("summary")]
    public string Summary { get; set; } = string.Empty;

    [DynamoDBProperty("analyzedAt")]
    public string AnalyzedAt { get; set; } = string.Empty;
}

[DynamoDBTable("TruthScorer-DailySummaries")]
public class DailySummary
{
    [DynamoDBHashKey("date")]
    public string Date { get; set; } = string.Empty;

    [DynamoDBProperty("totalPosts")]
    public int TotalPosts { get; set; }

    [DynamoDBProperty("avgMentalScore")]
    public double AvgMentalScore { get; set; }

    [DynamoDBProperty("avgMoralScore")]
    public double AvgMoralScore { get; set; }

    [DynamoDBProperty("overallScore")]
    public double OverallScore { get; set; }

    [DynamoDBProperty("topThemes")]
    public List<string> TopThemes { get; set; } = new();

    [DynamoDBProperty("dailySummary")]
    public string SummaryText { get; set; } = string.Empty;

    [DynamoDBProperty("postingHours")]
    public List<int> PostingHours { get; set; } = new();

    [DynamoDBProperty("quietHoursStart")]
    public int? QuietHoursStart { get; set; }

    [DynamoDBProperty("quietHoursEnd")]
    public int? QuietHoursEnd { get; set; }

    [DynamoDBProperty("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

// API Response DTOs
public class PostWithAnalysis
{
    public Post Post { get; set; } = new();
    public Analysis? Analysis { get; set; }
}

public class TrendData
{
    public string Date { get; set; } = string.Empty;
    public double AvgMentalScore { get; set; }
    public double AvgMoralScore { get; set; }
    public int PostCount { get; set; }
}
