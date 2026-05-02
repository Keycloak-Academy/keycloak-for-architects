/**
 * Trading App — confidential OAuth 2.0 / OIDC client (ASP.NET Core MVC).
 *
 * Contrast with banking-app (public client, PKCE, tokens in browser):
 * - Client secret stored server-side in appsettings.json
 * - Session managed via an encrypted httpOnly cookie (no tokens in browser)
 * - State: always enforced by the OIDC middleware (correlation cookie, CSRF protection)
 * - Nonce: required in ID token, validated on callback (replay protection)
 * - Roles extracted from realm_access.roles → ClaimTypes.Role in OnTokenValidated
 * - SaveTokens=true: access_token stored in the encrypted session cookie so
 *   server-side code can forward it to the Core Banking API.
 */

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var kc = builder.Configuration.GetSection("Keycloak");

// ── Authentication ──────────────────────────────────────────────────────────

builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    o.Cookie.Name     = "trading_app";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
})
.AddOpenIdConnect(o =>
{
    o.Authority             = kc["Authority"];
    o.ClientId              = kc["ClientId"];
    o.ClientSecret          = kc["ClientSecret"];
    o.ResponseType          = "code";          // authorization code flow
    o.CallbackPath          = "/signin-oidc";
    o.SignedOutCallbackPath = "/signout-callback-oidc";
    // ⚠️ INSECURE (demo only): HTTP allowed. Production must use HTTPS.
    o.RequireHttpsMetadata  = false;
    o.Scope.Add("openid");
    o.Scope.Add("profile");
    o.Scope.Add("email");

    // Store the access_token in the encrypted session cookie.
    // This allows the controller to forward the user token to the Core Banking API.
    o.SaveTokens = true;

    // Signature verification uses Keycloak's JWKS (auto-discovered from authority).
    o.TokenValidationParameters = new TokenValidationParameters
    {
        // ⚠️ INSECURE (demo only): audience validation disabled for the ID token.
        ValidateAudience = false,
    };

    // Nonce: included in every authorization request and validated when the
    // ID token is received (replay protection).
    o.ProtocolValidator.RequireNonce = true;

    // Map Keycloak realm roles to ASP.NET Core ClaimTypes.Role claims.
    // Keycloak sends: "realm_access": { "roles": ["user", "trader"] }
    // After mapping: User.IsInRole("trader") and [Authorize(Roles="trader")] work natively.
    o.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = ctx =>
        {
            if (ctx.Principal?.Identity is not ClaimsIdentity identity) return Task.CompletedTask;

            var realmAccess = identity.FindFirst("realm_access");
            if (realmAccess is null) return Task.CompletedTask;

            try
            {
                using var doc = JsonDocument.Parse(realmAccess.Value);
                if (doc.RootElement.TryGetProperty("roles", out var roles))
                    foreach (var role in roles.EnumerateArray())
                        if (role.GetString() is string r)
                            identity.AddClaim(new Claim(ClaimTypes.Role, r));
            }
            catch { /* ignore parse errors */ }

            return Task.CompletedTask;
        }
    };
});

// ── Services ────────────────────────────────────────────────────────────────

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run("http://localhost:5010");
