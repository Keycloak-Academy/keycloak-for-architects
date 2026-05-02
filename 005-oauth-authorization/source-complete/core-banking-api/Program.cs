/**
 * Core Banking API — educational resource server for Keycloak demo apps.
 *
 * JWT signatures are verified using Keycloak's public key (fetched from JWKS
 * endpoint at startup via the JwtBearer middleware's authority discovery).
 * All data is static/hardcoded.
 *
 * Endpoints:
 *   GET /api/info                      — public
 *   GET /api/accounts                  — requires role: customer
 *   GET /api/transactions/{accountId}  — requires role: customer
 *   GET /api/transactions              — requires realm role: customer
 *   GET /api/balances                  — requires realm role: customer AND scope: read:accounts
 *   GET /api/portfolio/repartition     — requires client role: trader (trading-app)
 *   GET /api/stocks                    — auth only (valid token required)
 *   GET /api/users                     — requires role: admin
 *   GET /api/users/{userId}/accounts   — requires role: admin
 */

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// CORS: only allow the two demo app origins
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:3010", "http://localhost:3020", "http://localhost:5010")
    .WithMethods("GET", "OPTIONS")
    .WithHeaders("Authorization", "Content-Type")));

// JWT Bearer: validates signatures using Keycloak's JWKS endpoint.
// The middleware auto-discovers the JWKS URI from {Authority}/.well-known/openid-configuration.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority            = builder.Configuration["Keycloak:Authority"];
        // ⚠️ INSECURE (demo only): HTTP allowed. Production must use HTTPS and set this to true.
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            // Resource-server audience validation: only tokens that include "core-banking-api"
            // in their aud claim are accepted. Callers must configure a Keycloak audience
            // mapper (Client Scopes → Audience) that adds "core-banking-api" to the token.
            ValidateAudience = true,
            ValidAudiences   = ["core-banking-api"],
        };
        // Map Keycloak's realm_access.roles JSON claim → ClaimTypes.Role
        // so ctx.User.IsInRole("customer") works in endpoint handlers.
        o.Events = new JwtBearerEvents
        {
            // OnChallenge fires when the middleware is about to return 401.
            // We suppress the default empty response and write a JSON body
            // explaining why the token was rejected — useful for students
            // inspecting API responses while learning the authorization flow.
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse(); // suppress the default 401

                var reason = ctx.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException      => "Token has expired.",
                    SecurityTokenInvalidAudienceException => "Token audience is invalid. Expected audience: core-banking-api. Add an Audience mapper in Keycloak for this client.",
                    SecurityTokenInvalidSignatureException => "Token signature is invalid.",
                    SecurityTokenInvalidIssuerException    => "Token issuer is invalid.",
                    SecurityTokenNoExpirationException     => "Token has no expiration claim.",
                    SecurityTokenException e               => $"Token validation failed: {e.Message}",
                    not null                               => $"Authentication failed: {ctx.AuthenticateFailure.Message}",
                    null when ctx.Request.Headers.ContainsKey("Authorization") => "Token could not be validated.",
                    _                                      => "No Bearer token provided. Add an Authorization: Bearer <token> header.",
                };

                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Unauthorized", detail = reason }));
            },

            OnTokenValidated = ctx =>
            {
                if (ctx.Principal?.Identity is not ClaimsIdentity identity) return Task.CompletedTask;

                // Map Keycloak realm roles → ClaimTypes.Role
                var realmAccess = identity.FindFirst("realm_access");
                if (realmAccess is not null)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(realmAccess.Value);
                        if (doc.RootElement.TryGetProperty("roles", out var roles))
                            foreach (var role in roles.EnumerateArray())
                                if (role.GetString() is string r)
                                    identity.AddClaim(new Claim(ClaimTypes.Role, r));
                    }
                    catch { }
                }

                // Map trading-app client roles → ClaimTypes.Role
                var resourceAccess = identity.FindFirst("resource_access");
                if (resourceAccess is not null)
                {
                    try
                    {
                        using var doc2 = JsonDocument.Parse(resourceAccess.Value);
                        if (doc2.RootElement.TryGetProperty("trading-app", out var ta) &&
                            ta.TryGetProperty("roles", out var clientRoles))
                            foreach (var role in clientRoles.EnumerateArray())
                                if (role.GetString() is string r)
                                    identity.AddClaim(new Claim(ClaimTypes.Role, r));
                    }
                    catch { }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Public ─────────────────────────────────────────────────────────────────

app.MapGet("/api/info", () => Results.Ok(new {
    message = "Welcome to the Core Banking API. Authenticate to access your data."
}));

// ── Role-protected ──────────────────────────────────────────────────────────
// .RequireAuthorization() → middleware returns 401 for missing/invalid/expired tokens.
// Manual role/audience/scope check inside → 403 for wrong claims.

app.MapGet("/api/accounts", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { message = "You have access to account information.", accounts = FakeData.Accounts });
}).RequireAuthorization();

app.MapGet("/api/transactions/{accountId}", (HttpContext ctx, string accountId) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { message = "You have access to transaction history.", transactions = FakeData.Transactions });
}).RequireAuthorization();

app.MapGet("/api/transactions", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer")) return Forbidden("customer");
    return Results.Ok(new { message = "You have access to transaction history.", transactions = FakeData.Transactions });
}).RequireAuthorization();

app.MapGet("/api/balances", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("customer"))  return Forbidden("customer");
    if (!HasScope(ctx, "read:accounts")) return ForbiddenScope("read:accounts");
    return Results.Ok(new { message = "You have access to balance details.", balances = new { checking = 1500, savings = 5000 } });
}).RequireAuthorization();

app.MapGet("/api/portfolio/repartition", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("trader")) return Forbidden("trader");
    return Results.Ok(new { portfolioRepartition = FakeData.Portfolio, totalValue = 50000.0 });
}).RequireAuthorization();

app.MapGet("/api/stocks", () =>
    Results.Ok(new { stocks = FakeData.Stocks })
).RequireAuthorization();

app.MapGet("/api/users", (HttpContext ctx) => {
    if (!ctx.User.IsInRole("admin")) return Forbidden("admin");
    return Results.Ok(new { message = "You have access to user information.", users = FakeData.Users });
}).RequireAuthorization();

app.MapGet("/api/users/{userId}/accounts", (HttpContext ctx, string userId) => {
    if (!ctx.User.IsInRole("admin")) return Forbidden("admin");
    return Results.Ok(new { message = $"Accounts for user {userId}", accounts = FakeData.GetUserAccounts(userId) });
}).RequireAuthorization();

// Stub for Lab 7 — step-up authentication. Currently auth-only; Lab 7 adds ACR-gated authorization.
app.MapPost("/api/transfer", (HttpContext ctx, TransferRequest req) => {
    return Results.Ok(new { message = "Transfer initiated", amount = req.Amount, toAccount = req.ToAccount });
}).RequireAuthorization();

app.Run("http://localhost:3011");

// ── Helpers ────────────────────────────────────────────────────────────────

static IResult Forbidden(string role) =>
    Results.Json(new { error = "Forbidden", detail = $"Required role: {role}" }, statusCode: 403);

// Returns true if the token's space-delimited "scope" claim contains the given scope.
static bool HasScope(HttpContext ctx, string scope)
{
    var scopeClaim = ctx.User.FindFirst("scope")?.Value
                  ?? ctx.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
    return scopeClaim is not null && scopeClaim.Split(' ').Contains(scope);
}

static IResult ForbiddenScope(string scope) =>
    Results.Json(new { error = "Forbidden", detail = $"Required scope: {scope}" }, statusCode: 403);

// ── Fake data ──────────────────────────────────────────────────────────────

static class FakeData
{
    public static readonly object[] Accounts = [
        new { id = "12345", type = "checking", balance = 1500 },
        new { id = "67890", type = "savings",  balance = 5000 },
    ];

    public static readonly object[] Transactions = [
        new { id = "1", description = "Coffee Shop", amount = -5,   date = "12 Aug, 2020 12:33" },
        new { id = "2", description = "Salary",      amount = 2000, date = "12 Aug, 2020 12:33" },
    ];

    public static readonly object[] Portfolio = [
        new { id = 1, name = "Tech Stocks",        type = "Stocks",       currentValue = 20000.0, percentage = 40 },
        new { id = 2, name = "Government Bonds",    type = "Bonds",        currentValue = 15000.0, percentage = 30 },
        new { id = 3, name = "Global Mutual Funds", type = "Mutual Funds", currentValue = 10000.0, percentage = 20 },
        new { id = 4, name = "Real Estate",         type = "Assets",       currentValue =  5000.0, percentage = 10 },
    ];

    public static readonly object[] Stocks = [
        new { symbol = "AAPL",  currentPrice = 150.25,  changePercentage =  1.2 },
        new { symbol = "GOOGL", currentPrice = 2800.75, changePercentage = -0.5 },
        new { symbol = "TSLA",  currentPrice = 850.40,  changePercentage =  2.1 },
    ];

    public static readonly object[] Users = [
        new { id = "u001", name = "Alice Johnson",  email = "alice@example.com",   role = "customer" },
        new { id = "u002", name = "Bob Smith",      email = "bob@example.com",     role = "customer" },
        new { id = "u003", name = "Charlie Brown",  email = "charlie@example.com", role = "admin"    },
    ];

    private static readonly (string UserId, string Id, string Type, int Balance)[] AllUserAccounts = [
        ("u001", "12345", "checking",   1500),
        ("u001", "67890", "savings",    5000),
        ("u002", "54321", "checking",   300),
        ("u003", "98765", "investment", 10000),
    ];

    public static object[] GetUserAccounts(string userId) =>
        AllUserAccounts
            .Where(a => a.UserId == userId)
            .Select(a => (object)new { userId = a.UserId, id = a.Id, type = a.Type, balance = a.Balance })
            .ToArray();
}

// Request model for the transfer endpoint (stub for Lab 7)
record TransferRequest(decimal Amount, string ToAccount, string Currency = "USD");
