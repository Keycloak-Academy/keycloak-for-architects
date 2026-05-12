# Lab 18 — FIDO2 / WebAuthn

Aurum Bank's mobile banking team wants to eliminate phishing vectors by replacing password-based primary authentication with FIDO2 passkeys on customer devices. Keycloak 26.4 promoted passkeys from a preview feature to first-class support, with **Conditional UI** (autofill) handled by the default `browser` flow — no flow duplication required. By the end of this lab you will have demonstrated how to enable passkeys at the realm policy level, register a passkey for a test user, sign in using the browser's native Conditional UI prompt, inspect the `amr` claim, and add a `Conditional - credential` step that skips 2FA when a passkey was used.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak **26.4 or newer** is running and the admin console is reachable at `http://localhost:8080`
- [ ] Your `{realm}` realm is accessible and contains user `alice` (password: `alice`)
- [ ] The `banking-app` React SPA from Lab 4 is running at `http://localhost:3010` and authenticating via Keycloak
- [ ] A device with a platform authenticator (Touch ID, Face ID, Windows Hello) or a roaming authenticator (YubiKey)
- [ ] A modern browser that supports Conditional UI (Chrome 108+, Safari 16+, Edge 108+)

If any prerequisite is missing, complete [Lab 4](../004-integrating-applications/README.md) before continuing.

---

## Background

### What changed in Keycloak 26.4

Earlier Keycloak versions required duplicating the `browser` flow and inserting a `WebAuthn Authenticator` or `WebAuthn Passwordless Authenticator` execution to use passkeys. Keycloak 26.4 made passkeys a supported, configurable feature of the **default browser flow** by gating it on a single realm policy toggle and surfacing the credential picker via the browser's Conditional UI.

| Approach | Pre-26.4 | 26.4+ |
|---|---|---|
| Enable passkeys | Duplicate `browser`, add WebAuthn execution, bind flow | Toggle **Passkeys** in `Webauthn Passwordless Policy` |
| Skip 2FA when passkey used | Build a separate passwordless flow | Add `Conditional - credential` step to the OTP subflow |
| User-facing UX | "Sign in with Security Key" button | Native browser autofill on the username field |

### WebAuthn ceremony overview

WebAuthn defines a public-key cryptography ceremony between three parties: the **Relying Party** (Keycloak), the **Client** (the browser), and the **Authenticator** (the device or security key). There are two distinct ceremonies:

| Ceremony | Purpose | What happens |
|---|---|---|
| **Registration** | Bind a new credential to a user account | The authenticator generates a key pair, stores the private key, returns the public key + attestation to Keycloak |
| **Authentication** | Prove possession of a previously registered credential | The authenticator signs a challenge with the stored private key; Keycloak verifies the signature with the stored public key |

### Conditional UI vs. Modal UI

Keycloak 26.4 offers two passkey UX styles, both driven by the same `Webauthn Passwordless Policy`:

| UI style | Triggered by | When to use |
|---|---|---|
| **Conditional UI** | Username field rendered with `autocomplete="username webauthn"` — browser surfaces matching passkeys when the user focuses the field | Platform passkeys synced via iCloud Keychain, Google Password Manager, Windows Hello |
| **Modal UI** | User clicks the **Sign in with Passkey** button on the login form | Hardware roaming authenticators (YubiKey) and cross-device hybrid transport (QR + Bluetooth) |

Both styles co-exist on the same login screen — the user can either focus the username field (Conditional) or click the button (Modal). No flow change is needed to enable either path; both are produced by the default `browser` flow when passkeys are enabled in the policy.

### Passkeys vs. traditional security keys

| Term | Definition | Key characteristic |
|---|---|---|
| **Passkey** | A WebAuthn credential that is **synchronizable** across devices (e.g., Apple iCloud Keychain, Google Password Manager) | Survives device loss; no shared secret |
| **Platform authenticator** | Built into the device (Touch ID, Face ID, Windows Hello) | Convenient, biometric, tied to device ecosystem |
| **Roaming authenticator** | External hardware key (YubiKey, Titan Security Key) | Cross-device, portable, often requires physical touch |
| **Resident key (discoverable credential)** | Credential stored on the authenticator with user metadata, so the user can be identified without typing a username | Required for Conditional UI |
| **AAGUID** | Authenticator Attestation GUID | Uniquely identifies the authenticator model; visible in Keycloak under credential details |

### The `amr` claim

The Authentication Methods Reference (`amr`) claim in the ID token indicates which authentication mechanisms were used during the session. Keycloak populates this claim based on the executions that succeeded in the authentication flow.

| Authentication method | Typical `amr` value |
|---|---|
| Password | `pwd` |
| OTP (TOTP/HOTP) | `otp` |
| WebAuthn / Passkey | `webauthn` |

Observing `amr` after a passkey login confirms the credential type Keycloak processed.

> **Note:** WebAuthn requires HTTPS or `localhost` in production browsers. If you are not running on `localhost`, serve Keycloak and the banking app over TLS before attempting this lab.

---

## Task 1 — Enable passkeys on the realm

> Estimated time: 5–10 min | Tools: admin console

**Goal:** Turn on passkey support at the realm policy level so that the default `browser` flow renders Conditional UI on the login page and the `Webauthn Register Passwordless` required action becomes available.

**Observable outcome:**
- **Authentication → Policies → Webauthn Passwordless Policy** shows **Passkeys: On**
- **Authentication → Required actions** lists `Webauthn Register Passwordless` as **Enabled**
- The login page rendered for the realm still uses the default `browser` flow (no custom flow created or bound)

<details>
<summary>Hint — where to find the policy</summary>

Passkeys are governed by the **passwordless** WebAuthn policy, not the two-factor one. There are two separate policies in the admin console under **Authentication → Policies**; the one you want is the one whose name contains `Passwordless`.

</details>

<details>
<summary>Hint — why no flow change is needed</summary>

In 26.4 the default `browser` flow already renders the username form with the `autocomplete="username webauthn"` hint and includes a hidden **Sign in with Passkey** button. The policy toggle is what activates both. If you find yourself duplicating the flow, stop — you are following the pre-26.4 procedure.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open the admin console and select your realm.
2. Navigate to **Authentication → Policies → Webauthn Passwordless Policy**.
3. Toggle **Passkeys** to **On**.
4. Confirm the following defaults (leave as-is unless you have a corporate requirement):
   - **User Verification Requirement:** `preferred` or `required`
   - **Require Resident Key:** `Yes` (this is what makes Conditional UI possible)
   - **Signature Algorithms:** `ES256`, `RS256` (defaults)
   - **Attestation Conveyance Preference:** `not specified` (defaults)
5. Click **Save**.
6. Navigate to **Authentication → Required actions**.
7. Locate `Webauthn Register Passwordless`. Confirm **Enabled** is on. Optionally toggle **Set as default action** if you want every new user to be prompted to register a passkey on first login.

**Verification:**

- Open an incognito window and navigate to `http://localhost:8080/realms/{realm}/account`.
- The Account Console should display a **Passkeys** (or **Passwordless**) section under **Account Security → Signing in**.
- Inspect the page source of the login page (`/realms/{realm}/protocol/openid-connect/auth?...`) and confirm the username `<input>` has `autocomplete="username webauthn"`.

</details>

---

## Task 2 — Register a passkey for `alice`

> Estimated time: 5–10 min | Tools: browser, admin console

**Goal:** Use the Account Console to register a passkey for `alice` and confirm the credential is stored in Keycloak with a visible AAGUID.

**Observable outcome:**
- The browser shows a system-level passkey enrollment dialog (Touch ID, Windows Hello, or security key)
- After enrollment, the Account Console **Signing in** page lists the new passkey with a label and creation date
- Admin console → **Users → alice → Credentials** shows a `passkey` (or `webauthn-passwordless`) credential entry with an AAGUID value

<details>
<summary>Hint — where users register their own passkeys</summary>

Self-service credential management lives in the Account Console (`/realms/{realm}/account`), not the admin console. Look for a **Set up Passkey** or **Add** button next to the Passkeys section. An admin can also force enrollment by attaching the `Webauthn Register Passwordless` required action to a user.

</details>

<details>
<summary>Hint — if the system dialog does not appear</summary>

Conditional and Modal UI both require a **secure context** (`localhost` or HTTPS) and a **user gesture** (a click) before invoking `navigator.credentials.create`. If nothing happens, open the browser devtools console and look for `NotAllowedError`, `SecurityError`, or `InvalidStateError`.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In an incognito window, navigate to `http://localhost:8080/realms/{realm}/account`.
2. Sign in as `alice` / `alice`.
3. Click **Account Security → Signing in** in the left navigation.
4. Locate the **Passkeys** (or **Passwordless**) section and click **Set up Passkey** (or **Add**).
5. The browser will show a system dialog:
   - **macOS / iOS:** Touch ID or Face ID prompt
   - **Windows:** Windows Hello PIN or biometric prompt
   - **Other:** Insert or tap your security key (YubiKey, etc.)
6. Complete the biometric or PIN verification.
7. When prompted, give the passkey a label (e.g., `Laptop Touch ID`) and click **Submit**.

**Verification in the admin console:**

1. Admin console → **Users → alice → Credentials**.
2. You should see a credential entry with:
   - **Type:** `passkey` or `webauthn-passwordless`
   - **AAGUID** (e.g., `f8a011f3-8c0a-4d15-9446-79c0c1c1e7c6`)
   - **Created** timestamp
   - **Label** (the value you entered above)

**Optional — show a passkey hint in the banking app:**

A pre-built `PasskeyDetector` component is provided in this lab's `source-complete/banking-app/src/components/` directory. Copy it into your banking app's `src/components/` folder, then render it on the login page:

```jsx
import PasskeyDetector from '../components/PasskeyDetector.jsx';

// Inside the card, above or below the Sign-in button:
<PasskeyDetector />
```

The component uses `window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()` to detect whether the current device supports passkeys.

> **Note:** Do not delete the credential yet — it is needed for Task 3.

</details>

---

## Task 3 — Authenticate with the passkey using Conditional UI

> Estimated time: 10–15 min | Tools: browser, banking app

**Goal:** Sign in to the banking app via the browser's Conditional UI prompt and verify the ID token `amr` claim contains `webauthn`.

**Observable outcome:**
- Focusing the username field on the Keycloak login page surfaces the registered passkey in the browser's native autofill dropdown
- Selecting the passkey completes authentication without typing the username or password
- The banking app dashboard renders an ID token whose `amr` array contains `webauthn`
- A **Sign in with Passkey** button is also visible on the login page (Modal UI), and clicking it produces the same result

<details>
<summary>Hint — what triggers the autofill prompt</summary>

Conditional UI is wired to the username field via `autocomplete="username webauthn"`. The browser only surfaces passkeys when the field is **focused** by a user gesture and a matching discoverable credential exists for the relying party's origin. If you clicked **Sign in with Passkey** explicitly, that is the Modal path — close the dialog and try focusing the username field directly to test Conditional UI.

</details>

<details>
<summary>Hint — where to read the `amr` claim</summary>

The banking app from Lab 4 already renders the full ID token payload on the dashboard page inside a `<pre>` block. After passkey authentication, scroll to the `amr` field in that JSON output. If your app does not display claims, decode the ID token at [jwt.io](https://jwt.io).

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open a fresh incognito window and navigate to `http://localhost:3010` (the banking app).
2. Click **Sign in with Keycloak**.
3. On the Keycloak login page:
   - **Conditional UI path:** Click into the **Username** input. The browser should display an autofill dropdown listing `alice`'s passkey (often with an icon and the label you set in Task 2). Select it.
   - **Modal UI path (alternative):** Click the **Sign in with Passkey** button below the password field. The browser opens its credential picker dialog. Select `alice`'s passkey.
4. Authenticate with the same biometric / PIN used during registration.
5. Keycloak redirects back to the banking app dashboard.
6. On the dashboard, locate the **ID token claims** card and find the `amr` field:
   ```json
   {
     "amr": ["webauthn"],
     "acr": "1"
   }
   ```
   - The presence of `webauthn` confirms passkey was used
   - The absence of `pwd` confirms the password was not entered

**Cross-check with the admin console:**

1. Admin console → **Users → alice → Sessions**.
2. The active session should list the client (`banking-app-spa` or equivalent) and the authentication method used.

> **Note:** If your browser does not surface the autofill dropdown, confirm: (1) you are on `localhost` or HTTPS, (2) the passkey was registered to the same origin as the login page, and (3) your browser supports Conditional UI (Chrome 108+, Safari 16+, Edge 108+). On older browsers, only the Modal path will work.

</details>

---

## Task 4 — Skip 2FA when the user authenticated with a passkey

> Estimated time: 10–15 min | Tools: admin console, browser

**Goal:** Add the `Conditional - credential` authenticator to the OTP subflow of the default `browser` flow so that a user who signed in with a passkey is not asked for a second factor (since the passkey already provides user verification and possession).

**Observable outcome:**
- The `browser` flow contains a `Conditional - credential` step inside the conditional OTP subflow, configured to match `passkey` (or `webauthn-passwordless`)
- A user with both a passkey and a configured OTP signs in with the passkey and lands on the banking app dashboard **without** being prompted for an OTP code
- The same user, signing in with the password, is still prompted for the OTP — the conditional only skips 2FA when the passkey path was taken
- The `amr` claim is `["webauthn"]` on the passkey login and `["pwd", "otp"]` on the password login

<details>
<summary>Hint — what the conditional checks</summary>

`Conditional - credential` evaluates whether a specific credential **type** has been used in the current authentication. If the configured credential type matches what the user just authenticated with, the surrounding subflow is **disabled** (skipped). Place it as the first execution inside the conditional subflow that gates OTP.

</details>

<details>
<summary>Hint — credential type name</summary>

The credential type string Keycloak uses for passkeys registered under the Passwordless policy is `passkey` in 26.4+ (older builds may surface `webauthn-passwordless`). Check **Users → alice → Credentials** to see the exact type recorded against the passkey you registered in Task 2 and use that value in the `Conditional - credential` configuration.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Admin console → **Authentication → Flows → browser**.
2. Locate the **Browser - Conditional OTP** subflow (the conditional subflow that prompts for OTP when the user has one configured).
3. Click the **+** menu on that subflow → **Add step**.
4. Select **Conditional - credential** and click **Add**.
5. Drag the new **Conditional - credential** execution to the **top** of the conditional OTP subflow.
6. Set its requirement to **REQUIRED**.
7. Click the **cog** icon next to **Conditional - credential** to open its configuration:
   - **Alias:** `skip-otp-if-passkey` (any descriptive name)
   - **Credential Type:** `passkey` (or `webauthn-passwordless` if that is what your build records — see Hint 2)
   - **Negate output:** **Off** — we want the subflow to run only when the credential type was **not** used. (If your build defaults to running the subflow on match, toggle this; verify with the test below.)
8. Click **Save**.

**Prepare a test user with both factors:**

1. Admin console → **Users → alice → Credentials**.
2. Click **Set up OTP** (or attach the `Configure OTP` required action and sign in once to enroll). Use any TOTP app.
3. Confirm `alice` now has both a `password`, a `passkey`, and an `otp` credential.

**Verification — passkey path skips OTP:**

1. Fresh incognito window → `http://localhost:3010` → **Sign in with Keycloak**.
2. Sign in via the passkey (Conditional UI autofill or Modal **Sign in with Passkey**).
3. You should be redirected to the banking app dashboard **without** an OTP prompt.
4. ID token `amr` claim: `["webauthn"]`.

**Verification — password path still requires OTP:**

1. Fresh incognito window → `http://localhost:3010` → **Sign in with Keycloak**.
2. Type the username `alice` and password `alice` (ignore the Conditional UI dropdown).
3. Keycloak should now prompt for the OTP code.
4. Enter a valid OTP from your TOTP app.
5. ID token `amr` claim: `["pwd", "otp"]`.

> **Note:** If both paths skip or both paths prompt, the `Negate output` setting is inverted from what your build expects. Toggle it and retest. The intent is: *skip the OTP subflow when (and only when) the user authenticated with a passkey.*

**Cleanup (optional):** Remove the `Conditional - credential` step before continuing to later labs if you prefer the default 2FA behavior. You may leave `alice`'s passkey credential in place — it does not interfere with password-based flows.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] **Authentication → Policies → Webauthn Passwordless Policy** has **Passkeys: On**
- [ ] `Webauthn Register Passwordless` is enabled under **Authentication → Required actions**
- [ ] `alice` has a registered passkey credential visible under **Users → alice → Credentials** with a non-empty AAGUID
- [ ] Focusing the username field on the login page surfaces `alice`'s passkey in the browser's autofill dropdown (Conditional UI)
- [ ] A `Sign in with Passkey` button is visible on the login page (Modal UI) and produces a successful login
- [ ] The ID token `amr` claim contains `webauthn` after a passkey login
- [ ] The `Conditional - credential` step in the default `browser` flow causes the OTP prompt to be skipped on passkey logins but still enforced on password logins

**macOS / Linux:**

```bash
# Optional CLI verification: inspect alice's credentials via Keycloak Admin API
# Replace {realm}, {token}, and {alice-uuid} with your values.
curl -s "http://localhost:8080/admin/realms/{realm}/users/{alice-uuid}/credentials" \
  -H "Authorization: Bearer {token}" | jq '.[] | select(.type | test("webauthn|passkey"))'
```

**Windows (PowerShell):**

```powershell
# Optional CLI verification: inspect alice's credentials via Keycloak Admin API (jq must be installed)
# Replace {realm}, {token}, and {alice-uuid} with your values.
curl.exe -s "http://localhost:8080/admin/realms/{realm}/users/{alice-uuid}/credentials" `
  -H "Authorization: Bearer {token}" | jq '.[] | select(.type | test(\"webauthn|passkey\"))'
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- **Attestation policy enforcement:** Set **Attestation Conveyance Preference** to `direct` in the `Webauthn Passwordless Policy` and configure a trust list of acceptable AAGUIDs. Test that a consumer passkey (e.g., Apple iCloud Keychain) is rejected while a corporate-managed authenticator is accepted.
- **Conditional UI on the banking app side:** Replace the static **Sign in with Keycloak** button with a username input on the SPA itself, decorated with `autocomplete="username webauthn"`, that drives a `navigator.credentials.get({ mediation: "conditional" })` call directly against Keycloak's WebAuthn endpoint — exposing the same passkey via your own UI.
- **Cross-device passkeys (hybrid transport):** Register a passkey on a mobile device and authenticate from a desktop browser using a QR code and Bluetooth hybrid transport. Inspect the authenticator metadata in Keycloak to confirm the transport method (`hybrid`, `internal`, `usb`, etc.).
- **Authentication Policies (26.4+):** Use the new realm-level **Authentication Policies** feature to require passkey-strength authentication for high-value clients (e.g., the payments client) while leaving low-risk clients on password + OTP. Observe how the `acr` claim differs across clients without modifying the underlying flow.
