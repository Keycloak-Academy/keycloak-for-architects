# Lab 3 — The OpenID Connect Playground

This lab addresses the gap between knowing that OIDC exists and understanding the exact request/response mechanics that an application and Keycloak exchange. By the end, you will have demonstrated that you can use the discovery endpoint to bootstrap a relying party, complete an authorization code flow with PKCE, refresh tokens without user re-authentication, add custom claims to the ID token through client scopes, and invoke the UserInfo endpoint with an access token.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running and reachable (shared or local)
- [ ] You can log in to the admin console with admin credentials
- [ ] A realm exists and you have created at least one test user with a password and a realm role assigned

If any prerequisite is missing, complete [Lab 2 — Identity & Protocol Fundamentals](../002-identity-fundamentals/README.md) before continuing.

---

## Background

### OIDC discovery and provider metadata

OpenID Connect defines a discovery mechanism so that relying parties do not need to hard-code endpoint URLs. Appending `/.well-known/openid-configuration` to the issuer URL returns a JSON document containing endpoints, supported grant types, response types, and signing algorithms.

| Key field | Purpose |
|---|---|
| `authorization_endpoint` | Where the user-agent is redirected to authenticate |
| `token_endpoint` | Where the client exchanges codes or refresh tokens for tokens |
| `userinfo_endpoint` | Where the client retrieves additional user claims with an access token |
| `introspection_endpoint` | Where a resource server validates an opaque or JWT token |
| `end_session_endpoint` | Where the client initiates logout |
| `jwks_uri` | Public keys used to verify token signatures |

### Authorization code flow parameters

| Parameter | Role |
|---|---|
| `response_type=code` | Requests an authorization code instead of a token directly |
| `scope` | Requests claims; must include `openid` for an ID token |
| `prompt` | Controls re-authentication behavior (`login`, `none`, etc.) |
| `max_age` | Forces re-authentication if the session is older than N seconds |
| `login_hint` | Prefills the username on the login page |
| `nonce` | Echoed in the ID token to prevent replay attacks |
| `state` | Opaque value returned in the callback to prevent CSRF |
| `code_challenge` / `code_verifier` | PKCE extension for public clients |

### Token types

| Token | Purpose | Lifetime hint |
|---|---|---|
| **Access token** | Authorizes requests to resource servers | `expires_in` |
| **ID token** | Carries identity claims about the authenticated user | `expires_in` |
| **Refresh token** | Obtains new tokens without user interaction | `refresh_expires_in` |

> **Note:** Keycloak rotates refresh tokens by default. A refresh response includes a new refresh token that the client must store and use for the next refresh. Reusing an old refresh token will fail.

---

## Task 1 — Register the playground client and complete discovery

> Estimated time: 7–10 min | Tools: admin console / browser

**Goal:** Register a public OIDC client for the playground and use the discovery endpoint to resolve the authorization, token, and userinfo URLs.

**Observable outcome:**
- The client `playground-oidc` appears in the **Clients** list
- The playground Step 1 displays the three endpoints (`authorization_endpoint`, `token_endpoint`, `userinfo_endpoint`) after fetching `/.well-known/openid-configuration`

<details>
<summary>Hint — client type and authentication</summary>

The playground is a browser-based tool, so it cannot keep a secret. Register it accordingly so that Keycloak expects PKCE instead of a client secret.

</details>

<details>
<summary>Hint — issuer URL structure</summary>

Keycloak realms are independent security domains. The issuer URL always ends in `/realms/{realm}` because the discovery document and signing keys are scoped to that realm.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In the admin console, go to **Clients** → **Create client**
2. Configure the client:
   - Client type: **OpenID Connect**
   - Client ID: `playground-oidc`
   - Client authentication: **Off** (public client)
   - Standard flow: **Enabled**
   - Direct access grants: optional (not required for this lab)
   - Valid redirect URIs: `https://labs-playground.keycloak.academy/oidc/callback`
   - Web origins: `https://labs-playground.keycloak.academy`
   - Click **Save**

3. Open [https://labs-playground.keycloak.academy/oidc](https://labs-playground.keycloak.academy/oidc) in your browser.
4. In **Step 1 – Enter your OpenID Connect Provider URL**, enter your issuer URL:
   - Shared: `https://labs-sso.keycloak.academy/realms/{realm}`
   - Local: `http://localhost:8080/realms/{realm}`
5. Click **Next**. The playground fetches `/.well-known/openid-configuration` and displays:
   - `authorization_endpoint`
   - `token_endpoint`
   - `userinfo_endpoint`

> **Note:** To remove later, go to **Clients** → `playground-oidc` → **Actions** → **Delete**.

</details>

---

## Task 2 — Authenticate a user through the authorization code flow

> Estimated time: 10–12 min | Tools: OIDC playground

**Goal:** Complete the full authorization code flow in the playground, observe the authorization request URL, the callback with the code, and the token exchange response.

**Observable outcome:**
- Step 3 shows the live authorization request URL with `response_type=code`, `client_id=playground-oidc`, and the requested scope
- Step 4 shows the callback URL containing the authorization code
- Step 5 shows a JSON token response including `access_token`, `id_token`, `refresh_token`, `expires_in`, and `token_type`

<details>
<summary>Hint — scope requirement</summary>

If the scope does not include `openid`, the token endpoint will not return an ID token. The default playground scope already includes it.

</details>

<details>
<summary>Hint — common token exchange errors</summary>

An `invalid_grant` error usually means the authorization code expired (default lifetime is one minute) or was already consumed. Speed and single use are both enforced.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In Step 2 of the playground, enter:
   - Client ID: `playground-oidc`
   - Click **Next**
2. In Step 3, leave the defaults:
   - Scope parameter: `openid profile email`
   - PKCE: enabled (recommended)
   - Nonce: enabled
   - State: enabled
3. Review the **Authorization request Builder** URL. It should resemble:

   ```
   https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/auth?
     &response_type=code
     &client_id=playground-oidc
     &redirect_uri=https://labs-playground.keycloak.academy/oidc/callback
     &scope=openid profile email
   ```

4. Click **Authorize !**
5. Log in with the test user you created in Lab 2.
6. In Step 4, observe the callback URL containing the `code` parameter.
7. Click **Exchange !**
8. In Step 5, verify the token response contains:

   ```json
   {
     "access_token": "eyJhbGciOiJSUzI1NiIsInR5...",
     "expires_in": 300,
     "refresh_expires_in": 1800,
     "refresh_token": "eyJhbGciOiJIUzI1NiIsInR5...",
     "token_type": "Bearer",
     "id_token": "eyJhbGciOiJSUzI1NiIsInR5...",
     "session_state": "...",
     "scope": "openid profile email"
   }
   ```

> **Note:** You can experiment with:
> - **Prompt = login**: forces re-authentication
> - **Max age = 60**: forces re-authentication if the session is older than 60 seconds
> - **Login hint = your username**: prefills the username field

</details>

---

## Task 3 — Add custom claims to the ID token with a client scope

> Estimated time: 12–15 min | Tools: admin console / OIDC playground

**Goal:** Create a user attribute, expose it through a reusable client scope with a protocol mapper, request that scope in the playground, and verify the custom claim appears in the ID token.

**Observable outcome:**
- The user attribute `badge_num` is saved on the user profile
- The client scope `company` exists with a **User Attribute** mapper named `badge_num`
- When the scope `company` is requested, the decoded ID token contains `"badge_num": "myvalue"`
- When the scope is omitted, the claim is absent

<details>
<summary>Hint — optional vs. default scopes</summary>

Adding a scope as optional means the client must explicitly request it. Adding it as default means it is always included. Optional scopes are useful when you want the client to declare what it needs or when user consent should be granular.

</details>

<details>
<summary>Hint — mapper configuration</summary>

The mapper bridges a user attribute (stored in Keycloak) to a token claim (consumed by the application). The claim name should follow the OpenID Connect standard claims convention when possible.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Add a custom user attribute:
   - Admin console → **Realm settings** → select your test user → **User profile** and then click on **Create attribute** :
   - Attribute [Name]: `badge_num`
   - Click **Add** → **Save**

2. Create a client scope:
   - Left menu → **Client scopes** → **Create client scope**
   - Name: `company`
   - Leave other fields as-is → click **Save**

3. Add a protocol mapper to the scope:
   - Inside the `company` scope → **Mappers** → **Add mapper** → **By configuration** → **User Attribute**
   - Name: `badge_num`
   - User Attribute: `badge_num`
   - Token Claim Name: `badge_num`
   - Claim JSON Type: **String**
   - **Add to ID token**: **On**
   - Click **Save**

4. Attach the scope to the client:
   - Left menu → **Clients** → `playground-oidc` → **Client scopes** tab
   - Click **Add client scope**
   - Select `company` → click **Add (optional)**

5. Test without the scope:
   - In the playground, click **Reset**
   - Re-run Steps 1–4 with the default scope `openid profile email`
   - In Step 5, decode the ID token and confirm `badge_num` is **absent**

6. Test with the scope:
   - Click **Reset**
   - In Step 3, set **Scope parameter** to `openid profile email company`
   - Complete the flow
   - Decode the ID token in Step 5 and confirm `"badge_num": "myvalue"` is present

> **Note:** To undo, remove the optional scope from the client, delete the mapper, delete the `company` client scope, and remove the user attribute.

</details>

---

## Task 4 — Invoke the UserInfo endpoint and map roles into the ID token

> Estimated time: 10–12 min | Tools: admin console / curl / OIDC playground

**Goal:** Enable realm roles in the ID token, verify their presence after a new flow, and call the UserInfo endpoint with an access token to retrieve user claims.

**Observable outcome:**
- The `realm roles` mapper has **Add to ID token** enabled
- A fresh ID token decoded in the playground contains `realm_access.roles` with the assigned role(s)
- A `curl` request to the UserInfo endpoint returns a JSON payload with `sub`, `preferred_username`, and other profile claims
- Calling UserInfo with an access token obtained without `openid` in scope fails

<details>
<summary>Hint — ID token vs. access token audience</summary>

Realm roles in the ID token authenticate the user to the specific client. The same roles in the access token are used to authorize requests to downstream resource servers. The default mapper settings differ for this reason.

</details>

<details>
<summary>Hint — UserInfo scope requirement</summary>

The UserInfo endpoint is part of OIDC. If the authorization request omits `openid`, the resulting access token is an OAuth 2.0 token only, and the UserInfo endpoint will reject it.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Enable realm roles in the ID token:
   - Admin console → **Client scopes** → select the built-in `roles` scope
   - **Mappers** → select `realm roles`
   - Turn on **Add to ID token** → click **Save**

2. Verify in the playground:
   - Click **Reset** and complete a new flow
   - In Step 5, click **Decode** on the ID token
   - Confirm the payload contains:

     ```json
     "realm_access": {
       "roles": ["myrole"]
     }
     ```

3. Call the UserInfo endpoint:
   - In Step 5, copy the raw `access_token`
   - Run:

     ```bash
     curl -s https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/userinfo \
       -H "Authorization: Bearer <access_token>"
     ```

     (Use `http://localhost:8080` for local.)

   - Verify the response resembles:

     ```json
     {
       "sub": "67855660-fd6e-4416-96d1-72c99db5e525",
       "email_verified": true,
       "preferred_username": "alice",
       "given_name": "Alice",
       "family_name": "Smith",
       "email": "alice@example.com"
     }
     ```

4. Verify the scope restriction:
   - Click **Reset**
   - In Step 3, set **Scope parameter** to `profile email` (remove `openid`)
   - Complete the flow
   - Observe that the token response contains **no** `id_token`
   - Copy the `access_token` and call UserInfo again — the request fails because the token was not issued through an OIDC flow

> **Note:** To undo, disable **Add to ID token** on the `realm roles` mapper.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] Discovery succeeded and the three endpoints (authorization, token, userinfo) are displayed in Step 1
- [ ] A user authenticated successfully and an authorization code was returned in the callback (Step 4)
- [ ] Tokens were exchanged and the ID token decoded in Step 5
- [ ] A custom attribute (`badge_num`) appears in the ID token after requesting the `company` scope
- [ ] Realm roles appear in the ID token after enabling **Add to ID token** on the `realm roles` mapper
- [ ] The UserInfo endpoint returns user claims when called with the access token

```bash
# Optional: refresh token test via curl
curl -s -X POST https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token" \
  -d "refresh_token=<refresh_token>" \
  -d "client_id=playground-oidc"
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Add a **Hardcoded claim** mapper directly to the `playground-oidc` client’s dedicated scopes, enable **Add to userinfo**, and observe the new claim in the UserInfo response but not in the ID token.
- Experiment with the `end_session_endpoint` by initiating a logout from the playground and inspecting the redirect behavior and session state cookie removal.
- Read the [OpenID Connect Core 1.0 standard claims](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims) and rename your custom mapper claim to a standard claim name (e.g., `nickname`) to see if the playground or UserInfo response changes.
