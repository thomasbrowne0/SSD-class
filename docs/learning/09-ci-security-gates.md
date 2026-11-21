# Security gates in CI (W40)

## What is asked

W40: create a CI/CD pipeline (test, build, push a container image to a registry) and add security gates such as
SCA, SAST, DAST, and pull request reviews with branch protection. The idea of a secure development life cycle is that
security is checked on every change, not added at the end.

## How it works

`.github/workflows/ci.yml` runs on every push and pull request. Every job is a gate: if it fails, the image is not
pushed.

| Gate | Tool | Checks |
|---|---|---|
| SCA (software composition analysis) | `dotnet list package --vulnerable` | Our NuGet packages, including the ones they depend on, against known vulnerabilities |
| SAST (static analysis) | Semgrep, C# rules | Our source code, without running it |
| DAST (dynamic analysis) | OWASP ZAP baseline | The running app, from the outside, like an attacker |

The last gate in W40, branch protection with pull request reviews, is a GitHub repository setting and not code.

## Walk through the code

**.github/workflows/ci.yml**

- Lines 12-30: the `test` job starts a Postgres service and runs `dotnet test`.
- Lines 33-36: SCA. The command prints vulnerable packages. `grep` makes the step fail if there are any.
- Lines 39-45: SAST. Runs inside the Semgrep container, `--error` fails the job on any finding.
- Lines 49-61: DAST. Starts the whole stack with `docker compose`, waits until `/Report` answers, then runs the ZAP
  baseline scan against it. ZAP fails the job on any warning that isn't explained in `.zap/rules.tsv`.
- Lines 62-66: the ZAP report is uploaded as a build artifact even if the scan fails.
- Line 70: `build-and-push` only runs if all three gates passed (`needs: [test, sast, dast]`).

**.zap/rules.tsv**: the ZAP alerts we accept, each with a reason. For example, the SameSite=None cookie is the OIDC
correlation cookie, which has to be sent when Keycloak posts back to us.

**Security headers** (`src/VulnTracker/Program.cs` lines 82-102). ZAP found these missing the first time it ran,
so we added them:

- `Content-Security-Policy`: only load scripts and styles from our own site (blocks injected scripts),
  `frame-ancestors 'none'` (no clickjacking), `form-action` (forms can only go to us or Keycloak).
- `X-Content-Type-Options: nosniff`: the browser must trust our content type.
- `Permissions-Policy`: turn off camera, microphone, location and payment.
- `Cross-Origin-*-Policy`: keep other sites from embedding or reading our pages.

**Host header filtering** (`src/VulnTracker/appsettings.json`, `AllowedHosts`). Only requests for `localhost` are
accepted. Before, any Host header was accepted, and the login code built Keycloak's redirect URI from it. Keycloak
refused the unknown URI, and the app answered with a 500 and a stack trace. Now a foreign Host gets a plain 400. In a
real deployment, `AllowedHosts` is set to the real domain. This was a finding from our security audit.

## How it's tested

The gates are the tests. Each one was also run locally before it was added to CI. The first ZAP run found 9 warnings.
Real ones were fixed (the headers), false positives were documented.

## Try it yourself

```sh
dotnet list package --vulnerable --include-transitive
```

```sh
docker run --rm -v "$PWD:/src" -w /src semgrep/semgrep:1.183.0 semgrep scan --config p/csharp --error --metrics=off --exclude tests
```

With the stack running (Linux, or Docker Desktop with host networking enabled):

```sh
docker run --rm --network host -v "$PWD/.zap:/zap/wrk" ghcr.io/zaproxy/zaproxy:2.17.0 zap-baseline.py -t http://localhost:5000/Report -c rules.tsv
```

## Questions you might get

**What is the difference between SAST and DAST?** SAST reads the code without running it and can point at the exact
line. DAST attacks the running app from outside and finds problems in how it behaves (headers, cookies, errors), but
can't see the code.

**What doesn't your DAST cover?** Only pages that don't need login. Logged-in pages are covered by our integration tests.

**Why is a gate useful if you can just ignore the warning?** Every accepted alert has to be written down with a reason
in `rules.tsv`, which is reviewed like code. New warnings fail the build automatically.

**What would branch protection add?** Nobody can push straight to main. Changes need a pull request, and the security
gates must pass before it can be merged.
