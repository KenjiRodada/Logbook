namespace Logbook.Models;

public sealed class TriageViewModel
{
    public string? Search { get; set; }
    public string Priority { get; set; } = "All";
    public int? GroupId { get; set; }
    public bool AssistanceHistoryAvailable { get; set; }
    public string? Notice { get; set; }
    public List<VulnerableGroupOption> GroupOptions { get; set; } = new();
    public List<TriageHouseholdRow> AllRows { get; set; } = new();
    public List<TriageHouseholdRow> Rows { get; set; } = new();
    public int RedCount => AllRows.Count(x => x.PriorityCode == "Red");
    public int YellowCount => AllRows.Count(x => x.PriorityCode == "Yellow");
    public int GreenCount => AllRows.Count(x => x.PriorityCode == "Green");
}

public sealed class VulnerableGroupOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TriageHouseholdRow
{
    public long HouseId { get; set; }
    public string HouseCode { get; set; } = "";
    public string Village { get; set; } = "-";
    public string Tumbol { get; set; } = "-";
    public string Amphur { get; set; } = "-";
    public int MemberCount { get; set; }
    public int VulnerableMemberCount { get; set; }
    public int VulnerableTypeCount { get; set; }
    public string VulnerableTypes { get; set; } = "-";
    public int HighDependencyCount { get; set; }
    public int OpenRequestCount { get; set; }
    public int AssistanceCount { get; set; }
    public int WelfareCardCount { get; set; }
    public DateTime? LastAssistanceDate { get; set; }
    public string PriorityCode { get; set; } = "Yellow";
    public string PriorityLabel { get; set; } = "ควรช่วยเหลือ";
    public string PriorityReason { get; set; } = "พบสมาชิกในกลุ่มเปราะบาง ควรตรวจสอบความต้องการ";
    public int SortRank { get; set; } = 2;
}
