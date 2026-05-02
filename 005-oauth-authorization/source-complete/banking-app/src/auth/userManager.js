import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

// Public client: no client secret, uses PKCE (S256 is default in oidc-client-ts v3).
// authority triggers auto-discovery at /.well-known/openid-configuration.
export const userManager = new UserManager({
  authority: `${process.env.KC_URL}/realms/${process.env.KC_REALM}`,
  client_id: process.env.CLIENT_ID,
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',
  scope: 'openid profile email read:accounts',
  // ⚠️ INSECURE (demo only): tokens stored in sessionStorage are accessible to any
  // JavaScript on the page (XSS risk). Production SPAs should use the BFF pattern
  // (backend-for-frontend) with httpOnly cookies, or a dedicated auth proxy.
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  // ⚠️ INSECURE (demo only): silent renew disabled — tokens expire and the user must
  // re-authenticate. Production apps should set this to true or use refresh tokens.
  automaticSilentRenew: false,
});

export const login  = () => userManager.signinRedirect();
export const logout = () => userManager.signoutRedirect();
