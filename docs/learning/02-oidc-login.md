# Login with OpenID Connect (AUTH-01)

## What is asked

AUTH-01 (MUST): authenticate through an external identity provider using OIDC, for example Keycloak.
W41 explains why: employees should have one account, managed in one place, instead of one per system.

## How it works

The app never sees a password. Keycloak handles the login, and the app only receives a signed token
that says who the user is and which roles they have.

The flow (authorization code flow):

1. The user opens the app without a session cookie. The app redirects the browser to Keycloak.
2. The user logs in at Keycloak.
3. Keycloak redirects the browser back to `/signin-oidc` on the app with a short-lived **authorization code**.
4. The app sends the code, its **client secret** and a **PKCE verifier** directly to Keycloak (back channel,
   not through the browser) and gets an **ID token** back.
5. The app checks the token's signature with Keycloak's public key, reads the user id (`sub`), username
   and roles, and puts them in an encrypted session cookie.

Some terms:

- **OAuth 2.0** is about giving an app permission (access tokens). **OIDC** is built on top of it and adds identity (ID token).
- **ID token**: who the user is, meant for our app. **Access token**: permission to call an API. We only need the ID token.
- **PKCE**: the app makes a random secret (verifier), sends its hash at the start and the verifier at the end. A stolen
  authorization code is useless without the verifier.
- **PAR (pushed authorization request)**: .NET 10 sends the login request parameters to Keycloak over the back channel
  first and only puts a reference in the browser URL. You can see this as `request_uri=...` in the redirect.

## Walk through the code

**src/VulnTracker/Program.cs**

- Lines 18-23: two authentication schemes. The cookie keeps the session (`DefaultScheme`). OIDC is used when a
  user who isn't logged in needs to be sent to the login page (`DefaultChallengeScheme`).
- Lines 27-30: Keycloak address, client id and client secret come from configuration.
- Line 31: `ResponseType = Code` selects the authorization code flow.
- Line 33: `SaveTokens = true` keeps the ID token, which logout needs (see below).
- Lines 35-37: keep the claim names as Keycloak sends them, use `preferred_username` as the name and `roles` as roles.
  This is why `User.IsInRole("analyst")` works later.
- Line 39: require HTTPS for Keycloak's metadata everywhere except Development.
- Lines 41-42: after a successful login, save the user to our `Users` table (see file 05).

**keycloak/vulntracker-realm.json**

- Lines 17-19: confidential client with a client secret.
- Line 23: the exact allowed redirect URIs. Keycloak refuses to send a code anywhere else.
- Line 29: PKCE with S256 is required.
- Line 35: the realm role mapper that puts the user's roles into a `roles` claim in the ID token.

**src/VulnTracker/Pages/Logout.cshtml.cs**

- Lines 11-12: signs out of both schemes. The cookie is deleted, and the browser is sent to Keycloak's logout
  endpoint so the Keycloak session ends too. Otherwise the next visit would log the user straight back in.
- Logout is POST only, with an antiforgery token, so another website can't log users out with a link.

## How it's tested

The tests replace Keycloak with a fake login scheme (`tests/VulnTracker.Tests/TestAppFactory.cs`), because
tests shouldn't depend on a running Keycloak. `FindingsPageTests.Index_requires_login` checks that a user
without a session is rejected. The real Keycloak login was tested by hand in the browser with every test user.

## Try it yourself

1. Open http://localhost:5000. You land on the Keycloak login page. Look at the URL: `client_id` and `request_uri`.
2. Log in as `developer` / `developer`. The page header shows your username and roles.
3. Click "Log out", then open http://localhost:5000 again. You have to log in again, so the Keycloak session ended too.

## Questions you might get

**Why not keep our own user table with passwords?** One account per employee, managed in one place. Onboarding,
offboarding, password rules and MFA are handled by the identity provider. We don't store passwords, so we can't leak them.

**What stops an attacker from stealing the authorization code?** The code is short-lived, can only be used once,
only goes to an exact redirect URI, and needs both the client secret and the PKCE verifier to be exchanged.

**What is the difference between authentication and authorization?** Authentication is "who are you" (Keycloak).
Authorization is "what are you allowed to do" (our policies, file 03).

**Why `MapInboundClaims = false`?** By default .NET renames claims to long Microsoft URIs. Turning that off keeps
`sub` and `roles` as Keycloak sends them.
