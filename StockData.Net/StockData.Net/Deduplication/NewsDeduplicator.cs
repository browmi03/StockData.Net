using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using StockData.Net.Configuration;

namespace StockData.Net.Deduplication;

/// <summary>
/// Deduplicates aggregated provider news responses.
/// </summary>
public class NewsDeduplicator
{
    private static readonly Regex ControlChars = new(@"[\u0000-\u001F\u007F]", RegexOptions.Compiled);
    private static readonly Regex CollapseWhitespace = new(@"\s+", RegexOptions.Compiled);
    private const int DeduplicationTimeoutMilliseconds = 500;

    private readonly INewsDeduplicationStrategy _strategy;

    public NewsDeduplicator(INewsDeduplicationStrategy? strategy = null)
    {
        _strategy = strategy ?? new LevenshteinSimilarityStrategy();
    }

    public Task<string> DeduplicateAsync(
        Dictionary<string, string> providerResponses,
        NewsDeduplicationConfiguration config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerResponses);
        ArgumentNullException.ThrowIfNull(config);

        if (providerResponses.Count == 0)
        {
            return Task.FromResult(string.Empty);
        }

        var startTime = Stopwatch.GetTimestamp();

        var maxArticles = Math.Clamp(config.MaxArticlesForComparison, 1, 200);
        var threshold = Math.Clamp(config.SimilarityThreshold, 0.0, 1.0);

        var parsedArticles = ParseArticles(providerResponses, maxArticles);
        if (parsedArticles.Count <= 1)
        {
            return Task.FromResult(SerializeArticles(parsedArticles));
        }

        var deduplicated = new List<NewsArticle>();
        var consumed = new bool[parsedArticles.Count];

        for (var i = 0; i < parsedArticles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWithinTimeout(startTime);

            if (consumed[i])
            {
                continue;
            }

            var current = parsedArticles[i];
            var cluster = new List<NewsArticle> { current };
            consumed[i] = true;

            for (var j = i + 1; j < parsedArticles.Count; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureWithinTimeout(startTime);

                if (consumed[j])
                {
                    continue;
                }

                var candidate = parsedArticles[j];

                if (IsExactUrlMatch(current, candidate))
                {
                    consumed[j] = true;
                    cluster.Add(candidate);
                    continue;
                }

                var similarity = _strategy.CalculateSimilarity(current, candidate, config);
                if (similarity >= threshold)
                {
                    consumed[j] = true;
                    cluster.Add(candidate);
                }
            }

            deduplicated.Add(MergeCluster(cluster));
        }

        var ordered = deduplicated
            .OrderByDescending(a => a.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(SerializeArticles(ordered));
    }

    private static List<NewsArticle> ParseArticles(
        Dictionary<string, string> providerResponses,
        int maxArticles)
    {
        var articles = new List<NewsArticle>(Math.Min(maxArticles, providerResponses.Count * 10));

        foreach (var (providerId, rawResponse) in providerResponses)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                continue;
            }

            var blocks = rawResponse
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var block in blocks)
            {
                if (articles.Count >= maxArticles)
                {
                    return articles;
                }

                var article = ParseBlock(block, providerId);
                if (article == null)
                {
                    continue;
                }

                articles.Add(article);
            }
        }

        return articles;
    }

    private static NewsArticle? ParseBlock(string block, string providerId)
    {
        var lines = block
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var title = ExtractValue(lines, "Title:");
        var publisher = ExtractValue(lines, "Publisher:");
        var published = ExtractValue(lines, "Published:");
        var url = ExtractValue(lines, "URL:");
        var tickersRaw = ExtractValue(lines, "Related Tickers:");

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var relatedTickers = string.IsNullOrWhiteSpace(tickersRaw)
            ? new List<string>()
            : tickersRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(SanitizeField)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        DateTimeOffset? publishedAt = null;
        if (!string.IsNullOrWhiteSpace(published) &&
            !published.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
            DateTimeOffset.TryParse(published, out var parsedDate))
        {
            publishedAt = parsedDate;
        }

        var sanitizedProviderId = SanitizeField(providerId);
        var sanitizedUrl = SanitizeUrl(url);
        var sanitizedPublisher = SanitizeField(publisher);

        return new NewsArticle
        {
            Title = SanitizeField(title),
            Publisher = sanitizedPublisher,
            ProviderId = sanitizedProviderId,
            Url = sanitizedUrl,
            PublishedAt = publishedAt,
            RelatedTickers = relatedTickers,
            Sources = new List<ArticleSource>
            {
                new()
                {
                    ProviderId = sanitizedProviderId,
                    OriginalUrl = sanitizedUrl,
                    Publisher = sanitizedPublisher
                }
            }
        };
    }

    private static string SerializeArticles(List<NewsArticle> articles)
    {
        if (articles.Count == 0)
        {
            return string.Empty;
        }

        var blocks = new List<string>(articles.Count);

        foreach (var article in articles)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Title: {article.Title}");
            builder.AppendLine($"Publisher: {article.Publisher}");

            var published = article.PublishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
            builder.AppendLine($"Published: {published}");

            if (article.RelatedTickers.Count > 0)
            {
                builder.AppendLine($"Related Tickers: {string.Join(", ", article.RelatedTickers)}");
            }

            if (article.Sources.Count > 0)
            {
                var sourceLabels = article.Sources
                    .Select(s => string.IsNullOrWhiteSpace(s.Publisher) ? null : s.Publisher)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (sourceLabels.Count == 0)
                {
                    sourceLabels = Enumerable.Range(1, article.Sources.Count)
                        .Select(index => $"Source {index}")
                        .ToList();
                }

                builder.AppendLine($"Sources: {string.Join(", ", sourceLabels)}");
            }

            builder.AppendLine($"URL: {article.Url}");

            if (article.IsMerged)
            {
                builder.Append($"Merged Count: {article.MergedCount}");
            }

            blocks.Add(builder.ToString().TrimEnd());
        }

        return string.Join("\n\n", blocks);
    }

    private static NewsArticle MergeCluster(List<NewsArticle> cluster)
    {
        if (cluster.Count == 1)
        {
            var single = cluster[0];
            single.IsMerged = false;
            single.MergedCount = 0;
            return single;
        }

        var primary = cluster[0];
        var merged = new NewsArticle
        {
            Title = primary.Title,
            Url = primary.Url,
            Publisher = primary.Publisher,
            ProviderId = primary.ProviderId,
            PublishedAt = cluster
                .Where(a => a.PublishedAt.HasValue)
                .Select(a => a.PublishedAt)
                .Min(),
            RelatedTickers = cluster
                .SelectMany(a => a.RelatedTickers)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Sources = cluster
                .SelectMany(a => a.Sources)
                .Concat(cluster.Select(a => new ArticleSource
                {
                    ProviderId = a.ProviderId,
                    OriginalUrl = a.Url,
                    Publisher = a.Publisher
                }))
                .GroupBy(s => $"{s.ProviderId}|{s.OriginalUrl}|{s.Publisher}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(s => s.ProviderId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            IsMerged = true,
            MergedCount = cluster.Count - 1
        };

        return merged;
    }

    private static bool IsExactUrlMatch(NewsArticle first, NewsArticle second)
    {
        if (string.IsNullOrWhiteSpace(first.Url) || string.IsNullOrWhiteSpace(second.Url))
        {
            return false;
        }

        return string.Equals(first.Url, second.Url, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractValue(IEnumerable<string> lines, string prefix)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return string.Empty;
    }

    private static string SanitizeUrl(string? input)
    {
        var sanitized = SanitizeField(input);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(sanitized, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            return uri.ToString();
        }

        return string.Empty;
    }

    private static string SanitizeField(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sanitized = ControlChars.Replace(input, string.Empty);
        sanitized = sanitized.Replace("<", string.Empty).Replace(">", string.Empty);
        sanitized = CollapseWhitespace.Replace(sanitized, " ").Trim();

        if (sanitized.Length > 512)
        {
            sanitized = sanitized[..512];
        }

        return sanitized;
    }

    private static void EnsureWithinTimeout(long startTime)
    {
        if (Stopwatch.GetElapsedTime(startTime).TotalMilliseconds > DeduplicationTimeoutMilliseconds)
        {
            throw new TimeoutException($"News deduplication exceeded {DeduplicationTimeoutMilliseconds}ms limit");
        }
    }
}
