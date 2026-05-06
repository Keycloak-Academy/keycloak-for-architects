# Lab 8 — Step-Up Authentication (ACR/AMR)

Aurum Bank's trading platform must protect high-value operations — placing buy orders above a threshold, beneficiary changes, and bulk operations — without forcing users through multi-factor authentication on every login. By the end of this lab you will have demonstrated how Keycloak maps ACR levels to authentication flows, how an ASP.NET Core application requests a stronger authentication class at runtime, and how the `claims` parameter forces re-authentication when the existing session's ACR is insufficient.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running and the admin console is reachable
- [ ] The `{realm}` realm is accessible and contains user `alice` (password: `alice`)
- [ ] The `trading-app` confidential client from Lab 5 exists and is functional
- [ ] The `trading-app` .NET project builds and runs (see Lab 5)

OTP enrollment for `alice` is covered in Task 1 of this lab.

If any prerequisite is missing, complete [Lab 5 — OAuth Authorization](005-oauth-authorization) and [Lab 6 — Managing Tokens and Sessions](006-tokens-and-sessions) before continuing.

---

## Background

### ACR and AMR

| Claim | Meaning | Example values |
|---|---|---|
| **acr** (Authentication Context Class Reference) | Named assurance level of the authentication event | `silver`, `gold`, `phr`, `phrh` |
| **amr** (Authentication Methods Reference) | List of methods actually used | `["pwd", "otp"]` |

The `amr` claim requires an explicit **Authentication Method Reference (AMR)** protocol mapper added to the `trading-app` client — it is **not included in tokens by default**. Once the mapper is in place, Keycloak populates the claim based on which authenticators succeed: the **Username Password Form** executor emits `"pwd"`; the **OTP Form** executor emits `"otp"`. When both run (i.e., `gold`), the claim contains both values: `["pwd", "otp"]`. Task 1 covers adding the mapper.

Keycloak returns `acr` in both the ID token and the access token. The value is determined by the **ACR-to-flow mapping** configured in Authentication → Flows. A client can request a specific ACR via the `acr_values` parameter; if the user's current session does not meet that level, Keycloak steps them up through the required authentication flow.

### Requesting step-up from the client

| Parameter | Purpose |
|---|---|
| `acr_values` | Request a specific ACR at login time **or mid-session** (non-essential — IdP will attempt to satisfy it) |
| `claims` | Request a specific ACR mid-session as an essential claim — IdP must satisfy it or return an error |
| `max_age` | Force re-authentication if the user authenticated longer than N seconds ago |

Both `acr_values` and `claims` can trigger step-up mid-session. The OIDC specification treats `acr_values` as a **voluntary** hint — if the current session already meets a lower level, the IdP may return a token without re-challenging. The `claims` parameter with `essential: true` is a **mandatory** demand — if the session does not satisfy the requested ACR, the IdP must re-authenticate the user. Use `acr_values` when a higher level is preferred but not strictly required; use `claims` with `essential: true` when the operation must not proceed without re-authentication (e.g., placing a buy order).

**What the trading app uses:** Both `Trading/StepUp` (dashboard) and `Order/StepUp` (order page) send the `claims` parameter with `essential: true` and `values: ["gold"]` — a JSON structure, not a plain string. This guarantees Keycloak always challenges the user for OTP regardless of the existing session state:

```json
{ "id_token": { "acr": { "essential": true, "values": ["gold"] } } }
```

`acr_values=gold` by contrast is a single plain string and acts as a hint only — Keycloak may skip the OTP challenge if the session LoA is already considered sufficient.

### Pushed Authorization Request (PAR)

When you inspect the browser URL during step-up you will notice the authorization request does **not** contain `claims`, `acr_values`, or `state` as query parameters. Instead it looks like this:

```
https://{keycloak}/realms/{realm}/protocol/openid-connect/auth
  ?client_id=trading-app
  &request_uri=urn:ietf:params:oauth:request-uri:9c781723-53c6-a34d-...
```

This is **PAR — Pushed Authorization Request** (RFC 9126). The .NET 9 OIDC middleware enables it automatically when Keycloak advertises the `pushed_authorization_request_endpoint` in its discovery document.

**Without PAR**, all parameters are exposed as plain text in the browser URL — visible in browser history, proxy logs, and referrer headers.

Using `acr_values` (simple string, non-essential):

```http
GET https://{keycloak}/realms/{realm}/protocol/openid-connect/auth
  ?client_id=trading-app
  &response_type=code
  &scope=openid
  &redirect_uri=http://localhost:5010/signin-oidc
  &state=abc123
  &nonce=xyz789
  &acr_values=gold
```

Using `claims` (JSON, essential — what the trading app sends):

```http
GET https://{keycloak}/realms/{realm}/protocol/openid-connect/auth
  ?client_id=trading-app
  &response_type=code
  &scope=openid
  &redirect_uri=http://localhost:5010/signin-oidc
  &state=abc123
  &nonce=xyz789
  &claims={"id_token":{"acr":{"essential":true,"values":["gold"]}}}
```

**With PAR**, the browser only ever sees an opaque reference — two steps instead of one:

| Step | Who | What happens |
|---|---|---|
| 1 | App → Keycloak (server-to-server POST) | All parameters (`claims`, `state`, `nonce`, `redirect_uri`, `client_secret`) are POSTed directly to Keycloak's PAR endpoint. Keycloak stores them and returns a short-lived opaque `request_uri` reference. |
| 2 | App → Browser → Keycloak | Browser is redirected to Keycloak with only `client_id` and `request_uri`. Keycloak looks up the stored parameters using the reference. |

**Step 1 — server-to-server POST (never touches the browser):**

```http
POST https://{keycloak}/realms/{realm}/protocol/openid-connect/ext/par/request
Content-Type: application/x-www-form-urlencoded

client_id=trading-app
&client_secret=...
&response_type=code
&scope=openid
&redirect_uri=http://localhost:5010/signin-oidc
&state=abc123
&nonce=xyz789
&claims={"id_token":{"acr":{"essential":true,"values":["gold"]}}}
```

Keycloak responds with a short-lived reference:

```json
{
  "request_uri": "urn:ietf:params:oauth:request-uri:9c781723-53c6-a34d-8aa8-66300916d69d",
  "expires_in": 60
}
```

**Step 2 — browser redirect using only the reference:**

```http
GET https://{keycloak}/realms/{realm}/protocol/openid-connect/auth
  ?client_id=trading-app
  &request_uri=urn:ietf:params:oauth:request-uri:9c781723-53c6-a34d-8aa8-66300916d69d
```

**Why this matters:**

| | Traditional redirect | PAR |
|---|---|---|
| Sensitive parameters in browser URL | Yes — visible in history and proxy logs | No — opaque reference only |
| Parameters tampered in transit | Possible | No — stored server-side before redirect |
| Client authenticated on the request | No | Yes — `client_secret` sent in the server-to-server POST |
| Long URL / size limit risk | Yes | No |

For this lab, PAR means the `claims` JSON (`essential: true`, `values: ["gold"]`) that triggers the step-up is sent server-to-server and never appears in the browser. The short `request_uri` in the browser URL is just a ticket — the real authorization intent is already safely stored in Keycloak.

### Level of Authentication (LoA) and Max Age

Keycloak's step-up mechanism uses **numeric LoA values** internally. Named ACR strings (e.g., `silver`, `gold`) are translated to LoA numbers via an **ACR-to-LoA mapping** configured at the realm level. The `Condition - Level of Authentication` authenticator then compares the session's current LoA against the required level to decide whether to challenge the user.

**Max Age** controls how long a satisfied LoA persists within a session:

| LoA | Named ACR | Authenticator | Max Age | Effect |
|---|---|---|---|---|
| 1 | `silver` | Username + Password | 36000s (10 h) | Reused for the entire SSO session; no re-prompt until session expires |
| 2 | `gold` | OTP Form | 0s | Valid for one authentication only; always re-challenged when requested |

**Flow logic to know:**
- On a user's first authentication, the first conditional sub-flow (Level 1) is always evaluated regardless of the requested level — the user has no established session LoA yet.
- Sub-flows must be ordered lowest-to-highest LoA. If Level 2 is placed first, it will always be challenged on first login.
- When a level is requested but the session already satisfies it and Max Age has not expired, Keycloak skips the challenge. If Max Age is 0, the challenge always runs.

### Why this matters for Aurum Bank

- **silver** — sufficient for viewing portfolio and stock prices (password only)
- **gold** — required for placing buy orders, beneficiary changes, and bulk operations (password + OTP)
- The application does not implement MFA logic; it delegates entirely to Keycloak via ACR policy

> **Note:** Do not hard-code OTP validation in your application. Always delegate step-up to the identity provider via ACR claims. This keeps MFA policy centralized and auditable.

---

## Task 1 — Configure ACR-to-flow mapping in Keycloak

> Estimated time: 10–15 min | Tools: admin console

**Goal:** Map the ACR values `silver` and `gold` to authentication flows so that `silver` requires password and `gold` requires password plus OTP.

**Observable outcome:**
- `alice` has an OTP credential configured (visible in **Users → alice → Credentials**)
- In **Authentication → Flows**, a flow named **Step-Up Browser Flow** exists with two conditional sub-flows: `Level 1 – Password` (LoA 1, Username Password Form) and `Level 2 – OTP` (LoA 2, OTP Form)
- The flow is bound as the **Browser Flow** in Realm Settings → Authentication → Flows
- **Realm Settings → Authentication → ACR-to-LoA mapping** shows `silver=1` and `gold=2`
- Requesting `acr_values=silver` returns a token with `"acr": "silver"` (password only, no OTP)
- Requesting `acr_values=gold` returns a token with `"acr": "gold"` (password + OTP)

<details>
<summary>Hint — how the conditional mechanism works</summary>

Rather than creating separate flows per ACR level, Keycloak uses a single flow with **conditional sub-flows**. Each sub-flow has a `Condition - Level of Authentication` condition that checks whether the requested LoA matches. If it does, the authenticators in that sub-flow run; if not, the sub-flow is skipped. The ACR-to-LoA mapping in Realm Settings translates text ACR values into the numeric LoA numbers the conditions compare against.

</details>

<details>
<summary>Hint — Max Age and why Level 2 must be 0</summary>

The `Max Age` setting on each `Condition - Level of Authentication` condition controls how long the satisfied level is valid within a session. Setting Max Age to `0` for the OTP sub-flow means the OTP challenge fires every time LoA 2 is requested — regardless of whether the user already satisfied it earlier in the session. This is the correct behavior for high-value operations that must always re-verify the second factor.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

**Part 0 — Enroll alice in OTP**

1. Admin console → **Users** → select `alice` → **Credentials** tab
2. Click **Add credential** → type **OTP** → **Save**
3. Open an authenticator app (Google Authenticator, Authy, etc.) and scan the QR code, or enter the secret key manually.
4. Enter a generated OTP code in the **One-time code** field to confirm enrollment → **Submit**
5. Confirm `alice` now has an **otp** entry in the Credentials tab.

> **Alternative — force enrollment at login:** Go to **Users → alice → Required Actions** and enable **Configure OTP**. Alice will be prompted to enroll herself during Task 2's browser login instead.

**Part A — Build the step-up flow**

1. Admin console → **Authentication** → **Flows** tab → **Create flow**
2. Name: `Step-Up Browser Flow` → **Save**
3. **Add execution** → select `Cookie` → **Add** → set requirement to **Alternative**
4. **Add sub-flow** → name `Step-Up Auth` → **Add** → set requirement to **Alternative**
5. Click **+** next to `Step-Up Auth` → **Add sub-flow** → name `Level 1 – Password` → **Add** → set requirement to **Conditional**
6. Click **+** next to `Level 1 – Password` → **Add condition** → select `Condition - Level of Authentication` → **Add** → set requirement to **Required**
7. Click the ⚙️ gear icon on `Condition - Level of Authentication`:
   - Alias: `Silver Level`
   - Level of Authentication (LoA): `1`
   - Max Age: `36000`
   - **Save**
8. Click **+** next to `Level 1 – Password` → **Add step** → select `Username Password Form` → **Add**
9. Click **+** next to `Step-Up Auth` → **Add sub-flow** → name `Level 2 – OTP` → **Add** → set requirement to **Conditional**
10. Click **+** next to `Level 2 – OTP` → **Add condition** → select `Condition - Level of Authentication` → **Add** → set requirement to **Required**
11. Click the ⚙️ gear icon on `Condition - Level of Authentication` (in `Level 2 – OTP`):
    - Alias: `Gold Level`
    - Level of Authentication (LoA): `2`
    - Max Age: `0`
    - **Save**
12. Click **+** next to `Level 2 – OTP` → **Add step** → select `OTP Form` → **Add** → set requirement to **Required**
13. At the top of the page, click the **Action** menu → **Bind flow** → select **Browser Flow** → **Save**

**Part B — Configure ACR-to-LoA mapping**

14. Admin console → **Realm Settings** → **Authentication** tab → **ACR-to-LoA Mapping** section
15. Click **Add** → Key: `silver`, Value: `1` → **Save**
16. Click **Add** → Key: `gold`, Value: `2` → **Save**

**Part C — Add the AMR protocol mapper to the trading app**

17. Admin console → **Clients** → `trading-app` → **Client Scopes** tab
18. Click on the `trading-app-dedicated` scope (first entry in the Assigned Client Scopes list)
19. Open the **Mappers** tab → click **Add Mapper** → **Configure a new mapper**
20. Select **Authentication Method Reference (AMR)** from the list → **Save** (accept all default settings)

The mapper reads values that Keycloak writes into user session notes when each authenticator completes. Without it, the `amr` claim is absent from all tokens regardless of which flow ran.

**Part D — Verify with a token request**

- macOS / Linux:

  ```bash
  # Request silver (password only)
  curl -X POST "http://localhost:8080/realms/{realm}/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password" \
    -d "client_id=trading-app" \
    -d "client_secret={secret}" \
    -d "username=alice" \
    -d "password=alice" \
    -d "acr_values=silver"
  ```

- Windows (PowerShell):

  ```powershell
  # Request silver (password only)
  curl.exe -X POST "http://localhost:8080/realms/{realm}/protocol/openid-connect/token" `
    -H "Content-Type: application/x-www-form-urlencoded" `
    -d "grant_type=password" `
    -d "client_id=trading-app" `
    -d "client_secret={secret}" `
    -d "username=alice" `
    -d "password=alice" `
    -d "acr_values=silver"
  ```
Decode the returned `access_token` at [jwt.io](https://jwt.io) and confirm `"acr": "silver"`.

> `acr_values=gold` requires OTP and cannot be satisfied by the password-grant flow alone. The Step Up browser flow in Task 4 demonstrates the full OTP challenge.

</details>

---

## Task 2 — Set a default authentication level on the Keycloak client

> Estimated time: 5–10 min | Tools: admin console, browser

**Goal:** Configure the `trading-app` client in Keycloak so that every login request defaults to `silver` (LoA 1) — keeping the ACR policy centralized in the IdP rather than in application code — and verify that `alice` logs in with password only, receiving a token with `"acr": "silver"`.

**Observable outcome:**
- In the Keycloak admin console, **Clients → trading-app → Advanced** shows **Default Level of Authentication** set to `1`
- Navigating to the trading app redirects to Keycloak, which runs the `Step-Up Browser Flow`
- Only the username/password screen is presented — no OTP form
- After login, the app shows the dashboard; the ID token contains `"acr": "silver"` and `"amr": ["pwd"]`
- Navigating to `/api/order` (Task 3) returns `403` — confirming that silver is insufficient for high-value operations

<details>
<summary>Hint — where to find the setting</summary>

The default authentication level is a per-client setting, not a realm-level one. Look in the **Advanced** tab of the `trading-app` client configuration — not in Realm Settings or Authentication Flows.

</details>

<details>
<summary>Hint — what value to enter</summary>

The field accepts the numeric LoA value, not the ACR string name. `silver` maps to LoA `1` in the realm's ACR-to-LoA mapping configured in Task 1. Setting `1` here tells Keycloak to apply that level automatically for any login from this client that does not carry an explicit `acr_values` or `claims` parameter.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Admin console → **Clients** → `trading-app` → **Advanced** tab
2. Scroll to the **Authentication level** section
3. Set **Default Level of Authentication** to `1`
4. Click **Save**

**Verify:**

5. Open `http://localhost:5010` in a private/incognito window and click **Sign in**
6. Log in as `alice` (password: `alice`) — confirm only the username/password screen appears, no OTP is prompted
7. After redirect back to the app, navigate to `http://localhost:5010/api/order` and confirm `403` is returned
8. Decode the ID token at [jwt.io](https://jwt.io) and confirm `"acr": "silver"` and `"amr": ["pwd"]`

</details>

---

## Task 3 — Protect a high-value order endpoint with an ACR policy in .NET

> Estimated time: 15–20 min | Tools: VS Code, browser, curl

**Goal:** Add a `/api/order` stub endpoint to the trading app and enforce that only sessions with `acr=gold` can invoke it, returning 403 for insufficient ACR.

**Observable outcome:**
- `GET /api/order` with a `gold` ACR session returns `200 OK` with a mock order response
- `GET /api/order` with a `silver` ACR session returns `403` with a message indicating insufficient authentication level
- The check is performed by inspecting the `acr` claim in the user's identity, not by custom MFA logic

<details>
<summary>Hint — where the ACR claim appears in ASP.NET Core</summary>

After OIDC authentication, claims from the ID token are available on `User.Claims`. The `acr` claim is a standard claim type — look for it by claim type string or use `FindFirst("acr")`.

</details>

<details>
<summary>Hint — how to structure the policy</summary>

You can either write an inline check in the controller action or define a custom authorization policy that requires `acr=gold`. The inline check is faster for a lab; the policy approach is more maintainable for production.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Add the order stub endpoint to `TradingController.cs`:**
   ```csharp
   [Authorize]
   public class TradingController : Controller
   {
       // ... existing code ...

       [HttpGet("api/order")]
       public IActionResult Order()
       {
           var acr = User.FindFirst("acr")?.Value;
           if (acr != "gold")
           {
               return StatusCode(403, new
               {
                   error = "Insufficient authentication level",
                   requiredAcr = "gold",
                   currentAcr = acr ?? "none"
               });
           }

           return Ok(new
           {
               status = "Order authorized",
               amount = 50000,
               currency = "USD",
               acr = acr
           });
       }
   }
   ```

2. **Test with `silver` ACR (password only):**
   - Run the trading app (silver is the default configured in Task 2 — no changes needed)
   - Open an incognito window, navigate to `http://localhost:5010`, and log in as `alice`
   - Navigate to `http://localhost:5010/api/order`
   - Expected: `403` with `{"error":"Insufficient authentication level","requiredAcr":"gold","currentAcr":"silver"}`

3. **Test with `gold` ACR (after step-up):**
   - Complete the Step Up flow from Task 4, then return here to verify
   - Navigate to `http://localhost:5010/api/order` in the gold session
   - Expected: `200 OK` with the mock order JSON

> **Note:** In production, define a reusable authorization policy instead of inline checks:
> ```csharp
> builder.Services.AddAuthorization(o =>
>     o.AddPolicy("AcrGold", p => p.RequireClaim("acr", "gold")));
> ```
> Then decorate the action with `[Authorize(Policy = "AcrGold")]`.

</details>

---

## Task 4 — Use the `claims` parameter to force re-authentication when ACR is insufficient

> Estimated time: 15–20 min | Tools: browser, VS Code

**Goal:** Implement a "Step Up" button in the trading app dashboard that, when clicked, sends the user back to Keycloak with a `claims` parameter demanding `acr=gold`, forcing OTP re-authentication if the current session is only `silver`.

**Observable outcome:**
- The trading dashboard shows a **Step Up to Gold** button when the current ACR is `silver`
- Clicking the button redirects to Keycloak, prompts for OTP, and returns to the dashboard
- After step-up, the dashboard shows the green gold banner and the **Buy Shares** button leads to the order form
- Navigating to `/order/initiate` without gold shows a "Verify Identity to Continue" prompt; after completing OTP the form is shown
- The `claims` parameter in the authorization request contains `{"id_token":{"acr":{"essential":true,"values":["gold"]}}}`

<details>
<summary>Hint — how to force re-authentication mid-session</summary>

The `claims` parameter with `essential: true` tells Keycloak that the claim is required, not optional. If the current session cannot satisfy it, the user must re-authenticate. In ASP.NET Core, you can trigger a new challenge with extra parameters by calling `ChallengeAsync` and passing `AuthenticationProperties` with custom items.

</details>

<details>
<summary>Hint — constructing the claims parameter</summary>

The `claims` parameter value is a JSON object. For ACR step-up, the structure is:
```json
{"id_token":{"acr":{"essential":true,"values":["gold"]}}}
```
Note `values` is an array (per the OIDC specification), not a singular `value` string.
In .NET, serialize the JSON and store it in `AuthenticationProperties.Items["claims"]`. The `OnRedirectToIdentityProvider` event handler then reads it and calls `ctx.ProtocolMessage.SetParameter("claims", ...)` — the middleware handles URL-encoding automatically.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Add a "Step Up" action to `TradingController.cs`:**
   ```csharp
   using Microsoft.AspNetCore.Authentication;
   using System.Text.Json;

   // ... inside TradingController ...

   public async Task<IActionResult> StepUp()
   {
       var claimsParameter = JsonSerializer.Serialize(new
       {
           id_token = new
           {
               acr = new { essential = true, values = new[] { "gold" } }
           }
       });

       var props = new AuthenticationProperties
       {
           RedirectUri = Url.Action("Dashboard", "Trading")
       };
       props.Items["claims"] = claimsParameter;

       await HttpContext.ChallengeAsync("OpenIdConnect", props);
       return new EmptyResult();
   }
   ```

2. **Wire the claims parameter into the OIDC redirect:**
   In `Program.cs`, replace the `OnRedirectToIdentityProvider` handler from Task 2 with this expanded version that also handles the `claims` parameter:
   ```csharp
   OnRedirectToIdentityProvider = ctx =>
   {
       // Default ACR for normal login — skip if a claims parameter is already set
       // (the claims parameter is the mandatory form; acr_values would be redundant)
       if (!ctx.ProtocolMessage.Parameters.ContainsKey("acr_values")
           && !ctx.Properties.Items.ContainsKey("claims"))
       {
           ctx.ProtocolMessage.SetParameter("acr_values", "silver");
       }

       if (ctx.Properties.Items.TryGetValue("claims", out var claims))
           ctx.ProtocolMessage.SetParameter("claims", claims);

       return Task.CompletedTask;
   }
   ```

3. **Update the dashboard view to show the Step Up button:**
   In `Views/Trading/Dashboard.cshtml`, add:
   ```html
   @if (ViewBag.CurrentAcr != "gold")
   {
       <div class="card" style="background:#fffde7;border:1px solid #ffe082;">
           <p>Your current authentication level is <strong>@ViewBag.CurrentAcr</strong>.</p>
           <p>Buying shares requires <strong>gold</strong> — step up to verify your identity with a second factor.</p>
           <a class="btn" href="@Url.Action("StepUp", "Trading")">Step Up to Gold</a>
       </div>
   }
   else
   {
       <div class="card" style="background:#e8f5e9;border:1px solid #a5d6a7;">
           <p>Authentication level: <strong>gold</strong>. You can place buy orders.</p>
       </div>
   }
   ```

4. **Pass the current ACR to the view in the `Dashboard` action:**
   ```csharp
   public IActionResult Dashboard()
   {
       ViewBag.CurrentAcr = User.FindFirst("acr")?.Value ?? "none";
       // ... rest of existing dashboard logic ...
   }
   ```

5. **Test the flow:**
   - Run the app and log in as `alice` (default `silver`)
   - Dashboard shows the yellow "Step Up to Gold" banner
   - Click **Step Up to Gold** → redirect to Keycloak → OTP prompt → back to dashboard with green gold banner
   - Click **Buy Shares** → `/order/initiate` now shows the order form (gold already satisfied)
   - Alternatively, navigate directly to `/order/initiate` with silver — the page shows "Verify Identity to Continue"; clicking it triggers OTP and returns to the order form
   - Navigate to `http://localhost:5010/api/order` → `200 OK`

> **Note:** The `claims` parameter with `essential: true` is the OIDC-standard way to demand a specific authentication context mid-session. It works regardless of `max_age` and does not require the application to track when the user last authenticated.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] `acr_values=silver` returns a token with `"acr": "silver"` (password only)
- [ ] `acr_values=gold` triggers the OTP form and returns `"acr": "gold"`, `"amr": ["pwd","otp"]`
- [ ] The `/api/order` endpoint returns `200` when `acr=gold` and `403` when `acr=silver`
- [ ] Clicking **Step Up to Gold** redirects through Keycloak with the `claims` parameter and returns a new token with `acr=gold`
- [ ] The `Step-Up Browser Flow` is bound as the Browser Flow in Realm Settings → Authentication → Flows
- [ ] ACR-to-LoA mapping shows `silver=1` and `gold=2` in Realm Settings → Authentication
- [ ] Re-clicking **Step Up to Gold** in an existing `gold` browser session still triggers the OTP challenge (Max Age 0 — verify via the browser flow in Task 4, not curl)

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Implement a custom authorization handler in .NET that reads `acr` from the access token (not the ID token) and enforces different ACR thresholds per endpoint (e.g., `platinum` for admin operations)
- Configure a `max_age=0` parameter alongside `claims` to force full re-authentication (password + OTP) even when the user already has a `gold` session
- Add AMR logging to the trading app audit trail: record which authentication methods were used for every high-value API call by reading the `amr` claim

---

## Implementation Note — `acr` claim visibility in ASP.NET Core

`User.FindFirst("acr")` returns `null` by default in ASP.NET Core even when `acr` is present in the token. Two middleware defaults combine to cause this:

**1. `MapInboundClaims = true` (default)**

The OIDC middleware inherits a legacy behavior from the WS-Federation era: it translates short JWT claim names into long WS-Federation URI strings before adding them to the identity. For example, `sub` becomes `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`. Claims without a mapping entry may be renamed or dropped unpredictably.

Setting `o.MapInboundClaims = false` disables this translation. All claim names are preserved exactly as they appear in the JWT — `acr` stays `acr`, `sub` stays `sub` — which is what modern OIDC/Keycloak applications expect.

**2. `ClaimActions` delete list**

The OIDC middleware maintains a `ClaimActions` pipeline — a list of rules applied to the ID token claims before they are written into the `ClaimsPrincipal`. Several claims are deleted by default (`nonce`, `aud`, `azp`, `acr`, and others) because they are considered protocol artifacts, not application claims.

`ClaimActions` is a **collection of rules**, not a collection of claims. `o.ClaimActions.Remove("acr")` removes the `DeleteClaimAction` rule for `acr` from that collection — it does not remove the claim itself. Once the delete rule is gone, `acr` is written into the identity normally.

**Fix applied in `Program.cs`:**

```csharp
o.MapInboundClaims = false;       // preserve JWT claim names as-is
o.ClaimActions.Remove("acr");     // stop the middleware from deleting acr
```

Both lines are required. `MapInboundClaims = false` alone does not restore a claim that is being actively deleted by a `ClaimActions` rule.
