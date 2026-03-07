using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace StockData.Net.Security;

/// <summary>
/// Wraps sensitive values to reduce accidental disclosure in logs, debugger views, and serialization.
/// </summary>
[DebuggerDisplay("[REDACTED]")]
public sealed class SecretValue : IDisposable, IEquatable<SecretValue>
{
    private char[] _secret;
    private bool _disposed;

    public SecretValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Secret value cannot be empty or whitespace.", nameof(value));
        }

        _secret = value.ToCharArray();
    }

    [JsonIgnore]
    public bool HasValue => !_disposed && _secret.Length > 0;

    public string ExposeSecret()
    {
        ThrowIfDisposed();
        return new string(_secret);
    }

    public override string ToString() => "[REDACTED]";

    public bool Equals(SecretValue? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        ThrowIfDisposed();
        other.ThrowIfDisposed();

        var left = Encoding.UTF8.GetBytes(_secret);
        var right = Encoding.UTF8.GetBytes(other._secret);

        try
        {
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(left);
            CryptographicOperations.ZeroMemory(right);
        }
    }

    public override bool Equals(object? obj) => obj is SecretValue other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_secret.Length, _disposed);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Array.Clear(_secret, 0, _secret.Length);
        _secret = [];
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}