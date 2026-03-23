using System.Text.RegularExpressions;

namespace StockData.Net.Security;

public static partial class SensitiveDataSanitizer
{
    private static readonly Regex TokenPattern = SensitiveTokenPattern();
    private static readonly Regex QuerySecretPattern = SensitiveQuerySecretPattern();
    private static readonly Regex StackTracePattern = StackTraceLikePattern();
    private static readonly Regex TripleDashPattern = TripleDashMarkerPattern();

    public static string Sanitize(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        var sanitized = QuerySecretPattern.Replace(message, "[REDACTED]");
        sanitized = StackTracePattern.Replace(sanitized, " ");
        sanitized = TripleDashPattern.Replace(sanitized, " ");

        // Redact likely secret fragments from all user-visible error paths.
        sanitized = TokenPattern.Replace(sanitized, "[REDACTED]");
        sanitized = Regex.Replace(sanitized, "\\s{2,}", " ").Trim();

        return sanitized;
    }

    [GeneratedRegex(@"\b(?=[A-Za-z0-9]{8,}\b)(?=[A-Za-z0-9]*[A-Za-z])(?=[A-Za-z0-9]*\d)[A-Za-z0-9]+\b", RegexOptions.Compiled)]
    private static partial Regex SensitiveTokenPattern();

    [GeneratedRegex(@"\b(?:apikey|token)\s*=\s*[^\s&]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveQuerySecretPattern();

    [GeneratedRegex(@"(?:\r?\n\s*at\s+[^\r\n]+)|(?:\bat\s+(?:System|StockData)\.[^\s]+)|(?:\bin\s+[^\s]+:\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StackTraceLikePattern();

    [GeneratedRegex(@"---", RegexOptions.Compiled)]
    private static partial Regex TripleDashMarkerPattern();
}