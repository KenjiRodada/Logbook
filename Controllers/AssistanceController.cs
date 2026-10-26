using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Logbook.Models;
using Logbook.Services;

namespace Logbook.Controllers;

[Authorize]
public sealed class AssistanceController(AssistanceHistoryService service, HouseholdService householdService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Choose(string? search = null)
    {
        ViewBag.Search = search;
        try
        {
            return View(await householdService.GetAsync(search));
        }
        catch
        {
            ViewBag.IsSampleData = true;
            var rows = HouseholdService.SampleRows();
            if (!string.IsNullOrWhiteSpace(search))
                rows = rows.Where(x => x.HouseCode.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Village.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            return View(rows);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index(long houseId, long? memberId = null)
    {
        if (houseId <= 0) return RedirectToAction("Index", "Household");
        try
        {
            var model = await service.GetAsync(houseId, memberId);
            if (model is null) return NotFound("ไม่พบครัวเรือนหรือสมาชิกที่ระบุ");
            return View(model);
        }
        catch (Exception ex)
        {
            var model = AssistanceHistoryService.Sample(houseId, memberId);
            model.Notice = "แสดงข้อมูลตัวอย่างเนื่องจากเชื่อมต่อ Database ไม่ได้ ข้อมูลนี้ไม่ใช่ข้อมูลจริง: " + ex.Message;
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Create(long houseId, long? memberId = null)
    {
        if (houseId <= 0) return RedirectToAction("Index", "Household");
        return View(new AssistanceCreateViewModel
        {
            HouseId = houseId,
            MemberId = memberId,
            AssistanceDate = DateTime.Today,
            Status = "ได้รับแล้ว"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssistanceCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(claim, out var parsed) ? parsed : null;
            await service.CreateAsync(model, userId);
            TempData["Success"] = "บันทึกประวัติการช่วยเหลือเรียบร้อยแล้ว";
            return RedirectToAction(nameof(Index), new { houseId = model.HouseId, memberId = model.MemberId });
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message.Contains("assistance_history", StringComparison.OrdinalIgnoreCase)
                ? "ยังไม่มีตารางบันทึกประวัติการช่วยเหลือ กรุณารัน Database/001_Create_Assistance_History.sql ใน Aiven ก่อน"
                : "บันทึกไม่สำเร็จ: " + ex.Message;
            return View(model);
        }
    }
}
