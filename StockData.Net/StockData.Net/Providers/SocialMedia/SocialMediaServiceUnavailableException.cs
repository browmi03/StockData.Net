namespace StockData.Net.Providers.SocialMedia;

public sealed class SocialMediaServiceUnavailableException : Exception
{
    public SocialMediaServiceUnavailableException(string message)
        : base(message)
    {
    }
}
