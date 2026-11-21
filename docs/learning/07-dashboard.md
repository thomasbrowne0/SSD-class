# Dashboard (FR-RPT-01)

## What is asked

FR-RPT-01 (MUST): a dashboard summarizing open findings by severity and by status.

## How it works

`/Dashboard` shows the total number of open findings, a table by severity and a table by status.
"Open" means New, Triaged or In Progress. Remediated and Accepted Risk are closed.

The counts use the same access rule as everything else, so a developer only sees counts for their own findings.
Each row links to the findings list with that filter applied.

## Walk through the code

**src/VulnTracker/Pages/Dashboard.cshtml.cs**

- Line 10: needs the `ViewFindings` policy.
- Lines 14-15: which statuses count as open.
- Line 26: start from `VisibleTo(User)` and keep only open findings.
- Lines 28-33: two `GroupBy` queries. The database does the counting (`SELECT Severity, COUNT(*) ... GROUP BY`),
  so we don't load every finding into memory.
- Lines 36-37: fill in zero for severities and statuses that have no findings, so the tables always have every row.
  Severities are listed most severe first.
- Line 38: the total.

**src/VulnTracker/Pages/Dashboard.cshtml**: two tables. Each label links to `/?severity=...` or `/?status=...`.

## How it's tested

**tests/VulnTracker.Tests/DashboardPageTests.cs**

- Line 21: creates findings for one developer (open, closed, and someone else's) and checks every count on the page.
- Line 51: every role can open the dashboard.
- Line 59: a user without a role gets 403.

## Try it yourself

Log in as `analyst` and open http://localhost:5000/Dashboard. Change a finding's status to Remediated and the open
count goes down. Log in as `developer` and the numbers only include findings assigned to the developer.

## Questions you might get

**Why do the counts respect access control?** A count is information too. Showing a developer "3 critical open
findings" for products they don't work on leaks something they shouldn't know.

**Why count in the database and not in C#?** It is faster and doesn't load the encrypted text of every finding just
to count them.
