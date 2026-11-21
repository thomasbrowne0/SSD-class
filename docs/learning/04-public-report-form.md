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

- Line 10: `[AllowAnonymous]` overrides the fallback "login required" policy for this page only.
- Lines 13-14: the form binds to `ReportInput`, not to the `Finding` entity.
- Lines 25-28: if validation fails, the form is shown again with error messages.
- Lines 30-41: the finding is built field by field. Status, source and date are set by the server, not taken
  from the form (lines 37-39). The severity is the reporter's estimate (line 36), which triage can change.
- Line 45: redirect after POST, so refreshing the page doesn't submit the report twice. The page then shows
  the acknowledgement because `submitted=true` is in the URL.
- Lines 49-65: `ReportInput` with validation: required fields, max lengths, a valid email (line 63), and a
  severity that must be one of the defined values (line 60).

**src/VulnTracker/Pages/Report.cshtml**: shows either the form or the acknowledgement. The form tag helper
adds the antiforgery token automatically.

## How it's tested

**tests/VulnTracker.Tests/ReportPageTests.cs**

- Line 20: an anonymous user can open the form.
- Line 28: an anonymous report creates an External, New finding with the email, and the response says thank you.
- Line 48: an invalid email is rejected and nothing is saved.
- Line 60: extra fields added by hand (status, source, assignee) are ignored.
- Line 77: an anonymous user can't open the list, the dashboard or a finding.

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

**What risks remain?** Someone could flood the form with fake reports, because there is no rate limiting or CAPTCHA.
It is written down as a known gap in the threat model.

**Is the reporter's email protected?** Yes, it is personal data (GDPR), so it is encrypted in the database (file 08).
