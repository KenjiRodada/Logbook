using MySqlConnector;
using Logbook.Data;
using Logbook.Models;

namespace Logbook.Services;

public sealed class DashboardService(MySqlConnectionFactory factory)
{
    public async Task<DashboardViewModel> GetAsync()
    {
        await using var cn = factory.Create();
        await cn.OpenAsync();
        return new DashboardViewModel
        {
            Households = await Count(cn, "t_house", "is_active = 1"),
            Members = await Count(cn, "t_house_member", "is_active = 1"),
            Villages = await Count(cn, "m_village", "is_active = 1"),
            Diseases = await Count(cn, "member_disease", null),
            WelfareMembers = await Count(cn, "member_welfare", "has_welfare_card = 1"),
            IsSampleData = false,
            Announcements = DefaultAnnouncements()
        };
    }

    private static async Task<long> Count(MySqlConnection cn, string table, string? condition)
    {
        var sql = $"SELECT COUNT(*) FROM `{table}`" + (string.IsNullOrEmpty(condition) ? "" : $" WHERE {condition}");
        await using var cmd = new MySqlCommand(sql, cn);
        var value = await cmd.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    public static DashboardViewModel Sample() => new()
    {
        Households = 12548, Members = 32746, Villages = 245, Diseases = 1284,
        WelfareMembers = 8973, IsSampleData = true,
        Announcements = DefaultAnnouncements()
    };

    private static List<Announcement> DefaultAnnouncements() => new()
    {
        new() { Title = "ประกาศอัปเดตข้อมูลครัวเรือน ปี 2569", Date = "20 ก.พ. 2569", Color = "blue" },
        new() { Title = "การจัดเก็บข้อมูล จปฐ. ปี 2569", Date = "15 ม.ค. 2569", Color = "orange" },
        new() { Title = "ปิดปรับปรุงระบบชั่วคราว", Date = "10 ม.ค. 2569", Color = "red" },
        new() { Title = "คู่มือการใช้งานระบบ Logbook", Date = "5 ม.ค. 2569", Color = "blue" }
    };
}
