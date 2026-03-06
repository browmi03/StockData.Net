using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class SymbolTranslatorSecurityTests
{
    private SymbolTranslator _translator = null!;

    [TestInitialize]
    public void Setup()
    {
        _translator = new SymbolTranslator();
    }

    [TestMethod]
    [DataRow("'; DROP TABLE--")]
    [DataRow("1 OR 1=1")]
    [DataRow("../../../etc/passwd")]
    [DataRow("..\\..\\windows")]
    [DataRow("VIX\r\nX-Injected: true")]
    [DataRow("<script>alert(1)</script>")]
    [DataRow("<img onerror=alert(1)>")]
    [DataRow("VIX%0d%0aHeader:bad")]
    [DataRow("AAPL;rm -rf /")]
    [DataRow("\u0000VIX")]
    public void Translate_InjectionLikeInput_PassesThroughUnchanged(string input)
    {
        var translated = _translator.Translate(input, "yahoo_finance");

        Assert.AreEqual(input, translated);
    }

    [TestMethod]
    [DataRow("VIX", "yahoo_finance", "^VIX")]
    [DataRow("vix", "YAHOO_FINANCE", "^VIX")]
    [DataRow("^vix", "yahoo_finance", "^VIX")]
    [DataRow("@vx", "yahoo_finance", "^VIX")]
    [DataRow("^VIX", "finviz", "@VX")]
    [DataRow("@vx", "FINVIZ", "@VX")]
    public void Translate_CaseNormalization_ReturnsStableOutput(string input, string providerId, string expected)
    {
        var translated = _translator.Translate(input, providerId);

        Assert.AreEqual(expected, translated);
    }

    [TestMethod]
    public void Translate_CasePermutationFlood_ResolvesConsistently()
    {
        var random = new Random(42);

        for (var i = 0; i < 1000; i++)
        {
            var chars = "vix".ToCharArray();
            for (var c = 0; c < chars.Length; c++)
            {
                chars[c] = random.Next(0, 2) == 0 ? char.ToLowerInvariant(chars[c]) : char.ToUpperInvariant(chars[c]);
            }

            var input = new string(chars);
            var translated = _translator.Translate(input, "yahoo_finance");
            Assert.AreEqual("^VIX", translated);
        }
    }

    [TestMethod]
    public void Translate_LargeUnknownSymbolVolume_DoesNotThrow()
    {
        for (var i = 0; i < 10000; i++)
        {
            var symbol = $"UNKNOWN_{i}";
            var translated = _translator.Translate(symbol, "yahoo_finance");
            Assert.AreEqual(symbol, translated);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Translate_InvalidSymbol_ThrowsArgumentException(string? symbol)
    {
        try
        {
            _translator.Translate(symbol!, "yahoo_finance");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void Translate_InvalidProvider_ThrowsArgumentException(string? providerId)
    {
        try
        {
            _translator.Translate("VIX", providerId!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }
}