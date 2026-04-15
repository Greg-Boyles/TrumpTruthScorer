using Amazon.DynamoDBv2.DataModel;

namespace Shared;

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