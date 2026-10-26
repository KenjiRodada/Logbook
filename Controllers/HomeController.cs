using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logbook.Controllers;

public sealed class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Error() => View("~/Views/Shared/Error.cshtml");
}
