using Microsoft.AspNetCore.Mvc;

namespace trading_app.Controllers;

// TODO Lab 8: Implement TransferController with:
//   - GET /Transfer/Initiate — shows a transfer form. Protected by [Authorize].
//   - POST /Transfer/Execute — processes the transfer. Protected by [Authorize(Policy = "AcrGold")].
//     The Execute action should validate acr=gold (enforced by the policy attribute) and return
//     a mock JSON result via ViewBag.Result rendered in the Success view.
public class TransferController : Controller
{
    public IActionResult Index()
    {
        return Content("TODO: Implement TransferController for Lab 8.");
    }
}
