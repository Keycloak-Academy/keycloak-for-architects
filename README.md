# Keycloak for Architects — Hands-On Curriculum

A progressive, hands-on curriculum for architects and advanced practitioners securing real-world applications with Keycloak. All labs follow a single canonical template and build on one another using a consistent **Aurum Bank** scenario.

## Prerequisites

Before starting the curriculum, ensure you have:

- [ ] Docker and Docker Compose installed
- [ ] .NET 10 SDK installed
- [ ] Node.js 20+ installed
- [ ] Postman installed
- [ ] Git installed

---

## Curriculum

| Module | Lab | Title | Description |
|--------|-----|-------|-------------|
| **M1 — Foundations** | [Lab 1](001-environment-setup/) | Environment Setup | Docker, OpenJDK, and local Keycloak setup |
| **M1 — Foundations** | [Lab 2](002-identity-fundamentals/) | Identity & Protocol Fundamentals | Realm, user, group, role creation; first secured React SPA |
| **M1 — Foundations** | [Lab 3](003-oidc-playground/) | OIDC Playground & Discovery | Discovery endpoint, auth code flow, custom claims, UserInfo |
| **M2 — Securing Apps** | [Lab 4](004-integrating-applications/) | Integrating Applications with Keycloak | Migrate from Basic Auth to OAuth 2.0 / OIDC |
| **M2 — Securing Apps** | [Lab 5](005-oauth-authorization/) | OAuth Authorization: Audience, Roles, and Scopes | Audience mappers, RBAC, scopes, consent flow |

---

## Narrative: Aurum Bank

All labs are framed around **Aurum Bank**, a fictional financial institution modernizing its digital platform:

- **Labs 1–3:** Set up the environment and learn identity fundamentals.
- **Labs 4–5:** Migrate core systems from Basic Auth to OAuth/OIDC; add authorization with audience, roles, and scopes.

Each lab's **Prerequisites** section links to the immediately preceding lab. Complete them in order for the best learning experience.

---

## Lab Template

Every lab follows the structure defined in [`lab-template.md`](lab-template.md):

1. **Title block** — module cross-reference + outcome statement
2. **Prerequisites** — observable environment state + link to prior lab
3. **Background** — concept tables and diagrams (no tasks)
4. **Tasks 1–4** — each with Goal, Observable outcome, Hints, and Solution in collapsible blocks
5. **Lab Checkpoint** — checkable observable states
6. **Going Further** — extension ideas

## Code Structure

Labs that include application code use the `source-initial/` / `source-complete/` pattern:

- `source-initial/` — starting state (typically the previous lab's `source-complete/`)
- `source-complete/` — reference solution

### Technology stack

| Component | Technology |
|-----------|------------|
| Resource servers | ASP.NET Core Minimal API |
| Confidential clients | ASP.NET Core MVC |
| Public clients (SPA) | React + `oidc-client-ts` |
| Infrastructure | Docker Compose |

---

## Support

- Keycloak documentation: [https://www.keycloak.org/documentation](https://www.keycloak.org/documentation)
