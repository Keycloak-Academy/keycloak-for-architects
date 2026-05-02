/**
 * Core Banking API — resource server secured with HTTP Basic Authentication.
 *
 * This is the STARTING POINT for the lab. Your task is to replace Basic Auth
 * with OAuth 2.0 JWT Bearer authentication (client_credentials grant).
 *
 * Endpoints:
 *   GET /api/info    — public
 *   GET /api/stocks  — requires Basic Auth (username + password)
 */

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// CORS: allow the frontend origins to call this API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:3010", "http://localhost:5010")
    .WithMethods("GET", "OPTIONS")
    .WithHeaders("Authorization", "Content-Type")));

// TODO: Replace Basic Auth with JWT Bearer (OAuth 2.0 client_credentials grant).
//
// Step 1 — Remove the BasicAuthHandler registration below.
// Step 2 — Add:
//   builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//       .AddJwtBearer(o => { o.Authority = ...; o.RequireHttpsMetadata = false; ... });
// Step 3 — Register the Keycloak:Authority in appsettings.json.
// Step 4 — In Keycloak, create a confidential client with Service Account enabled,
//           then obtain a token via the client_credentials grant and call /api/stocks.

builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("Basic", null);

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Public ─────────────────────────────────────────────────────────────────

app.MapGet("/api/info", () => Results.Ok(new {
    message = "Core Banking API. Authenticate with Basic credentials to call /api/stocks."
}));

// ── Basic-Auth-protected ────────────────────────────────────────────────────

app.MapGet("/api/stocks", () =>
    Results.Ok(new { stocks = FakeData.Stocks })
).RequireAuthorization();

app.Run("http://localhost:3011");

// ── Fake data ──────────────────────────────────────────────────────────────

static class FakeData
{
    public static readonly object[] Stocks = [
        new { symbol = "AAPL",  currentPrice = 150.25,  changePercentage =  1.2 },
        new { symbol = "GOOGL", currentPrice = 2800.75, changePercentage = -0.5 },
        new { symbol = "TSLA",  currentPrice = 850.40,  changePercentage =  2.1 },
    ];
}

// ── Basic Auth handler ─────────────────────────────────────────────────────

public class BasicAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(header!);
            if (!string.Equals(authHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.Fail("Expected Basic scheme"));

            var credentials = Encoding.UTF8
                .GetString(Convert.FromBase64String(authHeader.Parameter!))
                .Split(':', 2);

            var expectedUser = config["BasicAuth:Username"];
            var expectedPass = config["BasicAuth:Password"];

            if (credentials[0] != expectedUser || credentials[1] != expectedPass)
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));

            var claims    = new[] { new Claim(ClaimTypes.Name, credentials[0]) };
            var identity  = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket    = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Authorization header"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"Core Banking API\"";
        return base.HandleChallengeAsync(properties);
    }
}
