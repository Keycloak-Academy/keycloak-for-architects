# Lab 2 — Identity & Protocol Fundamentals

This lab addresses the foundational question every architect faces before integrating an application: what Keycloak objects must exist so that an application can delegate authentication and receive meaningful identity claims? By the end, you will have demonstrated that you can provision a realm namespace, create and credential a test user, assign roles and group membership, register a public OIDC client, and verify end-to-end authentication in a browser-based application.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running at `http://localhost:8080` (local) or the shared cloud URLs are accessible
- [ ] You can log in to the admin console with admin credentials
- [ ] Node.js and npm are installed

If any prerequisite is missing, complete [Lab 1 — Environment Setup](../001-environment-setup/README.md) before continuing.

---

## Background

### Realm as security boundary

A **realm** is the namespace your application registers into. It is fully isolated from other realms — it has its own configuration, its own set of clients (applications), and its own users. When your app sends an authentication request it always targets a specific realm, and the tokens it receives are signed by that realm's keys. This isolation also means a single Keycloak installation can host completely separate environments, for example one realm for internal employee-facing apps and another for customer-facing ones.

### Keycloak web interfaces

| Console | Audience | Purpose |
|---|---|---|
| **Admin console** | Developers / operators | Configure realms, clients, users, roles, identity providers, and security policies |
| **Account console** | End users | Self-service profile, password, MFA, sessions, and application access review |

Understanding the account console is valuable for developers because it represents functionality you get for free without building it yourself: password-change flows, MFA enrollment, and session management can all be delegated to Keycloak.

### Roles, groups, and token claims

| Concept | Scope | How it surfaces in tokens |
|---|---|---|
| **Realm role** | Global within the realm | Mapped into `realm_access.roles` (when configured) |
| **Group** | Collection of users | Membership can be mapped to claims; attributes are inherited by members |
| **Client role** | Specific to one client | Mapped into `resource_access.{client}.roles` |

> **Note:** Composite roles allow one role to include others dynamically. They are powerful but can introduce management complexity and performance overhead if deeply nested.

---

## Task 1 — Create a realm and explore its settings

> Estimated time: 5–7 min | Tools: admin console

**Goal:** Create (or select) a realm and inspect the settings that govern token lifespans, signing keys, and frontend URLs.

**Observable outcome:**
- The realm name appears in the top-left corner next to **Current realm**
- The **General**, **Tokens**, and **Keys** tabs are visible and populated
- You can identify the token lifespan and the active signing algorithm

<details>
<summary>Hint — where to start</summary>

Look for the realm selector near the top-left of the admin console. If you are on the shared cluster, your realm is already provisioned and you only need to select it.

</details>

<details>
<summary>Hint — what matters for later labs</summary>

Pay attention to the **Tokens** tab values (access token lifespan, refresh token lifespan) and the **Keys** tab active signing key. These directly affect how your applications validate tokens and when users must re-authenticate.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open the admin console:
   - Shared: `https://labs-sso-admin.keycloak.academy/admin/{realm}/console/`
   - Local: `http://localhost:8080/admin/{realm}/console/`
2. If running locally and no realm exists yet:
   - Click **Manage realms** (top-left) → **Create realm**
   - Enter a name (e.g. `training`) and click **Create**
3. Click **Manage realms** and select your realm — its name appears next to **Current realm**.
4. Browse **Realm Settings** in the left-hand menu:
   - **General** tab: display name, frontend URL
   - **Tokens** tab: token lifespans (e.g., access token lifespan, refresh token max reuse)
   - **Keys** tab: active signing keys your application will use to verify tokens

> **Note:** No cleanup needed for this task; it is read-only exploration.

</details>

---

## Task 2 — Create a user, group, and realm role

> Estimated time: 10–12 min | Tools: admin console

**Goal:** Provision a test user with a password, create a group and a realm role, and assign both to the user so that later token inspections show real claims.

**Observable outcome:**
- The user appears in the **Users** list with **Email Verified** set as you chose
- The group (e.g. `customers`) exists and the user is listed under its members
- The realm role (e.g. `myrole`) exists and appears in the user’s **Role Mappings**

<details>
<summary>Hint — user lifecycle</summary>

A user cannot log in until credentials are created separately from the user record. Consider whether you want the user to change the password on first login.

</details>

<details>
<summary>Hint — groups vs. roles</summary>

Groups are for organizing users and sharing attributes; roles are the permission claims applications read from tokens. You can map group membership into tokens, but the simplest path to a claim is a realm role assignment.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Create the user:
   - Left menu → **Users** → **Add user**
   - Username: `alice`
   - Fill in email, first name, and last name as desired
   - Toggle **Email Verified** if you know the email is valid
   - Click **Create**
2. Set the password:
   - Open the **Credentials** tab
   - **Set Password** → enter a password → click **Save**
   - Decide whether to leave **Temporary** enabled (forces a password change on first login)
3. Create a group:
   - Left menu → **Groups** → **Create group**
   - Name: `customers` → click **Create**
4. Add the user to the group:
   - Left menu → **Users** → select `alice`
   - **Groups** tab → **Join Group** → select `customers` → **Join**
5. Create a realm role:
   - Left menu → **Realm roles** → **Create Role**
   - Role name: `myrole`
   - Optional: add a description → click **Save**
6. Assign the role to the user:
   - Left menu → **Users** → select `alice`
   - **Role Mappings** tab → **Assign role** → filter **Realm roles** → select `myrole` → **Assign**

> **Note:** To undo, remove the role mapping, remove the user from the group, delete the group, delete the user, and delete the role in reverse order.

</details>

---

## Task 3 — Register a public OIDC client for the React SPA

> Estimated time: 7–10 min | Tools: admin console

**Goal:** Register the `banking-app` as a public OIDC client with the correct redirect and web-origin constraints so the React SPA can initiate login.

**Observable outcome:**
- The client `banking-app` appears in the **Clients** list
- **Client authentication** is **Off** and **Standard flow** is enabled
- **Valid redirect URIs** contains `http://localhost:3010/callback`
- **Web origins** contains `http://localhost:3010`

<details>
<summary>Hint — public vs. confidential</summary>

A browser-based SPA cannot keep a client secret safe, so it must be registered as a public client. This disables client-secret-based authentication at the token endpoint and enables PKCE by default.

</details>

<details>
<summary>Hint — redirect URI safety</summary>

Keycloak will reject authorization requests whose redirect URI is not explicitly listed. The post-logout redirect URI is equally important for a clean logout experience.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In the admin console, go to **Clients** → **Create client**
2. On the first page:
   - Client type: **OpenID Connect**
   - Client ID: `banking-app`
   - Name: `Banking App`
   - Click **Next**
3. On the second page:
   - Client authentication: **Off** (public client)
   - Authentication flow: **Standard flow** only
   - Click **Next**
4. On the final page:
   - Valid redirect URIs: `http://localhost:3010/callback`
   - Valid post-logout redirect URIs: `http://localhost:3010/`
   - Web origins: `http://localhost:3010`
   - Click **Save**

> **Note:** To remove later, go to **Clients** → `banking-app` → **Actions** → **Delete**.

</details>

---

## Task 4 — Run the React SPA and complete end-to-end login

> Estimated time: 10–12 min | Tools: terminal / browser

**Goal:** Configure the React application with your realm details, start it locally, sign in through Keycloak, and observe the ID token claims rendered by the app.

**Observable outcome:**
- `http://localhost:3010` loads and shows a **Sign in with Keycloak** button
- After login, the app displays:
  - A personalised greeting with the user’s first name
  - Their email address
  - The role(s) assigned to them
  - The raw ID token claims
- The browser network tab shows a redirect to Keycloak and a callback with a `code` parameter

<details>
<summary>Hint — environment file</summary>

The SPA reads Keycloak connection parameters from a local `.env` file. Copy the example file and replace the placeholder realm name with your own.

</details>

<details>
<summary>Hint — shared vs. local URLs</summary>

The issuer base URL and the admin console URL differ between the shared cloud environment and a local Docker instance. Use the correct scheme and host or the SPA will fail to discover endpoints.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Copy the environment file:

   ```bash
   cp banking-app/.env.example banking-app/.env
   ```

2. Edit `banking-app/.env`:
   - Shared cloud:

     ```
     KC_URL=https://labs-sso.keycloak.academy
     KC_REALM=your-realm-name
     CLIENT_ID=banking-app
     ```

   - Local:

     ```
     KC_URL=http://localhost:8080
     KC_REALM=your-realm-name
     CLIENT_ID=banking-app
     ```

3. Install dependencies and start the app:

   ```bash
   cd banking-app
   npm install
   npm start
   ```

4. Open `http://localhost:3010` in a browser.
5. Click **Sign in with Keycloak**.
6. Log in with the user credentials you created in Task 2.
7. After redirect, verify the app shows:
   - Greeting with first name
   - Email address
   - Assigned roles
   - Raw ID token claims

> **Note:** To stop the app, press `Ctrl+C` in the terminal. The app does not mutate shared configuration.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] A realm is selected and its name appears in the top-left corner of the admin console
- [ ] A user (e.g. `alice`) exists with a password set
- [ ] A realm role (e.g. `myrole`) exists and is assigned to the user
- [ ] A group (e.g. `customers`) exists and the user is a member
- [ ] The `banking-app` client is registered as a public OIDC client with the correct redirect URI
- [ ] The banking app runs locally at `http://localhost:3010` and shows the hello screen after login

```bash
# Optional CLI verification (local Docker with kcadm alias)
kcadm get users -r training -q username=alice
kcadm get roles -r training -q search=myrole
kcadm get clients -r training -q clientId=banking-app
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Enable the **Temporary** password flag for your test user, log in through the SPA, and observe the forced password-change flow before the ID token is issued.
- Create a composite role that includes `myrole` plus another role, assign only the composite role to a second test user, and inspect the ID token to confirm both roles appear.
- Redirect the user to the account console from the SPA (or open it directly) and explore self-service password update and session revocation.
