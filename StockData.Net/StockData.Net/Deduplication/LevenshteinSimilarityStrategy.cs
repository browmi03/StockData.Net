using System.Text.RegularExpressions;
using StockData.Net.Configuration;

namespace StockData.Net.Deduplication;

/// <summary>
/// Levenshtein-based title similarity strategy.
/// </summary>
public class LevenshteinSimilarityStrategy : INewsDeduplicationStrategy
{
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ControlChars = new(@"[\u0000-\u001F\u007F]", RegexOptions.Compiled);

    public string StrategyName => "Levenshtein";

    public double CalculateSimilarity(
        NewsArticle article1,
        NewsArticle article2,
        NewsDeduplicationConfiguration config)
    {
        var title1 = SanitizeForComparison(article1.Title);
        var title2 = SanitizeForComparison(article2.Title);

        if (string.IsNullOrWhiteSpace(title1) || string.IsNullOrWhiteSpace(title2))
        {
            return 0.0;
        }

        if (string.Equals(title1, title2, StringComparison.Ordinal))
        {
            return 1.0;
        }

        var distance = LevenshteinDistance(title1, title2);
        var maxLength = Math.Max(title1.Length, title2.Length);
        if (maxLength == 0)
        {
            return 0.0;
        }

        var score = 1.0 - (double)distance / maxLength;
        return Math.Clamp(score, 0.0, 1.0);
    }

    private static string SanitizeForComparison(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim();
        if (trimmed.Length > 512)
        {
            trimmed = trimmed[..512];
        }

        trimmed = ControlChars.Replace(trimmed, string.Empty);

        var normalized = new string(trimmed
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        return MultiWhitespace.Replace(normalized, " ").Trim();
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (source.Length == 0)
        {
            return target.Length;
        }

        if (target.Length == 0)
        {
            return source.Length;
        }

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];

        for (var j = 0; j <= target.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var substitutionCost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }
}
