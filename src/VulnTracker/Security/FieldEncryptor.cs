using System.Security.Cryptography;
using System.Text;

namespace VulnTracker.Security;

// W44 / FIND-11: encrypts finding fields before they are stored, so a stolen database or backup
// doesn't reveal the vulnerabilities (defense in depth). Key management: docs/cryptography.md.
//
// AES-256-GCM is authenticated encryption: it protects confidentiality, and a changed ciphertext
// fails to decrypt (integrity).
//
// Stored format: "<keyId>:<base64(nonce | ciphertext | tag)>"
// The key id says which key to decrypt with, so old values can still be read after a new key is added.
public class FieldEncryptor
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IReadOnlyDictionary<string, byte[]> keys;
    private readonly string currentKeyId;

    public FieldEncryptor(string currentKeyId, IReadOnlyDictionary<string, byte[]> keys)
    {
        if (!keys.ContainsKey(currentKeyId))
        {
            throw new InvalidOperationException($"Encryption key '{currentKeyId}' is not configured.");
        }
        foreach (var (id, key) in keys)
        {
            if (id.Length == 0 || id.Contains(':'))
            {
                throw new InvalidOperationException($"Invalid encryption key id '{id}'.");
            }
            if (key.Length != KeySize)
            {
                throw new InvalidOperationException($"Encryption key '{id}' must be {KeySize} bytes (AES-256).");
            }
        }
        this.currentKeyId = currentKeyId;
        this.keys = keys;
    }

    // Reads "Encryption:CurrentKeyId" and "Encryption:Keys:<id>" (base64). Set them as environment
    // variables or in a secret store, never in the database.
    public static FieldEncryptor FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Encryption");
        var currentKeyId = section["CurrentKeyId"]
            ?? throw new InvalidOperationException("Encryption:CurrentKeyId is not configured.");
        var keys = section.GetSection("Keys").GetChildren()
            .ToDictionary(k => k.Key, k => Convert.FromBase64String(k.Value ?? ""));
        return new FieldEncryptor(currentKeyId, keys);
    }

    // "purpose" (e.g. "Finding.Title") is authenticated with the data, so a value copied into another
    // column fails to decrypt.
    public string Encrypt(string plaintext, string purpose)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        // A fresh random nonce every time. Reusing a nonce with the same key breaks GCM.
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(keys[currentKeyId], TagSize);
        aes.Encrypt(
            nonce,
            plain,
            output.AsSpan(NonceSize, plain.Length),
            output.AsSpan(NonceSize + plain.Length, TagSize),
            AssociatedData(currentKeyId, purpose));

        return $"{currentKeyId}:{Convert.ToBase64String(output)}";
    }

    // Throws CryptographicException if the value was changed, has the wrong purpose, or the key is unknown.
    public string Decrypt(string stored, string purpose)
    {
        var separator = stored.IndexOf(':');
        if (separator <= 0)
        {
            throw new CryptographicException("Value is not encrypted.");
        }
        var keyId = stored[..separator];
        if (!keys.TryGetValue(keyId, out var key))
        {
            throw new CryptographicException($"Unknown encryption key '{keyId}'.");
        }
        var data = Convert.FromBase64String(stored[(separator + 1)..]);
        if (data.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Encrypted value is too short.");
        }

        var plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            data.AsSpan(0, NonceSize),
            data.AsSpan(NonceSize, plain.Length),
            data.AsSpan(NonceSize + plain.Length, TagSize),
            plain,
            AssociatedData(keyId, purpose));

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] AssociatedData(string keyId, string purpose) => Encoding.UTF8.GetBytes($"{keyId}:{purpose}");
}
