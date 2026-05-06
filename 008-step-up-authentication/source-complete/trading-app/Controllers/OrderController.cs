using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace trading_app.Controllers;

[Authorize]
public class OrderController : Controller
{
    public IActionResult Initiate()
    {
        ViewBag.HasGold = User.FindFirst("acr")?.Value == "gold";
        return View();
    }

    public async Task<IActionResult> StepUp()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action("Initiate", "Order")
        };
        props.Items["claims"] = JsonSerializer.Serialize(new
        {
            id_token = new { acr = new { essential = true, values = new[] { "gold" } } }
        });
        await HttpContext.ChallengeAsync("OpenIdConnect", props);
        return new EmptyResult();
    }

    [HttpPost]
    public IActionResult Execute(string symbol, int quantity, string orderType)
    {
        if (User.FindFirst("acr")?.Value != "gold")
            return RedirectToAction("Initiate");

        ViewBag.Result = JsonSerializer.Serialize(new
        {
            status    = "placed",
            symbol    = symbol,
            quantity  = quantity,
            orderType = orderType,
            acr       = User.FindFirst("acr")?.Value
        });
        return View("Success");
    }
}
