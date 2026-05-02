using Microsoft.AspNetCore.Mvc;

namespace trading_app.Controllers;

// TODO: Add [Authorize] once you configure OIDC authentication in Program.cs.
public class TradingController : Controller
{
    public IActionResult Dashboard() => View();
}
