# VulnTracker

CRA-focused vulnerability tracker – exam project for Secure Software Development (E2026). Solo version.

- [ROADMAP.md](ROADMAP.md) – what to build, mapped to the spec and lecture weeks
- [CONCEPTS_ELI5.md](CONCEPTS_ELI5.md) – course concepts explained simply

Project documents:

| Document | Lecture |
|---|---|
| [docs/requirements.md](docs/requirements.md) – functional and security requirements, roles, abuse cases | W38 |
| [docs/design.md](docs/design.md) – tech stack, architecture, user stories | W39 |
| [docs/threat-model.md](docs/threat-model.md) – DFD, STRIDE, attack tree, mitigations | W39 |
| [docs/cryptography.md](docs/cryptography.md) – encryption of findings and key management | W44 |

## Pages

| URL | Who |
|---|---|
| `/Report` | Anyone, no login: report a vulnerability |
| `/Dashboard` | All roles: open findings by severity and status |
| `/` | All roles: findings list with filters |
| `/Findings/Create` | Analyst, PM |
| `/Findings/Details/{id}`, `/Findings/Edit/{id}` | Per role and assignment (see [docs/requirements.md](docs/requirements.md)) |
| `/Findings/Assign/{id}` | Analyst, PM |

## Run locally

Requires the .NET 10 SDK and Docker.

Start the database and Keycloak:

```sh
docker compose up -d db keycloak
```

Run the app on http://localhost:5133 (applies database migrations on startup):

```sh
dotnet run --project src/VulnTracker
```

Or run everything in containers (app on http://localhost:5000):

```sh
docker compose up -d --build
```

## Login

Login goes through Keycloak (OpenID Connect). The realm `vulntracker` is imported from
[keycloak/vulntracker-realm.json](keycloak/vulntracker-realm.json) on first start.

Test users (password = username), one per spec role:

| User | Role |
|---|---|
| `analyst` | analyst |
| `developer` | developer |
| `pm` | pm |
| `auditor` | auditor |

Keycloak admin console: http://localhost:8080/admin (admin / admin).

A user must log in once before findings can be assigned to them (the app records Keycloak users on login).

Realm import only happens when Keycloak's data is empty. After editing the realm file, recreate the container:

```sh
docker compose up -d --force-recreate keycloak
```

## Secrets and configuration

The users above, the client secret `dev-only-client-secret` and the encryption key `dev1` in
`appsettings.Development.json` are **local dev values only**. In production, set these as environment variables
(or in a secret store), never in git or the database:

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection |
| `Oidc__Authority`, `Oidc__ClientSecret` | Keycloak realm and client secret |
| `Encryption__CurrentKeyId`, `Encryption__Keys__<id>` | AES-256 key(s) for finding encryption, base64. See [docs/cryptography.md](docs/cryptography.md) |

## Test

Tests need the database running (`docker compose up -d db`). They use a separate `vulntracker_test` database.

```sh
dotnet test
```

## CI/CD and security gates

[.github/workflows/ci.yml](.github/workflows/ci.yml) runs on every push and pull request:

| Job | What | Lecture |
|---|---|---|
| `test` | Unit and integration tests, then **SCA**: fails if a NuGet package has a known vulnerability | W40 |
| `sast` | **SAST**: Semgrep C# rules, fails on any finding | W40 |
| `dast` | **DAST**: starts the stack and runs a ZAP baseline scan. Fails on any warning not explained in [.zap/rules.tsv](.zap/rules.tsv) | W40 |
| `build-and-push` (main only) | Builds the image, generates a CycloneDX **SBOM** with Syft (uploaded as an artifact), scans it with **Grype** (fails on fixable high/critical), then pushes to GitHub Container Registry | W40, W45 |

Run the same checks locally (with the stack running for DAST):

```sh
dotnet list package --vulnerable --include-transitive
```

```sh
docker run --rm -v "$PWD:/src" -w /src semgrep/semgrep:1.183.0 semgrep scan --config p/csharp --error --metrics=off --exclude tests
```

```sh
docker run --rm --network host -v "$PWD/.zap:/zap/wrk" ghcr.io/zaproxy/zaproxy:2.17.0 zap-baseline.py -t http://localhost:5000/Report -c rules.tsv
```

### Branch protection (manual GitHub setting)

The last W40 gate is a repository setting, not code. In GitHub: **Settings → Branches → Add branch ruleset** for `main`:

- Require a pull request before merging (solo: set required approvals to 0, since you can't approve your own PR)
- Require status checks to pass: `test`, `sast`, `dast`
- Block force pushes

After this, changes go through pull requests instead of direct pushes to `main`.

Note: on GitHub Free, branch protection only works on **public** repositories. For a private repo you need GitHub Pro,
which is free for students through the GitHub Student Developer Pack.

## Database changes

After changing an entity in `src/VulnTracker/Data`:

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/VulnTracker -o Data/Migrations
```
