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