# Keycloak Demo Apps

Three real-world apps showing how different client types integrate with Keycloak. They all share a single resource server and a dedicated realm.

> **Prerequisites:** Keycloak running on `http://localhost:8080` with your realm configured.

---

## Quick Start

Open `demo.code-workspace` in VS Code, then press **`Ctrl+Shift+B`** → *Start All Demo Apps*.

Or start each app manually:

```bash
# 1 — Resource server
cd core-banking-api && dotnet run

# 2 — React SPA
cd banking-app && npm install && npm start

# 3 — Trading web app
cd trading-app && dotnet run
```

---

## Realm Setup

Ensure your realm has the following clients and users configured before running the apps.

### Required users

Create users in your realm with the following roles:

| Role | Notes |
|---|---|
| `customer` | realm role |
| `trader` | client role on `trading-app` |
| `admin` | realm role |

---

## The Three Apps

### 1. `core-banking-api` — Resource Server (port 3011)

C# ASP.NET Core Minimal API. Validates Bearer tokens using Keycloak's JWKS (RS256 signature verification). Returns hardcoded fake data.

**Auth patterns demonstrated:**

| Endpoint | Protection | Test with |
|---|---|---|
| `GET /api/accounts` | Role: `customer` | any user with `customer` role |
| `GET /api/transactions/{id}` | Role: `customer` | any user with `customer` role |
| `GET /api/transactions` | Role: `customer` | any user with `customer` role |
| `GET /api/balances` | Role: `customer` | any user with `customer` role |
| `GET /api/portfolio/repartition` | Client role: `trader` (trading-app) | any user with `trader` client role |
| `GET /api/stocks` | Auth only (valid token) | any authenticated client |
| `GET /api/users` | Role: `admin` | any user with `admin` role |
| `GET /api/info` | Public | anyone |

CORS is restricted to `localhost:3010` and `localhost:5010` only.

---

### 2. `banking-app` — React SPA (port 3010)

**Client type: public** — no client secret. Uses authorization code + PKCE.

```
http://localhost:3010
```

The entire OAuth flow runs in the browser:

```
User clicks Login
  → browser redirects to Keycloak with code_challenge (PKCE)
  → Keycloak authenticates user, redirects back with ?code=...
  → /callback exchanges code for tokens (using code_verifier)
  → tokens stored in sessionStorage
  → dashboard calls core-banking-api with Bearer token
```

**Key files:**
- `src/auth/userManager.js` — `oidc-client-ts` `UserManager` config
- `src/pages/CallbackPage.jsx` — handles the redirect callback
- `src/api/bankingApi.js` — injects `Authorization: Bearer` header

**Environment (`.env`):**
```
KC_URL=http://localhost:8080
KC_REALM=your-realm
CLIENT_ID=banking-app-spa
API_BASE_URL=http://localhost:3011
```

> ⚠️ Tokens are in `sessionStorage` (XSS risk). Production SPAs should use the BFF pattern with httpOnly cookies.

---

### 3. `trading-app` — ASP.NET Core MVC (port 5010)

**Client type: confidential** — has a client secret stored server-side.

```
http://localhost:5010
```

The flow is driven by the server:

```
User clicks Login
  → ASP.NET OIDC middleware redirects to Keycloak (with state + nonce)
  → Keycloak authenticates user, redirects to /signin-oidc
  → middleware exchanges code for tokens, validates signature + nonce
  → access_token stored in encrypted session cookie (SaveTokens=true)
  → TradingController retrieves token via GetTokenAsync("access_token")
  → server calls core-banking-api with Bearer token (server-to-server, no CORS)
```

**Key files:**
- `Program.cs` — OIDC middleware wiring, `OnTokenValidated` role mapping
- `Controllers/TradingController.cs` — `GetTokenAsync` + API call pattern
- `Services/BankingApiService.cs` — typed `HttpClient` forwarding Bearer token

**Configuration (`appsettings.json`):**
```json
{
  "Keycloak": {
    "Authority":     "http://localhost:8080/realms/your-realm",
    "ClientId":      "trading-app",
    "ClientSecret":  "trading-app-secret"
  },
  "BankingApi": { "BaseUrl": "http://localhost:3011" }
}
```

> ⚠️ `ClientSecret` is hardcoded in `appsettings.json`. Production: use environment variables or a secrets manager.

---

## Key Contrasts

| | `banking-app` | `trading-app` |
|---|---|---|
| Client type | Public | Confidential |
| Client secret | None | Server-side only |
| PKCE | Yes (required) | No |
| State + Nonce | `oidc-client-ts` handles automatically | OIDC middleware enforces (`RequireNonce=true`) |
| Token storage | `sessionStorage` (browser) | Encrypted cookie (server) |
| API call origin | Browser → API (CORS applies) | Server → API (no CORS) |
| Role extraction | `user.profile.realm_access.roles` | `OnTokenValidated` → `ClaimTypes.Role` |
| Logout | `signoutRedirect()` | `SignOut(Cookie + OIDC)` → end_session |

---

## Security Notes (demo vs. production)

The following settings are intentionally simplified for this demo:

| Setting | Demo value | Production recommendation |
|---|---|---|
| `RequireHttpsMetadata` | `false` | `true` — enforce TLS everywhere |
| `ValidateAudience` | `false` | `true` — validate against expected audience |
| `ClientSecret` location | `appsettings.json` | Environment variable / Key Vault |
| Token storage (SPA) | `sessionStorage` | BFF pattern with httpOnly cookies |
| `automaticSilentRenew` | `false` | `true` — refresh tokens transparently |
| Realm `sslRequired` | `"none"` | `"external"` or `"all"` |
| User passwords | Match username | Strong, unique, hashed by Keycloak |
