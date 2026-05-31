using System.Security.Cryptography;
using System.Text;

namespace WebScraper.Api.Auth;

/// <summary>
/// SHA-256 hashing + constant-time comparison helpers for API keys. Pulled into a
/// shared class so the auth handler and the management service agree on the encoding
/// (lowercase hex, no separators) — a mismatch here means valid keys silently fail to auth.
/// </summary>
public static class ApiKeyHasher
{
    public static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Constant-time equality check on two hex digests. Returns false if either side is
    /// null/empty so we don't accidentally match an unconfigured key.
    /// </summary>
    public static bool ConstantTimeEquals(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        var ab = Encoding.ASCII.GetBytes(a.ToLowerInvariant());
        var bb = Encoding.ASCII.GetBytes(b.ToLowerInvariant());
        if (ab.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }

    /// <summary>
    /// Generates a URL-safe random key string (base64url, no padding). 32 bytes of entropy.
    /// Prefix it caller-side (e.g. "sk_live_" + Generate()) to make the type recognisable.
    /// </summary>
    public static string GenerateRandomKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
