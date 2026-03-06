using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class SymbolTranslatorTests
{
    private SymbolTranslator _translator = null!;

    [TestInitialize]
    public void Setup()
    {
        _translator = new SymbolTranslator();
    }

    [TestMethod]
    [DataRow("VIX", "^VIX")]
    [DataRow("GSPC", "^GSPC")]
    [DataRow("DJI", "^DJI")]
    [DataRow("IXIC", "^IXIC")]
    [DataRow("RUT", "^RUT")]
    [DataRow("NDX", "^NDX")]
    [DataRow("NYA", "^NYA")]
    [DataRow("OEX", "^OEX")]
    [DataRow("MID", "^MID")]
    [DataRow("FTSE", "^FTSE")]
    [DataRow("GDAXI", "^GDAXI")]
    [DataRow("N225", "^N225")]
    [DataRow("HSI", "^HSI")]
    [DataRow("SSEC", "^SSEC")]
    [DataRow("AXJO", "^AXJO")]
    [DataRow("KS11", "^KS11")]
    [DataRow("BSESN", "^BSESN")]
    [DataRow("SOX", "^SOX")]
    [DataRow("XOI", "^XOI")]
    [DataRow("HUI", "^HUI")]
    [DataRow("XAU", "^XAU")]
    [DataRow("VXN", "^VXN")]
    [DataRow("RVX", "^RVX")]
    [DataRow("TNX", "^TNX")]
    [DataRow("TYX", "^TYX")]
    [DataRow("FVX", "^FVX")]
    [DataRow("IRX", "^IRX")]
    public void Translate_CanonicalSymbol_ToYahooFormat_ReturnsExpected(string input, string expected)
    {
        var translated = _translator.Translate(input, "yahoo_finance");

        Assert.AreEqual(expected, translated);
    }

    [TestMethod]
    [DataRow("^VIX")]
    [DataRow("^GSPC")]
    [DataRow("^DJI")]
    public void Translate_YahooFormat_ToYahoo_PassesThrough(string input)
    {
        var translated = _translator.Translate(input, "yahoo_finance");

        Assert.AreEqual(input, translated);
    }

    [TestMethod]
    public void Translate_YahooFormat_ToYahoo_DoesNotDoubleTranslate()
    {
        var translated = _translator.Translate("^VIX", "yahoo_finance");

        Assert.AreEqual("^VIX", translated);
    }

    [TestMethod]
    [DataRow("@VX", "^VIX")]
    [DataRow("@SPX", "^GSPC")]
    [DataRow("@DJI", "^DJI")]
    public void Translate_FinvizFormat_ToYahoo_ReturnsExpected(string input, string expected)
    {
        var translated = _translator.Translate(input, "yahoo_finance");

        Assert.AreEqual(expected, translated);
    }

    [TestMethod]
    [DataRow("vix")]
    [DataRow("ViX")]
    [DataRow("^vix")]
    public void Translate_MixedCaseInputs_ToYahoo_ReturnsNormalizedYahooSymbol(string input)
    {
        var translated = _translator.Translate(input, "yahoo_finance");

        Assert.AreEqual("^VIX", translated);
    }

    [TestMethod]
    public void Translate_UnmappedSymbol_PassesThrough()
    {
        var translated = _translator.Translate("AAPL", "yahoo_finance");

        Assert.AreEqual("AAPL", translated);
    }

    [TestMethod]
    public void Translate_KnownSymbol_UnknownProvider_PassesThrough()
    {
        var translated = _translator.Translate("VIX", "unknown_provider");

        Assert.AreEqual("VIX", translated);
    }

    [TestMethod]
    [DataRow("VIX", "@VX")]
    [DataRow("^VIX", "@VX")]
    [DataRow("^GSPC", "@SPX")]
    public void Translate_ToFinviz_FromCanonicalOrYahoo_ReturnsFinvizFormat(string input, string expected)
    {
        var translated = _translator.Translate(input, "finviz");

        Assert.AreEqual(expected, translated);
    }

    [TestMethod]
    public void Translate_NullSymbol_ThrowsArgumentException()
    {
        try
        {
            _translator.Translate(null!, "yahoo_finance");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Translate_EmptyOrWhitespaceSymbol_ThrowsArgumentException(string input)
    {
        try
        {
            _translator.Translate(input, "yahoo_finance");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Translate_NullProviderId_ThrowsArgumentException()
    {
        try
        {
            _translator.Translate("VIX", null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void Translate_EmptyOrWhitespaceProviderId_ThrowsArgumentException(string providerId)
    {
        try
        {
            _translator.Translate("VIX", providerId);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }
}