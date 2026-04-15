namespace Shared;

public static class DailySummaryService
{
    public static DailySummary CalculateDailySummary(string date, List<Post> posts, List<Analysis> analyses)
    {
        var avgMental = analyses.Count > 0 ? analyses.Average(a => a.MentalScore) : 0;
        var avgMoral = analyses.Count > 0 ? analyses.Average(a => a.MoralScore) : 0;
        var overallScore = (avgMental + avgMoral) / 2;
        var postingHours = posts
            .Select(p => DateTime.Parse(p.CreatedAt).Hour)
            .Distinct()
            .OrderBy(h => h)
            .ToList();
        var (quietStart, quietEnd) = CalculateQuietHours(postingHours);
        var topThemes = analyses
            .SelectMany(a => a.KeyThemes ?? new List<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => t.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return new DailySummary
        {
            Date = date,
            TotalPosts = posts.Count,
            AvgMentalScore = Math.Round(avgMental, 1),
            AvgMoralScore = Math.Round(avgMoral, 1),
            OverallScore = Math.Round(overallScore, 1),
            TopThemes = topThemes,
            PostingHours = postingHours,
            QuietHoursStart = quietStart,
            QuietHoursEnd = quietEnd,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
    }

    public static (int? start, int? end) CalculateQuietHours(List<int> postingHours)
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
}
