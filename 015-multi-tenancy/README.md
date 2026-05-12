# Lab 15 — Multi-Tenancy with Organizations

This lab addresses the need to serve multiple distinct customers or business units from a single Keycloak realm while preserving isolation of users, branding, and authentication policy. By the end, you will have demonstrated how to model two organizations within one realm, assign users to organizations, route authentication locally or through an external IdP per organization, and include organization context in tokens.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running and accessible at `https://labs-sso-admin.keycloak.academy/admin/{realm}/console/`
- [ ] The `{realm}` realm is accessible with admin credentials
- [ ] The realm contains at least one test user (e.g., `jdoe`) available for organization assignment
- [ ] The client `expensely` exists in the realm

If any prerequisite is missing, complete [Lab 5] before continuing.

---

## Background

### Organizations vs. realms

Keycloak traditionally isolates tenants at the realm boundary. Realms are fully separate — users, clients, and roles do not overlap. The **Organization** feature (introduced in Keycloak 24+) adds a lighter-weight tenancy layer *inside* a single realm, allowing shared clients and realm-level configuration while keeping user membership and authentication routing organization-specific.

| Approach | Isolation level | Use case |
|---|---|---|
| Separate realms | Full | Strict regulatory or operational separation |
| Organizations within one realm | Logical | Shared infrastructure, distinct customer accounts |

### Authentication routing per organization

Each organization can define its own identity providers. When a user authenticates, Keycloak can route the login to:

- **Local authentication** — username/password managed inside the realm.
- **External IdP** — redirect to an organization-specific SAML or OIDC provider.

### Token claims

Organization membership can be surfaced in tokens via the **organization** client scope. This allows downstream applications to make authorization decisions based on which organization the user belongs to.

---

## Task 1 — Enable the Organization feature and create two organizations

> Estimated time: 5–7 min | Tools: admin console

**Goal:** Enable organizations for the realm and create two organizations with distinct domains.

**Observable outcome:**
- The **Organizations** toggle is ON in **Realm Settings**.
- **TechCo Solutions** exists with domain `techcosolutions.com`.
- **GreenEarth Logistics** exists with domain `greenearthlogistics.com`.

<details>
<summary>Hint — where the feature flag lives</summary>

Organization support is a realm-level setting. Look inside the realm settings for a section specifically named after the feature, not under authentication or user federation.

</details>

<details>
<summary>Hint — what a domain represents</summary>

The domain associated with an organization is used for routing and discovery. Consider how Keycloak might use this value when a user enters an email address at login time.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Log in to the Keycloak Admin Console and select your realm.
2. Go to **Realm Settings** → **Organizations** and confirm the toggle is **ON**.
3. In the left sidebar, click **Organizations**.
4. Click **Create organization**.
   - **Name**: `TechCo Solutions`
   - **Domain**: `techcosolutions.com`
   - Click **Save**.
5. Click **Create organization** again.
   - **Name**: `GreenEarth Logistics`
   - **Domain**: `greenearthlogistics.com`
   - Click **Save**.
6. Confirm both organizations appear in the list.

> **Note:** If you need to restore the environment later, delete the organizations from the same list.

</details>

---

## Task 2 — Assign a user to an organization

> Estimated time: 3–5 min | Tools: admin console

**Goal:** Add the realm user `jdoe` to the **TechCo Solutions** organization.

**Observable outcome:**
- `jdoe` appears under the **Members** tab of **TechCo Solutions**.
- The user record shows organization membership.

<details>
<summary>Hint — where membership is managed</summary>

Organization membership is not managed from the global **Users** list. Open the organization itself to find the tab that controls who belongs to it.

</details>

<details>
<summary>Hint — invitation vs. direct add</summary>

There are two ways to add members: directly from existing realm users, or by sending an email invitation. For a user who already exists in the realm, the direct path is faster.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Go to **Organizations** → **TechCo Solutions**.
2. Click the **Members** tab.
3. Click **Add member**.
4. Select the user `jdoe` from the list and confirm.
5. Verify `jdoe` now appears under **Members**.

> **Note:** You can also use **Invite member** to send an email invitation link. That path is out of scope for this lab but useful for onboarding external users.

</details>

---

## Task 3 — Configure external IdP authentication for an organization

> Estimated time: 5–7 min | Tools: admin console

**Goal:** Configure **GreenEarth Logistics** to redirect authentication to its external identity provider.

**Observable outcome:**
- **GreenEarth Logistics** has an identity provider link under its **Identity Providers** tab.
- Users authenticating on behalf of **GreenEarth Logistics** are redirected to the external IdP.

<details>
<summary>Hint — where organization IdPs live</summary>

Each organization can override or supplement realm-level identity providers. Look for a tab inside the organization detail page that is named after the concept of brokering.

</details>

<details>
<summary>Hint — which provider to select</summary>

The lab environment provides a pre-configured external identity provider named after the organization itself. You do not need to create a new provider from scratch.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Go to **Organizations** → **GreenEarth Logistics**.
2. Click the **Identity Providers** tab.
3. Click **Add identity provider**.
4. Select the external identity provider named **GreenEarth Logistics** from the list.
5. Click **Save**.
6. Confirm the provider appears under the organization's **Identity Providers** tab.

> **Note:** Removing the provider link will restore local authentication for this organization.

</details>

---

## Task 4 — Include the organization claim in tokens

> Estimated time: 3–5 min | Tools: admin console / curl / OIDC playground

**Goal:** Add the **organization** client scope to the `expensely` client so that the ID token includes organization membership.

**Observable outcome:**
- The `expensely` client has the `organization` scope assigned.
- An ID token obtained for a user contains an `organization` claim with the organization name.

<details>
<summary>Hint — where scopes are assigned to a client</summary>

Client scopes are managed from the client detail page. Look for a tab that lists default and optional scopes.

</details>

<details>
<summary>Hint — default vs. optional</summary>

If the scope is optional, the client or authorization request must explicitly request it. If it is default, it is included automatically. Consider which behavior fits a multi-tenant application.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Go to **Clients** and select `expensely`.
2. Click the **Client scopes** tab.
3. Click **Add client scope**.
4. Select `organization` and add it as **Optional** (or **Default** if you want it always included).
5. Click **Save**.
6. Authenticate a user assigned to **TechCo Solutions** via the `expensely` client.
7. Inspect the ID token and verify it contains:

   ```json
   "organization": {
     "techco-solutions": {
       "name": ["TechCo Solutions"]
     }
   }
   ```

> **Note:** Remove the `organization` scope from the client if it interferes with other lab configurations.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] The **Organizations** feature is enabled in **Realm Settings**
- [ ] **TechCo Solutions** and **GreenEarth Logistics** are created with correct domains
- [ ] The user `jdoe` is a member of **TechCo Solutions**
- [ ] **GreenEarth Logistics** has the external IdP linked under its **Identity Providers** tab
- [ ] The `expensely` client includes the `organization` scope
- [ ] An ID token for an organization member contains the `organization` claim

**macOS / Linux:**

```bash
# Optional: request a token and inspect the organization claim
curl -s -X POST \
  -d "grant_type=password" \
  -d "client_id=expensely" \
  -d "username=jdoe" \
  -d "password=<PASSWORD>" \
  "https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/token" | jq -R 'split(".") | .[1] | @base64d | fromjson | .organization'
```

**Windows (PowerShell):**

```powershell
# Optional: request a token and inspect the organization claim (jq must be installed)
curl.exe -s -X POST `
  -d "grant_type=password" `
  -d "client_id=expensely" `
  -d "username=jdoe" `
  -d "password=<PASSWORD>" `
  "https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/token" | jq -R 'split(\".\") | .[1] | @base64d | fromjson | .organization'
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Create an organization-specific theme and bind it so that each organization sees distinct branding on the login page.
- Configure organization-level role mappings and observe how they interact with realm roles in token claims.
- Use the email invitation flow to onboard a new member and inspect the invitation token structure.
