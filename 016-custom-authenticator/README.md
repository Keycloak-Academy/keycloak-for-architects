# Lab 16 — Building a Custom Authenticator

This lab addresses the need to go beyond built-in authentication flows and implement runtime logic that adapts the login experience based on contextual risk. By the end, you will have demonstrated how to implement a custom `Authenticator` using the Keycloak Authentication SPI, package it as a provider JAR, deploy it into the server, wire it into a custom flow, and observe risk-based step-up behavior driven by brute-force detection data.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running locally or in a lab environment where you can restart the server
- [ ] You have write access to `$KC_HOME/providers` (Linux/macOS) or `%KC_HOME%\providers` (Windows) and can run `$KC_HOME/bin/kc.sh build` (Linux/macOS) or `%KC_HOME%\bin\kc.bat build` (Windows)
- [ ] Maven (`./mvnw` on Linux/macOS, `mvnw.cmd` on Windows) is available to build the provider
- [ ] The realm has a test user (e.g., `alice`) with a known password
- [ ] The account console or another client is available to test login

If any prerequisite is missing, complete [Lab 8] before continuing.

---

## Background

### Authentication SPI overview

Keycloak's Authentication SPI allows developers to plug custom logic into authentication flows. Every authenticator implements two interfaces:

| Interface | Responsibility |
|---|---|
| `AuthenticatorFactory` | Declares metadata, configuration properties, and requirements |
| `Authenticator` | Implements runtime logic via `authenticate()` and `action()` |

### Risk-based and step-up authentication

Rather than always requiring a second factor, risk-based authentication evaluates context at runtime to decide whether step-up is necessary. Factors can include:

- Failed login attempt count (brute-force detection)
- Device fingerprint or location
- External fraud detection score

This lab uses a simple risk score based solely on failed login attempts. After three consecutive failures, the next successful password entry forces OTP configuration and verification.

### Brute Force Detection

Keycloak tracks failed login attempts per user when **Brute Force Detection** is enabled. The custom authenticator reads this state and sets a user attribute (`my.risk.based.auth.2fa.required`) that the downstream **Conditional OTP Form** execution consumes to decide whether to prompt for OTP.

### Code location

The example code for this lab is available under the `simple-risk-based-authenticator` folder in this repository.

---

## Task 1 — Build and deploy the custom authenticator

> Estimated time: 5–7 min | Tools: terminal / Maven

**Goal:** Compile the `simple-risk-based-authenticator` project and install the resulting JAR into Keycloak's `providers` directory.

**Observable outcome:**
- `target/simple-risk-based-authenticator.jar` exists after Maven build.
- The JAR is copied to `$KC_HOME/providers/`.
- Keycloak starts successfully after `kc.sh build`.
- The custom authenticator **My Simple Risk-Based Authenticator** appears in the authentication flow designer.

<details>
<summary>Hint — why a build step is required</summary>

Keycloak is built on Quarkus. Adding a provider changes the deployment model, so the runtime must be re-augmented before the new classes are available.

</details>

<details>
<summary>Hint — how to confirm the provider loaded</summary>

After restarting, look in the admin console under the authentication flow designer. If the provider is correctly deployed, it will appear as an execution type you can add to a sub-flow.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open a terminal and navigate to the provider directory:

   ```bash
   cd simple-risk-based-authenticator
   ```

2. Build the JAR:

   - macOS / Linux:

     ```bash
     ./mvnw clean package
     ```

   - Windows (PowerShell):

     ```powershell
     .\mvnw.cmd clean package
     ```

3. Copy the artifact to Keycloak:

   - macOS / Linux:

     ```bash
     cp target/simple-risk-based-authenticator.jar $KC_HOME/providers/
     ```

   - Windows (PowerShell):

     ```powershell
     Copy-Item target\simple-risk-based-authenticator.jar "$env:KC_HOME\providers\"
     ```

4. Rebuild the Keycloak runtime:

   - macOS / Linux:

     ```bash
     $KC_HOME/bin/kc.sh build
     ```

   - Windows (PowerShell):

     ```powershell
     & "$env:KC_HOME\bin\kc.bat" build
     ```

5. Start Keycloak:

   - macOS / Linux:

     ```bash
     $KC_HOME/bin/kc.sh start-dev
     ```

   - Windows (PowerShell):

     ```powershell
     & "$env:KC_HOME\bin\kc.bat" start-dev
     ```

6. In the Admin Console, go to **Authentication** → **Flows** and confirm **My Simple Risk-Based Authenticator** is available when adding an execution.

> **Note:** To remove the provider later, delete the JAR from `$KC_HOME/providers/` (Linux/macOS) or `%KC_HOME%\providers\` (Windows), run `kc.sh build` / `kc.bat build`, and restart.

</details>

---

## Task 2 — Create and bind a risk-based browser flow

> Estimated time: 7–10 min | Tools: admin console

**Goal:** Duplicate the built-in **Browser** flow, replace the default OTP execution with the custom risk-based authenticator, and bind the new flow as the realm's browser flow.

**Observable outcome:**
- A new flow named **My Risk-Based Browser Flow** exists.
- The **Browser - Conditional OTP** sub-flow contains:
  - **My Simple Risk-Based Authenticator** (REQUIRED)
  - **Conditional OTP Form** (REQUIRED)
- The realm's **Browser flow** binding points to **My Risk-Based Browser Flow**.

<details>
<summary>Hint — how to safely modify a built-in flow</summary>

Keycloak does not allow editing built-in flows directly. You must create a copy first. The copy inherits all executions, which you can then adjust.

</details>

<details>
<summary>Hint — what the sub-flow must enforce</summary>

The custom authenticator evaluates risk and sets a user attribute. The conditional OTP form must be configured to look for that exact attribute name and force OTP when the attribute is present.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Go to **Authentication** → **Flows**.
2. Locate the **Browser** flow, click the **⋮** menu, and select **Duplicate**.
3. Name the copy **My Risk-Based Browser Flow**.
4. Inside **My Risk-Based Browser Flow**, locate the sub-flow **My Risk-Based Browser Flow Browser - Conditional OTP**.
5. Delete the existing **OTP Form** execution from that sub-flow.
6. Ensure the sub-flow itself is marked **REQUIRED**.
7. Add an execution:
   - Select **My Simple Risk-Based Authenticator**.
   - Set it to **REQUIRED**.
8. Add another execution:
   - Select **Conditional OTP Form**.
   - Set it to **REQUIRED**.
9. Click the **⚙** (gear) icon on the **Conditional OTP Form** row and configure:
   - **Alias**: `conditional-otp`
   - **OTP control User Attribute**: `my.risk.based.auth.2fa.required`
   - **Fallback OTP handling**: `force`
10. Click **Save**.
11. Click the **⋮** menu on **My Risk-Based Browser Flow** and select **Bind flow** → **Browser flow**.
12. Confirm the realm now uses **My Risk-Based Browser Flow** for browser logins.

> **Note:** To revert, bind the original **Browser** flow back under **Authentication** → **Bindings**.

</details>

---

## Task 3 — Enable Brute Force Detection

> Estimated time: 3–5 min | Tools: admin console

**Goal:** Enable **Brute Force Detection** so that failed login attempts are tracked and available to the custom authenticator.

**Observable outcome:**
- The **Brute Force Detection** toggle is ON under **Realm Settings**.
- Failed login attempts for a user are recorded and visible in the user's **Events** or **Details** tab.

<details>
<summary>Hint — where security defenses live</summary>

Brute-force protection is a realm-level security defense, not an authentication flow setting. Look under the realm settings for a tab related to security and login protections.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In the Admin Console, go to **Realm Settings**.
2. Click the **Security Defenses** tab.
3. Scroll down to the **Brute Force Detection** section.
4. Toggle **Enabled** to **ON**.
5. (Optional) Adjust thresholds such as **Max Login Failures** and **Wait Increment** to match your testing pace.
6. Click **Save**.

> **Note:** Disable brute-force detection after the lab if it interferes with other testing.

</details>

---

## Task 4 — Verify risk-based step-up authentication

> Estimated time: 5–7 min | Tools: browser

**Goal:** Confirm that a user with no failed logins can authenticate with password only, but after three consecutive failed attempts the next successful login forces OTP.

**Observable outcome:**
- `alice` logs in with password only on the first attempt.
- After three failed login attempts with an incorrect password, the fourth attempt with the correct password prompts for OTP configuration.
- On subsequent logins after the OTP is configured, the user is prompted for the OTP code.

<details>
<summary>Hint — what behavior proves the authenticator is working</summary>

The authenticator itself does not present a UI. It silently sets a user attribute. The visible change is that the conditional OTP form activates after the risk threshold is crossed.

</details>

<details>
<summary>Hint — how to reset the risk state</summary>

The risk state is tied to the user's failed login count tracked by brute-force detection. That count decays over time or can be cleared by an administrator.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open the account console at `https://labs-sso.keycloak.academy/realms/{realm}/account`.
2. Log in as `alice` with the correct password.
3. Confirm you are logged in after providing only the password (no OTP prompt).
4. Log out.
5. Attempt to log in again, but enter an **incorrect** password. Repeat this three times.
6. On the fourth attempt, enter the **correct** password.
7. You should now be prompted to configure an OTP authenticator.
8. Complete OTP setup and finish login.
9. Log out and log in again with the correct password.
10. Confirm you are now prompted for the OTP code.

> **Note:** To return the user to a clean state, clear the user's **Failed Login Attempts** count in the admin console or disable and re-enable brute-force detection.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] The custom flow **My Risk-Based Browser Flow** is bound to the Browser flow in **Authentication → Bindings**
- [ ] **Brute Force Detection** is enabled under **Realm Settings → Security Defenses**
- [ ] After three consecutive failed logins, the user is prompted for OTP on the next successful attempt
- [ ] A fresh user with no failed attempts can log in with password only

**macOS / Linux:**

```bash
# Optional: list authentication flows and their bindings via Admin API
curl -s -H "Authorization: Bearer <ADMIN_TOKEN>" \
  "https://labs-sso.keycloak.academy/admin/realms/{realm}/authentication/flows" | jq -r '.[].alias'
```

**Windows (PowerShell):**

```powershell
# Optional: list authentication flows and their bindings via Admin API (jq must be installed)
curl.exe -s -H "Authorization: Bearer <ADMIN_TOKEN>" `
  "https://labs-sso.keycloak.academy/admin/realms/{realm}/authentication/flows" | jq -r '.[].alias'
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Extend the authenticator to read an external risk score from a REST API and combine it with the local failed-login count.
- Add device fingerprinting logic by inspecting request headers and geolocation, then store a trusted-device flag on the user profile.
- Implement a custom required action that is triggered instead of the conditional OTP form, presenting the user with a CAPTCHA challenge when risk is elevated.
