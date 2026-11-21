# Public report form (AUTH-03, FIND-02)

## What is asked

AUTH-03 (MUST): a publicly accessible vulnerability reporting form with an acknowledgement.
FIND-02 (MUST): an external reporter can submit a new finding through that form. The spec's data section
says the reporter's contact email must be stored. The role table says external reporters can submit but
"cannot view anything". The CRA asks manufacturers to have a way for people to report vulnerabilities to them.

## How it works

`/Report` is the only page that works without login. It can only create findings. It never reads them,
and the thank-you message doesn't show the finding or its id.

The finding is saved with status *New* and source *External*, today's date as discovery date, and the
reporter's email. An analyst picks it up from there (triage).

## Walk through the code

**src/VulnTracker/Pages/Report.cshtml.cs**

- Line 11: `[AllowAnonymous]` overrides the fallback "login required" policy for this page only.
- Line 12: `[EnableRateLimiting]` applies the rate limit (see below).
- Lines 18-19: the form binds to `ReportInput`, not to the `Finding` entity.
- Lines 30-33: if validation fails, the form is shown again with error messages.
- Lines 35-46: the finding is built field by field. Status, source and date are set by the server, not taken
  from the form (lines 42-44). The severity is the reporter's estimate (line 41), which triage can change.
- Line 50: redirect after POST, so refreshing the page doesn't submit the report twice. The page then shows
  the acknowledgement because `submitted=true` is in the URL.
- Lines 54-70: `ReportInput` with validation: required fields, max lengths, a valid email (line 68), and a
  severity that must be one of the defined values (line 65).

**Rate limit** (`src/VulnTracker/Program.cs` lines 57-73). Each IP address can submit 5 reports per 10 minutes.
The sixth gets `429 Too Many Requests`. Only submissions (POST) are limited, so the form itself always loads. The
number comes from the setting `RateLimiting:ReportsPerWindow`. It is applied in the pipeline at line 106. This was a
finding from our security audit: before, a script could flood the form.

**src/VulnTracker/Pages/Report.cshtml**: shows either the form or the acknowledgement. The form tag helper
adds the antiforgery token automatically.

## How it's tested

**tests/VulnTracker.Tests/ReportPageTests.cs**

- Line 20: an anonymous user can open the form.
- Line 28: an anonymous report creates an External, New finding with the email, and the response says thank you.
- Line 48: an invalid email is rejected and nothing is saved.
- Line 60: extra fields added by hand (status, source, assignee) are ignored.
- Line 77: an anonymous user can't open the list, the dashboard or a finding.

`SecurityFixesTests.Report_form_is_rate_limited` runs its own copy of the app with a limit of 2 and checks that the
third submission gets a 429.

## Try it yourself

1. Open http://localhost:5000/Report in a private window (not logged in). Submit a report.
2. Log in as `analyst`. The report is in the list with source External, and the dashboard counts it.

## Questions you might get

**What is mass assignment and how did you prevent it?** If a form binds straight to the database entity, an attacker
can add fields like `Status=AcceptedRisk` or `AssigneeId=...` to the request. We bind to `ReportInput`, which only has
the five allowed fields, and set everything else on the server.

**Why redirect after POST?** Post/Redirect/Get: refreshing the thank-you page doesn't send the form again.

**Why doesn't the acknowledgement show the finding id?** The reporter doesn't need it, and ids are sequential, so
showing them would tell outsiders how many findings exist.

**How do you stop someone flooding the form?** A rate limit of 5 submissions per IP per 10 minutes.

**What risks remain?** An attacker with many IP addresses can get around a per-IP limit. A CAPTCHA would be the
next step. Behind a reverse proxy, the app would also need forwarded headers to see the real client IP.

**Is the reporter's email protected?** Yes, it is personal data (GDPR), so it is encrypted in the database (file 08).
