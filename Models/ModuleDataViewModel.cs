namespace Logbook.Models;

public sealed class ModuleDataViewModel
{
    public string ModuleId { get; set; } = "";
    public string Title { get; set; } = "ระบบข้อมูล";
    public string Description { get; set; } = "";
    public string? DbMessage { get; set; }
    public bool IsSampleData { get; set; }
    public string? SelectedView { get; set; }
    public string? Search { get; set; }
    public string? InformationNote { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public List<ReportDefinition> Reports { get; set; } = new();
}

public sealed class ReportDefinition
{
    public string ViewName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public long? RowCount { get; set; }
}
