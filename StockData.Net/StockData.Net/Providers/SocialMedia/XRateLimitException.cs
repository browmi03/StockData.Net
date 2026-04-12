namespace StockData.Net.Providers.SocialMedia;

public sealed class XRateLimitException : Exception
{
    public DateTimeOffset? ResetAtUtc { get; }

    public XRateLimitException(string message, DateTimeOffset? resetAtUtc = null)
        : base(message)
    {
        ResetAtUtc = resetAtUtc;
    }
}
