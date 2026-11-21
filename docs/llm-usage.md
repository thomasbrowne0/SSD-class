# Use of LLMs and agents

We used Claude (Anthropic) through Claude Code for this project. The model was Claude Opus 5.5.
This document covers which parts Claude wrote, how we normally work with it, how this project
was actually built, and how we checked the quality and security of what it produced.

## Which parts are co-authored by an LLM

Almost all of the repository was written by Claude and then checked by us:

- the application code in `src/` (pages, access control, encryption, database and migrations)
- the tests in `tests/`
- `Dockerfile`, `compose.yaml` and the Keycloak realm file with the test users
- the GitHub Actions pipeline (`.github/workflows/ci.yml`) and the ZAP rules file
- the documents in `docs/` and the README

What came from us:

- the source material: the Moodle pages for every week, printed to PDF, and the exam project spec
- the scope: build what the spec marks MUST plus what the lectures require (encryption from W44), nothing extra
- the decisions where there was a choice. Claude proposed options with a recommendation, and we picked. Examples:
  .NET 10 instead of .NET 9 (9 runs out of support in November 2026), developers only see findings assigned to them,
  only Analyst and PM may set Accepted Risk, PM approves disclosures, users are stored on first login so they can be assigned
- reviewing the roadmap and plans, running the app, and confirming the CI pipeline passed on GitHub

## Our workflow with Claude

This is how we normally set up a project with Claude.

1. **Scope from the course itself.** We print the Moodle page for each week to PDF and give those to Claude, so requirements
   come from the course material and not from what the model remembers. This is our main way to avoid hallucinated requirements.
2. **Roadmap first.** Claude turns the PDFs into a roadmap (`ROADMAP.md`) that maps every requirement to a lecture week. We review it,
   and Claude checks that the gathered requirements match the curriculum.
3. **No tech outside the scope.** We don't add frameworks or tools that the course doesn't ask for. Extra tech is extra to learn
   and makes the exam harder, so we keep the stack as small as the requirements allow.
4. **Design before code.** We decide the architecture and how the project should be structured before building.
5. **Coordinator and threads.** For bigger projects, Claude runs a coordinator that hands parts of the work to separate agents (threads).
   Each thread has its own context, and the coordinator only receives their results and specs. This keeps the context clean and
   avoids wasting tokens on repeated explanations.
6. **Review with Claude skills.** When the project is built, we run it against Claude skills that check security, vulnerabilities
   and code smells.
7. **Learning files.** For each requirement we write a `learning.md` that explains how it works, checked against our real code,
   so we can study the system for the exam.

## How this project was actually built

This project followed the workflow above, with these differences:

- **One session, no coordinator.** The project was small enough to build in a single Claude Code session, one step at a time
  (skeleton, login, access control, findings pages, then report form, dashboard, encryption and CI). We didn't use the coordinator and threads.
- **Missing spec at the start.** The project spec was behind the Moodle login and wasn't in the PDFs. Claude flagged this,
  we downloaded it, and the roadmap was updated to match it before the features were built.
- **Design and threat model written after the code.** The architecture was decided early (ASP.NET Razor Pages, PostgreSQL, Keycloak, Docker),
  but `design.md`, `requirements.md` and `threat-model.md` were written after most of the code. They describe the system as it is built.
- **No Claude review skills.** Instead of review skills, we checked the code with tests and security tools in CI (see the next section).
- **Study notes instead of learning files.** We made a roadmap and a summary of each week's concepts, kept as personal study notes
  outside the repository. We didn't write a `learning.md` per requirement.

## How we ensured quality and security

- **Requirements are traceable.** Every spec requirement ID is mapped to the code that implements it in [requirements.md](requirements.md).
- **Facts checked against sources, not memory.** Library and tool versions were looked up when used. The OWASP Top 10 list was taken from
  the official site. The teacher's example repositories and course pages were used for the Keycloak and Docker setup.
- **Automated tests.** 99 tests, including every access rule tested against every role, the report form, the dashboard and the encryption.
  To make sure the tests really catch mistakes, access rules were broken on purpose and we checked that the tests failed.
- **Security gates in CI.** Every push runs a dependency check for known vulnerabilities, Semgrep static analysis and an OWASP ZAP scan
  of the running app. Images are only pushed after an SBOM is generated with Syft and scanned with Grype.
- **Findings from the tools were fixed, not ignored.** ZAP found missing security headers, which were added. The alerts we accept are
  listed with a reason in `.zap/rules.tsv`.
- **Manual checks.** We clicked through the app in a browser as each test user, and tested the Docker setup from a fresh clone the same way
  the pentest group will.
- **Known gaps are written down.** No audit log and no rate limiting on the public form. Both are in the [threat model](threat-model.md).
