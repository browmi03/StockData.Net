using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class TickerCountryInferrerTests
{
    [TestMethod]
    public void InferIsoCountryCode_NullInput_ReturnsNull()
        => Assert.IsNull(TickerCountryInferrer.InferIsoCountryCode(null));

    [TestMethod]
    public void InferIsoCountryCode_EmptyInput_ReturnsNull()
        => Assert.IsNull(TickerCountryInferrer.InferIsoCountryCode(""));

    [TestMethod]
    public void InferIsoCountryCode_WhitespaceInput_ReturnsNull()
        => Assert.IsNull(TickerCountryInferrer.InferIsoCountryCode("   "));

    [TestMethod]
    public void InferIsoCountryCode_LoneCaret_ReturnsNull()
        => Assert.IsNull(TickerCountryInferrer.InferIsoCountryCode("^"));

    [TestMethod]
    public void InferIsoCountryCode_PlainUsTicker_ReturnsUS()
        => Assert.AreEqual("US", TickerCountryInferrer.InferIsoCountryCode("AAPL"));

    [TestMethod]
    public void InferIsoCountryCode_IndexWithCaret_ReturnsUS()
        => Assert.AreEqual("US", TickerCountryInferrer.InferIsoCountryCode("^GSPC"));

    [TestMethod]
    public void InferIsoCountryCode_UnknownSuffix_ReturnsNull()
        => Assert.IsNull(TickerCountryInferrer.InferIsoCountryCode("AAPL.XX"));

    [TestMethod]
    [DataRow("RY.TO", "CA", DisplayName = "TSX main board")]
    [DataRow("ACB.V", "CA", DisplayName = "TSX Venture")]
    [DataRow("NFI.CN", "CA", DisplayName = "CNSX")]
    [DataRow("HSBA.L", "GB", DisplayName = "London Stock Exchange")]
    [DataRow("BATS.IL", "GB", DisplayName = "London IOB")]
    [DataRow("CBA.AX", "AU", DisplayName = "ASX")]
    [DataRow("AIR.NZ", "NZ", DisplayName = "NZX")]
    [DataRow("SAP.DE", "DE", DisplayName = "XETRA")]
    [DataRow("BMW.F", "DE", DisplayName = "Frankfurt")]
    [DataRow("AIR.PA", "FR", DisplayName = "Euronext Paris")]
    [DataRow("ASML.AS", "NL", DisplayName = "Euronext Amsterdam")]
    [DataRow("NESN.SW", "CH", DisplayName = "SIX Swiss Exchange")]
    [DataRow("VALE3.BR", "BR", DisplayName = "B3 BDR")]
    [DataRow("PETR4.SA", "BR", DisplayName = "B3 main")]
    [DataRow("0700.HK", "HK", DisplayName = "HKEX")]
    [DataRow("7203.T", "JP", DisplayName = "Tokyo Stock Exchange")]
    [DataRow("005930.KS", "KR", DisplayName = "Korea Stock Exchange")]
    [DataRow("263750.KQ", "KR", DisplayName = "KOSDAQ")]
    [DataRow("600519.SS", "CN", DisplayName = "Shanghai")]
    [DataRow("000858.SZ", "CN", DisplayName = "Shenzhen")]
    [DataRow("RELIANCE.BO", "IN", DisplayName = "BSE")]
    [DataRow("INFY.NS", "IN", DisplayName = "NSE")]
    public void InferIsoCountryCode_SuffixMapping_ReturnsExpectedCode(string ticker, string expectedCode)
        => Assert.AreEqual(expectedCode, TickerCountryInferrer.InferIsoCountryCode(ticker));

    [TestMethod]
    public void InferIsoCountryCode_CasingInsensitive_ReturnsCA()
        => Assert.AreEqual("CA", TickerCountryInferrer.InferIsoCountryCode("RY.to"));
}
