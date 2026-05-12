# Lab 17 — User Experience Customization — Themes and Email Templates

This lab addresses the need to align Keycloak's end-user-facing UI with corporate branding and to control the content of transactional emails. By the end, you will have demonstrated how to create, build, and deploy a custom login theme; how to configure a client to use that theme; and how to override email templates and subject lines using FreeMarker and message bundles.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running locally or in a lab environment where you can restart the server
- [ ] You have write access to `$KC_HOME/providers` (Linux/macOS) or `%KC_HOME%\providers` (Windows) and can run `$KC_HOME/bin/kc.sh build` (Linux/macOS) or `%KC_HOME%\bin\kc.bat build` (Windows)
- [ ] Maven (`./mvnw` on Linux/macOS, `mvnw.cmd` on Windows) is available to build the theme JAR
- [ ] The realm has a test user and a client (e.g., `account-console`) available for testing login
- [ ] SMTP settings are configured or a local mail catcher (e.g., Mailpit) is available for email testing

If any prerequisite is missing, complete [Lab 2] before continuing.

---

## Background

### Theme types and inheritance

Keycloak themes are organized by type. Each type controls a different surface area:

| Theme type | Controls |
|---|---|
| `login` | Login, registration, password reset, OTP setup pages |
| `account` | End-user account console (legacy) |
| `email` | Transactional email content and subject lines |
| `admin` | Administration console styling |

Themes inherit from a parent. In Keycloak 26, the recommended base is `keycloak.v2` (PatternFly 4). You override only the files you want to change.

### Theme structure

A minimal login theme directory looks like this:

```
mytheme/
└── login/
    ├── theme.properties
    ├── messages/
    └── resources/
        ├── css/
        ├── img/
        └── js/
```

- `theme.properties` — declares the parent theme and resource mappings.
- `resources/` — static assets served by Keycloak.
- `messages/` — property files for localized strings.

### Email templates

Email themes use **FreeMarker** (`.ftl`) for HTML bodies and `.properties` files for subject lines. Common templates include:

| Template | Purpose |
|---|---|
| `email-verification.ftl` | Email address verification |
| `password-reset.ftl` | Password reset link |
| `executeActions.ftl` | Generic required-action email |

Available variables in all email templates:

| Variable | Description |
|---|---|
| `${user.firstName}` | User's first name |
| `${user.email}` | User's email address |
| `${realmName}` | Display name of the realm |
| `${link}` | Action link (verification URL, reset password URL) |
| `${linkExpiration}` | Human-readable link expiry duration |

### Caching

Keycloak caches templates and theme configuration for performance. In development mode (`kc.sh start-dev`), caching is disabled automatically. For production, configure caching explicitly if you need to support dynamic theme updates.

> **Note:** Theme parents in Keycloak 26:
> - `keycloak.v2` — PatternFly 4 base; stable and recommended for custom themes.
> - `keycloak.v3` — PatternFly 5 base; ships in KC 26 but still evolving.
> - `keycloak` — legacy PatternFly 3 base; deprecated, avoid for new themes.

---

## Task 1 — Build and deploy a custom login theme

> Estimated time: 7–10 min | Tools: terminal / Maven / admin console

**Goal:** Compile the example `mytheme` project, deploy it to Keycloak, and configure the `account-console` client to use it.

**Observable outcome:**
- `target/mytheme.jar` exists after Maven build.
- The JAR is copied to `$KC_HOME/providers/` and Keycloak starts after `kc.sh build`.
- The `account-console` client has **Login Theme** set to `mytheme`.
- The login page reflects custom CSS styling instead of the default Keycloak theme.

<details>
<summary>Hint — how themes are packaged</summary>

Keycloak themes can be deployed as JARs placed in the `providers` directory, just like custom authenticators. The JAR must contain resources under a standard theme path.

</details>

<details>
<summary>Hint — where to select the theme for a client</summary>

Each client can specify its own login theme. Look in the client settings for a dropdown related to appearance or theme.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Navigate to the theme project:

   ```bash
   cd labs/themes-and-emails/mytheme
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
     cp target/mytheme.jar $KC_HOME/providers/
     ```

   - Windows (PowerShell):

     ```powershell
     Copy-Item target\mytheme.jar "$env:KC_HOME\providers\"
     ```

4. Rebuild and start Keycloak:

   - macOS / Linux:

     ```bash
     $KC_HOME/bin/kc.sh build
     $KC_HOME/bin/kc.sh start-dev
     ```

   - Windows (PowerShell):

     ```powershell
     & "$env:KC_HOME\bin\kc.bat" build
     & "$env:KC_HOME\bin\kc.bat" start-dev
     ```

5. In the Admin Console, go to **Clients** and select `account-console`.
6. Under **Login Theme**, select `mytheme` from the dropdown.
7. Click **Save**.
8. Open `https://labs-sso.keycloak.academy/realms/{realm}/account` in a browser.
9. Confirm the login page shows the custom styling (e.g., custom CSS classes defined in `css/signin.css`).

> **Note:** To revert, set **Login Theme** back to `keycloak.v2` (or blank) on the client.

</details>

---

## Task 2 — Customize email templates and subject lines

> Estimated time: 7–10 min | Tools: file editor / admin console / mail catcher

**Goal:** Override the password-reset email template and its subject line in the custom theme, set the realm email theme to `mytheme`, and verify the rendered email.

**Observable outcome:**
- The theme contains an `email/html/password-reset.ftl` template.
- The theme contains an `email/messages/messages_en.properties` file with a custom subject line.
- The realm **Email Theme** is set to `mytheme`.
- Triggering a password reset sends an email that reflects the custom template and subject.

<details>
<summary>Hint — two files are needed for a complete override</summary>

An email override requires both a FreeMarker template for the body and a message bundle entry for the subject. Without the properties file, Keycloak falls back to the default subject line.

</details>

<details>
<summary>Hint — how to test without a real SMTP server</summary>

You can configure a local mail catcher such as Mailpit or MailHog as the realm's SMTP server. This lets you inspect rendered emails without sending them to real inboxes.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. In your theme directory, create the email structure if it does not exist:

   ```
   mytheme/
   └── email/
       ├── messages/
       │   └── messages_en.properties
       └── html/
           └── password-reset.ftl
   ```

2. Copy the base template to override:

   - macOS / Linux:

     ```bash
     cp $KC_HOME/themes/base/email/html/password-reset.ftl \
        mytheme/email/html/password-reset.ftl
     ```

   - Windows (PowerShell):

     ```powershell
     Copy-Item "$env:KC_HOME\themes\base\email\html\password-reset.ftl" `
       mytheme\email\html\password-reset.ftl
     ```

3. Edit `mytheme/email/html/password-reset.ftl` to customize the HTML content.

4. Create or edit `mytheme/email/messages/messages_en.properties`:

   ```properties
   passwordResetSubject=Reset your {0} password
   emailVerificationSubject=Verify your email address for {0}
   ```

5. Rebuild and redeploy the theme JAR:

   - macOS / Linux:

     ```bash
     ./mvnw clean package
     cp target/mytheme.jar $KC_HOME/providers/
     $KC_HOME/bin/kc.sh build
     $KC_HOME/bin/kc.sh start-dev
     ```

   - Windows (PowerShell):

     ```powershell
     .\mvnw.cmd clean package
     Copy-Item target\mytheme.jar "$env:KC_HOME\providers\"
     & "$env:KC_HOME\bin\kc.bat" build
     & "$env:KC_HOME\bin\kc.bat" start-dev
     ```

6. In the Admin Console, go to **Realm Settings** → **Themes**.
7. Set **Email Theme** to `mytheme`.
8. Click **Save**.
9. Ensure SMTP is configured under **Realm Settings** → **Email** (or use a local mail catcher).
10. Go to **Users** → select a test user → **Actions** → **Send reset password email**.
11. Inspect the received email and confirm it uses your custom template and subject line.

> **Note:** To revert, set **Email Theme** back to `keycloak.v2` (or blank).

</details>

---

## Task 3 — Verify theme caching behavior

> Estimated time: 3–5 min | Tools: terminal / browser

**Goal:** Confirm that running Keycloak in development mode disables theme caching so that template changes are reflected immediately.

**Observable outcome:**
- Keycloak was started with `kc.sh start-dev`.
- Editing a template file and refreshing the login page shows the change without a server restart.

<details>
<summary>Hint — the flag that controls caching</summary>

Development mode automatically disables caching. For production deployments, you would need to explicitly configure cache settings or restart the server after theme changes.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Confirm Keycloak was started with:

   - macOS / Linux:

     ```bash
     $KC_HOME/bin/kc.sh start-dev
     ```

   - Windows (PowerShell):

     ```powershell
     & "$env:KC_HOME\bin\kc.bat" start-dev
     ```

2. Make a visible change to a theme file (e.g., add a comment or change a CSS class in `login/resources/css/signin.css`).
3. Refresh the login page in your browser.
4. Confirm the change appears immediately.

> **Note:** If you are running in production mode, theme caching is enabled by default and changes require a restart or explicit cache invalidation.

</details>

---

## Task 4 — Inspect available template variables

> Estimated time: 3–5 min | Tools: file editor / mail catcher

**Goal:** Add a reference to an available FreeMarker variable in an email template and verify it renders correctly.

**Observable outcome:**
- The customized email template references at least one variable such as `${user.firstName}` or `${realmName}`.
- The rendered email contains the actual value (e.g., the user's first name or realm display name).

<details>
<summary>Hint — which variables are always present</summary>

All email templates receive a standard set of context variables related to the user, the realm, and the action link. You do not need to declare them; they are injected by Keycloak.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open `mytheme/email/html/password-reset.ftl`.
2. Add a line that uses one of the standard variables, for example:

   ```html
   <p>Hello ${user.firstName},</p>
   <p>You requested a password reset for your account in the realm <strong>${realmName}</strong>.</p>
   ```

3. Save the file, rebuild and redeploy the JAR, or confirm caching is disabled in dev mode.
4. Trigger a password reset email for a user who has a first name set.
5. Inspect the rendered email and confirm the variables are replaced with actual values.

> **Note:** If `${user.firstName}` is empty, ensure the user's profile has a first name populated.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] The login page at `https://labs-sso.keycloak.academy/realms/{realm}/account` shows the custom theme styling
- [ ] No caching issues are present (confirm you started Keycloak in dev mode)
- [ ] The email theme is set to `mytheme` in **Realm Settings → Themes**
- [ ] A triggered password-reset email reflects the custom template and subject line
- [ ] FreeMarker variables such as `${user.firstName}` or `${realmName}` render correctly in the email

**macOS / Linux:**

```bash
# Verify the login page uses the custom theme
curl -s -o /dev/null -w "%{http_code}" \
  "https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/auth?client_id=account-console&response_type=code&redirect_uri=https://labs-sso.keycloak.academy/realms/{realm}/account"
```

**Windows (PowerShell):**

```powershell
# Verify the login page uses the custom theme
curl.exe -s -o NUL -w "%{http_code}" `
  "https://labs-sso.keycloak.academy/realms/{realm}/protocol/openid-connect/auth?client_id=account-console&response_type=code&redirect_uri=https://labs-sso.keycloak.academy/realms/{realm}/account"
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Extend the theme to override the `login.ftl` template itself (not just CSS) and add a custom footer with support links.
- Add a second locale by creating `messages_fr.properties` and testing language switching on the login page.
- Create an `account` theme type override and observe how the account console styling changes when bound to a client.
