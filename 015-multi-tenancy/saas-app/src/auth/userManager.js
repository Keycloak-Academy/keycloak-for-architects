import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

// Public client: no client secret, uses PKCE (S256 is default in oidc-client-ts v3).
// organization scope requests the org membership claim from Keycloak 24+.
export const userManager = new UserManager({
  authority: `${process.env.KC_URL}/realms/${process.env.KC_REALM}`,
  client_id: process.env.CLIENT_ID,
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',
  scope: 'openid profile email organization',
  // ⚠️ INSECURE (demo only): sessionStorage is readable by any JS on the page.
  // Production apps should use the BFF pattern with httpOnly cookies.
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: false,
});

export const login  = () => userManager.signinRedirect();
export const logout = () => userManager.signoutRedirect();
