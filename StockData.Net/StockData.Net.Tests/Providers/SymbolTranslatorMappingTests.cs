using System.Text.RegularExpressions;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class SymbolTranslatorMappingTests
{
    private static readonly string[] UsIndices =
    {
        "VIX", "GSPC", "DJI", "IXIC", "RUT", "NDX", "NYA", "OEX", "MID"
    };

    private static readonly string[] InternationalIndices =
    {
        "FTSE", "GDAXI", "N225", "HSI", "SSEC", "AXJO", "KS11", "BSESN"
    };

    private static readonly string[] SectorIndices =
    {
        "SOX", "XOI", "HUI", "XAU"
    };

    private static readonly string[] VolatilityIndices =
    {
        "VIX", "VXN", "RVX"
    };

    private static readonly string[] BondIndices =
    {
        "TNX", "TYX", "FVX", "IRX"
    };

    private static readonly string[] AllUniqueIndices =
    {
        "VIX", "GSPC", "DJI", "IXIC", "RUT", "NDX", "NYA", "OEX", "MID",
        "FTSE", "GDAXI", "N225", "HSI", "SSEC", "AXJO", "KS11", "BSESN",
        "SOX", "XOI", "HUI", "XAU", "VXN", "RVX", "TNX", "TYX", "FVX", "IRX"
    };

    [TestMethod]
    public void Mappings_ContainAllMajorUSIndices()
    {
        var translator = new SymbolTranslator();

        foreach (var symbol in UsIndices)
        {
            var translated = translator.Translate(symbol, "yahoo_finance");
            Assert.AreEqual($"^{symbol}", translated);
        }
    }

    [TestMethod]
    public void Mappings_ContainAllInternationalIndices()
    {
        var translator = new SymbolTranslator();

        foreach (var symbol in InternationalIndices)
        {
            var translated = translator.Translate(symbol, "yahoo_finance");
            Assert.AreEqual($"^{symbol}", translated);
        }
    }

    [TestMethod]
    public void Mappings_ContainAllSectorIndices()
    {
        var translator = new SymbolTranslator();

        foreach (var symbol in SectorIndices)
        {
            var translated = translator.Translate(symbol, "yahoo_finance");
            Assert.AreEqual($"^{symbol}", translated);
        }
    }

    [TestMethod]
    public void Mappings_ContainAllVolatilityAndBondIndices()
    {
        var translator = new SymbolTranslator();

        foreach (var symbol in VolatilityIndices.Concat(BondIndices))
        {
            var translated = translator.Translate(symbol, "yahoo_finance");
            Assert.AreEqual($"^{symbol}", translated);
        }
    }

    [TestMethod]
    public void Mappings_AllUniqueIndices_AreTranslatable()
    {
        var translator = new SymbolTranslator();

        Assert.HasCount(27, AllUniqueIndices);

        foreach (var symbol in AllUniqueIndices)
        {
            var yahoo = translator.Translate(symbol, "yahoo_finance");
            var finviz = translator.Translate(symbol, "finviz");

            Assert.IsTrue(yahoo.StartsWith("^", StringComparison.Ordinal));
            Assert.IsTrue(finviz.StartsWith("@", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Mappings_ReverseLookupConsistency_ForYahooAndFinviz()
    {
        var translator = new SymbolTranslator();

        foreach (var symbol in AllUniqueIndices)
        {
            var yahoo = translator.Translate(symbol, "yahoo_finance");
            var finviz = translator.Translate(symbol, "finviz");

            var fromYahooToFinviz = translator.Translate(yahoo, "finviz");
            var fromFinvizToYahoo = translator.Translate(finviz, "yahoo_finance");

            Assert.AreEqual(finviz, fromYahooToFinviz);
            Assert.AreEqual(yahoo, fromFinvizToYahoo);
        }
    }

    [TestMethod]
    public void Mappings_NoDuplicateYahooFormats()
    {
        var translator = new SymbolTranslator();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in AllUniqueIndices)
        {
            var yahoo = translator.Translate(symbol, "yahoo_finance");
            Assert.IsTrue(seen.Add(yahoo), $"Duplicate Yahoo mapping detected for {yahoo}");
        }
    }

    [TestMethod]
    public void Mappings_AllYahooValues_UseAllowedPatternAndLength()
    {
        var translator = new SymbolTranslator();
        var pattern = new Regex("^\\^[A-Z0-9.-]+$", RegexOptions.CultureInvariant);

        foreach (var symbol in AllUniqueIndices)
        {
            var yahoo = translator.Translate(symbol, "yahoo_finance");
            Assert.IsTrue(pattern.IsMatch(yahoo), $"Unexpected Yahoo format: {yahoo}");
            Assert.IsLessThanOrEqualTo(10, yahoo.Length, $"Yahoo symbol exceeds max length 10: {yahoo}");
        }
    }
}