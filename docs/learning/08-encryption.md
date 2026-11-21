# Encryption of findings (FIND-11, W44)

## What is asked

FIND-11 (SHOULD in the spec, but it is the W44 lecture task): encrypt finding details to protect confidentiality.
W44 says the tracker is "a repository of unpatched vulnerabilities waiting for attackers to exploit", so apply
cryptography for confidentiality **and integrity**, following defense in depth, and plan how keys are managed.

## How it works

Title, description and reporter email are encrypted before they are written to the database and decrypted when
read. The rest of the app never sees ciphertext. Someone who steals the database or a backup only sees values like
`dev1:mIYIunoOaZbk...`.

- **AES-256-GCM** is authenticated encryption. It hides the content (confidentiality) and adds a 16-byte tag. If
  anyone changes even one bit of the stored value, decryption fails (integrity).
- **Nonce**: 12 random bytes, new for every value. GCM breaks badly if a nonce is reused with the same key. Random nonces
  also mean the same text gives a different ciphertext each time, so you can't spot equal values.
- **Associated data**: the key id and the column name (for example `dev1:Finding.Title`) are authenticated together with
  the value. A ciphertext copied from the Description column into the Title column fails to decrypt.
- **Key id prefix**: every value starts with the id of the key that encrypted it. That makes key rotation possible:
  add a new key, make it the current one, and old values still decrypt with the old key.

Asset, severity, status and dates are not encrypted, because filters and the dashboard need them in SQL. That trade-off
is written down in `docs/cryptography.md`.

## Walk through the code

**src/VulnTracker/Security/FieldEncryptor.cs**

- Lines 16-18: key 32 bytes (AES-256), nonce 12 bytes, tag 16 bytes.
- Lines 23-42: the constructor refuses a missing current key, a key id with `:` in it, or a key that isn't 32 bytes.
  The app fails at startup instead of running with a bad key.
- Lines 46-54: keys are read from configuration (`Encryption:CurrentKeyId` and `Encryption:Keys:<id>`, base64).
- Lines 58-75: `Encrypt`. Line 64 fills the nonce with random bytes. Lines 66-72 encrypt with the associated data.
  Line 74 returns `keyId:base64(nonce | ciphertext | tag)`.
- Lines 78-106: `Decrypt`. Lines 80-89 split off the key id and look up the key. Lines 97-103 decrypt. `AesGcm.Decrypt`
  throws if the tag doesn't match, which is what happens when the data was changed.
- Line 108: the associated data.

**src/VulnTracker/Data/AppDbContext.cs**

- Lines 23-25: the three encrypted columns.
- Lines 29-35: an Entity Framework value converter. EF calls `Encrypt` when saving and `Decrypt` when loading, so no
  page can forget to encrypt.

**src/VulnTracker/Program.cs** line 13: the encryptor is created once from configuration.

## Key management

Read `docs/cryptography.md` for the full plan. The short version:

- Generated from a secure random source, 32 bytes, never derived from a password.
- Production: environment variables or a secret store. Never in the database, never in git.
- Development: `appsettings.Development.json` has the dev key `dev1`. It is public in the repo, so it protects nothing.
- Rotation: add a key, switch `CurrentKeyId`, re-save old rows, remove the old key.
- Leaked key: everything encrypted with it is exposed. Rotate, re-encrypt, treat it as an incident.

## How it's tested

**tests/VulnTracker.Tests/EncryptionTests.cs**

- Line 16: encrypt then decrypt gives the original text.
- Line 26: the ciphertext doesn't contain the text and is different every time.
- Line 40: one changed byte makes decryption fail (integrity).
- Line 52: a value moved to another column fails.
- Line 61: old values still decrypt after key rotation.
- Line 72: unknown key fails. Line 81: plaintext in the database is rejected. Line 87: a 16-byte key is refused.
- Line 99: reads the raw database column and checks that it really is ciphertext.

## Try it yourself

Create a finding in the app, then look at what is actually stored:

```sh
docker compose exec db psql -U postgres -d vulntracker -c 'select "Id", "Title", "AffectedAsset" from "Findings"'
```

## Questions you might get

**What is the difference between hashing and encryption?** A hash is one-way: you can't get the text back. Encryption
can be reversed with the key. We need the text back, so we encrypt.

**Why GCM and not CBC or ECB?** ECB leaks patterns. CBC only gives confidentiality, so you'd need a separate MAC for
integrity. GCM does both.

**What does encryption not protect against?** Someone who takes over the running app server, because the key is there.
It protects against a stolen database, backup or disk.

**Why store the key id with the value?** So keys can be rotated without decrypting and re-encrypting everything at once.

**Where are passwords stored?** Nowhere in our app. Keycloak stores them (hashed). We only get signed tokens.
