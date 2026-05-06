using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using trading_app.Models;

namespace trading_app.Controllers;

[Authorize]
public class TradingController : Controller
{
    public IActionResult Dashboard()
    {
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

    // TODO Lab 8: Add a StepUp() action that triggers a mid-session OIDC challenge
    // using the claims parameter with essential:true to demand acr=gold.
    // Redirect back to Dashboard on success.
}
