using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using trading_app.Models;

namespace trading_app.Controllers;

[Authorize]
public class TradingController : Controller
{
    public IActionResult Dashboard()
    {
        ViewBag.CurrentAcr = User.FindFirst("acr")?.Value ?? "none";
        var vm = new TradingDashboardViewModel
        {
            Stocks = new object[]
            {
                new { symbol = "AAPL", currentPrice = 213.49, changePercentage =  1.2 },
                new { symbol = "MSFT", currentPrice = 415.28, changePercentage = -0.4 },
                new { symbol = "NVDA", currentPrice = 875.40, changePercentage =  2.8 },
                new { symbol = "TSLA", currentPrice = 248.50, changePercentage = -1.1 },
            },
            Portfolio = new object[]
            {
                new { name = "US Equities", type = "Equity", currentValue = 45000.0, percentage = 45.0 },
                new { name = "EU Bonds",    type = "Bond",   currentValue = 30000.0, percentage = 30.0 },
                new { name = "Cash",        type = "Cash",   currentValue = 15000.0, percentage = 15.0 },
                new { name = "Commodities", type = "Alt",    currentValue = 10000.0, percentage = 10.0 },
            },
            TotalValue = 100000.0
        };
        return View(vm);
    }

    [HttpGet("api/order")]
    public IActionResult Order()
    {
        var acr = User.FindFirst("acr")?.Value;
        if (acr != "gold")
        {
            return StatusCode(403, new
            {
                error = "Insufficient authentication level",
                requiredAcr = "gold",
                currentAcr = acr ?? "none"
            });
        }

        return Ok(new
        {
            status = "Order authorized",
            amount = 50000,
            currency = "USD",
            acr = acr
        });
    }

    // Trigger mid-session step-up to gold using the OIDC claims parameter.
    // The claims parameter with essential:true forces re-authentication even
    // when an existing session is present, because Max Age is 0 for LoA 2.
    public async Task<IActionResult> StepUp()
    {
        var claimsParameter = JsonSerializer.Serialize(new
        {
            id_token = new
            {
                acr = new { essential = true, values = new[] { "gold" } }
            }
        });

        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action("Dashboard", "Trading")
        };
        props.Items["claims"] = claimsParameter;

        await HttpContext.ChallengeAsync("OpenIdConnect", props);
        return new EmptyResult();
    }
}
