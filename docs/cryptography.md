# Cryptography and key management (W44)

> "Your vulnerability tracker is essentially a repository of unpatched vulnerabilities waiting for attackers to exploit." – W44

Following defense in depth, finding data is encrypted in the database. It is still protected if the
database, a backup or a disk is stolen.

## What is encrypted

| Field | Encrypted | Why |
|---|---|---|
| Title | ✅ | Often names the vulnerability ("SQL injection in login"). |
| Description | ✅ | The details and steps to reproduce: the most dangerous part. |
| ReporterEmail | ✅ | Personal data (GDPR). |
| AffectedAsset, Severity, Status, dates, assignee | ❌ | Needed in SQL for filters (FIND-05) and the dashboard (FR-RPT-01). Trade-off: an attacker with the database learns *which asset* has *how severe* an open problem, but not what it is. |

## How

- **Algorithm:** AES-256-GCM (`System.Security.Cryptography.AesGcm`). This is **authenticated encryption**: it gives confidentiality,
  and the 16-byte authentication tag detects any change to the ciphertext (integrity).
- **Nonce:** 12 random bytes, new for every value. A nonce must never repeat with the same key in GCM. Random nonces
  also mean equal texts get different ciphertexts, so equal values can't be spotted.
- **Associated data:** `"<keyId>:<purpose>"`, e.g. `dev1:Finding.Title`. It isn't secret, but it's authenticated. A ciphertext copied from
  the Description column into the Title column fails to decrypt, and so does a ciphertext whose key id was changed.
- **Stored format:** `<keyId>:<base64(nonce | ciphertext | tag)>`
- **Where:** `src/VulnTracker/Security/FieldEncryptor.cs`. It's applied in `AppDbContext` as an EF Core value converter, so the rest of the
  app only ever sees plaintext. Nothing can forget to encrypt.
- **Tests:** `tests/VulnTracker.Tests/EncryptionTests.cs` covers round trip, a random nonce, tampering, a value moved to another column,
  key rotation, an unknown key, plaintext rejected, key length, and that the database really holds ciphertext.

Not done: no hashing of passwords, because the app stores no passwords. Keycloak handles them.

## Key management

| Question | Answer |
|---|---|
| **Generation** | 32 random bytes from a cryptographically secure generator, base64-encoded. See the command below. Never a password. |
| **Storage (production)** | Environment variables or a secret store (e.g. Docker/Kubernetes secrets, Azure Key Vault): `Encryption__CurrentKeyId` and `Encryption__Keys__<id>`. **Never** in the database, **never** in git, never logged. |
| **Storage (development)** | `appsettings.Development.json` has the dev key `dev1`. It's in git on purpose so the project runs out of the box, so it protects nothing. It must never be used outside development. |
| **Access** | Only the app process reads the key. The database server and DB admins never have it. Separating the two is what makes encryption useful. |
| **Rotation** | Add a new key (e.g. `prod2`) next to the old one and set `CurrentKeyId=prod2`. New and updated values use the new key. Old values still decrypt because each value carries its key id. Once old rows are re-saved, remove the old key. |
| **Leaked key** | Treat all data encrypted with it as exposed. Rotate immediately, re-encrypt all rows with the new key, remove the leaked key, and follow incident handling (CRA/NIS2 reporting may apply). |
| **Lost key** | Data encrypted with it is gone. Back up keys separately from the database backups. |

Generate a key:

```sh
openssl rand -base64 32
```

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

## Other cryptography in the system

| Where | What | Notes |
|---|---|---|
| Login | OIDC tokens signed by Keycloak (RS256). The app validates the signature with Keycloak's public keys. | Digital signatures (W44). |
| Login | PKCE: SHA-256 hash of a random verifier. | Stops a stolen authorization code being used. |
| Session cookie | Encrypted and signed by ASP.NET Data Protection. | In the container its keys are stored inside the container, so a restart logs everyone out. Production should store them persistently and protected. |
| In transit | **HTTPS is required in production.** Local development uses plain HTTP. | TLS is set up at deployment. The app enforces HTTPS metadata for OIDC outside Development. |

## Limits

- The key sits on the app server. An attacker who fully takes over the app can decrypt everything.
- Moving a whole encrypted row's values between findings isn't detected, because the row id isn't part of the associated data.
  It was left out because the id is only known after the insert.
