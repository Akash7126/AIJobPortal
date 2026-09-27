using System.Security.Cryptography;
using System.Text;
using JobPlatform.AccountIdentity.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Base64 of 32 random bytes. Encrypts reversible secrets (MFA seeds, signing private keys) with AES-256-GCM. Supply via secret store / environment.</summary>
    public string MasterKey { get; set; } = string.Empty;

    /// <summary>Server-side pepper for HMAC hashing of OTPs, refresh tokens and API secrets.</summary>
    public string Pepper { get; set; } = string.Empty;

    public int Pbkdf2Iterations { get; set; } = 210_000;
}

/// <summary>
/// Password hashing with PBKDF2-HMAC-SHA512 (Q-05: strong adaptive one-way hashing). The stored format is self-describing
/// ($pbkdf2-sha512$i=&lt;iterations&gt;$salt$hash) so iterations can be raised and old hashes rehashed on next login.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "$pbkdf2-sha512$";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly int _iterations;
    private readonly Lazy<string> _dummy;

    public Pbkdf2PasswordHasher(IOptions<SecurityOptions> options)
    {
        _iterations = options.Value.Pbkdf2Iterations;
        _dummy = new Lazy<string>(() => Hash("dummy-password-for-timing"));
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA512, HashBytes);
        return $"{Prefix}i={_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string secret, string hash)
    {
        if (!TryParse(hash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool NeedsRehash(string hash) => !TryParse(hash, out var iterations, out _, out _) || iterations < _iterations;

    public void SimulateVerify(string password) => Verify(password, _dummy.Value);

    private static bool TryParse(string hash, out int iterations, out byte[] salt, out byte[] expected)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        expected = Array.Empty<byte>();
        if (!hash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = hash[Prefix.Length..].Split('$');
        if (parts.Length != 3 || !parts[0].StartsWith("i=", StringComparison.Ordinal) || !int.TryParse(parts[0][2..], out iterations))
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Keyed HMAC-SHA256 for one-time codes and high-entropy random secrets (never used for passwords).</summary>
public sealed class HmacSecretHasher : IOtpHasher, IApiSecretHasher
{
    private readonly byte[] _key;

    public HmacSecretHasher(IOptions<SecurityOptions> options) =>
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("secret-hasher|" + options.Value.Pepper));

    public string Hash(string secret) => Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(secret)));

    public bool Verify(string secret, string hash)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed class SecretGenerator : ISecretGenerator
{
    public string GenerateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public string GenerateApiKeyId() => "jp_" + Base64Url(RandomNumberGenerator.GetBytes(12));

    public string GenerateApiSecret() => "jps_" + Base64Url(RandomNumberGenerator.GetBytes(32));

    public string GenerateToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>AES-256-GCM for reversible secrets. Output = base64(nonce | tag | ciphertext).</summary>
public sealed class AesGcmProtector
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly byte[] _key;

    public AesGcmProtector(IOptions<SecurityOptions> options)
    {
        var key = string.IsNullOrWhiteSpace(options.Value.MasterKey) ? Array.Empty<byte>() : Convert.FromBase64String(options.Value.MasterKey);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("Security:MasterKey must be a base64-encoded 32-byte key.");
        }

        _key = key;
    }

    public string Protect(byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using var aes = new AesGcm(_key, TagBytes);
        aes.Encrypt(nonce, plaintext, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public byte[] Unprotect(string protectedValue)
    {
        var all = Convert.FromBase64String(protectedValue);
        var nonce = all[..NonceBytes];
        var tag = all[NonceBytes..(NonceBytes + TagBytes)];
        var cipher = all[(NonceBytes + TagBytes)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagBytes);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }
}

/// <summary>RFC 6238 TOTP (SHA-1, 6 digits, 30 s) with a +/-1 step window. Seeds are stored AES-256-GCM encrypted.</summary>
public sealed class TotpMfaService : IMfaService
{
    private const int Digits = 6;
    private const int StepSeconds = 30;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly AesGcmProtector _protector;

    public TotpMfaService(AesGcmProtector protector) => _protector = protector;

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public string Protect(string secret) => _protector.Protect(Encoding.UTF8.GetBytes(secret));

    public string Unprotect(string protectedSecret) => Encoding.UTF8.GetString(_protector.Unprotect(protectedSecret));

    public string BuildProvisioningUri(string issuer, string accountLabel, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountLabel)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public bool VerifyCode(string secret, string code, DateTime nowUtc) => FindMatchingTimeStep(secret, code, nowUtc) is not null;

    public long? FindMatchingTimeStep(string secret, string code, DateTime nowUtc)
    {
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        var key = Base32Decode(secret);
        var counter = new DateTimeOffset(nowUtc, TimeSpan.Zero).ToUnixTimeSeconds() / StepSeconds;
        long? matched = null;
        for (var offset = -1; offset <= 1; offset++)
        {
            // All three candidates are always compared (constant-time), the latest matching step wins.
            var expected = Compute(key, counter + offset);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code)))
            {
                matched = counter + offset;
            }
        }

        return matched;
    }

    /// <summary>Exposed for tests that need to produce a valid code for a known secret.</summary>
    public static string ComputeCode(string base32Secret, DateTime nowUtc) =>
        Compute(Base32Decode(base32Secret), new DateTimeOffset(nowUtc, TimeSpan.Zero).ToUnixTimeSeconds() / StepSeconds);

    private static string Compute(byte[] key, long counter)
    {
        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(message);
        }

        var hash = HMACSHA1.HashData(key, message);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            sb.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return sb.ToString();
    }

    private static byte[] Base32Decode(string text)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text.TrimEnd('=').ToUpperInvariant())
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException("Invalid base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
