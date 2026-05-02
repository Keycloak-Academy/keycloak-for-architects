# Keycloak for Architects — Hands-On Curriculum

A progressive, hands-on curriculum for architects and advanced practitioners securing real-world applications with Keycloak. All labs follow a single canonical template and build on one another using a consistent **Aurum Bank** scenario.

## Prerequisites

Before starting the curriculum, ensure you have:

- [ ] Docker and Docker Compose installed
- [ ] .NET 10 SDK installed
- [ ] Node.js 20+ installed
- [ ] Postman installed
- [ ] Git installed

## Quick Start

Spin up the local Keycloak instance used across all labs:

```bash
docker compose -f keycloak-compose.yml up -d
```

The admin console is available at `http://localhost:8080/admin/master/console/` with credentials `admin` / `admin`.

---

## Curriculum

| Module | Lab | Title | Description |
|--------|-----|-------|-------------|
| **M1 — Foundations** | [Lab 1](001-environment-setup/) | Environment Setup | Docker, OpenJDK, and local Keycloak setup |
| **M1 — Foundations** | [Lab 2](002-identity-fundamentals/) | Identity & Protocol Fundamentals | Realm, user, group, role creation; first secured React SPA |
| **M1 — Foundations** | [Lab 3](003-oidc-playground/) | OIDC Playground & Discovery | Discovery endpoint, auth code flow, custom claims, UserInfo |
| **M2 — Securing Apps** | [Lab 4](004-integrating-applications/) | Integrating Applications with Keycloak | Migrate from Basic Auth to OAuth 2.0 / OIDC |
| **M2 — Securing Apps** | [Lab 5](005-oauth-authorization/) | OAuth Authorization: Audience, Roles, and Scopes | Audience mappers, RBAC, scopes, consent flow |
| **M3 — Token Lifecycle** | [Lab 6](006-tokens-and-sessions/) | Managing Tokens and Sessions | Token lifetimes, refresh rotation, offline tokens, revocation |
| **M3 — Token Lifecycle** | [Lab 7](007-step-up-authentication/) | Step-Up Authentication (ACR/AMR) | ACR values, OTP step-up, high-value transaction protection |
| **M3 — Token Lifecycle** | [Lab 8](008-custom-auth-flows/) | Customizing Authentication Flows & Protocol Mappers | Custom browser flows, protocol mappers, SPI overview |
| **M4 — Authorization Services** | [Lab 9](009-fga/) | Fine-Grained Authorization | Resources, scopes, policies, permissions, RPT (Postman) |
| **M4 — Authorization Services** | [Lab 10](010-uma/) | User-Managed Access (UMA) | Resource sharing, PAT, permission tickets (Postman) |
| **M5 — Advanced Token Patterns** | [Lab 11](011-token-exchange/) | Token Exchange | Subject, client, and impersonation token exchange |
| **M5 — Advanced Token Patterns** | [Lab 12](012-impersonation/) | Impersonation & Service Accounts | Admin console and API-based impersonation (Postman) |
| **M5 — Advanced Token Patterns** | [Lab 13](013-ciba/) | Client-Initiated Backchannel Authentication | Decoupled auth with support-agent email approval (Docker) |
| **M6 — Identity Providers & Storage** | [Lab 14](014-ldap-user-storage/) | External User Storage with LDAP | User Federation, attribute/role mappers |
| **M6 — Identity Providers & Storage** | [Lab 15](015-azure-ad-identity-provider/) | External Identity Provider: Azure AD | Microsoft Entra ID federation |
| **M7 — Advanced Topics** | [Lab 16](016-multi-tenancy/) | Multi-Tenancy via Organizations | Realm-per-tenant, Organizations feature, dynamic authority |
| **M7 — Advanced Topics** | [Lab 17](017-custom-authenticator/) | Building a Custom Authenticator | Risk-based authenticator Java SPI |
| **M7 — Advanced Topics** | [Lab 18](018-themes-and-emails/) | Themes and Email Templates | Custom login theme, email templates |
| **M7 — Advanced Topics** | [Lab 19](019-fido-webauthn/) | FIDO2 / WebAuthn | Passkeys, passwordless login, WebAuthn flow |

---

## Narrative: Aurum Bank

All labs are framed around **Aurum Bank**, a fictional financial institution modernizing its digital platform:

- **Labs 1–3:** Set up the environment and learn identity fundamentals.
- **Labs 4–5:** Migrate core systems from Basic Auth to OAuth/OIDC; add authorization with audience, roles, and scopes.
- **Labs 6–8:** Manage token lifecycles, enforce step-up authentication for high-value transfers, and customize auth flows.
- **Labs 9–10:** Protect the document vault with fine-grained policies and enable user-managed resource sharing.
- **Labs 11–13:** Exchange tokens between microservices, handle service-desk impersonation, and implement decoupled CIBA for high-net-worth clients.
- **Labs 14–15:** Integrate external identity sources (LDAP, Azure AD).
- **Labs 16–19:** Scale to multi-tenant SaaS, build custom authenticators and themes, and enable passwordless passkey authentication.

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
| Custom SPIs | Java / Maven |
| Infrastructure | Docker Compose |

---

## Support

- Report issues or feedback at [https://github.com/anthropics/claude-code/issues](https://github.com/anthropics/claude-code/issues)
- Keycloak documentation: [https://www.keycloak.org/documentation](https://www.keycloak.org/documentation)
