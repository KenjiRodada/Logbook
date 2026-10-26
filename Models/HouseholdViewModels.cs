namespace Logbook.Models;

public sealed class HouseholdRow
{
    public long HouseId { get; set; }
    public string HouseCode { get; set; } = "";
    public int SurveyYear { get; set; }
    public string Village { get; set; } = "";
    public string Tumbol { get; set; } = "";
    public string Amphur { get; set; } = "";
    public int MemberCount { get; set; }
}

public sealed class HouseholdDetailsViewModel
{
    public long HouseId { get; set; }
    public string HouseCode { get; set; } = "";
    public int SurveyYear { get; set; }
    public string HouseNo { get; set; } = "-";
    public string Location { get; set; } = "-";
    public string Village { get; set; } = "-";
    public string Tumbol { get; set; } = "-";
    public string Amphur { get; set; } = "-";
    public string Province { get; set; } = "-";
    public int MemberCount { get; set; }
    public bool IsSampleData { get; set; }
    public List<HouseholdMemberRow> Members { get; set; } = new();
    public List<AssistanceRecord> AssistanceRecords { get; set; } = new();
    public List<WelfareSnapshot> WelfareStatuses { get; set; } = new();
    public bool AssistanceHistoryTableAvailable { get; set; } = true;
    public string? AssistanceHistoryNotice { get; set; }
}

public sealed class HouseholdMemberRow
{
    public long MemberId { get; set; }
    public string FullName { get; set; } = "";
    public int? Age { get; set; }
    public string Gender { get; set; } = "-";
    public string Occupation { get; set; } = "-";
}


public sealed class AssistanceRecord
{
    public long? AssistanceId { get; set; }
    public string Scope { get; set; } = "ระดับครัวเรือน";
    public string Recipient { get; set; } = "ครัวเรือน";
    public string Category { get; set; } = "การช่วยเหลือ";
    public string ProgramName { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime? AssistanceDate { get; set; }
    public DateTime? FinishedDate { get; set; }
    public decimal? Amount { get; set; }
    public string Status { get; set; } = "ไม่ระบุสถานะ";
    public string Source { get; set; } = "";

    public bool IsCompleted
    {
        get
        {
            var s = (Status ?? "").Trim().ToLowerInvariant();
            return new[] { "เสร็จสิ้น", "ดำเนินการเสร็จสิ้น", "สำเร็จ", "ได้รับแล้ว", "ส่งมอบแล้ว", "ปิดงาน", "completed", "complete", "done", "delivered", "received" }.Contains(s);
        }
    }
}

public sealed class WelfareSnapshot
{
    public long MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string WelfareName { get; set; } = "-";
    public bool HasWelfareCard { get; set; }
    public bool IsNewAdded { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class AssistanceHistoryViewModel
{
    public long HouseId { get; set; }
    public string HouseCode { get; set; } = "";
    public string HouseholdLabel { get; set; } = "";
    public long? MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string Village { get; set; } = "-";
    public string Tumbol { get; set; } = "-";
    public string Amphur { get; set; } = "-";
    public bool IsSampleData { get; set; }
    public bool HistoryTableAvailable { get; set; } = true;
    public string? Notice { get; set; }
    public List<AssistanceRecord> Records { get; set; } = new();
    public List<WelfareSnapshot> WelfareStatuses { get; set; } = new();
    public int CompletedCount => Records.Count(x => x.IsCompleted);
    public int TotalCount => Records.Count;
}

public sealed class AssistanceCreateViewModel
{
    [System.ComponentModel.DataAnnotations.Range(1, long.MaxValue)]
    public long HouseId { get; set; }
    public long? MemberId { get; set; }
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "กรุณาเลือกประเภทการช่วยเหลือ")]
    public string AssistanceType { get; set; } = "เงินช่วยเหลือ";
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "กรุณาระบุชื่อโครงการ/รายการ")]
    [System.ComponentModel.DataAnnotations.StringLength(150)]
    public string ProgramName { get; set; } = "";
    [System.ComponentModel.DataAnnotations.StringLength(2000)]
    public string? Description { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 9999999999.99, ErrorMessage = "จำนวนเงินไม่ถูกต้อง")]
    public decimal? Amount { get; set; }
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
    public DateTime AssistanceDate { get; set; } = DateTime.Today;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Status { get; set; } = "ได้รับแล้ว";
    [System.ComponentModel.DataAnnotations.StringLength(150)]
    public string? Provider { get; set; }
}
