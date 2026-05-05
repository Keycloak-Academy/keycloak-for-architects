# Lab 4 — Integrating Applications with Keycloak

This lab addresses the problem of retrofitting an existing application stack that currently relies on HTTP Basic Authentication with no identity infrastructure. By the end, you will have demonstrated how to configure a resource server for JWT Bearer validation, register both public and confidential clients in Keycloak, and wire the OIDC Authorization Code flow into a server-side web application so that user identity and roles flow from the token into the application session.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running at `http://localhost:8080` (or the shared cloud instance URL provided by the instructor)
- [ ] The `{realm}` realm is accessible with user `alice`
- [ ] .NET 10 SDK and Node.js 20+ are installed
- [ ] Lab 3 is completed (realm, users, and roles configured), or you are using the provided `source-initial/` state

If any prerequisite is missing, complete [Lab 3] before continuing.

**Keycloak endpoints — replace `{realm}` with your realm name throughout this lab:**

| | Shared cloud instance | Local Keycloak |
|---|---|---|
| Admin console | `https://labs-sso-admin.keycloak.academy/admin/{realm}/console/` | `http://localhost:8080/admin/{realm}/console/` |
| OIDC issuer (`{issuer}`) | `https://labs-sso.keycloak.academy/realms/{realm}` | `http://localhost:8080/realms/{realm}` |

> All configuration snippets in this lab use `{issuer}` as a placeholder. Substitute it with the value from the table above that matches your environment.

---

## Background

### Application types and security models

This lab works with two application types that have different security models: a REST API (resource server) and a server-side web application (confidential client).

```
source-initial/   ← starting point — Basic Auth everywhere, no Keycloak
source-complete/  ← reference solution — OAuth 2.0 / OIDC fully integrated
```

Both variants include the same three applications:

| App | Port | Type |
|-----|------|------|
| `core-banking-api` | 3011 | ASP.NET Core Minimal API (resource server) |
| `trading-app` | 5010 | ASP.NET Core MVC (server-side web app) |
| `banking-app` | 3010 | React SPA (public client — unchanged from Lab 1) |

Open `demo.code-workspace` in VS Code to load all three apps in a single workspace.

### Client type comparison

After completing both parts of this lab, you will have two authenticated clients side by side:

| | `banking-app` (Lab 1) | `trading-app` (this lab) |
|--|--|--|
| Client type | Public | Confidential |
| Client secret | None | Yes — stored server-side |
| PKCE | Yes (required) | Not needed |
| Token storage | sessionStorage (browser) | Encrypted httpOnly cookie |
| Token visible to browser | Yes | No |
| API calls | Client-side (CORS required) | None in this lab (added in Lab 5) |

> **Note:** `RequireHttpsMetadata = false` appears in several code snippets for local development only. Always enforce HTTPS in production.

---

## Task 1 — Register a service account and secure the API with JWT Bearer authentication

> Estimated time: 15–20 min | Tools: admin console, curl, VS Code

**Goal:** Replace Basic Auth on `core-banking-api` with JWT Bearer authentication so that client applications obtain a token from Keycloak using their `client_id` and `client_secret`, then present that token as a Bearer header.

**Observable outcome:**
- `GET http://localhost:3011/api/stocks` without a token returns **401**
- `GET http://localhost:3011/api/stocks` with a valid Bearer token returns **200**
- The `azp` claim in the token contains the client ID, and there is no `sub` claim for a human user

<details>
<summary>Hint — which Keycloak client configuration enables machine-to-machine authentication?</summary>

Think about the grant type that involves only a client ID and secret, with no user interaction. In Keycloak, this capability is controlled by a specific toggle on the client's capability config page.

</details>

<details>
<summary>Hint — what must change in the ASP.NET resource server to accept JWTs instead of Basic Auth?</summary>

You will need to remove the custom authentication handler, register a different authentication scheme that points to your Keycloak realm as the authority, and add the corresponding NuGet package.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

### 1.1 — Register a service account client in Keycloak

In the admin console, go to **Clients → Create client**:

| Field | Value |
|-------|-------|
| Client type | OpenID Connect |
| Client ID | `core-banking-api-consumer` |

On the **Capability config** page:

| Field | Value |
|-------|-------|
| Client authentication | **On** (confidential client) |
| Authentication flow | uncheck Standard flow and Direct access — check **Service accounts roles** only |

Save, then go to the **Credentials** tab and note the **Client secret**.

> **Service accounts roles** enables the `client_credentials` grant — this is the machine-to-machine flow. There is no human user involved; the client authenticates as itself.

### 1.2 — Test token issuance

Obtain a token using curl (replace `{realm}` and `{secret}`):

- Linux / macOS:

  ```bash
  curl -s -X POST \
    {issuer}/protocol/openid-connect/token \
    -d grant_type=client_credentials \
    -d client_id=core-banking-api-consumer \
    -d client_secret={secret} \
    | jq .
  ```

- Windows (PowerShell):

  ```powershell
  curl.exe -s -X POST `
    {issuer}/protocol/openid-connect/token `
    -d grant_type=client_credentials `
    -d client_id=core-banking-api-consumer `
    -d client_secret={secret} `
    | ConvertFrom-Json | ConvertTo-Json -Depth 10
  ```

You will receive an `access_token`. Inspect it at [jwt.io](https://jwt.io) — notice the `azp` claim (authorised party) contains your client ID, and there is no `sub` claim for a human user.

### 1.3 — Update `core-banking-api`

Open `source-initial/core-banking-api/Program.cs`. Your task:

**a) Remove the `BasicAuthHandler` registration** and its class definition at the bottom of the file.

**b) Add JWT Bearer authentication:**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority             = builder.Configuration["Keycloak:Authority"];
        o.RequireHttpsMetadata  = false; // ⚠️ demo only
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
        };
    });
```

**c) Update `appsettings.json`** — replace the `BasicAuth` section with:

```json
"Keycloak": {
  "Authority": "{issuer}"
}
```

**d) Update `core-banking-api.csproj`** — replace the (empty) project with:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
</ItemGroup>
```

Restart the API. Calling `GET http://localhost:3011/api/stocks` without a token should now return **401**. Calling it with a valid Bearer token should return **200**.

- Linux / macOS:

  ```bash
  # Should return 401
  curl http://localhost:3011/api/stocks

  # Should return 200
  curl http://localhost:3011/api/stocks \
    -H "Authorization: Bearer {access_token}"
  ```

- Windows (PowerShell):

  ```powershell
  # Should return 401
  curl.exe http://localhost:3011/api/stocks

  # Should return 200
  curl.exe http://localhost:3011/api/stocks `
    -H "Authorization: Bearer {access_token}"
  ```

</details>

---

## Task 2 — Map Keycloak roles and enforce role-based access control on API endpoints

> Estimated time: 20–25 min | Tools: VS Code, curl

**Goal:** Map Keycloak's nested role claims to ASP.NET's `ClaimTypes.Role` and add role-protected endpoints to `core-banking-api` so that tokens are checked not just for validity but for the specific roles required by each operation.

**Observable outcome:**
- `GET /api/accounts` with no token → 401; `trader` token → 403 `Required role: customer`; `customer` token → 200
- `GET /api/portfolio/repartition` with `trader` token (client role on `trading-app`) → 200; `customer` token → 403 `Required role: trader`
- `GET /api/users` with `admin` token → 200; `customer` token → 403 `Required role: admin`
- `GET /api/stocks` with any valid token → 200 (auth only — no role required)

<details>
<summary>Hint — where does Keycloak store realm roles and client roles in the JWT?</summary>

Keycloak places roles in nested JSON structures inside the token. ASP.NET's role-checking method expects a flat claim type. Consider which event in the JWT Bearer pipeline lets you transform claims after the token signature is validated but before the request reaches your endpoint.

</details>

<details>
<summary>Hint — how does the API distinguish between a missing token (401) and a valid token without the required role (403)?</summary>

The `.RequireAuthorization()` call on each endpoint lets the middleware handle the unauthenticated case (401). The role check happens inside the endpoint handler, where you return a custom JSON response with status 403 when the token is valid but the role is absent.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

### 2.1 — Map realm_access.roles and resource_access roles to ClaimTypes.Role

Open `source-initial/core-banking-api/Program.cs`. Add the following `using` statements at the top:

```csharp
using System.Security.Claims;
using System.Text.Json;
```

Then add an `Events` block inside the `AddJwtBearer` call, immediately after `TokenValidationParameters`:

```csharp
o.Events = new JwtBearerEvents
{
    OnTokenValidated = ctx =>
    {
        if (ctx.Principal?.Identity is not ClaimsIdentity identity) return Task.CompletedTask;

        // 1. Realm roles (customer, admin, …) from realm_access.roles
        var realmAccess = identity.FindFirst("realm_access");
        if (realmAccess is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(realmAccess.Value);
                if (doc.RootElement.TryGetProperty("roles", out var roles))
                    foreach (var role in roles.EnumerateArray())
                        if (role.GetString() is string r)
                            identity.AddClaim(new Claim(ClaimTypes.Role, r));
            }
            catch { }
        }

        // 2. trading-app client roles (trader, …) from resource_access.trading-app.roles
        var resourceAccess = identity.FindFirst("resource_access");
        if (resourceAccess is not null)
        {
            try
            {
                using var doc2 = JsonDocument.Parse(resourceAccess.Value);
                if (doc2.RootElement.TryGetProperty("trading-app", out var ta) &&
                    ta.TryGetProperty("roles", out var clientRoles))
                    foreach (var role in clientRoles.EnumerateArray())
                        if (role.GetString() is string r)
                            identity.AddClaim(new Claim(ClaimTypes.Role, r));
            }
            catch { }
        }

        return Task.CompletedTask;
    }
};
```

This event fires immediately after signature verification. It reads both `realm_access.roles` (realm-level roles like `customer`, `admin`) and `resource_access["trading-app"].roles` (client-scoped roles like `trader`), mapping each to a `ClaimTypes.Role` claim. After this, `ctx.User.IsInRole("customer")` and `ctx.User.IsInRole("trader")` both work in endpoint handlers.

### 2.2 — Add a helper and the role-protected endpoints

Add the helper function after the `app.Run(...)` call:

```csharp
static IResult Forbidden(string role) =>
    Results.Json(new { error = "Forbidden", detail = $"Required role: {role}" }, statusCode: 403);
```

Replace the existing `/api/stocks` endpoint and add the remaining ones:

```csharp
app.MapGet("/api/accounts", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { accounts = FakeData.Accounts });
}).RequireAuthorization();

app.MapGet("/api/transactions/{accountId}", (HttpContext ctx, string accountId) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { transactions = FakeData.Transactions });
}).RequireAuthorization();

app.MapGet("/api/transactions", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { transactions = FakeData.Transactions });
}).RequireAuthorization();

app.MapGet("/api/balances", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { balances = new { checking = 1500, savings = 5000 } });
}).RequireAuthorization();

app.MapGet("/api/portfolio/repartition", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("trader")) return Forbidden("trader");
    return Results.Ok(new { portfolioRepartition = FakeData.Portfolio, totalValue = 50000.0 });
}).RequireAuthorization();

app.MapGet("/api/stocks", () =>
    Results.Ok(new { stocks = FakeData.Stocks })
).RequireAuthorization();

app.MapGet("/api/users", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("admin")) return Forbidden("admin");
    return Results.Ok(new { users = FakeData.Users });
}).RequireAuthorization();

app.MapGet("/api/users/{userId}/accounts", (HttpContext ctx, string userId) => {
    if (!ctx.User.IsInRole("admin")) return Forbidden("admin");
    return Results.Ok(new { accounts = FakeData.GetUserAccounts(userId) });
}).RequireAuthorization();
```

Add the missing fake data members to the `FakeData` class:

```csharp
public static readonly object[] Accounts = [
    new { id = "12345", type = "checking", balance = 1500 },
    new { id = "67890", type = "savings",  balance = 5000 },
];

public static readonly object[] Transactions = [
    new { id = "1", description = "Coffee Shop", amount = -5,   date = "12 Aug, 2020 12:33" },
    new { id = "2", description = "Salary",      amount = 2000, date = "12 Aug, 2020 12:33" },
];

public static readonly object[] Portfolio = [
    new { id = 1, name = "Tech Stocks",        type = "Stocks",       currentValue = 20000.0, percentage = 40 },
    new { id = 2, name = "Government Bonds",    type = "Bonds",        currentValue = 15000.0, percentage = 30 },
    new { id = 3, name = "Global Mutual Funds", type = "Mutual Funds", currentValue = 10000.0, percentage = 20 },
    new { id = 4, name = "Real Estate",         type = "Assets",       currentValue =  5000.0, percentage = 10 },
];

public static readonly object[] Users = [
    new { id = "u001", name = "Alice Johnson", email = "alice@example.com",   role = "customer" },
    new { id = "u002", name = "Bob Smith",     email = "bob@example.com",     role = "customer" },
    new { id = "u003", name = "Charlie Brown", email = "charlie@example.com", role = "admin"    },
];

private static readonly (string UserId, string Id, string Type, int Balance)[] AllUserAccounts = [
    ("u001", "12345", "checking",   1500),
    ("u001", "67890", "savings",    5000),
    ("u002", "54321", "checking",   300),
    ("u003", "98765", "investment", 10000),
];

public static object[] GetUserAccounts(string userId) =>
    AllUserAccounts
        .Where(a => a.UserId == userId)
        .Select(a => (object)new { userId = a.UserId, id = a.Id, type = a.Type, balance = a.Balance })
        .ToArray();
```

> **Note:** `ValidateAudience` stays `false` — audience validation is introduced in Lab 5. Role enforcement is sufficient for this lab's authorization goals.

### 2.3 — Verify the API

| Request | Expected |
|---------|---------|
| No token | 401 |
| Valid `trader` token → `GET /api/accounts` | 403 `Required role: customer` |
| Valid `customer` token → `GET /api/accounts` | 200 |
| Valid `customer` token → `GET /api/stocks` | 200 (auth only — any valid token works) |
| Valid `customer` token → `GET /api/portfolio/repartition` | 403 `Required role: trader` |
| Valid `trader` token (client role) → `GET /api/portfolio/repartition` | 200 |

</details>

---

## Task 3 — Register a confidential client and add OIDC middleware to the trading app

> Estimated time: 20–25 min | Tools: admin console, VS Code

**Goal:** Turn `trading-app` into an OIDC confidential client so that users must log in with Keycloak before accessing the dashboard, and the app displays the user's name and roles from the ID token.

**Observable outcome:**
- Navigating to `http://localhost:5010` redirects to the Keycloak login page when not authenticated
- After login, the trading app displays a personalised greeting with the user's name and realm roles
- The access token is never visible in the browser (stored in an encrypted httpOnly cookie)

<details>
<summary>Hint — which authentication flow is appropriate for a server-side web application that needs to identify users?</summary>

Consider the flow that uses a redirect to Keycloak, followed by an authorization code exchange on the back channel. This requires the client to authenticate itself with a secret.

</details>

<details>
<summary>Hint — how does ASP.NET maintain user session state after the OIDC handshake completes?</summary>

The OIDC middleware validates the ID token and then issues its own session mechanism. Think about which ASP.NET authentication scheme is typically used for this local session and how it relates to the challenge scheme.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

### 3.1 — Register a confidential client in Keycloak

In the admin console, **Clients → Create client**:

| Field | Value |
|-------|-------|
| Client type | OpenID Connect |
| Client ID | `trading-app` |

On **Capability config**:

| Field | Value |
|-------|-------|
| Client authentication | **On** (confidential) |
| Authentication flow | **Standard flow** only |

On the **Login settings** page:

| Field | Value |
|-------|-------|
| Valid redirect URIs | `http://localhost:5010/signin-oidc` |
| Post logout redirect URIs | `http://localhost:5010/signout-callback-oidc` |

Save, then copy the **Client secret** from the **Credentials** tab.

### 3.2 — Add OIDC middleware to `trading-app`

In `source-initial/trading-app`:

**a) Update `trading-app.csproj`** — add the OIDC package:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" Version="10.0.0" />
```

**b) Update `Program.cs`** — add authentication before the existing services:

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
```

```csharp
var kc = builder.Configuration.GetSection("Keycloak");

builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    o.Cookie.Name     = "trading_app";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
})
.AddOpenIdConnect(o =>
{
    o.Authority             = kc["Authority"];
    o.ClientId              = kc["ClientId"];
    o.ClientSecret          = kc["ClientSecret"];
    o.ResponseType          = "code";
    o.CallbackPath          = "/signin-oidc";
    o.SignedOutCallbackPath = "/signout-callback-oidc";
    o.RequireHttpsMetadata  = false; // ⚠️ demo only
    o.Scope.Add("openid");
    o.Scope.Add("profile");
    o.Scope.Add("email");

    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = false,
    };

    // Map Keycloak realm roles → ClaimTypes.Role
    o.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = ctx =>
        {
            if (ctx.Principal?.Identity is not ClaimsIdentity identity) return Task.CompletedTask;
            var realmAccess = identity.FindFirst("realm_access");
            if (realmAccess is null) return Task.CompletedTask;
            try
            {
                using var doc = JsonDocument.Parse(realmAccess.Value);
                if (doc.RootElement.TryGetProperty("roles", out var roles))
                    foreach (var role in roles.EnumerateArray())
                        if (role.GetString() is string r)
                            identity.AddClaim(new Claim(ClaimTypes.Role, r));
            }
            catch { }
            return Task.CompletedTask;
        }
    };
});
```

Also add `app.UseAuthentication();` and `app.UseAuthorization();` before `app.MapControllerRoute(...)`.

**c) Update `appsettings.json`** — add `ClientId` and `ClientSecret` to the existing `Keycloak` section:

```json
"Keycloak": {
  "Authority": "{issuer}",
  "ClientId": "trading-app",
  "ClientSecret": "{secret}"
}
```

### 3.3 — Protect the dashboard and display user info

**a) Add `[Authorize]` to `TradingController.Dashboard`:**

```csharp
using Microsoft.AspNetCore.Authorization;

[Authorize]
public class TradingController : Controller { ... }
```

**b) Update `Views/Trading/Dashboard.cshtml`** to display the connected user's name and roles (reading from `User.Claims`). See `source-complete/trading-app/Views/Trading/Dashboard.cshtml` for the reference implementation.

**c) Add a logout action** to `HomeController`:

```csharp
[HttpGet("/logout")]
public IActionResult Logout() =>
    SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        CookieAuthenticationDefaults.AuthenticationScheme,
        OpenIdConnectDefaults.AuthenticationScheme
    );
```

Restart the trading app. Navigating to `http://localhost:5010` should now redirect you to the Keycloak login page. After login you will see a personalised greeting with your name and roles.

</details>

---

## Task 4 — Run the initial state and verify the authentication behavior

> Estimated time: 5–10 min | Tools: VS Code, browser

**Goal:** Start all three applications from `source-initial/` and confirm the baseline behavior before proceeding to authorization in Lab 5.

**Observable outcome:**
- `http://localhost:5010` redirects to Keycloak when not logged in
- After login, the trading app displays the user's name and realm roles
- `http://localhost:3011/api/stocks` returns 401 without a token and 200 with a valid Bearer token

<details>
<summary>Hint — how do you start all three applications simultaneously in VS Code?</summary>

The workspace includes a task that can launch all three apps at once. Look for the task that corresponds to the initial (unfinished) state.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

Start all three apps from `source-initial/`:

```bash
# Terminal 1
cd source-initial/core-banking-api
dotnet run

# Terminal 2
cd source-initial/trading-app
dotnet run

# Terminal 3
cd source-initial/banking-app
npm install && npm start
```

Or use **Ctrl+Shift+B** in VS Code to run the **Start All (initial)** task.

Open `http://localhost:5010` — the trading app redirects immediately to `/Trading/Dashboard` with no login required. The dashboard shows a placeholder. There is no authentication and no API calls yet.

This is the state you will build on.

</details>

---

## Task 5 — Compare client types and inspect the reference solution

> Estimated time: 5–10 min | Tools: browser, VS Code

**Goal:** Verify that you can run the complete solution and compare the security characteristics of the public SPA (`banking-app`) and the confidential server-side app (`trading-app`).

**Observable outcome:**
- `source-complete/` runs without errors
- The comparison table accurately describes the differences between the two client types
- You can confirm that the trading app's token is stored in an httpOnly cookie and is not visible to browser JavaScript

<details>
<summary>Hint — where can you observe the difference in token storage between the two client types?</summary>

Use the browser's developer tools to inspect storage mechanisms. One application keeps tokens in a cookie-based store; the other uses the browser's session storage.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

At any point you can consult `source-complete/` to see the finished implementation.

To run the complete solution:

- Linux / macOS:

  ```bash
  cd source-complete/core-banking-api && dotnet run
  cd source-complete/trading-app && dotnet run
  cd source-complete/banking-app && npm install && npm start
  ```

- Windows (PowerShell):

  ```powershell
  cd source-complete/core-banking-api; dotnet run
  cd source-complete/trading-app; dotnet run
  cd source-complete/banking-app; npm install; npm start
  ```

Or use the **Start All (complete)** VS Code task.

### Comparing the two client types

After completing both parts, you have two authenticated clients side by side:

| | `banking-app` (Lab 1) | `trading-app` (this lab) |
|--|--|--|
| Client type | Public | Confidential |
| Client secret | None | Yes — stored server-side |
| PKCE | Yes (required) | Not needed |
| Token storage | sessionStorage (browser) | Encrypted httpOnly cookie |
| Token visible to browser | Yes | No |
| API calls | Client-side (CORS required) | None in this lab (added in Lab 5) |

> **Note:** To verify the trading app's token is not visible to JavaScript, open DevTools → Application → Cookies and observe the `trading_app` cookie. It is marked `HttpOnly`. In contrast, the banking app's token is visible in DevTools → Application → Session Storage under the `user` key.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] `GET http://localhost:3011/api/stocks` returns 401 without a token
- [ ] `GET http://localhost:3011/api/stocks` returns 200 with a valid Bearer token from `core-banking-api-consumer`
- [ ] `GET http://localhost:3011/api/accounts` with a `customer` token returns 200
- [ ] `GET http://localhost:3011/api/accounts` with a `trader` token returns 403 `Required role: customer`
- [ ] `GET http://localhost:3011/api/portfolio/repartition` with a `trader` token (client role on `trading-app`) returns 200
- [ ] `GET http://localhost:3011/api/portfolio/repartition` with a `customer` token returns 403 `Required role: trader`
- [ ] `GET http://localhost:3011/api/users` with an `admin` token returns 200
- [ ] `http://localhost:5010` redirects to Keycloak when not logged in
- [ ] After login, the trading app displays the user's name and realm roles
- [ ] Signing out invalidates the session and redirects to the home page
- [ ] The `azp` claim in the service account token contains `core-banking-api-consumer`

**Linux / macOS:**

```bash
# Quick verification for the API
curl -s -o /dev/null -w "%{http_code}" http://localhost:3011/api/stocks
# Expected: 401

curl -s -o /dev/null -w "%{http_code}" http://localhost:3011/api/stocks \
  -H "Authorization: Bearer {access_token}"
# Expected: 200

curl -s http://localhost:3011/api/accounts \
  -H "Authorization: Bearer {customer_token}" | jq .
# Expected: 200 with accounts array

curl -s http://localhost:3011/api/accounts \
  -H "Authorization: Bearer {trader_token}" | jq .
# Expected: 403 {"error":"Forbidden","detail":"Required role: customer"}
```

**Windows (PowerShell):**

```powershell
# Quick verification for the API
curl.exe -s -o NUL -w "%{http_code}" http://localhost:3011/api/stocks
# Expected: 401

curl.exe -s -o NUL -w "%{http_code}" http://localhost:3011/api/stocks `
  -H "Authorization: Bearer {access_token}"
# Expected: 200

curl.exe -s http://localhost:3011/api/accounts `
  -H "Authorization: Bearer {customer_token}" | ConvertFrom-Json | ConvertTo-Json -Depth 10
# Expected: 200 with accounts array

curl.exe -s http://localhost:3011/api/accounts `
  -H "Authorization: Bearer {trader_token}" | ConvertFrom-Json | ConvertTo-Json -Depth 10
# Expected: 403 {"error":"Forbidden","detail":"Required role: customer"}
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Implement PKCE manually in a small JavaScript client to understand how the code challenge and verifier are generated, then compare with the behavior of `oidc-client-ts`.
- Configure the `core-banking-api` to validate the audience claim (`aud`) and reject tokens that do not include the API's client ID — this is the first step of Lab 5.
- Add a second resource server and configure Keycloak so that tokens issued for one API are rejected by the other, demonstrating audience isolation in a microservices architecture.
