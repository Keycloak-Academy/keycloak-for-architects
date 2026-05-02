# Keycloak Demo Apps

Three real-world apps sharing a single resource server and a Keycloak realm. Together they demonstrate every mechanism available to restrict what a holder of an access token can actually do.

> **Prerequisites:** Keycloak running on `http://localhost:8080` with your realm configured (provided by the instructor or created manually).

---

## Quick Start

Open `demo.code-workspace` in VS Code, then press **`Ctrl+Shift+B`** → *Start All Demo Apps*.

Or start each app manually:

```bash
# 1 — Resource server
cd core-banking-api && dotnet run

# 2 — React SPA (first-party)
cd banking-app && npm install && npm start

# 3 — Trading web app
cd trading-app && dotnet run

# 4 — React SPA (third-party, consent-gated)
cd my-savings && npm install && npm start
```

---

## Test users

| Username | Password | Roles |
|---|---|---|
| `customer-user` | `customer` | customer |
| `trader-user` | `trader` | trader (client role: trading-app) |
| `admin-user` | `admin` | admin |
| `alice` | `alice` | customer, admin |

---

## How Access Is Restricted: 5 Layers

A JWT access token can be constrained along five independent axes. The demo endpoints cover all of them — working through the table below, each endpoint adds one layer of restriction on top of the previous ones.

### Layer 0 — No restriction (public)

`GET /api/info` requires no token at all. Any HTTP client can call it.

**Without this layer:** every API call would require authentication even for public information.

---

### Layer 1 — Token validity

`GET /api/stocks` requires a valid Bearer token. The JwtBearer middleware validates:
- **Signature** — using Keycloak's public key fetched from the JWKS endpoint at startup
- **Expiry** (`exp` claim) — tokens are time-limited
- **Issuer** (`iss` claim) — must match the configured Keycloak authority

**Without this layer:** anyone could call protected endpoints without authenticating.

---

### Layer 2 — Audience binding

All protected endpoints also require the `aud` claim to contain `core-banking-api`.

```csharp
ValidateAudience = true,
ValidAudiences   = ["core-banking-api"],
```

This is enforced globally by the middleware — before any endpoint handler runs.

**Without this layer:** a token issued for `banking-app-spa` could be replayed against any other service that accepts tokens from the same Keycloak realm.

---

### Layer 3 — Scope (user-delegated permission)

`GET /api/balances` additionally requires the `scope` claim to contain `read:accounts`.

```csharp
if (!HasScope(ctx, "read:accounts")) return ForbiddenScope("read:accounts");
```

Scopes represent *what the application is permitted to do on behalf of the user*. Whether a scope lands in the token depends on the client type:

| Client | Consent Required | How `read:accounts` is granted |
|--------|-----------------|-------------------------------|
| `banking-app-spa` | OFF (first-party) | Automatically at login — no consent screen |
| `my-savings-app` | ON (third-party) | User must approve on Keycloak's consent screen |

The resource server validates the scope claim identically in both cases. The difference is in *how* the scope reaches the token, not in how it is enforced.

**Without this layer:** any authenticated `customer` user could read balances regardless of whether the calling application has been granted that permission.

---

### Layer 4 — Realm role (user permission)

`GET /api/accounts`, `GET /api/transactions`, `GET /api/users` check for realm roles:

```csharp
if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
```

Realm roles are assigned by an administrator and represent *what the user is allowed to do*. They are mapped from `realm_access.roles` to `ClaimTypes.Role` in the `OnTokenValidated` event.

**Without this layer:** any authenticated user — regardless of their assigned role — could access every endpoint.

---

### Layer 5 — Client role (application-level permission)

`GET /api/portfolio/repartition` checks for a client role scoped to the `trading-app` client:

```csharp
if (!ctx.User.IsInRole("trader")) return Forbidden("trader");
```

Client roles are assigned by an administrator to specific users *within a specific client application*. They are mapped from `resource_access["trading-app"].roles`.

**Without this layer:** a user could request a token from `trading-app` but call portfolio endpoints from any other context.

---

## Endpoints

Ordered from fewest to most restrictions. Each row lists the layers that apply.

| Endpoint | Layers | Protection | Test with |
|---|---|---|---|
| `GET /api/info` | 0 | Public | anyone |
| `GET /api/stocks` | 1+2 | Valid token + audience | any authenticated client |
| `GET /api/accounts` | 1+2+4 | Role: `customer` | `customer-user` |
| `GET /api/transactions/{id}` | 1+2+4 | Role: `customer` | `customer-user` |
| `GET /api/transactions` | 1+2+4 | Role: `customer` | `customer-user` |
| `GET /api/users` | 1+2+4 | Role: `admin` | `admin-user` |
| `GET /api/users/{id}/accounts` | 1+2+4 | Role: `admin` | `admin-user` |
| `GET /api/portfolio/repartition` | 1+2+5 | Client role: `trader` (trading-app) | `trader-user` |
| **`GET /api/balances`** | **1+2+3+4** | **Role: `customer` + Scope: `read:accounts`** | `customer-user` after scope granted |

`/api/balances` is the showcase endpoint — it is the only one that requires both a user role (admin-assigned) and a scope (application-level permission, consent-gated for third-party apps).

CORS is restricted to `localhost:3010`, `localhost:3020`, and `localhost:5010`.

---

## The Apps

### `core-banking-api` — Resource Server (port 3011)

C# ASP.NET Core Minimal API. Validates Bearer tokens using Keycloak's JWKS (RS256). Returns hardcoded fake data. Enforces all 5 restriction layers across its endpoints.

**Key files:**
- `Program.cs` — JWT setup, role mapping, endpoint definitions, `HasScope`/`Forbidden` helpers

---

### `banking-app` — React SPA (port 3010)

**Client type: public** — no client secret. Authorization code + PKCE.

```
http://localhost:3010
```

Requests `read:accounts` in its default scope — Keycloak grants it automatically because `banking-app-spa` has **Consent Required = OFF** (first-party, trusted app). No consent screen appears.

```
User clicks Login
  → browser redirects to Keycloak with code_challenge (PKCE)
  → Keycloak authenticates, redirects back with ?code=...
  → /callback exchanges code for tokens (code_verifier)
  → tokens stored in sessionStorage
  → dashboard calls core-banking-api with Bearer token
```

**Key files:**
- `src/auth/userManager.js` — `oidc-client-ts` config; scope includes `read:accounts`
- `src/pages/CallbackPage.jsx` — handles the redirect callback
- `src/api/bankingApi.js` — injects `Authorization: Bearer` header
- `src/components/BalancesPanel.jsx` — calls `/api/balances`

**Environment (`.env`):**
```
KC_URL=http://localhost:8080
KC_REALM=<your-realm>
CLIENT_ID=banking-app-spa
API_BASE_URL=http://localhost:3011
```

> ⚠️ Tokens in `sessionStorage` (XSS risk). Production SPAs should use the BFF pattern with httpOnly cookies.

---

### `trading-app` — ASP.NET Core MVC (port 5010)

**Client type: confidential** — client secret stored server-side.

```
http://localhost:5010
```

```
User clicks Login
  → ASP.NET OIDC middleware redirects to Keycloak (state + nonce)
  → Keycloak redirects to /signin-oidc
  → middleware exchanges code, validates signature + nonce
  → access_token stored in encrypted session cookie (SaveTokens=true)
  → TradingController retrieves token via GetTokenAsync("access_token")
  → server calls core-banking-api with Bearer token (server-to-server, no CORS)
```

**Key files:**
- `Program.cs` — OIDC middleware, `OnTokenValidated` role mapping
- `Controllers/TradingController.cs` — `GetTokenAsync` + API calls
- `Services/BankingApiService.cs` — typed `HttpClient` forwarding Bearer token

**Configuration (`appsettings.json`):**
```json
{
  "Keycloak": {
    "Authority":    "http://localhost:8080/realms/<your-realm>",
    "ClientId":     "trading-app",
    "ClientSecret": "trading-app-secret"
  },
  "StocksClient": {
    "ClientId":     "trading-app-service",
    "ClientSecret": "<service-client-secret>"
  },
  "BankingApi": { "BaseUrl": "http://localhost:3011" }
}
```

The dashboard makes two types of API calls with different token sources:
- **`/api/stocks`** — obtained via **client credentials** using the dedicated `trading-app-service` client (no user identity). `StocksClient` credentials are posted to the token endpoint; the resulting service token is used for the request.
- **`/api/portfolio/repartition`** — the user's own access token (from the encrypted session cookie) is forwarded directly. The API validates the `trader` client role on that token.

**Keycloak setup for `trading-app-service`:** Create a confidential client with **Service accounts roles** enabled and all standard flows disabled. Add an **Audience** mapper on its dedicated client scope pointing to `core-banking-api` — required because the API validates `aud: "core-banking-api"` on every token.

> ⚠️ Client secrets are hardcoded. Production: use environment variables or a secrets manager.

---

### `my-savings` — React SPA (port 3020)

**Client type: public** — no client secret. **Consent Required = ON** (third-party app).

```
http://localhost:3020
```

Demonstrates user-delegated consent. The app initially logs in without `read:accounts`. A button triggers a second `signinRedirect` with `scope: 'openid profile email read:accounts'` and `prompt: 'consent'` — Keycloak shows the consent screen, the user approves, and the token now includes `read:accounts`. Only then does `/api/balances` return 200.

This is the same endpoint as `banking-app` uses — the resource server enforces `read:accounts` identically. The difference is in *how the scope was granted*, not how it is checked.

**Key file:** `src/auth/userManager.js` — `grantBalanceAccess()` triggers re-authentication with `prompt: 'consent'`.

---

## Key Contrasts

| | `banking-app` | `trading-app` |
|---|---|---|
| Client type | Public | Confidential |
| Client secret | None | Server-side only |
| PKCE | Yes (required) | No |
| State + Nonce | `oidc-client-ts` handles automatically | OIDC middleware enforces |
| Token storage | `sessionStorage` (browser) | Encrypted cookie (server) |
| API call origin | Browser → API (CORS applies) | Server → API (no CORS) |
| Role extraction | `user.profile.realm_access.roles` | `OnTokenValidated` → `ClaimTypes.Role` |
| Logout | `signoutRedirect()` | `SignOut(Cookie + OIDC)` → end_session |

---

## Security Notes (demo vs. production)

| Setting | Demo value | Production recommendation |
|---|---|---|
| `RequireHttpsMetadata` | `false` | `true` — enforce TLS everywhere |
| `ValidateAudience` | `true` (enabled) | Keep `true` |
| `ClientSecret` location | `appsettings.json` | Environment variable / Key Vault |
| Token storage (SPA) | `sessionStorage` | BFF pattern with httpOnly cookies |
| `automaticSilentRenew` | `false` | `true` — refresh tokens transparently |
| Realm `sslRequired` | `"none"` | `"external"` or `"all"` |
| User passwords | Match username | Strong, unique, hashed by Keycloak |
