namespace Shared;

public class BackfillRequest
{
    public string Mode { get; set; } = "analyze";
    public string Handle { get; set; } = "realDonaldTrump";
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int MaxPages { get; set; } = 20;
    public int MaxAnalysisPosts { get; set; } = 100;
    public string? ResumeCursor { get; set; }
}

public class BackfillResponse
{
    public string Mode { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int PagesFetched { get; set; }
    public int PostsFetched { get; set; }
    public int PostsInserted { get; set; }
    public int PostsUpdated { get; set; }
    public int PostsSkippedExisting { get; set; }
    public int PostsOutsideRange { get; set; }
    public int AnalysesCreated { get; set; }
    public int AnalysesSkippedExisting { get; set; }
    public int AnalysesSkippedEmpty { get; set; }
    public int SummariesGenerated { get; set; }
    public int RemainingAnalyzablePostsWithoutAnalysis { get; set; }
    public string? NextCursor { get; set; }
    public List<string> ProcessedDates { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
