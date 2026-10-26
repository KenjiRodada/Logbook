using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Logbook.Models;
using Logbook.Services;

namespace Logbook.Controllers;

[Authorize]
public sealed class HouseholdController(HouseholdService service, AssistanceHistoryService assistanceService) : Controller
{
    public async Task<IActionResult> Index(string? search, string? amphur, string? tumbol)
    {
        ViewBag.Search = search; ViewBag.Amphur = amphur; ViewBag.Tumbol = tumbol;
        try { return View(await service.GetAsync(search, amphur, tumbol)); }
        catch (Exception ex)
        {
            ViewBag.DbMessage = "แสดงข้อมูลตัวอย่าง เนื่องจากยังเชื่อมต่อฐานข้อมูลไม่ได้: " + ex.Message;
            var rows = HouseholdService.SampleRows();
            if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(x => x.HouseCode.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Village.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(amphur)) rows = rows.Where(x => x.Amphur == amphur).ToList();
            if (!string.IsNullOrWhiteSpace(tumbol)) rows = rows.Where(x => x.Tumbol == tumbol).ToList();
            return View(rows);
        }
    }

    public async Task<IActionResult> Details(long id = 1)
    {
        HouseholdDetailsViewModel model;
        try
        {
            var result = await service.GetDetailsAsync(id);
            if (result is null) return NotFound();
            model = result;
            try
            {
                var assistance = await assistanceService.GetAsync(id);
                if (assistance is not null)
                {
                    model.AssistanceRecords = assistance.Records;
                    model.WelfareStatuses = assistance.WelfareStatuses;
                    model.AssistanceHistoryTableAvailable = assistance.HistoryTableAvailable;
                    model.AssistanceHistoryNotice = assistance.Notice;
                }
            }
            catch (Exception ex)
            {
                model.IsSampleData = true;
                model.AssistanceRecords = AssistanceHistoryService.Sample(id).Records;
                model.WelfareStatuses = AssistanceHistoryService.Sample(id).WelfareStatuses;
                model.AssistanceHistoryNotice = "แสดงข้อมูลตัวอย่าง เนื่องจากอ่านประวัติจาก Database ไม่ได้: " + ex.Message;
            }
            return View(model);
        }
        catch
        {
            model = HouseholdService.SampleDetails(id);
            var sample = AssistanceHistoryService.Sample(id);
            model.AssistanceRecords = sample.Records;
            model.WelfareStatuses = sample.WelfareStatuses;
            model.AssistanceHistoryTableAvailable = false;
            model.AssistanceHistoryNotice = sample.Notice;
            return View(model);
        }
    }
}
