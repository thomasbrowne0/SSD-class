# VulnTracker

CRA-focused vulnerability tracker – exam project for Secure Software Development (E2026). Solo version.

- [ROADMAP.md](ROADMAP.md) – what to build, mapped to the spec and lecture weeks
- [CONCEPTS_ELI5.md](CONCEPTS_ELI5.md) – course concepts explained simply

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

All of these, and the client secret `dev-only-client-secret`, are local dev values only.
In production, set `Oidc__Authority`, `Oidc__ClientSecret` and `ConnectionStrings__Default` as environment variables.

Realm import only happens when Keycloak's data is empty. After editing the realm file, recreate the container:

```sh
docker compose up -d --force-recreate keycloak
```

## Test

Tests need the database running (`docker compose up -d db`). They use a separate `vulntracker_test` database.

```sh
dotnet test
```

## Database changes

After changing an entity in `src/VulnTracker/Data`:

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/VulnTracker -o Data/Migrations
```
