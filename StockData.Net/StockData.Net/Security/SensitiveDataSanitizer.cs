using System.Text.RegularExpressions;

namespace StockData.Net.Security;

public static partial class SensitiveDataSanitizer
{
    private static readonly Regex TokenPattern = SensitiveTokenPattern();

    public static string Sanitize(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        // Redact likely secret fragments from all user-visible error paths.
        return TokenPattern.Replace(message, "[REDACTED]");
    }

    [GeneratedRegex(@"\b(?=[A-Za-z0-9]{8,}\b)(?=[A-Za-z0-9]*[A-Za-z])(?=[A-Za-z0-9]*\d)[A-Za-z0-9]+\b", RegexOptions.Compiled)]
    private static partial Regex SensitiveTokenPattern();
}