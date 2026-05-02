using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using trading_app.Models;

namespace trading_app.Controllers;

[Authorize]
public class TradingController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public TradingController(IHttpClientFactory http, IConfiguration config)
    {
        _http   = http;
        _config = config;
    }

    public async Task<IActionResult> Dashboard()
    {
        // Retrieve the user's access token saved in the encrypted session cookie
        // (requires SaveTokens = true in Program.cs).
        var userToken = await HttpContext.GetTokenAsync("access_token") ?? "";

        var vm = new TradingDashboardViewModel();

        // Fetch stocks with a client_credentials token (service-to-service).
        // The user identity is not involved — this demonstrates machine-to-machine auth.
        try { vm.Stocks = await FetchStocksAsync(); }
        catch (Exception ex) { vm.StocksError = ex.Message; }

        // Fetch portfolio with the user's access token (forwarded from session).
        // Requires the user to have the 'trader' client role (trading-app) in Keycloak.
        try
        {
            var (portfolio, total) = await FetchPortfolioAsync(userToken);
            vm.Portfolio  = portfolio;
            vm.TotalValue = total;
        }
        catch (Exception ex) { vm.PortfolioError = ex.Message; }

        return View(vm);
    }

    // Obtain a service token via client_credentials, then call /api/stocks.
    // No user identity involved — pure machine-to-machine call.
    private async Task<object[]> FetchStocksAsync()
    {
        var authority    = _config["Keycloak:Authority"]!;
        var clientId     = _config["StocksClient:ClientId"]!;
        var clientSecret = _config["StocksClient:ClientSecret"]!;
        var tokenUrl     = $"{authority}/protocol/openid-connect/token";
        var apiBase      = _config["BankingApi:BaseUrl"]!;

        var http = _http.CreateClient();

        // 1. Obtain access token via client_credentials grant
        var tokenResp = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
        }));
        tokenResp.EnsureSuccessStatusCode();

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync());
        var serviceToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;

        // 2. Call /api/stocks with the service token
        var req = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/stocks");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);

        var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("stocks").Deserialize<object[]>() ?? [];
    }

    // Forward the user's access token to /api/portfolio/repartition.
    // Requires the 'trader' client role (trading-app) in the token (enforced by the API).
    private async Task<(object[] Portfolio, double TotalValue)> FetchPortfolioAsync(string userToken)
    {
        var apiBase = _config["BankingApi:BaseUrl"]!;
        var http    = _http.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/portfolio/repartition");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var portfolio   = doc.RootElement.GetProperty("portfolioRepartition").Deserialize<object[]>() ?? [];
        var totalValue  = doc.RootElement.GetProperty("totalValue").GetDouble();
        return (portfolio, totalValue);
    }
}
