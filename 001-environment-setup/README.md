# Lab 1 — Environment Setup

This lab addresses the need for a consistent, reproducible Keycloak runtime — whether shared or self-hosted — so that later labs can focus on configuration and protocol mechanics without environment drift. By the end, you will have demonstrated that you can access a running Keycloak instance (shared or local), create an initial admin account, and verify reachability via HTTP.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Docker Desktop is installed and running (for local path), or you have received shared cloud credentials
- [ ] Node.js and npm are installed (`node --version` and `npm --version` return versions)
- [ ] Port 8080 is free on your workstation (for local path)

This is the first lab; there is no prior lab to complete.

---

## Background

### Shared cloud environment

Each participant has been assigned a dedicated realm on the shared Keycloak cluster. Your credentials (realm name, username, and password) will be communicated separately.

| Resource | URL |
|---|---|
| Admin console | `https://labs-sso-admin.keycloak.academy/admin/{realm}/console/` |
| Account console | `https://labs-sso.keycloak.academy/realms/{realm}/account/` |
| OIDC issuer base URL | `https://labs-sso.keycloak.academy/realms/{realm}` |

Replace `{realm}` with the realm name provided to you.

If you are using the shared cloud environment, skip the local installation sections below and proceed directly to the Lab Checkpoint.

---

### Self-hosted options

Keycloak can be installed in several ways:

| Method | Best for | Prerequisites |
|---|---|---|
| Docker container | Quick local development | Docker Desktop |
| OpenJDK | Long-running local dev or custom Java tuning | OpenJDK |
| Kubernetes / Operator | Production or team environments | K8s cluster |

This lab covers Docker (recommended) and OpenJDK. Docker isolates dependencies and makes cleanup trivial; OpenJDK gives you full control over JVM flags and persistence.

> **Note:** The commands in this lab start Keycloak in `start-dev` mode. This disables HTTPS enforcement, uses an embedded H2 database, and relaxes other production safeguards. Never use `start-dev` in production.

---

## Task 1 — Verify Node.js and npm

> Estimated time: 2–3 min | Tools: terminal

**Goal:** Confirm that Node.js and npm are installed and meet the minimum versions required by later labs.

**Observable outcome:**
- The terminal prints version numbers for both `node` and `npm`
- No "command not found" errors appear

<details>
<summary>Hint — where to look</summary>

Check your operating system's package manager or the Node.js website if the commands are missing. The labs expect a current LTS release.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Open a terminal and run:

   ```bash
   node --version
   npm --version
   ```

2. If either command is not found, download the Windows installer from [Node.js v24.14.1 (Windows x64)](https://nodejs.org/dist/v24.14.1/node-v24.14.1-x64.msi) and run it.
3. Re-open your terminal and repeat the version checks.

> **Note:** No cleanup needed; this is a read-only verification.

</details>

---

## Task 2 — Launch Keycloak with Docker

> Estimated time: 5–7 min | Tools: terminal / Docker

**Goal:** Start a Keycloak container in development mode and verify it responds on `http://localhost:8080`.

**Observable outcome:**
- Docker shows a running container named `keycloak`
- Opening `http://localhost:8080` displays the Keycloak welcome page
- The admin account `admin/admin` can log in at `http://localhost:8080/admin/master/console/`

<details>
<summary>Hint — bootstrap credentials</summary>

Keycloak does not create a default admin account for security reasons. The container needs environment variables to seed the first admin user on startup.

</details>

<details>
<summary>Hint — port binding</summary>

Binding to `127.0.0.1:8080` limits exposure to your local machine. If another service already uses 8080, either free the port or map to a different host port.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Run the container:

   - Linux / macOS:

     ```bash
     docker run --name keycloak -p 127.0.0.1:8080:8080 \
       -e KC_BOOTSTRAP_ADMIN_USERNAME=admin \
       -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin \
       quay.io/keycloak/keycloak:26.5.0 \
       start-dev
     ```

   - Windows (PowerShell):

     ```powershell
     docker run --name keycloak -p 127.0.0.1:8080:8080 `
       -e KC_BOOTSTRAP_ADMIN_USERNAME=admin `
       -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin `
       quay.io/keycloak/keycloak:26.5.0 `
       start-dev
     ```

2. Wait for the log line confirming the server started.
3. Open `http://localhost:8080` in a browser.
4. Click **Administration Console** and log in with:
   - Username: `admin`
   - Password: `admin`

> **Note:** To stop and remove the container later:
> - Linux / macOS: `docker stop keycloak && docker rm keycloak`
> - Windows: `docker stop keycloak; docker rm keycloak`

</details>

---

## Task 3 — (Optional) Launch Keycloak with OpenJDK

> Estimated time: 10–15 min | Tools: terminal

**Goal:** Install OpenJDK, download Keycloak, start it in development mode, and verify reachability.

**Observable outcome:**
- `java -version` prints a version string
- Keycloak starts without errors and prints a success message
- `http://localhost:8080` is reachable

<details>
<summary>Hint — environment variables</summary>

Keycloak reads `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD` from the environment on first startup to create the admin account.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

1. Install OpenJDK:
   - **Windows / macOS:** Download from [Adoptium](https://adoptium.net/)
   - **Fedora:** `sudo dnf install java-latest-openjdk`
   - **Ubuntu:** search for current OpenJDK packages in your distribution's repository
2. Verify Java:

   ```bash
   java -version
   ```

3. Download and extract Keycloak, then set `KC_HOME` to the extracted directory.
4. Set bootstrap credentials:
   - Linux / macOS:

     ```bash
     export KC_BOOTSTRAP_ADMIN_USERNAME=admin
     export KC_BOOTSTRAP_ADMIN_PASSWORD=change_me
     ```

   - Windows:

     ```cmd
     set KC_BOOTSTRAP_ADMIN_USERNAME=admin && set KC_BOOTSTRAP_ADMIN_PASSWORD=change_me
     ```

5. Start Keycloak:
   - Linux / macOS:

     ```bash
     cd $KC_HOME
     bin/kc.sh start-dev
     ```

   - Windows:

     ```cmd
     cd %KC_HOME%
     bin\kc.bat start-dev
     ```

6. Open `http://localhost:8080` and log into the admin console with the credentials you set.

> **Note:** Use a strong password in production and consider enabling two-factor authentication for the admin account.

</details>

---

## Task 4 — Configure the Admin CLI alias

> Estimated time: 3–5 min | Tools: terminal

**Goal:** Create a local shell alias that runs `kcadm.sh` inside a transient Docker container so you can manage Keycloak from the host without installing Java.

**Observable outcome:**
- Running `kcadm config credentials --server http://localhost:8080 --realm master --user admin --password admin` succeeds and stores a token
- `kcadm get realms` returns the list of realms

<details>
<summary>Hint — volume mount</summary>

The CLI stores authentication tokens in a local directory. Mount that directory into the container so the token persists across invocations.

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

**Linux / macOS:**

1. Create a directory for CLI state:

   ```bash
   mkdir -p $(echo $HOME)/.acme/.keycloak
   ```

2. Define the alias:

   ```bash
   alias kcadm="docker run --net=host -i --user=1000:1000 --rm -v $(echo $HOME)/.acme/.keycloak:/opt/keycloak/.keycloak:z --entrypoint /opt/keycloak/bin/kcadm.sh quay.io/keycloak/keycloak:26.5.0"
   ```

3. Authenticate:

   ```bash
   kcadm config credentials --server http://localhost:8080 --realm master --user admin --password admin
   ```

4. Verify:

   ```bash
   kcadm get realms
   ```

> **Note:** Add the alias to your shell profile (e.g., `~/.bashrc`) if you want it available in new terminals.

---

**Windows (PowerShell):**

1. Create a directory for CLI state:

   ```powershell
   New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.acme\.keycloak"
   ```

2. Define a PowerShell function:

   ```powershell
   function kcadm {
       docker run --rm -i `
         -v "$env:USERPROFILE\.acme\.keycloak:/opt/keycloak/.keycloak" `
         --entrypoint /opt/keycloak/bin/kcadm.sh `
         quay.io/keycloak/keycloak:26.5.0 @args
   }
   ```

3. Authenticate:

   ```powershell
   kcadm config credentials --server http://host.docker.internal:8080 --realm master --user admin --password admin
   ```

   > **Note:** On Windows, Docker Desktop routes host-to-container traffic via `host.docker.internal`, not `localhost`.

4. Verify:

   ```powershell
   kcadm get realms
   ```

> **Note:** To make the function available in new terminals, add it to your PowerShell profile: `notepad $PROFILE`.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] `node --version` and `npm --version` return version strings
- [ ] Keycloak is running and reachable at `http://localhost:8080` (local) or the shared URLs are accessible (cloud)
- [ ] The admin console login succeeds with the provided credentials
- [ ] (Optional) The `kcadm` alias returns realm data without errors

```bash
# Quick health check (local Docker — Linux / macOS)
curl -s http://localhost:8080 | grep -i keycloak
```

```powershell
# Quick health check (local Docker — Windows PowerShell)
(Invoke-WebRequest -Uri http://localhost:8080).Content | Select-String keycloak
```

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Run Keycloak with an external PostgreSQL container instead of the embedded H2 database, and verify that realm data persists across container restarts.
- Start Keycloak in production mode (`start` instead of `start-dev`) with a self-signed TLS certificate and observe which configuration options become mandatory.
- Read the [Keycloak Server Administration Guide — Installing Keycloak](https://www.keycloak.org/guides#server) to compare bare-metal, container, and Kubernetes installation trade-offs.
