# Lab 6 — Module 3: Managing Tokens and Sessions

> **Module 3 — Managing Tokens and Sessions:** This module covers the full lifecycle of OIDC tokens and Keycloak sessions — how they are issued, how long they live, how they are renewed, and how they are revoked. This lab uses Postman to make every token operation observable and repeatable.

Keycloak issues three token types in an OIDC flow: a short-lived access token used to authorize API calls, a refresh token used to silently renew access without re-login, and an ID token that conveys identity to the client. By the end of this lab you will have observed token expiry and refresh rotation live, calculated refresh token lifespan from session settings, tested offline tokens that survive SSO session expiry, and revoked sessions from the admin console.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running and the admin console is reachable
- [ ] Your `{realm}` is accessible and contains user `alice` (password: `alice`)
- [ ] A public client (e.g. `oidc-playground`) exists in the realm with **Direct Access Grants** enabled
- [ ] Postman is installed and the `tokens-and-sessions` collection and environment are imported

**Import the Postman files from this lab folder:**
- `tokens-and-sessions.postman_collection.json`
- `tokens-and-sessions.postman_environment.json`

After importing, open the **Tokens & Sessions** environment and fill in:

| Variable | Value |
|---|---|
| `kc_url` | `https://labs-sso.keycloak.academy` or `http://localhost:8080` |
| `realm` | your realm name |
| `client_id` | `oidc-playground` (or your public client ID) |
| `username` | `alice` |
| `password` | `alice` |

**Keycloak endpoints — replace `{realm}` throughout:**

| | Shared cloud instance | Local Keycloak |
|---|---|---|
| Admin console | `https://labs-sso-admin.keycloak.academy/admin/{realm}/console/` | `http://localhost:8080/admin/{realm}/console/` |
| Token endpoint | `https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/token` | `http://localhost:8080/realms/{realm}/protocol/openid-connect/token` |
| Account console | `https://labs-sso.keycloak.academy/realms/{realm}/account` | `http://localhost:8080/realms/{realm}/account` |

---

## Background

### Token types and default lifetimes

| Token | Purpose | Default lifetime |
|---|---|---|
| **Access token** | Authorizes API calls (sent in `Authorization: Bearer` header) | 5 minutes |
| **Refresh token** | Silently renews the access token without re-login | Derived from session settings — see below |
| **ID token** | Conveys user identity to the client application | 5 minutes |
| **Offline token** | A refresh token that persists beyond SSO session expiry | Until offline session idle expires (30 days default) |

### Session settings that control token lifetimes

Navigate to **Realm Settings → Sessions** and **Realm Settings → Tokens** to find these settings:

| Setting | Location | Description |
|---|---|---|
| **SSO Session Idle** | Sessions | Session expires after this period of inactivity |
| **SSO Session Max** | Sessions | Absolute maximum session lifetime regardless of activity |
| **Client Session Idle** | Sessions | Per-client idle override (overrides SSO Session Idle for that client) |
| **Client Session Max** | Sessions | Per-client max override (overrides SSO Session Max for that client) |
| **Offline Session Idle** | Sessions | Idle timeout for offline sessions |
| **Access Token Lifespan** | Tokens | How long an access token is valid |

### Refresh token lifespan formula

The refresh token lifetime is not a single configurable field — it is derived from session settings at token issuance time.

**When Client Session Idle and Client Session Max are NOT set:**

```
refresh token lifetime = Min(SSO Session Max, SSO Session Idle)
```

**When Client Session Idle or Client Session Max ARE set:**

```
refresh token lifespan = Min(SSO Session Idle, Client Session Idle, SSO Session Max, Client Session Max)
```

> **Example:** SSO Session Idle = 30 min, SSO Session Max = 10 hours, no client-level overrides → refresh token lifetime = `Min(10h, 30min)` = **30 minutes**. The `exp` claim in the refresh token JWT encodes this calculated value.

> **Note:** Offline tokens do not follow this formula — they expire only when **Offline Session Idle** elapses without a refresh attempt. The `refresh_expires_in` field in the token response is `0` for offline tokens, signalling no expiry.

---

## Task 1 — Acquire tokens and inspect the response

> Estimated time: 5–10 min | Tools: Postman

**Goal:** Obtain a token set using the password grant and inspect the raw response fields — particularly `expires_in`, `refresh_expires_in`, and the encoded JWT claims.

**Observable outcome:**
- The token endpoint returns `access_token`, `refresh_token`, `id_token`, `expires_in`, and `refresh_expires_in`
- `expires_in` matches the **Access Token Lifespan** configured in Realm Settings → Tokens (default: 300 seconds)
- `refresh_expires_in` matches the refresh token lifespan derived from session settings
- Decoding the access token at [jwt.io](https://jwt.io) shows `exp` (Unix timestamp), `iat`, `sub` (user ID), and `realm_access.roles`

<details>
<summary>Hint — where to find the token lifespan in the response</summary>

The token endpoint response body (JSON) includes `expires_in` (seconds until access token expires) and `refresh_expires_in` (seconds until refresh token expires). These are not in the JWT itself — they are top-level fields in the JSON response.

Cross-reference `refresh_expires_in` with the formula in the Background section to confirm it matches the session configuration.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In Postman, select the **Tokens & Sessions** environment.
2. Open **1 — Get Token (regular)** and click **Send**.
3. In the response body, note:
   ```json
   {
     "access_token": "eyJ...",
     "expires_in": 300,
     "refresh_expires_in": 1800,
     "refresh_token": "eyJ...",
     "token_type": "Bearer",
     "id_token": "eyJ...",
     "session_state": "...",
     "scope": "openid profile email"
   }
   ```
4. Copy the `access_token` value and paste it into the **Encoded** field at [jwt.io](https://jwt.io). Confirm the `exp` and `iat` difference equals `expires_in`.
5. In the admin console, go to **Realm Settings → Tokens** and verify **Access Token Lifespan** is 5 minutes (300 seconds) — matching `expires_in`.
6. Apply the refresh token formula from the Background section to your current **SSO Session Idle** and **SSO Session Max** values — confirm the result matches `refresh_expires_in`.

</details>

---

## Task 2 — Force access token expiry and use the refresh token

> Estimated time: 10 min | Tools: Postman, admin console

**Goal:** Reduce the access token lifespan to 60 seconds, observe expiry, then use the refresh token to obtain a new access token without re-authenticating.

**Observable outcome:**
- After 60 seconds the `access_token` stored in the collection is expired (a protected API call would return 401)
- **3 — Refresh (valid)** returns a new `access_token` and a new `refresh_token`
- The old `refresh_token` (captured before the refresh) is now invalid — replaying it returns `{"error": "invalid_grant"}`

<details>
<summary>Hint — refresh token rotation</summary>

Each time you use a refresh token, Keycloak issues a new one and immediately invalidates the previous one. This is **refresh token rotation** — if a refresh token is used twice, the second use fails with `invalid_grant`. Keycloak interprets a replayed refresh token as evidence of a leak and can optionally revoke the entire session.

The Postman collection saves the current `refresh_token` as `old_refresh_token` automatically before each refresh — this makes the rotation replay test straightforward.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Shorten the access token lifespan:**
   - Admin console → **Realm Settings → Tokens**
   - Set **Access Token Lifespan** to `1 Minute` (60 seconds)
   - Save

2. **Acquire a fresh token:**
   - Postman: run **1 — Get Token (regular)**
   - Note `expires_in: 60` in the response

3. **Wait 60 seconds**, then run **3 — Refresh (valid)**
   - The request exchanges the stored `refresh_token` for a new `access_token`
   - Both `access_token` and `refresh_token` collection variables are updated
   - The previous `refresh_token` is saved as `old_refresh_token`

4. **Test rotation — replay the old refresh token:**
   - Run **4 — Refresh (replay — expect 400)**
   - Response: `{"error": "invalid_grant", "error_description": "Token is not active"}`
   - This confirms the old refresh token is invalidated after a single use

5. **Restore the access token lifespan** to 5 minutes before continuing.

</details>

---

## Task 3 — Calculate and verify refresh token lifespan

> Estimated time: 10 min | Tools: Postman, admin console

**Goal:** Observe how changing SSO Session Idle and Client Session Idle affects the `refresh_expires_in` value in the token response. Verify the formula holds.

**Observable outcome:**
- With only realm-level settings: `refresh_expires_in` = `Min(SSO Session Max, SSO Session Idle)` in seconds
- After setting **Client Session Idle** on your client: `refresh_expires_in` = `Min(SSO Session Idle, Client Session Idle, SSO Session Max, Client Session Max)` in seconds
- Reducing SSO Session Idle to 2 minutes and refreshing after 2 minutes of inactivity results in `invalid_grant`

<details>
<summary>Hint — where to set Client Session Idle</summary>

Client-level session overrides are in **Clients → {your client} → Advanced → Advanced Settings**. The fields are **Client Session Idle** and **Client Session Max**. Setting either to a value smaller than the realm default causes the formula to pick the smaller value, shortening the refresh token lifetime for sessions on that specific client.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

**Part A — Realm-level formula:**

1. Admin console → **Realm Settings → Sessions**. Note current values (e.g. SSO Session Idle: 30 min, SSO Session Max: 10 hours).
2. Run **1 — Get Token (regular)** in Postman.
3. Observe `refresh_expires_in` in the response (e.g. 1800 = 30 min).
4. Confirm: `Min(10h, 30min)` = 1800 seconds. ✓

**Part B — Client-level override:**

1. Admin console → **Clients → {your client} → Advanced → Advanced Settings**
2. Set **Client Session Idle** to `5 Minutes`
3. Run **1 — Get Token (regular)** again
4. Observe `refresh_expires_in: 300` (5 minutes) — the client-level idle now wins the `Min()` formula
5. Reset **Client Session Idle** to empty (inherits realm default) after this test

**Part C — Idle expiry in action:**

1. Set **SSO Session Idle** to `2 Minutes`
2. Acquire a token, wait 2 minutes without any refresh
3. Run **3 — Refresh (valid)** → `{"error": "invalid_grant"}` — the session has idled out
4. Restore SSO Session Idle to its original value

</details>

---

## Task 4 — Test offline tokens

> Estimated time: 10–15 min | Tools: Postman, admin console

**Goal:** Obtain an offline token by including the `offline_access` scope, verify it behaves differently from a regular refresh token, and confirm it appears under **Offline Sessions** in the admin console.

**Observable outcome:**
- The token response for the offline request contains `refresh_expires_in: 0` (no expiry)
- Decoding the offline refresh token JWT shows `"typ": "Offline"` in the header or `"typ": "Offline"` in the payload
- The session appears under **Users → alice → Sessions** with the type **Offline**
- After calling **7 — Logout (revoke regular session)**, **3 — Refresh (valid)** returns `invalid_grant` while **5 — Refresh offline token** still returns `200 OK`

<details>
<summary>Hint — what makes an offline token different</summary>

Requesting `offline_access` alongside `openid` in the scope parameter causes Keycloak to issue an **offline refresh token** instead of a regular one. The key differences:

- `refresh_expires_in: 0` in the token response (0 = does not expire based on session idle)
- The token is tied to an **offline session**, not a regular SSO session — closing the browser or the SSO session expiring does not invalidate it
- It expires only if unused for longer than **Offline Session Idle** (default: 30 days), or if the admin explicitly revokes it
- The `scope` in the response includes `offline_access`

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

**Step 1 — Enable offline_access scope on your client (if not already):**

1. Admin console → **Clients → {your client} → Client scopes**
2. Confirm `offline_access` appears in **Assigned client scopes** (it is a default realm scope — it should already be there)

**Step 2 — Acquire an offline token:**

1. Postman: run **2 — Get Token (offline)**
2. Observe the response:
   ```json
   {
     "access_token": "eyJ...",
     "expires_in": 300,
     "refresh_expires_in": 0,
     "refresh_token": "eyJ...",
     "token_type": "Bearer",
     "scope": "openid profile email offline_access"
   }
   ```
   Note `refresh_expires_in: 0` and `offline_access` in `scope`.
3. The offline refresh token is stored as `offline_refresh_token` in the collection.

**Step 3 — Decode the offline refresh token:**

Paste the `refresh_token` from step 2 into [jwt.io](https://jwt.io). In the payload, look for:
```json
{ "typ": "Offline", ... }
```
This confirms it is an offline token, not a regular refresh token.

**Step 4 — Verify in the admin console:**

1. Admin console → **Sessions** → select **Offline** — you should see the offline session listed with the client name and creation time

**Step 5 — Refresh with the offline token:**

1. Postman: run **5 — Refresh offline token**
2. A new `access_token` is returned. The offline refresh token is rotated (a new one is returned and stored)

**Step 6 — Also get a regular token (needed for the next step):**

1. Postman: run **1 — Get Token (regular)** — this creates a separate regular SSO session and populates `refresh_token`

**Step 7 — Prove offline token survives RP-initiated logout:**

1. Postman: run **7 — Logout (revoke regular session)** → `204 No Content`
   - This calls the OIDC logout endpoint with the regular `refresh_token`, revoking only that session
2. Run **3 — Refresh (valid)** → `{"error": "invalid_grant"}` — the regular session is gone
3. Run **5 — Refresh offline token** → `200 OK` — the offline session is a separate server-side session and was not affected

> **Note:** The logout endpoint targets only the session associated with the `refresh_token` in the request body — the offline session has its own independent lifecycle. This is different from admin console "Log out all sessions", which terminates ALL sessions including offline ones.

**Step 8 — Revoke the offline session (cleanup):**

Admin console → **Users → alice → Sessions → Offline sessions** → **Revoke** (trash icon).

> **Note:** Offline tokens should be stored encrypted at rest by the application. They represent long-term delegated access and must be treated with the same care as passwords.

</details>

---

## Task 5 — Decode a token and revoke sessions

> Estimated time: 5 min | Tools: Postman, admin console

**Goal:** Decode the access token JWT in Postman to inspect its claims, then revoke alice's sessions from the admin console and confirm the token is no longer usable.

**Observable outcome:**
- **6 — Decode access token** logs the decoded header and payload (including `sub`, `exp`, `realm_access.roles`) in the Postman Console
- After revoking alice's sessions in the admin console, running **3 — Refresh (valid)** returns `{"error": "invalid_grant"}`

> **Note:** Token introspection (`/token/introspect`) requires **client authentication** — only confidential clients (with a `client_secret`) can call it. Public clients cannot authenticate to the introspection endpoint. If you have a confidential client available you can call the endpoint with `client_id` + `client_secret` as form body parameters.

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Ensure you have a current `access_token` (run **1 — Get Token (regular)** if needed).
2. Postman: run **6 — Decode access token**.
   - Open the **Postman Console** (`View → Show Postman Console`) to see the decoded payload printed by the test script.
   - Key claims to note: `sub` (user UUID), `exp` (Unix expiry timestamp), `iat` (issued at), `realm_access.roles`.
3. Admin console → **Users → alice → Sessions** → **Log out all sessions** (trash icon).
4. Postman: run **3 — Refresh (valid)** → `{"error": "invalid_grant"}` — the session is gone, so the refresh token is also invalid.

**Additional revocation mechanisms:**

| Method | How |
|---|---|
| Admin console | Users → {user} → Sessions → Log out all sessions |
| Backchannel logout | Keycloak sends a logout token to the client's backchannel logout URL |
| Front-channel logout | Keycloak redirects to the client's front-channel logout endpoint |

Configure logout URLs per client at **Clients → {client} → Settings → Logout settings**.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] You called the token endpoint from Postman and inspected `expires_in` and `refresh_expires_in` in the response
- [ ] You applied the refresh token lifespan formula to your realm's session settings and the result matched `refresh_expires_in`
- [ ] You observed `invalid_grant` when replaying an already-used refresh token (rotation)
- [ ] You set a Client Session Idle override and confirmed it changed `refresh_expires_in`
- [ ] You obtained an offline token, confirmed `refresh_expires_in: 0`, and found it listed under alice's offline sessions
- [ ] You revoked alice's session from the admin console and confirmed **3 — Refresh (valid)** returned `invalid_grant`

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Configure a client-specific backchannel logout URL and observe the logout token payload Keycloak sends when a session is revoked.
- Write a small script (Python or bash) that automates token refresh and stops when `invalid_grant` is returned — useful for testing session idle timeouts.
- Explore Keycloak's Token Introspection endpoint (`/protocol/openid-connect/token/introspect`) with a confidential client and compare the introspection response against the decoded JWT claims.
- [Keycloak documentation: Sessions and tokens](https://www.keycloak.org/docs/latest/server_admin/#_sessions)
