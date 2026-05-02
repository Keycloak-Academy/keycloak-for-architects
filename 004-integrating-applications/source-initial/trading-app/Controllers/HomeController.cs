using Microsoft.AspNetCore.Mvc;

namespace trading_app.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Dashboard", "Trading");
}
