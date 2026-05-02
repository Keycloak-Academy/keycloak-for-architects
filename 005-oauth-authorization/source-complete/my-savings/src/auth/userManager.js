import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

// Public client: no client secret, uses PKCE (S256 is default in oidc-client-ts v3).
// Default scope does NOT include read:accounts — it is requested separately via
// grantBalanceAccess(), which triggers a new authorization request with an explicit
// consent prompt so the user can approve the additional permission.
export const userManager = new UserManager({
  authority: `${process.env.KC_URL}/realms/${process.env.KC_REALM}`,
  client_id: process.env.CLIENT_ID,
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',
  scope: 'openid profile email',
  // ⚠️ INSECURE (demo only): tokens stored in sessionStorage are accessible to any
  // JavaScript on the page (XSS risk). Production SPAs should use the BFF pattern.
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: false,
});

export const login  = () => userManager.signinRedirect();
export const logout = () => userManager.signoutRedirect();

// Re-authenticate requesting read:accounts + the core-banking-api audience.
// core-banking-api-audience is an optional scope on my-savings-app (not default),
// so the initial login token does not carry the API audience claim. It is only
// added here, when the user explicitly grants balance access.
// Keycloak reuses the existing session and only triggers OAUTH_GRANT for
// read:accounts (not yet approved) — profile/email/audience are silent.
export const grantBalanceAccess = () =>
  userManager.signinRedirect({
    scope: 'openid profile email read:accounts core-banking-api-audience',
  });
