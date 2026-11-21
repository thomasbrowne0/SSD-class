# Docker setup (W40, W47)

## What is asked

W40: set up the project and a pipeline that can build and push a container image.
W47: another group must be able to start the whole app with all dependencies in Docker with a few
commands. Installing dependencies and building must happen inside a container, because running a
stranger's build scripts on your own machine is a supply chain risk. Test users must come from a
Keycloak realm import.

## How it works

There are three containers, defined in `compose.yaml`:

- `db`: PostgreSQL 18
- `keycloak`: Keycloak 26.8.1, which imports our realm (client, roles, test users) on first start
- `app`: our ASP.NET app, built from the `Dockerfile`

`docker compose up -d --build` builds the app image and starts all three. The app waits until the
database and Keycloak report healthy before it starts.

## Walk through the code

**Dockerfile** is a multi-stage build:

- Lines 1-6: the first stage uses the .NET SDK image. It copies only the `.csproj` first and runs
  `dotnet restore` (line 4), then copies the rest and publishes (line 6). Copying the project file first
  means Docker can cache the restore step when only code changes.
- Lines 8-13: the second stage starts from the much smaller ASP.NET runtime image and copies only the
  published output. The SDK, source code and build tools are not in the final image.
- Line 12: `USER $APP_UID` runs the app as the built-in non-root user. If someone breaks into the app,
  they are not root inside the container.

**compose.yaml**:

- Lines 4-9: Postgres, port bound to `127.0.0.1` only, so other machines on the network can't reach it.
- Lines 18-19: Keycloak runs `start-dev --import-realm`, which imports every JSON file in
  `/opt/keycloak/data/import`. The `keycloak/` folder is mounted there.
- Lines 24-25: `KC_HOSTNAME` tells Keycloak that browsers reach it on `localhost:8080`.
  `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` lets the app container reach it as `keycloak:8080` at the same time.
- Lines 41-42: the app talks to Keycloak on `keycloak:8080` (`Oidc__Authority`), but the browser uses
  `localhost:8080` (`Oidc__BrowserAuthority`, used for the CSP header, see file 09).

**keycloak/vulntracker-realm.json**:

- Lines 50, 60, 70, 80: the four test users, one per role. Password = username.
- Line 19: the client secret (dev value only).

## How it's tested

The setup was tested by cloning the repository into an empty folder and following the README:
one `docker compose up -d --build`, about a minute to start, then logging in as a test user.
The CI `dast` job (ci.yml line 55) starts the same compose stack on every push.

## Try it yourself

```sh
docker compose up -d --build
docker compose ps
docker compose logs app
```

Then open http://localhost:5000 and log in as `analyst` / `analyst`.

## Questions you might get

**Why multi-stage?** The final image only has the runtime and our compiled app. Fewer packages means a
smaller attack surface and fewer vulnerabilities for the SBOM scan to find.

**Why build inside a container?** The person running it doesn't execute our build scripts on their own
machine, and everyone builds with exactly the same SDK version.

**Why non-root?** Least privilege. A compromised app process can do less damage.

**Why bind ports to 127.0.0.1?** So the database and Keycloak are only reachable from this machine,
not from the network.

**Are the passwords in compose.yaml a problem?** They are dev values. In a real deployment they come from
environment variables or a secret store (see the README and `docs/cryptography.md`).
