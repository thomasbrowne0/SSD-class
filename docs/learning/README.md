# Learning files

One file per requirement. Each one explains what the requirement asks, how it works, where it is in
our code (file and line numbers), how it is tested, how to try it yourself, and questions we expect
at the exam.

Read them in this order, because each one builds on the one before:

1. [Docker setup](01-docker-setup.md) (W40, W47)
2. [Login with OpenID Connect](02-oidc-login.md) (AUTH-01)
3. [Access control](03-access-control.md) (AUTH-02)
4. [Public report form](04-public-report-form.md) (AUTH-03, FIND-02)
5. [Create, edit and assign findings](05-findings-pages.md) (FIND-01, FIND-04)
6. [Filter and search](06-filter-and-search.md) (FIND-05)
7. [Dashboard](07-dashboard.md) (FR-RPT-01)
8. [Encryption of findings](08-encryption.md) (FIND-11, W44)
9. [Security gates in CI](09-ci-security-gates.md) (W40)
10. [SBOM and supply chain](10-sbom-supply-chain.md) (W45)

Line numbers are from the version of the code these files were written against. If code moves,
search for the method name instead.

To run anything mentioned here, start the stack first:

```sh
docker compose up -d --build
```
