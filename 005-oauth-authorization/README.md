# Lab 5 — Module 2: OAuth Authorization: Audience and Scopes

This lab demonstrates three independent strategies for restricting what an access token authorises: **audience binding** (only tokens explicitly issued for this API are accepted), **scope-based consent** (users control which applications can request sensitive data on their behalf), and **client-level role filtering** via Full Scope Allowed (tokens contain only the roles the client is permitted to see). Role-based access control is already in place from Lab 4; here each strategy adds a distinct constraint on top of a valid, role-bearing token. By the end, you will have demonstrated how these layers interact in a multi-application architecture — and why no single mechanism is sufficient on its own.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running at `http://localhost:8080` (or the shared cloud instance URL provided by the instructor)
- [ ] The `{realm}` realm is accessible with users `alice`, `customer-user`, `trader-user`, and `admin-user`
- [ ] .NET 10 SDK and Node.js 20+ are installed
- [ ] Lab 4 is completed, or `source-initial/` is available (it is Lab 4's finished state)

If any prerequisite is missing, complete [Lab 4] before continuing.

**Keycloak endpoints — replace `{realm}` with your realm name throughout this lab:**

| | Shared cloud instance | Local Keycloak |
|---|---|---|
| Admin console | `https://labs-sso-admin.keycloak.academy/admin/{realm}/console/` | `http://localhost:8080/admin/{realm}/console/` |
| OIDC issuer (`{issuer}`) | `https://labs-sso.keycloak.academy/realms/{realm}` | `http://localhost:8080/realms/{realm}` |

**Test users:**

| Username | Password | Roles |
|----------|----------|-------|
| `customer-user` | `customer` | `customer` |
| `trader-user` | `trader` | `trader` (client role: trading-app) |
| `admin-user` | `admin` | `admin` |
| `alice` | `alice` | `customer`, `admin` |

---

## Background

### Realm Setup

If you are working on a provided cloud realm, skip this section — everything below is already configured. If you are running your own Keycloak instance, complete these steps before Task 1.

#### 1 — Realm roles

Go to **Realm roles → Create role** and create the following roles:

| Role name | Description |
|-----------|-------------|
| `customer` | Access to account, transaction, and balance endpoints |
| `admin` | Access to user management endpoints |

> The default `user` and `offline_access` roles are created automatically — no action needed.

#### 2 — Clients

##### `banking-app` — public client (React SPA, port 3010)

1. **Clients → Create client**
   - Client type: `OpenID Connect`
   - Client ID: `banking-app`
2. **Capability config**
   - Client authentication: **OFF** (public client)
   - Standard flow: **ON**
3. **Login settings**
   - Valid redirect URIs: `http://localhost:3010/callback`
   - Valid post-logout redirect URIs: `http://localhost:3010/`
   - Web origins: `http://localhost:3010`
4. Save

##### `trading-app` — confidential client (ASP.NET MVC, port 5010)

1. **Clients → Create client**
   - Client type: `OpenID Connect`
   - Client ID: `trading-app`
2. **Capability config**
   - Client authentication: **ON** (confidential client)
   - Standard flow: **ON**
3. **Login settings**
   - Valid redirect URIs: `http://localhost:5010/signin-oidc`
   - Valid post-logout redirect URIs: `http://localhost:5010/signout-callback-oidc`
   - Web origins: `http://localhost:5010`
4. Save
5. **Credentials** tab — copy the client secret and update `trading-app/appsettings.json`

##### `trading-app` client role — `trader`

1. **Clients → trading-app → Roles** tab → **Create role**
   - Role name: `trader`
2. Save

##### `trading-app-service` — service account client (machine-to-machine)

1. **Clients → Create client**
   - Client type: `OpenID Connect`
   - Client ID: `trading-app-service`
2. **Capability config**
   - Client authentication: **ON** (confidential client)
   - Standard flow: **OFF**, Direct access grants: **OFF**
   - Service accounts roles: **ON** (client_credentials only)
3. **Login settings** — no redirect URIs needed
4. Save
5. **Credentials** tab — copy the client secret and add it to `trading-app/appsettings.json` under `StocksClient.ClientSecret`

**Audience mapper (required):** The `core-banking-api` validates `aud: "core-banking-api"` on every token. The service token from `trading-app-service` must include this audience or the API returns 401.
1. **Clients → trading-app-service → Client scopes** tab → click `trading-app-service-dedicated`
2. **Mappers → Configure a new mapper → Audience**
3. Set **Included Client Audience** = `core-banking-api`, name = `core-banking-api audience`
4. Save

#### 3 — Test users

Go to **Users → Create new user** for each user below. After saving, go to the **Credentials** tab → **Set password** (disable Temporary).

| Username | Password | Role assignments |
|----------|----------|-----------------|
| `customer-user` | `customer` | Realm role: `customer` |
| `admin-user` | `admin` | Realm role: `admin` |
| `alice` | `alice` | Realm roles: `customer` + `admin` |
| `trader-user` | `trader` | Client role `trader` on `trading-app` |

**Assigning realm roles:** Users → select user → **Role mapping** tab → **Assign role** → filter by realm roles → select and assign.

**Assigning the `trader` client role:** Users → `trader-user` → **Role mapping** tab → **Assign role** → filter by clients → select `trading-app` → assign `trader`.

### Authorization concepts

This lab introduces three authorization layers:

| Layer | What it controls | Where enforced |
|---|---|---|
| **Audience** | Whether the token was issued for this specific API | JWT middleware (`ValidateAudience`) |
| **Roles** | What the user is allowed to do | Endpoint handler (`IsInRole`) |
| **Scopes** | What the user has consented to let the application do | Endpoint handler (`HasScope`) |

The `aud` (audience) claim identifies the intended recipient of a token. A token must include the API's identifier in `aud` before the middleware accepts it. This prevents tokens issued for one service from being replayed against another.

Roles represent what a user is allowed to do. Keycloak stores them in `realm_access.roles` as a nested JSON object. ASP.NET's built-in `IsInRole()` expects roles in a `ClaimTypes.Role` claim — you must map them manually.

OAuth scopes represent *permissions the user grants to an application*. Unlike roles (assigned by an admin), a scope requires the user's explicit approval at login.

By default, Keycloak creates clients with **Full Scope Allowed = on**. This means every role the user holds appears in every access token, regardless of which service the token is intended for. Disabling it is a production requirement — it enforces least privilege.

**Key principle:** Roles in token = (roles assigned to user) ∩ (roles the client is allowed to see)

> **Note:** `RequireHttpsMetadata = false` appears in code snippets for local development only. Always enforce HTTPS in production.

---

## Task 1 — Restrict API access with audience validation

> Estimated time: 15–20 min | Tools: admin console, curl, VS Code

**Goal:** Configure the Audience mapper in Keycloak and enable global audience validation in `core-banking-api` so that only tokens explicitly issued for the API are accepted.

**Observable outcome:**
- Tokens from `banking-app` and `trading-app` include `core-banking-api` in the `aud` claim
- Tokens from a client without the audience mapper are rejected with 401 at the middleware level
- `GET /api/stocks` with no token returns 401

<details>
<summary>Hint — where in Keycloak do you configure what audience claim is added to a token?</summary>

The audience claim is controlled by a protocol mapper. Look in the client-specific scope configuration for a mapper type that lets you specify which client should appear in the `aud` claim.

</details>

<details>
<summary>Hint — what change in the ASP.NET API tells the JWT middleware to reject tokens that lack the expected audience?</summary>

In the JWT Bearer options, there is a validation parameter that controls whether the audience is checked and which values are considered valid.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

### 1.1 — Configure the Audience mapper in Keycloak

By default, Keycloak does not add your API's identifier to the `aud` claim. You need to add an Audience Protocol Mapper for each client that calls the API:

**For `banking-app`:**
1. Admin console → **Clients → banking-app → Client scopes** tab → click `banking-app-dedicated`
2. **Mappers → Configure a new mapper → Audience**
3. Set **Included Client Audience** = `core-banking-api`, name = `core-banking-api audience`
4. Save

**For `trading-app`:**
Repeat the same steps on the `trading-app-dedicated` client scope.

Log in to either app and inspect the access token at [jwt.io](https://jwt.io). The `aud` array should now contain `core-banking-api`.

### 1.2 — Enable global audience validation in core-banking-api

Open `source-initial/core-banking-api/Program.cs`. Change `TokenValidationParameters` to validate the audience:

```csharp
o.TokenValidationParameters = new TokenValidationParameters
{
    // Accept only tokens that list "core-banking-api" as an intended recipient.
    ValidateAudience = true,
    ValidAudiences   = ["core-banking-api"],
};
```

> With `ValidateAudience = true`, any token that does not include `core-banking-api` in its `aud` claim is rejected with 401 at the middleware level — before your endpoint code runs.

### 1.3 — Verify

**macOS / Linux:**

```bash
# Token from banking-app or trading-app (aud includes "core-banking-api")
# → 200 OK
curl http://localhost:3011/api/stocks \
  -H "Authorization: Bearer {valid-token-with-audience}"

# Token from a client without the audience mapper (aud missing "core-banking-api")
# → 401 Unauthorized {"error":"Unauthorized","detail":"Token audience is invalid..."}
curl http://localhost:3011/api/stocks \
  -H "Authorization: Bearer {wrong-audience-token}"

# No token → 401 Unauthorized
curl http://localhost:3011/api/stocks
```

**Windows (PowerShell):**

```powershell
# Token from banking-app or trading-app (aud includes "core-banking-api")
# → 200 OK
curl.exe http://localhost:3011/api/stocks `
  -H "Authorization: Bearer {valid-token-with-audience}"

# Token from a client without the audience mapper (aud missing "core-banking-api")
# → 401 Unauthorized {"error":"Unauthorized","detail":"Token audience is invalid..."}
curl.exe http://localhost:3011/api/stocks `
  -H "Authorization: Bearer {wrong-audience-token}"

# No token → 401 Unauthorized
curl.exe http://localhost:3011/api/stocks
```

</details>

---

## Task 2 — Implement user-delegated permissions with OAuth scopes and consent

> Estimated time: 30–35 min | Tools: admin console, VS Code, browser

**Goal:** Add a scope-and-role-protected balances endpoint, create a new public client (`my-savings-app`) that requires explicit user consent for the `read:accounts` scope, and build the `my-savings` React SPA from scratch to demonstrate the consent flow.

**Observable outcome:**
- `GET /api/balances` with `admin` token (no `customer` role) → 403 `Required role: customer`
- `GET /api/balances` with `customer` token but no `read:accounts` scope → 403 `Required scope: read:accounts`
- `my-savings`: first login shows "Grant access" button, no balance data
- Clicking "Grant access" displays the Keycloak consent screen
- After approving: balance data appears; `user.scope` in sessionStorage contains `read:accounts`; `GET /api/balances` → 200
- `banking-app`: Balances card appears without a consent screen; `customer` role + `read:accounts` scope both present in token → 200

<details>
<summary>Hint — what Keycloak client setting controls whether a user sees a consent screen before a scope is granted?</summary>

There is a toggle on the client settings page that determines whether the user must explicitly approve access for each scope. When enabled, Keycloak will prompt the user even if they already have an active session.

</details>

<details>
<summary>Hint — how can you avoid duplicating the same audience mapper across every client that calls the same API?</summary>

Instead of adding the mapper individually to each client's dedicated scope, you can define it once in a reusable scope and assign that scope to any client that needs it. This pattern mirrors how shared scopes like `read:accounts` work.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

### 2.1 — Add scope enforcement to the balances endpoint in core-banking-api

Open `source-initial/core-banking-api/Program.cs`. The `/api/balances` endpoint from Lab 4 enforces the `customer` role. Extend it to also require the `read:accounts` scope:

```csharp
app.MapGet("/api/balances", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer"))  return Forbidden("customer");
    if (!HasScope(ctx, "read:accounts")) return ForbiddenScope("read:accounts");
    return Results.Ok(new { balances = new { checking = 1500, savings = 5000 } });
}).RequireAuthorization();
```

Also add the two helper functions after the existing `Forbidden` helper at the bottom of the file:

```csharp
// Returns true if the token's space-delimited "scope" claim contains the given scope.
static bool HasScope(HttpContext ctx, string scope)
{
    var scopeClaim = ctx.User.FindFirst("scope")?.Value
                  ?? ctx.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
    return scopeClaim is not null && scopeClaim.Split(' ').Contains(scope);
}

static IResult ForbiddenScope(string scope) =>
    Results.Json(new { error = "Forbidden", detail = $"Required scope: {scope}" }, statusCode: 403);
```

Also add port 3020 (the upcoming `my-savings` app) to the CORS origins:

```csharp
.WithOrigins("http://localhost:3010", "http://localhost:3020", "http://localhost:5010")
```

**Verify:**
- `customer` token **without** `read:accounts` scope → 403 `Required scope: read:accounts`
- `admin` token (no `customer` role) → 403 `Required role: customer`
- `customer` token **with** `read:accounts` scope → 200

> **Note:** At this stage you can obtain a token from `banking-app` to test the 200 case — you will update that app to request `read:accounts` in Step 2.5. After Step 2.3 you will also confirm this with `my-savings` after explicit consent.

This endpoint now requires **two independent checks**: a user permission (role) and an application-level permission (scope). The role is assigned by an administrator; the scope is granted either automatically for trusted first-party apps, or through explicit user consent for third-party apps — as you will see in the steps that follow.

### 2.2 — Configure Keycloak for my-savings-app

**a) Configure the `read:accounts` client scope for consent:**
1. Admin console → **Client Scopes → read:accounts**
2. **Settings** tab: set **Display on consent screen = ON**, **Consent text** = `Read your account balance`
3. Save

**b) Create the `my-savings-app` client:**
1. **Clients → Create client**, Client type = OpenID Connect, Client ID = `my-savings-app`
2. **Capability config**: Client authentication = OFF (public client), Standard flow only
3. **Login settings**: Valid redirect URIs = `http://localhost:3020/callback`, Post logout = `http://localhost:3020/`
4. Save
5. **Client details → Settings → Consent Required = ON**
6. **Client scopes** tab → **Add client scope** → add `read:accounts` as **Optional**

**c) Use a shared client scope for the audience mapper — don't repeat yourself**

You've now created three clients that all call `core-banking-api`: `banking-app`, `trading-app`, and `my-savings-app`. In Part 1 you added the same Audience Protocol Mapper individually to the first two. Adding it again to `my-savings-app` would work — but this pattern doesn't scale: every new client that calls the API requires the same manual step, and a forgotten mapper produces a 401 with no obvious cause.

The right solution is a **shared client scope**: define the audience mapper once, then assign the scope to any client that needs it. This is exactly how `read:accounts` works — it's a scope that represents access to a specific resource, and clients opt in by being assigned it. The audience mapper for `core-banking-api` deserves the same treatment.

**Create the shared scope:**
1. Admin console → **Client Scopes → Create client scope**
2. Name: `core-banking-api-audience`, Type: **Default**
3. Inside the new scope → **Mappers** tab → **Mappers → Configure a new mapper → Audience**
4. Set **Included Client Audience** = `core-banking-api`, name = `core-banking-api audience`
5. Save

**Assign it to `my-savings-app` as Optional (not Default):**
- **Clients → my-savings-app → Client scopes** tab → **Add client scope** → select `core-banking-api-audience` → add as **Optional**

Adding it as **Optional** (not Default) means the audience claim is *not* included in the initial login token — the app has no reason to call the API at that point. It is included only when `grantBalanceAccess()` explicitly requests `core-banking-api-audience` in the scope parameter alongside `read:accounts`.

Log in to `my-savings` and inspect the initial token at [jwt.io](https://jwt.io) — `aud` should **not** contain `core-banking-api`. After clicking "Grant access to balance", inspect the new token — `aud` should now include `core-banking-api`.

> You could also retroactively assign `core-banking-api-audience` to `banking-app` and `trading-app` and remove their per-client mappers — the result is identical.

### 2.3 — Build my-savings from scratch

Create `source-initial/my-savings/` with the following files. The app is modelled after `banking-app` — same routing structure, same auth context.

**`package.json`** (port 3020):

```json
{
  "name": "my-savings",
  "version": "1.0.0",
  "scripts": {
    "start": "parcel src/index.html --port 3020",
    "build": "parcel build src/index.html"
  },
  "dependencies": {
    "oidc-client-ts": "^3.1.0",
    "react": "^18.3.1",
    "react-dom": "^18.3.1",
    "react-router-dom": "^6.26.1"
  },
  "devDependencies": { "parcel": "^2.12.0" }
}
```

**`.env`:**

```
KC_URL=<Keycloak root URL — https://labs-sso.keycloak.academy for cloud, http://localhost:8080 for local>
KC_REALM=<your-realm>
CLIENT_ID=my-savings-app
API_BASE_URL=http://localhost:3011
```

**`src/auth/userManager.js`:**

```javascript
import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

export const userManager = new UserManager({
  authority: `${process.env.KC_URL}/realms/${process.env.KC_REALM}`,
  client_id: process.env.CLIENT_ID,
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',
  scope: 'openid profile email',        // read:accounts is NOT included by default
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: false,
});

export const login  = () => userManager.signinRedirect();
export const logout = () => userManager.signoutRedirect();

// Re-authenticate requesting read:accounts + the core-banking-api audience.
//
// How it works:
// 1. signinRedirect() sends the user to Keycloak with scope including read:accounts
//    and core-banking-api-audience (an optional scope on my-savings-app).
// 2. Keycloak detects an existing session — no re-login required.
// 3. Keycloak checks which requested scopes have not yet been approved:
//    profile/email were consented at initial login, so only read:accounts is new.
//    core-banking-api-audience has Display on consent screen = OFF, so it is
//    added silently without appearing on the consent screen.
// 4. OAUTH_GRANT fires only for read:accounts — the consent screen shows a single
//    item: "Read your account balance". The user clicks Yes.
// 5. Keycloak records the approval, issues a new code, and redirects to /callback.
// 6. The new access token contains read:accounts in scope and core-banking-api in aud,
//    satisfying both checks in the API (HasScope + audience validation).
export const grantBalanceAccess = () =>
  userManager.signinRedirect({
    scope: 'openid profile email read:accounts core-banking-api-audience',
  });
```

**`src/auth/AuthContext.jsx`** — identical to `banking-app/src/auth/AuthContext.jsx`.

**`src/pages/LoginPage.jsx`** — same as `banking-app`, change the title to "My Savings".

**`src/pages/CallbackPage.jsx`** — identical to `banking-app`.

**`src/pages/DashboardPage.jsx`:**

```jsx
import React, { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { logout, grantBalanceAccess } from '../auth/userManager.js';

export default function DashboardPage() {
  const { user } = useAuth();
  const [balance, setBalance] = useState(null);
  const [error, setError]     = useState(null);

  // oidc-client-ts stores the granted scope string on user.scope.
  // read:accounts only appears here after the user has approved it on the consent screen.
  const hasScope = (user?.scope ?? '').split(' ').includes('read:accounts');
  const roles    = user?.profile?.realm_access?.roles ?? [];

  useEffect(() => {
    if (!hasScope) return;
    fetch(`${process.env.API_BASE_URL}/api/balances`, {
      headers: { Authorization: `Bearer ${user.access_token}` },
    })
      .then(r => r.json())
      .then(data => setBalance(data.balances))
      .catch(e => setError(e.message));
  }, [hasScope, user?.access_token]);

  return (
    <div className="container">
      {/* Profile card */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1>{user?.profile?.name ?? user?.profile?.preferred_username}</h1>
          <p style={{ color: '#888', fontSize: '0.9rem', marginTop: 4 }}>{user?.profile?.email}</p>
          <div style={{ marginTop: 8 }}>
            {roles.map(r => <span key={r} className="badge">{r}</span>)}
          </div>
        </div>
        <button className="secondary" onClick={logout}>Sign out</button>
      </div>

      {/* Balance card */}
      <div className="card">
        <h2>Account Balance</h2>
        {hasScope ? (
          error   ? <p className="error">{error}</p>
          : balance ? (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0', borderBottom: '1px solid #eee' }}>
                <span>Checking account</span><strong>${balance.checking.toLocaleString()}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0' }}>
                <span>Savings account</span><strong>${balance.savings.toLocaleString()}</strong>
              </div>
            </>
          ) : <p style={{ color: '#888' }}>Loading…</p>
        ) : (
          <div style={{ background: '#fffde7', border: '1px solid #ffe082', borderRadius: 6, padding: 14 }}>
            <p style={{ marginBottom: 12, fontSize: '0.9rem', color: '#555' }}>
              <strong>My Savings</strong> does not yet have permission to read your balance.
              Click below — Keycloak will show you exactly what you are approving.
            </p>
            <button onClick={grantBalanceAccess}>Grant access to balance</button>
          </div>
        )}
      </div>
    </div>
  );
}
```

**`src/App.jsx`**, **`src/index.jsx`**, **`src/index.html`** — same structure as `banking-app`. Update the page `<title>` to "My Savings".

Start the app:

- macOS / Linux:

  ```bash
  cd source-initial/my-savings
  npm install && npm start
  ```

- Windows (PowerShell):

  ```powershell
  cd source-initial/my-savings
  npm install; npm start
  ```

### 2.4 — Walk through the consent flow

1. Open `http://localhost:3020` → sign in as any user
2. Dashboard shows your profile and a "Grant access to balance" button — no balance data
3. Click **Grant access to balance** → Keycloak shows the consent screen:
   > *My Savings is requesting access to: Read your account balance*
4. Click **Yes** → redirected back to the dashboard
5. Balance data appears: `Checking: $1,500 · Savings: $5,000`

Open DevTools → **Application → Session Storage** — inspect `user` key. The `scope` field now contains `read:accounts`. Open DevTools → **Network** — the `/api/balances` request carries `Authorization: Bearer ...`.

Notice what just happened: the API rejected the request until the *application* had been granted `read:accounts` by the user. The role (`customer`) was necessary but not sufficient — the scope provides an independent, user-controlled gate.

### 2.5 — Add read:accounts to banking-app (first-party, no consent)

**First, assign `read:accounts` to `banking-app` in Keycloak:**

Admin console → **Clients → banking-app → Client scopes** tab → **Add client scope** → select `read:accounts` → add as **Optional**.

> The scope must be assigned to the client before Keycloak will honour requests for it. Without this step the scope is silently omitted from the token and `/api/balances` returns 403 even if the app requests it.

**Then, update the app to request the scope:**

Update `source-initial/banking-app/src/auth/userManager.js` to request `read:accounts` in the default scope:

```javascript
scope: 'openid profile email read:accounts',
```

Because `banking-app` has **Consent Required = OFF** in Keycloak, this scope is granted automatically at login — no consent screen appears. This is the correct behaviour for a first-party application that the organisation fully controls and trusts.

Add a `BalancesPanel` component to the dashboard (see `source-complete/banking-app/src/components/BalancesPanel.jsx` for the reference implementation) and wire it into `DashboardPage` alongside the existing `TransactionsPanel`.

**Verify:** log in as `customer-user` → the Balances card appears immediately with no consent screen. Log in as `trader-user` (no `customer` role) → 403 `Required role: customer`.

> **Compare:** `my-savings` required the user to click "Grant access" before `/api/balances` returned data. `banking-app` receives the same scope automatically. The resource server enforces `read:accounts` identically in both cases — the difference is only in how the scope reaches the token.

</details>

---

## Task 3 — Disable Full Scope Allowed to enforce least privilege

> Estimated time: 15–20 min | Tools: admin console, browser, curl

**Goal:** Disable Full Scope Allowed on `banking-app`, observe that tokens no longer contain any roles, and then re-grant exactly the `customer` role so that the intersection principle is enforced.

**Observable outcome:**
- With Full Scope Allowed ON: `alice`'s token contains both `customer` and `admin`
- With Full Scope Allowed OFF: `realm_access.roles` is empty; `/api/accounts` returns 403
- After assigning only `customer` to the client: token contains `customer` only
- `GET /api/accounts` → 200; `GET /api/portfolio/repartition` → 403 (intersection principle confirmed)

<details>
<summary>Hint — what client setting controls whether all of a user's roles appear in the access token regardless of which application requested it?</summary>

Look in the client's scope configuration for a toggle that defaults to ON. When turned OFF, the token contains only the intersection of the user's roles and the roles the client is explicitly allowed to see.

</details>

<details>
<summary>Hint — after disabling the broad role inclusion, how do you selectively re-introduce only the roles a specific client needs?</summary>

You can assign roles to a client either directly through its client scopes tab or by creating a reusable client scope that carries the role assignment. The latter is preferred when multiple clients need the same set of roles.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

### 3.1 — Observe the problem

1. Log in to `banking-app` as `alice` (roles: `customer` and `admin`)
2. Inspect the access token at [jwt.io](https://jwt.io)
3. `realm_access.roles` contains both `customer` **and** `admin` — even though `banking-app` only needs `customer` to call `/api/accounts`

A leaked `banking-app` token grants admin-level access to the API. That is not intended.

### 3.2 — Disable Full Scope Allowed

1. Admin console → **Clients → banking-app**
2. **Client scopes** tab → turn off **Full Scope Allowed**
3. Log in again as `alice` and inspect the new token: `realm_access.roles` is now empty
4. Test: `GET /api/accounts` with this token → 403 (customer role missing from token)

The app is broken. The next step fixes it correctly.

### 3.3 — Re-grant exactly what the client needs

**Option A — add a role scope directly:**

1. **Clients → banking-app → Client scopes tab → Add client scope**
2. Select the `customer` realm role scope (or create one if it doesn't exist: **Client Scopes → Create**, type = Roles, assign the `customer` realm role under the Scope tab)
3. Log in again: token now contains `customer` only — `admin` is absent

**Option B — dedicated client scope (reusable across multiple clients):**

1. **Client Scopes → Create client scope**, name = `customer`, type = optional
2. Inside the scope → **Scope** tab → **Assign role** → select realm role `customer`
3. **Clients → banking-app → Client scopes → Add client scope** → add `customer` as optional
4. Request `scope=customer` at login — only the `customer` role will appear in the token

### 3.4 — Verify the intersection principle

With Full Scope Allowed OFF and only `customer` assigned to `banking-app`:

| User | User's roles | Token roles (banking-app) | `/api/accounts` | `/api/users` |
|------|-------------|-------------------------------|-----------------|--------------|
| alice | customer, admin | customer | 200 | 403 |
| customer-user | customer | customer | 200 | 403 |
| admin-user | admin | *(empty)* | 403 | 403 |

The intersection principle is enforced: `alice` holds the `admin` role, but `banking-app` is not allowed to see it, so it never reaches the API.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] Token from `banking-app` or `trading-app` includes `core-banking-api` in `aud` claim
- [ ] Token without `core-banking-api` audience → 401 at middleware level (before endpoint handler)
- [ ] `GET /api/balances` with `admin` token (no `customer` role) → 403 `Required role: customer`
- [ ] `GET /api/balances` with `customer` token but no `read:accounts` scope → 403 `Required scope: read:accounts`
- [ ] `my-savings`: first login shows "Grant access" button, no balance data
- [ ] Clicking "Grant access" displays the Keycloak consent screen
- [ ] After approving: balance data appears; `user.scope` in sessionStorage contains `read:accounts`; `GET /api/balances` → 200
- [ ] `banking-app` (Step 2.5): Balances card appears without a consent screen; `customer` role + `read:accounts` scope both present in token → 200
- [ ] With Full Scope Allowed ON: `alice`'s token contains both `customer` and `admin`
- [ ] With Full Scope Allowed OFF: `realm_access.roles` is empty; `/api/accounts` returns 403
- [ ] After assigning only `customer` to the client: token contains `customer` only
- [ ] `GET /api/accounts` → 200; `GET /api/portfolio/repartition` → 403 (intersection principle confirmed)

**macOS / Linux:**

```bash
# Quick API verification
curl -s http://localhost:3011/api/accounts -H "Authorization: Bearer {customer_token}" | jq .
curl -s http://localhost:3011/api/balances -H "Authorization: Bearer {customer_token}" | jq .
```

**Windows (PowerShell):**

```powershell
# Quick API verification (jq must be installed; otherwise pipe to ConvertFrom-Json | ConvertTo-Json)
curl.exe -s http://localhost:3011/api/accounts -H "Authorization: Bearer {customer_token}" | jq .
curl.exe -s http://localhost:3011/api/balances -H "Authorization: Bearer {customer_token}" | jq .
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Implement a custom protocol mapper in Java that adds a dynamic claim to the access token based on an external API call, deploy it to `$KC_HOME/providers/`, and verify it appears in the token.
- Replace the `read:accounts` scope with a User-Managed Access (UMA) resource set and policy, so that the user can grant or revoke balance access from the Keycloak account console rather than through the application consent screen.
- Write an integration test using `WebApplicationFactory` that validates each endpoint returns the correct status code (401, 403, 200) for tokens with different combinations of audience, roles, and scopes.
