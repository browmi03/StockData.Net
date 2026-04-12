using System;
using System.Collections.Generic;

namespace StockData.Net.Providers;

internal static class TickerCountryInferrer
{
    private static readonly Dictionary<string, string> SuffixToIsoCode = new(StringComparer.OrdinalIgnoreCase)
    {
        [".TO"] = "CA",
        [".V"] = "CA",
        [".CN"] = "CA",
        [".L"] = "GB",
        [".IL"] = "GB",
        [".AX"] = "AU",
        [".NZ"] = "NZ",
        [".DE"] = "DE",
        [".F"] = "DE",
        [".PA"] = "FR",
        [".AS"] = "NL",
        [".SW"] = "CH",
        [".BR"] = "BR",
        [".SA"] = "BR",
        [".HK"] = "HK",
        [".T"] = "JP",
        [".KS"] = "KR",
        [".KQ"] = "KR",
        [".SS"] = "CN",
        [".SZ"] = "CN",
        [".BO"] = "IN",
        [".NS"] = "IN",
    };

    public static string? InferIsoCountryCode(string? ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return null;
        }

        var symbol = ticker.StartsWith("^", StringComparison.Ordinal) ? ticker[1..] : ticker;

        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var dotIndex = symbol.LastIndexOf('.');
        if (dotIndex < 0)
        {
            return "US";
        }

        var suffix = symbol[dotIndex..];
        return SuffixToIsoCode.TryGetValue(suffix, out var isoCode) ? isoCode : null;
    }
}
