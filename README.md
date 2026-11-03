# VulnTracker

CRA-focused vulnerability tracker – exam project for Secure Software Development (E2026). Solo version.

- [ROADMAP.md](ROADMAP.md) – what to build, mapped to the spec and lecture weeks
- [CONCEPTS_ELI5.md](CONCEPTS_ELI5.md) – course concepts explained simply

## Run locally

Requires the .NET 10 SDK and Docker.

Start the database:

```sh
docker compose up -d db
```

Run the app (applies database migrations on startup):

```sh
dotnet run --project src/VulnTracker
```

Or run everything in containers (app on http://localhost:5000):

```sh
docker compose up -d --build
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
