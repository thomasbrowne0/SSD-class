# Filter and search (FIND-05)

## What is asked

FIND-05 (MUST): filter and search findings by status, severity, assigned owner and affected asset.

## How it works

The findings list at `/` has a GET form with four fields. They end up in the URL as query parameters, for
example `/?severity=High&asset=api`, so a filtered view can be bookmarked or shared.

The important part: the filters are applied **on top of** what the user is allowed to see. A developer who filters
by "assigned to: analyst" still only gets their own findings, because the access rule is applied first.

## Walk through the code

**src/VulnTracker/Pages/Index.cshtml.cs**

- Line 11: the page needs the `ViewFindings` policy.
- Lines 15-25: the four filters. `SupportsGet = true` means they are read from the query string.
- Line 34: start from `VisibleTo(User)`, the access rule from file 03.
- Lines 36-51: each filter adds a `Where` only if it has a value.
- Line 50: the asset search is case-insensitive and matches part of the name.
- Lines 53-56: the query runs once, includes the assignee's username, newest first.

All of this is LINQ that Entity Framework turns into **parameterized SQL**. The user's text is sent as a parameter,
never pasted into the SQL string, so SQL injection is not possible here. Characters like `%` in the search text are
treated as normal text, not as wildcards.

Title and description are encrypted (file 08), so they can't be searched in SQL. That's why the search is on the
asset, which the spec asks for anyway.

## How it's tested

**tests/VulnTracker.Tests/FindingsPageTests.cs**

- Line 37: a developer only sees findings assigned to them.
- Line 50: filter by severity and asset (the test uses upper case to check that the search ignores case).
- Line 66: filter by status and assignee.

## Try it yourself

As `analyst`, create a few findings with different assets and severities, then try:

- http://localhost:5000/?severity=High
- http://localhost:5000/?asset=API
- http://localhost:5000/?status=New&severity=Critical

## Questions you might get

**How do you prevent SQL injection in the search?** EF Core sends the value as a SQL parameter. We never build SQL
strings from user input, and Semgrep scans the code for injection patterns in CI.

**Can a developer use the assignee filter to see other people's findings?** No. The query starts from `VisibleTo`,
which already limits a developer to their own findings.

**Why can't you search in the title?** It is encrypted in the database. Searching encrypted columns would need
something like a separate search index or deterministic encryption, which weakens the encryption.
