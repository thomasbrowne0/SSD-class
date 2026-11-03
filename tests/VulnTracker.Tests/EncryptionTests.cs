using System.Security.Cryptography;
using VulnTracker.Security;

namespace VulnTracker.Tests;

// W44 / FIND-11: field encryption, without a database.
public class FieldEncryptorTests
{
    private static readonly byte[] Key1 = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Key2 = RandomNumberGenerator.GetBytes(32);

    private static FieldEncryptor Encryptor(string current = "k1") =>
        new(current, new Dictionary<string, byte[]> { ["k1"] = Key1, ["k2"] = Key2 });

    [Fact]
    public void Decrypt_returns_original_text()
    {
        var encryptor = Encryptor();

        var stored = encryptor.Encrypt("SQL injection in login 🔓", "Finding.Title");

        Assert.Equal("SQL injection in login 🔓", encryptor.Decrypt(stored, "Finding.Title"));
    }

    [Fact]
    public void Ciphertext_does_not_contain_plaintext_and_is_different_every_time()
    {
        var encryptor = Encryptor();

        var first = encryptor.Encrypt("secret", "Finding.Title");
        var second = encryptor.Encrypt("secret", "Finding.Title");

        Assert.DoesNotContain("secret", first);
        // Random nonce: equal texts can't be spotted in the database.
        Assert.NotEqual(first, second);
        Assert.StartsWith("k1:", first);
    }

    [Fact]
    public void Tampered_value_fails_to_decrypt()
    {
        var encryptor = Encryptor();
        var stored = encryptor.Encrypt("Severity is critical", "Finding.Description");
        var bytes = Convert.FromBase64String(stored["k1:".Length..]);
        bytes[^1] ^= 1;
        var tampered = "k1:" + Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => encryptor.Decrypt(tampered, "Finding.Description"));
    }

    [Fact]
    public void Value_moved_to_another_column_fails_to_decrypt()
    {
        var encryptor = Encryptor();
        var stored = encryptor.Encrypt("reporter@example.test", "Finding.ReporterEmail");

        Assert.ThrowsAny<CryptographicException>(() => encryptor.Decrypt(stored, "Finding.Title"));
    }

    [Fact]
    public void Old_values_still_decrypt_after_key_rotation()
    {
        var before = Encryptor(current: "k1").Encrypt("old finding", "Finding.Title");

        var rotated = Encryptor(current: "k2");

        Assert.Equal("old finding", rotated.Decrypt(before, "Finding.Title"));
        Assert.StartsWith("k2:", rotated.Encrypt("new finding", "Finding.Title"));
    }

    [Fact]
    public void Value_with_unknown_key_fails_to_decrypt()
    {
        var stored = Encryptor().Encrypt("text", "Finding.Title");
        var withoutKey1 = new FieldEncryptor("k2", new Dictionary<string, byte[]> { ["k2"] = Key2 });

        Assert.ThrowsAny<CryptographicException>(() => withoutKey1.Decrypt(stored, "Finding.Title"));
    }

    [Fact]
    public void Plaintext_in_database_is_rejected()
    {
        Assert.ThrowsAny<CryptographicException>(() => Encryptor().Decrypt("not encrypted", "Finding.Title"));
    }

    [Fact]
    public void Key_must_be_256_bits()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new FieldEncryptor("short", new Dictionary<string, byte[]> { ["short"] = new byte[16] }));
    }
}

// Checks what is actually stored in the database.
[Collection(AppCollection.Name)]
public class EncryptedStorageTests(TestAppFactory factory)
{
    [Fact]
    public async Task Finding_text_fields_are_encrypted_in_the_database()
    {
        var finding = await factory.AddFinding();

        var title = await factory.RawColumn(finding.Id, "Title");
        var description = await factory.RawColumn(finding.Id, "Description");

        Assert.DoesNotContain("SQL injection", title);
        Assert.DoesNotContain("concatenates", description);
        Assert.StartsWith("dev1:", title);
        // The app still reads the plaintext.
        Assert.Equal(finding.Title, (await factory.GetFinding(finding.Id))!.Title);
    }

    [Fact]
    public async Task Filter_columns_stay_readable()
    {
        var finding = await factory.AddFinding(asset: "billing-service");

        Assert.Equal("billing-service", await factory.RawColumn(finding.Id, "AffectedAsset"));
    }
}
