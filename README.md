# VulnTracker

A vulnerability tracker made for the Secure Software Development course at EASV (autumn 2026).
It is built around the EU Cyber Resilience Act: findings come in from internal reports, an external
report form or scanners, get triaged and assigned, and are tracked until they are fixed.

Built with ASP.NET Core 10 (Razor Pages), PostgreSQL and Keycloak for login.

## Use of LLMs

Most of the code and documents were written with Claude (Claude Code). Which parts, how we worked with it
and how we checked its output is described in [docs/llm-usage.md](docs/llm-usage.md).

## Running it

You only need Docker (Docker Desktop on Windows/Mac). Restoring packages and building the app happens
inside a container, so you don't need .NET installed.

```sh
git clone https://github.com/thomasbrowne0/SSD-class.git
cd SSD-class
docker compose up -d --build
```

The first start takes a minute or two. Keycloak is usually the last to be ready, so if the login page
doesn't load straight away, wait a bit and refresh.

- App: http://localhost:5000
- Report a vulnerability (no login needed): http://localhost:5000/Report
- Keycloak admin console: http://localhost:8080/admin (user `admin`, password `admin`)

To stop it:

```sh
docker compose down
```

Add `-v` to also delete the database. If you ran an older version of this project before, run `docker compose down -v`
once, so the database is created again with the app's own account.

## Test users

Keycloak creates these users from `keycloak/vulntracker-realm.json` when it starts.
The password is the same as the username.

| Username | Password | Role |
|---|---|---|
| analyst | analyst | Analyst |
| developer | developer | Developer |
| pm | pm | PM |
| auditor | auditor | Auditor |

Roughly: analysts and PMs create, triage and assign findings, developers only see and edit the findings
assigned to them, and auditors can read everything but change nothing. The full permission table is in
[docs/requirements.md](docs/requirements.md).

A user has to log in once before findings can be assigned to them, because the app saves users the
first time they log in.

Keycloak has brute-force protection on: after 5 wrong passwords the account is locked for a while (1 minute at first,
longer after repeated lockouts, at most 15 minutes). An admin can unlock it in the Keycloak admin console under
Users > the user > Brute force.

## Running without Docker

Needs the .NET 10 SDK. Start the database and Keycloak in Docker, then run the app:

```sh
docker compose up -d db keycloak
dotnet run --project src/VulnTracker
```

The app runs on http://localhost:5133 and applies database migrations on startup.

## Tests

The tests need the database running (`docker compose up -d db`). They use their own `vulntracker_test` database.

```sh
dotnet test
```

## Configuration

The passwords, client secret and encryption key in this repo (`compose.yaml`, `appsettings.Development.json`,
the realm file) are for local development only. In a real deployment they are set with environment variables:

- `ConnectionStrings__Default`. The app uses its own database account `vulntracker`, not the postgres superuser.
  `db/init/01-app-user.sql` creates it when the database is first created.
- `Oidc__Authority` and `Oidc__ClientSecret`
- `Encryption__CurrentKeyId` and `Encryption__Keys__<id>` (base64, 32 bytes)
- `AllowedHosts`, the host name the app is served on (only `localhost` is accepted by default)

How the encryption keys are handled is described in [docs/cryptography.md](docs/cryptography.md).

## CI

GitHub Actions ([ci.yml](.github/workflows/ci.yml)) runs on every push and pull request:

- tests, then a check for NuGet packages with known vulnerabilities
- Semgrep static analysis
- an OWASP ZAP baseline scan of the running app (accepted alerts and why are in [.zap/rules.tsv](.zap/rules.tsv))
- on `main` only: build the image, generate an SBOM with Syft, scan it with Grype, and push to GitHub Container Registry if nothing high or critical is found

## Documentation

- [Requirements, roles and abuse cases](docs/requirements.md)
- [Design: tech stack, architecture and user stories](docs/design.md)
- [Threat model](docs/threat-model.md)
- [Cryptography and key management](docs/cryptography.md)
- [Use of LLMs and agents](docs/llm-usage.md)
- [Learning files, one per requirement](docs/learning/README.md)

## Database changes

After changing an entity in `src/VulnTracker/Data`:

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/VulnTracker -o Data/Migrations
```
