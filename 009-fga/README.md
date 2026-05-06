# Lab 9 — Fine-Grained Authorization

Aurum Bank's document vault must enforce access rules that are too granular for coarse role-based checks: a customer can view their own statements during business hours, a relationship manager can view any statement in their branch, and an auditor can view any statement but never delete it. By the end of this lab you will have demonstrated how to model these rules as Keycloak resources, scopes, policies, and permissions, and how to obtain and use a Requesting Party Token (RPT) to access a protected endpoint.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running and the admin console is reachable
- [ ] The `{realm}` realm is accessible and contains users `jdoe` (password: `jdoe`, role: `admin`) and `alice` (password: `alice`, role: `user`)
- [ ] A confidential client (e.g., `document-vault`) exists in the realm from [Lab 5 — OAuth Authorization](005-oauth-authorization)
- [ ] Postman is installed and the `_ Fine-grained Permissions.postman_collection.json` from this lab folder is imported
- [ ] The document-vault resource server is running — start it with `dotnet run` from `document-vault-api/`; it listens on `http://localhost:8090`

**Import the Postman files from this lab folder:**
- `_ Fine-grained Permissions.postman_collection.json`

After importing, create or update a Postman environment with:

| Variable | Value |
|---|---|
| `KC_URL` | `http://localhost:8080` or your cloud instance |
| `KC_REALM` | your realm name |
| `APP_URL` | `http://localhost:8090` |

If any prerequisite is missing, complete [Lab 5 — OAuth Authorization](005-oauth-authorization) before continuing.

---

## Background

### Authorization Services core concepts

| Concept | Definition | Example in Aurum Bank |
|---|---|---|
| **Resource** | A protected entity (URI, object, or abstract item) | `Monthly Statement Q1`, `Wire Transfer Record` |
| **Scope** | An action that can be performed on a resource | `view`, `delete`, `modify`, `approve` |
| **Policy** | A rule that evaluates to grant or deny | `User has role admin`, `Time is between 09:00 and 17:00` |
| **Permission** | A binding of resource/scope to policy | `Allow admin to delete Wire Transfer Record` |
| **RPT** (Requesting Party Token) | An access token containing granted permissions | JWT with `authorization.permissions` claim |
| **PEP** (Policy Enforcement Point) | The interceptor that checks permissions before serving a request | Keycloak policy enforcer or custom middleware |


### Why this matters for Aurum Bank

Roles answer "who are you?" — scopes answer "what did the user consent to?" — but fine-grained authorization answers "can you perform this specific action on this specific document right now?" Time-based, role-based, and resource-based policies can be combined without changing application code.

> **Note:** Authorization Services requires the client to be **confidential** and **Authorization Enabled** turned on. Public clients cannot use the Authorization Services API because they cannot authenticate to the token endpoint for RPT exchange.

> **Note for .NET developers:** Keycloak ships a native Policy Enforcer for Java (`keycloak-policy-enforcer`) and Quarkus (`quarkus-keycloak-authorization`) that validates RPTs automatically via configuration. **No official equivalent exists for .NET.** The `document-vault-api` in this lab implements the same logic explicitly — inspecting `authorization.permissions` in the JWT — which is both the correct production pattern for .NET applications and a useful learning artifact that makes the mechanism visible rather than hiding it behind a library.

---

## Task 1 — Enable Authorization Services on a confidential client and define resources and scopes

> Estimated time: 10–15 min | Tools: admin console

**Goal:** Turn on Authorization Services for the `document-vault` client, create two resources (`Document Resource`, `Administration Resource`), and define scopes (`view`, `delete`, `modify`).

**Observable outcome:**
- The `document-vault` client shows an **Authorization** tab in the admin console
- Under **Authorization → Resources**, two resources exist with associated URIs
- Under **Authorization → Scopes**, the scopes `view`, `delete`, and `modify` are listed

<details>
<summary>Hint — where to enable authorization</summary>

Authorization Services is a client-level capability. Look in the client's **Settings** or **Capability config** for a toggle that enables authorization. The client must already be confidential (client authentication ON).

</details>

<details>
<summary>Hint — resource URIs vs resource names</summary>

A resource's **URI** is what the PEP matches against the incoming request path. The **name** is what you reference in permission tickets and RPT requests. Both can be the same, but they serve different purposes: URI for path matching, name for API references.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Enable Authorization Services:**
   - Admin console → **Clients → document-vault → Settings**
   - Scroll to **Capability config**
   - Toggle **Authorization** to **ON**
   - Save
   - An **Authorization** tab now appears on the client

2. **Create scopes:**
   - Admin console → **Clients → document-vault → Authorization → Scopes**
   - Click **Create authorization scope**
   - Name: `view` → Save
   - Repeat for `delete` and `modify`

3. **Create resources:**
   - Admin console → **Clients → document-vault → Authorization → Resources**
   - Click **Create resource**
   - Name: `Document Resource`
   - Display name: `Document Resource`
   - URIs: `/api/documents/*`
   - Scopes: select `view`, `delete`, `modify`
   - Save

   - Click **Create resource** again
   - Name: `Administration Resource`
   - Display name: `Administration Resource`
   - URIs: `/api/admin`
   - Scopes: select `view`, `modify`
   - Save

4. **Verify the resources exist:**
   - The **Resources** list should show all three with their URIs and scopes

</details>

---

## Task 2 — Write role-based and time-based policies

> Estimated time: 10–15 min | Tools: admin console

**Goal:** Create two policies — one that grants access to users with the `admin` realm role, and one that grants access only during business hours (09:00–17:00). These policies will later be attached to permissions.

**Observable outcome:**
- Under **Authorization → Policies**, a `Role-based Policy` named `Admin Policy` exists, targeting the `admin` realm role
- A `Time-based Policy` named `Business Hours Policy` exists, active from 09:00 to 17:00
- The policies can be evaluated independently in the admin console **Evaluate** tab

<details>
<summary>Hint — policy types in Keycloak</summary>

Keycloak offers several policy types: Role-based, Time-based, User-based, Aggregated, and JavaScript (deprecated). For this lab you need two specific types. Look in the **Policies** tab for a **Create policy** dropdown.

</details>

<details>
<summary>Hint — time policy timezone</summary>

Time-based policies use the server's system timezone by default. If you are testing outside 09:00–17:00 local time, either adjust your system clock, create a time policy that covers your current time, or use the **Evaluate** tab to simulate a different time.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Create the `Admin Policy` (role-based):**
   - Admin console → **Clients → document-vault → Authorization → Policies**
   - Click **Create client policy → Role**
   - Name: `Admin Policy`
   - Description: `Grants access to admin users`
   - Realm roles: click **Assign role** → choose `admin`
   - Logic: **Positive**
   - Save

2. **Create the `Business Hours Policy` (time-based):**
   - Click **Create policy → Time**
   - Name: `Business Hours Policy`
   - Description: `Grants access during business hours`
   - Enable the **Repeat** toggle — this reveals the day/month/hour/minute range fields
   - Fill in **all** numeric range fields — the evaluator requires explicit from/to values for every visible field:
     - Day of Month: `1` to `31`
     - Month: `1` to `12`
     - Hour: `9` to `17`
     - Minute: `0` to `59`
   - Logic: **Positive**
   - Save



</details>

---

## Task 3 — Test resource-based and scope-based permissions via Postman

> Estimated time: 15–20 min | Tools: Postman, admin console

**Goal:** Create permissions that bind resources and scopes to the policies from Task 2, then test them using the Postman collection.

**Observable outcome:**
- A resource-based permission `Administration Resource Permission` exists, binding `Administration Resource` to `Admin Policy`
- A scope-based permission `Document View Permission` exists, binding `Document Resource` + scope `view` to `Business Hours Policy`
- Postman request `Request api/admin path` with `jdoe`'s token returns 200 (admin role)
- Postman request `Request /api/documents/* path` with `alice`'s token returns 200 during business hours and 403 outside business hours

<details>
<summary>Hint — resource-based vs scope-based</summary>

A **resource-based permission** protects the entire resource regardless of scope. A **scope-based permission** protects only specific actions (scopes) on that resource. You need both types in this task.

</details>

<details>
<summary>Hint — which Postman request to use</summary>

Use the `Get Token from KC` request first to populate `KC_TOKEN`, then use `Request api/admin path` and `Request /api/documents/* path` to test access. Remember to change the username in the token request between `jdoe` and `alice`.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Create the resource-based permission:**
   - Admin console → **Clients → document-vault → Authorization → Permissions**
   - Click **Create resource-based permission**
   - Name: `Administration Resource Permission`
   - Description: `Only admins can access administration endpoints`
   - **Apply To Resource Type**: leave **off** — this toggle protects all resources sharing a type; here we are protecting one specific named resource
   - Resources: select `Administration Resource`
   - Policy: select `Admin Policy`
   - Decision Strategy: **Unanimous**
   - Save

2. **Create the scope-based permission:**
   - Click **Create scope-based permission**
   - Name: `Document View Permission`
   - Description: `Document viewing allowed during business hours`
   - Resource: select `Document Resource` — this filters the Scopes list to only the scopes attached to that resource
   - Scopes: select `view`
   - Policy: select `Business Hours Policy`
   - Decision Strategy: **Unanimous**
   - Save

3. **Test with Postman:**

   a. **Get a token for `jdoe`:**
      - Open `Get Token from KC`
      - Set `username = jdoe`, `password = jdoe`
      - Click **Send**
      - The `KC_TOKEN` environment variable is updated automatically

   b. **Test admin access:**
      - Open `Request api/admin path` → click **Send**
      - Expected: `200 OK` (or the application's response) because `jdoe` has the `admin` role

   c. **Get a token for `alice`:**
      - Open `Get Token from KC`
      - Set `username = alice`, `password = alice`
      - Click **Send**

   d. **Test document access:**
      - Open `Request /api/documents/* path` → click **Send**
      - Expected: `200 OK` during business hours, `403 Forbidden` outside business hours

   e. **Verify the 403 outside business hours:**
      - If you are testing outside 09:00–17:00, the request returns 403 because the `Business Hours Policy` does not evaluate to true
      - Temporarily edit the `Business Hours Policy` to cover your current time, re-test, then restore it

> **Note:** The `document-vault-api` app included in this lab is the PEP. It validates the RPT's `authorization.permissions` claim on every request. Run it with `dotnet run` from `document-vault-api/` before testing with Postman.

</details>

---

## Task 4 — Obtain and use an RPT to access a protected document endpoint

> Estimated time: 15–20 min | Tools: Postman

**Goal:** Use the `urn:ietf:params:oauth:grant-type:uma-ticket` grant to exchange an access token for an RPT, then use the RPT as a Bearer token to access a protected resource.

**Observable outcome:**
- The `Get RPT` request in Postman returns a new token containing `authorization.permissions` in the payload
- Using the RPT in `Request /api/documents/* path` returns 200 even if the original access token would have been rejected
- The RPT payload at [jwt.io](https://jwt.io) shows the granted permissions array with resource names and scopes

<details>
<summary>Hint — what an RPT is and when you need it</summary>

An RPT is a specialized access token that encodes the specific permissions granted by Keycloak's authorization engine. You need it when the resource server uses a PEP that expects permission claims rather than simple role or scope checks. The grant type is `urn:ietf:params:oauth:grant-type:uma-ticket`.

</details>

<details>
<summary>Hint — permission parameter format</summary>

The `permission` parameter in the RPT request can be a resource name (e.g., `Document Resource`) or a resource name plus scope separated by `#` (e.g., `Document Resource#view`). Requesting a specific scope is more precise and demonstrates scope-based evaluation.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. **Ensure you have a valid access token:**
   - Run `Get Token from KC` with `username = jdoe` to populate `KC_TOKEN`

2. **Request an RPT:**
   - Open `Get RPT` in Postman
   - The request is pre-configured with:
     - `grant_type`: `urn:ietf:params:oauth:grant-type:uma-ticket`
     - `audience`: `document-vault`
     - `permission`: `Document Resource`
     - `response_include_resource_name`: `true` ← required so the RPT JWT contains `rsname`; without it Keycloak only embeds the resource UUID (`rsid`) and the PEP cannot match by name
   - Click **Send**
   - The response contains a new `access_token` — this is the RPT. Postman saves it to the `Token` environment variable.

3. **Inspect the RPT:**
   - Copy the RPT value from the response
   - Paste it into [jwt.io](https://jwt.io)
   - In the payload, look for:
     ```json
     {
       "authorization": {
         "permissions": [
           {
             "rsid": "...",
             "rsname": "Document Resource",
             "scopes": ["view", "delete", "modify"]
           }
         ]
       }
     }
     ```
   - This proves the authorization server granted access to `Document Resource`

4. **Use the RPT to access the endpoint:**
   - Open `Request /api/documents/* path`
   - Manually change the `Authorization` header from `Bearer {{KC_TOKEN}}` to `Bearer {{Token}}` (the RPT)
   - Click **Send**
   - Expected: `200 OK`

5. **Test with a permission the user does not have:**
   - In `Get RPT`, change the `permission` value to `Administration Resource`
   - Ensure you are using `alice`'s token (alice does not have the `admin` role)
   - Click **Send**
   - Expected: `403 Forbidden` with an error indicating access was denied

> **Note:** The RPT is short-lived and contains a snapshot of permissions at request time. If policies change (e.g., business hours end), the existing RPT may still be valid until it expires. For high-security scenarios, configure short RPT lifespans or use introspection.

> **PEP implementation note (.NET):** Keycloak ships a native policy enforcer for Java (`keycloak-policy-enforcer`) and Quarkus (`quarkus-keycloak-authorization`) that does this RPT claim inspection automatically via configuration — no custom code required on those stacks. No official equivalent exists for .NET. The `document-vault-api` implements the same logic explicitly: read `authorization.permissions`, match `rsname` and `scopes`. This is both the correct production pattern for .NET applications and a useful learning artifact — you can see exactly what a native enforcer does internally.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] The `document-vault` client has Authorization Enabled and shows the Authorization tab
- [ ] Three resources (`User Resource`, `Document Resource`, `Administration Resource`) exist with correct URIs and scopes
- [ ] `Admin Policy` (role-based) and `Business Hours Policy` (time-based) exist under Authorization → Policies
- [ ] `Request api/admin path` with `jdoe`'s token returns 200; with `alice`'s token returns 403
- [ ] `Get RPT` returns a token containing `authorization.permissions` with the requested resource and scopes
- [ ] Using the RPT in `Request /api/documents/* path` returns 200

**macOS / Linux:**

```bash
# Optional CLI verification: introspect the RPT to see permissions
curl -s -X POST "http://localhost:8080/realms/{realm}/protocol/openid-connect/token/introspect" \
  -u "document-vault:{client_secret}" \
  -d "token={rpt}" | jq '.permissions'
```

**Windows (PowerShell):**

```powershell
# Optional CLI verification: introspect the RPT to see permissions (jq must be installed)
curl.exe -s -X POST "http://localhost:8080/realms/{realm}/protocol/openid-connect/token/introspect" `
  -u "document-vault:{client_secret}" `
  -d "token={rpt}" | jq '.permissions'
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Create an **Aggregated Policy** that combines `Admin Policy` AND `Business Hours Policy` with a **Unanimous** decision strategy, then attach it to `Administration Resource` to require both admin role AND business hours
- Write a **User-based Policy** that grants access only to `alice` for a specific resource, then test it by requesting an RPT as both `alice` and `jdoe`
- Configure the Keycloak Policy Enforcer in a Spring Boot or Quarkus application and observe the PEP automatically rejecting requests that lack a valid RPT
- Refactor the `document-vault-api` PEP to use ASP.NET Core's policy-based authorization: implement `IAuthorizationRequirement` and `AuthorizationHandler<T>` to encapsulate the RPT claim check, register named policies (`DocumentView`, `AdminView`), and replace the inline `HasPermission` calls with `.RequireAuthorization("DocumentView")` — this is the production pattern once the mechanism is understood
