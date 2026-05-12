# Lab 18 — FIDO2 / WebAuthn

Aurum Bank's mobile banking team wants to eliminate phishing vectors by replacing password-based primary authentication with FIDO2 passkeys on customer devices. By the end of this lab you will have demonstrated how to integrate WebAuthn into a Keycloak realm, register and authenticate with a passkey, inspect the `amr` claim to distinguish authentication methods, and configure a fully passwordless browser flow.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running and the admin console is reachable at `http://localhost:8080`
- [ ] Your `{realm}` realm is accessible and contains user `alice` (password: `alice`)
- [ ] The `banking-app` React SPA from Lab 4 is running at `http://localhost:3010` and authenticating via Keycloak
- [ ] Lab 7 (Step-Up Authentication) has been completed — you understand how to modify authentication flows and bind them to the realm

If any prerequisite is missing, complete [Lab 4](../004-integrating-applications/README.md) and [Lab 7](../007-step-up-authentication/README.md) before continuing.

---

## Background

### WebAuthn ceremony overview

WebAuthn defines a public-key cryptography ceremony between three parties: the **Relying Party** (Keycloak), the **Client** (the browser), and the **Authenticator** (the device or security key). There are two distinct ceremonies:

| Ceremony | Purpose | What happens |
|---|---|---|
| **Registration** | Bind a new credential to a user account | The authenticator generates a key pair, stores the private key, and returns the public key + attestation to Keycloak |
| **Authentication** | Prove possession of a previously registered credential | The authenticator signs a challenge with the stored private key; Keycloak verifies the signature with the stored public key |

### Passkeys vs. traditional security keys

| Term | Definition | Key characteristic |
|---|---|---|
| **Passkey** | A WebAuthn credential that is **synchronizable** across devices (e.g., Apple iCloud Keychain, Google Password Manager, Windows Hello) | Survives device loss; no shared secret |
| **Platform authenticator** | Built into the device (Touch ID, Face ID, Windows Hello) | Convenient, biometric, tied to the device ecosystem |
| **Roaming authenticator** | External hardware key (YubiKey, Titan Security Key) | Cross-device, portable, often requires physical touch |
| **Attestation** | Cryptographic proof of authenticator provenance | Keycloak can validate attestation to enforce corporate device policies |
| **AAGUID** | Authenticator Attestation GUID | Uniquely identifies the authenticator model; visible in Keycloak under the credential details |

### The `amr` claim

The Authentication Methods Reference (`amr`) claim in the ID token indicates which authentication mechanisms were used during the session. Keycloak populates this claim based on the executions that succeeded in the authentication flow.

| Authentication method | Typical `amr` value |
|---|---|
| Password | `pwd` |
| OTP (TOTP/HOTP) | `otp` |
| WebAuthn | `webauthn` |
| Passwordless WebAuthn | `webauthn` (without `pwd`) |

Observing `amr` after a passkey login confirms that the browser flow executed WebAuthn rather than password validation.

> **Note:** WebAuthn requires HTTPS or `localhost` in production browsers. If you are not running on `localhost`, serve Keycloak and the banking app over TLS before attempting this lab. Safari and Chrome may also require user interaction before invoking `navigator.credentials.create`.

---

## Task 1 — Enable WebAuthn as an authentication flow step

> Estimated time: 10–15 min | Tools: admin console

**Goal:** Add the WebAuthn Authenticator execution to the browser authentication flow and bind the updated flow so that Keycloak offers passkey registration after password login.

**Observable outcome:**
- The browser flow contains a `WebAuthn Authenticator` execution marked **REQUIRED** or **ALTERNATIVE**
- Logging in as `alice` with a password presents a "Register Security Key" or "Register Passkey" prompt
- The admin console **Authentication** tab shows the modified flow without errors

<details>
<summary>Hint — where to add WebAuthn in the flow</summary>

Think about the order of credential collection. The user must first identify themselves (username), then authenticate with a primary credential (password), and only then be offered the option to register a secondary credential (WebAuthn). In Keycloak's browser flow, the WebAuthn execution belongs inside the conditional subflow that runs after successful password validation.

</details>

<details>
<summary>Hint — required vs. alternative</summary>

Setting the execution to **REQUIRED** forces every user to register a passkey on their next login. Setting it to **ALTERNATIVE** makes it optional — users can skip it. For this lab, start with **ALTERNATIVE** so the registration prompt is offered but not enforced.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open the admin console and navigate to **Authentication**.
2. Click the **browser** flow to open its detail view.
3. Click the **⋮** (Actions) menu on the **browser** flow and select **Duplicate**.
4. Name the new flow `Browser with WebAuthn` and click **Duplicate**.
5. Inside the duplicated flow, locate the **Browser - Conditional OTP** subflow (or the conditional subflow that follows password validation).
6. Click the **+** button on the right-hand side of that subflow, then click **Add step**.
7. From the provider list, select **WebAuthn Authenticator** and click **Add**.
8. Drag the new **WebAuthn Authenticator** execution to sit immediately after the **Username Password Form** (or inside the conditional subflow after the password step).
9. Click the requirement dropdown for **WebAuthn Authenticator** and set it to **ALTERNATIVE**.
10. Click the **⋮** (Actions) menu on your new `Browser with WebAuthn` flow and select **Bind flow** → **Browser flow**.
11. Save and confirm the binding.

**Verification:**
- Open an incognito window and navigate to the banking app (`http://localhost:3010`).
- Click **Sign in with Keycloak** and log in as `alice` / `alice`.
- After the password is accepted, Keycloak should display a "Register Security Key" or "Register a Passkey" prompt.
- Do not register yet — cancel the prompt and return to the admin console.

> **Note:** If you ever need to revert, bind the original **browser** flow back to the **Browser flow** binding.

</details>

---

## Task 2 — Register a passkey for a test user in the browser

> Estimated time: 10–15 min | Tools: browser, admin console

**Goal:** Complete the WebAuthn registration ceremony for `alice` and confirm the credential is stored in Keycloak with a visible AAGUID.

**Observable outcome:**
- The browser displays a system-level passkey / security key enrollment dialog (Touch ID, Windows Hello, or security key blinking)
- After enrollment, the user lands on the banking app dashboard
- Admin console → **Users → alice → Credentials** shows a **Passwordless** or **WebAuthn** credential entry with an AAGUID value

<details>
<summary>Hint — what the browser needs</summary>

The browser must be on a secure context (`localhost` or HTTPS) and the user must have interacted with the page before the authenticator can be invoked. If the prompt does not appear, check the browser console for errors such as "NotAllowedError" or "SecurityError".

</details>

<details>
<summary>Hint — credential storage location</summary>

Registered WebAuthn credentials are attached to the user account, not to the client. You can inspect them from the user's **Credentials** tab in the admin console. Look for a table row that lists the authenticator's AAGUID and the registration date.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open an incognito window and navigate to `http://localhost:3010` (the banking app).
2. Click **Sign in with Keycloak**.
3. Enter `alice` / `alice` and submit.
4. When Keycloak shows the "Register a Passkey" (or "Register Security Key") screen, click **Register**.
5. The browser will show a system dialog:
   - **macOS / iOS:** Touch ID or Face ID prompt
   - **Windows:** Windows Hello PIN or biometric prompt
   - **Other:** Insert or tap your security key (YubiKey, etc.)
6. Complete the biometric or PIN verification.
7. Keycloak redirects back to the banking app dashboard.

**Verification in the admin console:**

1. Admin console → **Users → alice → Credentials**.
2. Scroll to the **Passwordless** or **WebAuthn Register Passwordless** section.
3. You should see a credential entry with:
   - **AAGUID** (e.g., `f8a011f3-8c0a-4d15-9446-79c0c1c1e7c6`)
   - **Registered** timestamp
   - **Label** (optional — defaults to the authenticator model name)

**Verification in the frontend (optional):**

A pre-built `PasskeyDetector` component is provided in this lab's `source-complete/banking-app/src/components/` directory. Copy it into your banking app's `src/components/` folder, then drop it into the login page to show a conditional UI hint:

```jsx
// In LoginPage.jsx, import and render:
import PasskeyDetector from '../components/PasskeyDetector.jsx';

// Inside the card, above or below the Sign-in button:
<PasskeyDetector />
```

The component uses `window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()` to detect whether the current device supports passkeys.

> **Note:** Do not delete the credential yet — it is needed for Task 3.

</details>

---

## Task 3 — Authenticate using the passkey and observe the `amr` claim

> Estimated time: 10–15 min | Tools: browser, banking app

**Goal:** Log out and re-authenticate using only the registered passkey, then inspect the ID token to confirm the `amr` claim contains `webauthn` without `pwd`.

**Observable outcome:**
- After logout, signing in again prompts for the passkey instead of a password
- The banking app dashboard displays the ID token claims
- The `amr` array in the token payload includes `webauthn`
- The `acr` claim reflects the authentication flow class reference (e.g., `1` or the level configured in your realm)

<details>
<summary>Hint — when is the passkey offered vs. the password?</summary>

Keycloak offers a passkey when the user has at least one registered WebAuthn credential and the browser flow is configured to use WebAuthn. If the user has both a password and a passkey, the exact UI depends on whether WebAuthn is configured as an alternative or required step. Look for a "Sign in with a passkey" link or button on the login page.

</details>

<details>
<summary>Hint — where to read the `amr` claim</summary>

The banking app from Lab 4 already renders the full ID token payload on the dashboard page inside a `<pre>` block. After passkey authentication, scroll to the `amr` field in that JSON output. If your app does not display claims, decode the ID token at [jwt.io](https://jwt.io).

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In the banking app, click **Sign out** to clear the session.
2. Close the incognito window entirely (to ensure no cached session remains), then open a new incognito window.
3. Navigate to `http://localhost:3010` and click **Sign in with Keycloak**.
4. On the Keycloak login page, enter `alice` and submit (or look for a **Sign in with a passkey** option and click it).
5. The browser prompts for the passkey:
   - Select the previously registered passkey from the list
   - Authenticate with the same biometric / PIN used during registration
6. Keycloak redirects back to the banking app dashboard.
7. On the dashboard, locate the **ID token claims** card and find the `amr` field:
   ```json
   {
     "amr": ["webauthn"],
     "acr": "1"
   }
   ```
   - The presence of `webauthn` confirms the authentication method
   - The absence of `pwd` confirms the password was not used in this session

**Cross-check with the admin console:**

1. Admin console → **Users → alice → Sessions**
2. The active session should list the client (`banking-app-spa` or equivalent) and the authentication method used.

> **Note:** If `amr` contains both `pwd` and `webauthn`, you may have authenticated through a flow that collected both. Ensure you are using a fresh browser session and that the WebAuthn step is not nested inside a password-required subflow.

</details>

---

## Task 4 — Configure passwordless login (WebAuthn as the primary credential)

> Estimated time: 15–20 min | Tools: admin console, browser

**Goal:** Create a dedicated passwordless browser flow where WebAuthn is the primary (and only) authentication mechanism, then bind it to the realm and verify that `alice` can sign in without ever entering a password.

**Observable outcome:**
- A new authentication flow named `Browser Passwordless` exists and is bound to the **Browser flow**
- The flow contains `WebAuthn Passwordless Authenticator` as a **REQUIRED** step
- Logging in as `alice` from a fresh incognito window immediately prompts for the passkey — no username or password screen appears
- The resulting ID token `amr` claim contains only `webauthn`

<details>
<summary>Hint — the passwordless execution is a separate provider</summary>

Keycloak provides two WebAuthn executions: **WebAuthn Authenticator** (used as a second factor after a password) and **WebAuthn Passwordless Authenticator** (used as a standalone first factor). The passwordless variant expects the user to identify themselves via the passkey credential ID alone, skipping the username/password form entirely.

</details>

<details>
<summary>Hint — user verification requirement</summary>

Passwordless authentication relies on the authenticator proving both possession and user verification (biometric or PIN). If the flow fails with a "User verification required" error, check the **WebAuthn Passwordless Authenticator** configuration: the **User Verification Requirement** should be set to **required**.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Admin console → **Authentication**.
2. Click the **browser** flow, then click **Duplicate**.
3. Name the new flow `Browser Passwordless` and click **Duplicate**.
4. Inside `Browser Passwordless`:
   - Delete the **Username Password Form** execution (or the **Username Form** + **Password Form** pair if you customized the flow in Lab 7).
   - Delete any **OTP Form** or **Conditional OTP** subflow — passwordless should not fall back to OTP.
5. Click the **+** button on the top-level flow (not inside a subflow) and select **Add step**.
6. Choose **WebAuthn Passwordless Authenticator** and click **Add**.
7. Set the requirement for **WebAuthn Passwordless Authenticator** to **REQUIRED**.
8. Click the **cog** icon next to **WebAuthn Passwordless Authenticator** to open its configuration:
   - **User Verification Requirement:** `required`
   - **Resident Key:** `required` (ensures the credential is discoverable without a username)
   - Save the configuration.
9. Click the **⋮** (Actions) menu on `Browser Passwordless` and select **Bind flow** → **Browser flow**.
10. Confirm the binding.

**Verification:**

1. Open a fresh incognito window and navigate to `http://localhost:3010`.
2. Click **Sign in with Keycloak**.
3. The browser should immediately show the passkey selection dialog (no username or password screen).
4. Select `alice`'s passkey and authenticate with biometric / PIN.
5. You are redirected to the banking app dashboard.
6. Inspect the ID token claims and confirm:
   ```json
   {
     "amr": ["webauthn"]
   }
   ```

**Restore the default flow (cleanup):**

After verifying passwordless login, bind the original **browser** flow back to the **Browser flow** binding so that subsequent labs expect a standard username/password login. You may leave `alice`'s passkey credential in place — it does not interfere with password-based flows.

> **Note:** Passwordless flows require every user to have a registered passkey. If a user without a credential attempts to log in, Keycloak will show an error. In production, provide a fallback flow or an onboarding path that lets users register a passkey from a known device before switching to passwordless.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] The `Browser with WebAuthn` flow contains a `WebAuthn Authenticator` step and was bound to the realm
- [ ] `alice` has a registered WebAuthn credential visible under **Users → alice → Credentials** with a non-empty AAGUID
- [ ] After logout, `alice` can authenticate using the passkey without entering a password
- [ ] The ID token `amr` claim contains `webauthn` after passkey authentication
- [ ] The `Browser Passwordless` flow was created, bound, and tested; the original **browser** flow was restored afterward

**macOS / Linux:**

```bash
# Optional CLI verification: inspect alice's credentials via Keycloak Admin API
# Replace {realm}, {token}, and {keycloak} with your values.
curl -s "http://localhost:8080/admin/realms/{realm}/users/{alice-uuid}/credentials" \
  -H "Authorization: Bearer {token}" | jq '.[] | select(.type | contains("webauthn"))'
```

**Windows (PowerShell):**

```powershell
# Optional CLI verification: inspect alice's credentials via Keycloak Admin API (jq must be installed)
# Replace {realm}, {token}, and {keycloak} with your values.
curl.exe -s "http://localhost:8080/admin/realms/{realm}/users/{alice-uuid}/credentials" `
  -H "Authorization: Bearer {token}" | jq '.[] | select(.type | contains(\"webauthn\"))'
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- **Attestation policy enforcement:** Configure the WebAuthn authenticator to require attestation and restrict AAGUIDs to a corporate device whitelist. Test that a consumer passkey (e.g., Apple iCloud Keychain) is rejected while a corporate-managed authenticator is accepted.
- **Step-up with WebAuthn:** Combine this lab with Lab 7 by creating a flow that uses passwordless WebAuthn for level-1 access, then requires a second WebAuthn credential (or OTP) for level-2 access to high-value transactions. Observe how the `amr` claim evolves across the step-up boundary.
- **Cross-device passkeys (hybrid transport):** Register a passkey on a mobile device and authenticate from a desktop browser using a QR code and Bluetooth hybrid transport. Inspect the authenticator metadata in Keycloak to confirm the transport method (`hybrid`, `internal`, `usb`, etc.).
