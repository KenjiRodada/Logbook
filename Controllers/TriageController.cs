using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Logbook.Services;

namespace Logbook.Controllers;

[Authorize]
public sealed class TriageController(TriageService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, string? priority = "All", int? groupId = null)
    {
        try
        {
            return View(await service.GetAsync(search, priority, groupId));
        }
        catch (Exception ex)
        {
            var model = TriageService.Sample(search, priority, groupId);
            model.Notice = "ระบบยังอ่านฐานข้อมูลคัดกรองไม่ได้ จึงแสดงข้อมูลตัวอย่างเพื่อให้ดูหน้าจอ: " + ex.Message;
            return View(model);
        }
    }
}
