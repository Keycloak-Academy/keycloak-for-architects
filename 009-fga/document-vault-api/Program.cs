/**
 * Document Vault API — resource server with RPT Policy Enforcement Point (PEP).
 *
 * Validates Requesting Party Tokens (RPTs) issued by Keycloak Authorization Services.
 * An RPT is a standard JWT that contains an extra "authorization.permissions" claim
 * listing the resources and scopes the authorization server granted to the caller.
 *
 * This PEP rejects requests whose RPT does not include a matching resource + scope entry,
 * making Keycloak's fine-grained authorization decisions observable at the HTTP level.
 *
 * Endpoints:
 *   GET /api/documents/{id} — requires RPT permission: Document Resource + view scope
 *   GET /api/admin          — requires RPT permission: Administration Resource + view scope
 */

using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var kc = builder.Configuration.GetSection("Keycloak");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority            = kc["Authority"];
        // ⚠️ INSECURE (demo only): HTTP allowed. Production must use HTTPS and set this to true.
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            // RPTs issued by Keycloak for this client carry aud: "document-vault".
            ValidateAudience = true,
            ValidAudiences   = [kc["ClientId"]!],
        };
        o.Events = new JwtBearerEvents
        {
            // ── DEBUG: log every JWT validation failure to the console ──────────
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[AUTH FAILED] {ctx.Exception.GetType().Name}: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },

            // ── DEBUG: log all parsed claims so HasPermission issues are visible ─
            OnTokenValidated = ctx =>
            {
                Console.WriteLine("[TOKEN VALIDATED] Claims:");
                foreach (var c in ctx.Principal!.Claims)
                    Console.WriteLine($"  {c.Type} = {c.Value}");
                return Task.CompletedTask;
            },

            // OnChallenge fires when the middleware is about to return 401.
            // We suppress the default empty response and write a JSON body explaining the failure.
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();

                var reason = ctx.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException         => "Token has expired.",
                    SecurityTokenInvalidAudienceException => $"Token audience is invalid. Expected: {kc["ClientId"]}.",
                    SecurityTokenInvalidSignatureException => "Token signature is invalid.",
                    SecurityTokenInvalidIssuerException   => "Token issuer is invalid.",
                    not null                              => $"Token validation failed: {ctx.AuthenticateFailure.Message}",
                    null when ctx.Request.Headers.ContainsKey("Authorization")
                                                          => "Token could not be validated.",
                    _                                     => "No Bearer token provided. Obtain an RPT via the uma-ticket grant and pass it as Authorization: Bearer <rpt>.",
                };

                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Unauthorized", detail = reason }));
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ──────────────────────────────────────────────────────────────────
// .RequireAuthorization() → JwtBearer middleware returns 401 for missing/invalid tokens.
// HasPermission() check inside the handler → 403 when the RPT lacks the required permission.

app.MapGet("/api/documents/{id}", (string id, HttpContext ctx) =>
{
    if (!HasPermission(ctx, "Document Resource", "view"))
        return ForbiddenRpt("Document Resource", "view");
    return Results.Ok(new
    {
        id,
        title   = $"Quarterly Statement Q1 — {id}",
        owner   = "alice",
        content = "Balance: $12,450.00 | Period: Jan–Mar 2024",
        uri     = $"/api/documents/{id}"
    });
}).RequireAuthorization();

app.MapGet("/api/admin", (HttpContext ctx) =>
{
    if (!HasPermission(ctx, "Administration Resource", "view"))
        return ForbiddenRpt("Administration Resource", "view");
    return Results.Ok(new { status = "operational", totalUsers = 42, registeredResources = 3 });
}).RequireAuthorization();

// ── DEBUG endpoint — remove before publishing the lab ──────────────────────
// Returns every claim the JWT middleware parsed, plus the raw "authorization"
// claim value so you can confirm HasPermission sees what you expect.
app.MapGet("/debug/claims", (HttpContext ctx) =>
{
    var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });
    var authRaw = ctx.User.FindFirst("authorization")?.Value;
    return Results.Ok(new { claims, authorizationClaimRaw = authRaw });
}).RequireAuthorization();

app.Run("http://localhost:8090");

// ── Helpers ────────────────────────────────────────────────────────────────────

// Returns true if the RPT's "authorization.permissions" claim grants the given resource + scope.
// A regular access token (no "authorization" claim) always returns false.
static bool HasPermission(HttpContext ctx, string resourceName, string scope)
{
    var authValue = ctx.User.FindFirst("authorization")?.Value;
    if (authValue is null) return false;

    try
    {
        using var doc = JsonDocument.Parse(authValue);
        if (!doc.RootElement.TryGetProperty("permissions", out var perms)) return false;

        foreach (var perm in perms.EnumerateArray())
        {
            if (!perm.TryGetProperty("rsname", out var rsname)) continue;
            if (rsname.GetString() != resourceName) continue;

            if (!perm.TryGetProperty("scopes", out var scopes)) continue;
            foreach (var s in scopes.EnumerateArray())
                if (s.GetString() == scope) return true;
        }
    }
    catch (JsonException) { /* not a valid RPT */ }

    return false;
}

static IResult ForbiddenRpt(string resource, string scope) =>
    Results.Json(
        new { error = "Forbidden", required = $"{resource}#{scope}", hint = "Obtain an RPT that includes this resource and scope via the uma-ticket grant." },
        statusCode: 403);
