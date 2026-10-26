using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Logbook.Services;

namespace Logbook.Controllers;

[Authorize]
public sealed class ModulesController(ModuleDataService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string id = "Reports", string? view = null, string? search = null)
    {
        if (id == "Users" && !User.IsInRole("admin"))
            return Forbid();

        try
        {
            return View(await service.GetAsync(id, view, search));
        }
        catch (Exception)
        {
            var model = ModuleDataService.Sample(id, view, search);
            model.DbMessage = "ยังอ่านฐานข้อมูลไม่ได้ จึงแสดงข้อมูลตัวอย่างเพื่อให้ตรวจรูปแบบหน้าเว็บได้ ข้อมูลนี้ไม่ใช่ข้อมูลจริง";
            return View(model);
        }
    }
}
