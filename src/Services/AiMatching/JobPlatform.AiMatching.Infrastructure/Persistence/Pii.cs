using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

/// <summary>Application-level encryption of parsed resume content (handover section 8: PII encrypted).</summary>
public interface IPiiProtector
{
    string Protect(string plain);

    string Unprotect(string stored);
}

public sealed class NullPiiProtector : IPiiProtector
{
    public static readonly NullPiiProtector Instance = new();

    public string Protect(string plain) => plain;

    public string Unprotect(string stored) => stored;
}

/// <summary>AES-256-GCM, random nonce per value, stored as "v1:" + base64(nonce | tag | ciphertext). Values without the prefix are read as legacy plain text.</summary>
public sealed class AesGcmPiiProtector : IPiiProtector
{
    private const string Prefix = "v1:";
    private readonly byte[] _key;

    public AesGcmPiiProtector(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("The data protection key must be 32 bytes (base64 of 32 random bytes).", nameof(key));
        }

        _key = key;
    }

    public string Protect(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return Prefix + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string Unprotect(string stored)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return stored;
        }

        var all = Convert.FromBase64String(stored[Prefix.Length..]);
        var plain = new byte[all.Length - 28];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }
}

/// <summary>JSON column helpers: read-only lists stored as one JSON text column, with a comparer so change tracking sees content changes.</summary>
public static class JsonColumns
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static string Write<T>(IReadOnlyList<T> value) => JsonSerializer.Serialize(value, Options);

    public static IReadOnlyList<T> Read<T>(string json) => string.IsNullOrWhiteSpace(json) ? Array.Empty<T>() : JsonSerializer.Deserialize<List<T>>(json, Options)!.ToArray();

    public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> value) => value.ToArray();

    public static PropertyBuilder<IReadOnlyList<T>> HasJsonList<T>(this PropertyBuilder<IReadOnlyList<T>> property)
    {
        property.HasConversion(v => Write(v), s => Read<T>(s),
            new ValueComparer<IReadOnlyList<T>>((a, b) => Write(a!) == Write(b!), v => Write(v).GetHashCode(), v => Copy(v)));
        return property;
    }

    public static PropertyBuilder<string> Encrypted(this PropertyBuilder<string> property, IPiiProtector protector)
    {
        property.HasConversion(v => protector.Protect(v), s => protector.Unprotect(s));
        return property;
    }
}
