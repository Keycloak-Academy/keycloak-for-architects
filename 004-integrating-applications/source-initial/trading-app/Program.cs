/**
 * Trading App — ASP.NET Core MVC skeleton.
 *
 * This is the STARTING POINT for the lab. Your task is to turn this into an
 * OIDC confidential client that authenticates users via Keycloak.
 *
 * TODO:
 * Step 1 — Add the OpenIdConnect NuGet package to trading-app.csproj.
 * Step 2 — Add OIDC authentication middleware (AddAuthentication + AddOpenIdConnect)
 *           using the Keycloak Authority, ClientId, and ClientSecret from appsettings.json.
 * Step 3 — Add [Authorize] to TradingController.
 * Step 4 — Display the logged-in user's name, email, and roles on the dashboard.
 * Step 5 — Add a logout action to HomeController and a login page (Views/Home/Index.cshtml).
 */

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run("http://localhost:5010");
