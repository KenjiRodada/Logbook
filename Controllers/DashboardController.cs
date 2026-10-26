using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Logbook.Services;

namespace Logbook.Controllers;

public sealed class DashboardController(DashboardService service) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        try { return View(await service.GetAsync()); }
        catch (Exception ex)
        {
            var model = DashboardService.Sample();
            model.DbMessage = ex.Message;
            return View(model);
        }
    }
}
