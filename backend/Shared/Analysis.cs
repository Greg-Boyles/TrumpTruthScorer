using Amazon.DynamoDBv2.DataModel;

namespace Shared;

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