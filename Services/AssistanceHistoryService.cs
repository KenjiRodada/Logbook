using MySqlConnector;
using Logbook.Data;
using Logbook.Models;

namespace Logbook.Services;

public sealed class AssistanceHistoryService(MySqlConnectionFactory factory)
{
    public async Task<AssistanceHistoryViewModel?> GetAsync(long houseId, long? memberId = null)
    {
        await using var cn = factory.Create();
        await cn.OpenAsync();

        const string headerSql = """
            SELECT h.house_id, h.house_code,
                   COALESCE(v.name_th, '-') AS village_name,
                   COALESCE(t.name_th, '-') AS tumbol_name,
                   COALESCE(a.name_th, '-') AS amphur_name,
                   CASE WHEN @memberId IS NULL THEN '' ELSE
                     TRIM(CONCAT(COALESCE(px.name, ''), COALESCE(m.first_name, ''), ' ', COALESCE(m.last_name, ''))) END AS member_name
            FROM t_house h
            LEFT JOIN m_village v ON v.village_id = h.village_id
            LEFT JOIN m_tumbol t ON t.tumbol_id = v.tumbol_id
            LEFT JOIN m_amphur a ON a.amphur_id = t.amphur_id
            LEFT JOIN t_house_member m ON m.house_id = h.house_id AND m.member_id = @memberId
            LEFT JOIN m_prefix px ON px.prefix_id = m.prefix_id
            WHERE h.house_id = @houseId AND h.is_active = 1
              AND (@memberId IS NULL OR m.member_id IS NOT NULL)
            LIMIT 1
            """;

        await using var headerCmd = new MySqlCommand(headerSql, cn);
        headerCmd.Parameters.AddWithValue("@houseId", houseId);
        headerCmd.Parameters.AddWithValue("@memberId", memberId is null ? DBNull.Value : memberId.Value);
        await using var hr = await headerCmd.ExecuteReaderAsync();
        if (!await hr.ReadAsync()) return null;

        var vm = new AssistanceHistoryViewModel
        {
            HouseId = Convert.ToInt64(hr["house_id"]),
            HouseCode = Convert.ToString(hr["house_code"]) ?? "",
            HouseholdLabel = "ครัวเรือนรหัส " + (Convert.ToString(hr["house_code"]) ?? ""),
            Village = Convert.ToString(hr["village_name"]) ?? "-",
            Tumbol = Convert.ToString(hr["tumbol_name"]) ?? "-",
            Amphur = Convert.ToString(hr["amphur_name"]) ?? "-",
            MemberId = memberId,
            MemberName = Convert.ToString(hr["member_name"]) ?? ""
        };
        await hr.CloseAsync();

        // This new table stores general support delivered to a household or person.
        // If it hasn't been created yet, legacy repair request history still displays below.
        try
        {
            const string historySql = """
                SELECT ah.assistance_id, ah.member_id, ah.assistance_type, ah.program_name,
                       ah.description, ah.amount, ah.assistance_date, ah.status, ah.provider,
                       COALESCE(TRIM(CONCAT(COALESCE(px.name, ''), COALESCE(m.first_name, ''), ' ', COALESCE(m.last_name, ''))), '') AS person_name
                FROM assistance_history ah
                LEFT JOIN t_house_member m ON m.member_id = ah.member_id
                LEFT JOIN m_prefix px ON px.prefix_id = m.prefix_id
                WHERE ah.house_id = @houseId
                  AND (@memberId IS NULL OR ah.member_id = @memberId OR ah.member_id IS NULL)
                ORDER BY ah.assistance_date DESC, ah.assistance_id DESC
                """;
            await using var cmd = new MySqlCommand(historySql, cn);
            cmd.Parameters.AddWithValue("@houseId", houseId);
            cmd.Parameters.AddWithValue("@memberId", memberId is null ? DBNull.Value : memberId.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var rowMemberId = r["member_id"] is DBNull ? (long?)null : Convert.ToInt64(r["member_id"]);
                vm.Records.Add(new AssistanceRecord
                {
                    AssistanceId = Convert.ToInt64(r["assistance_id"]),
                    Scope = rowMemberId.HasValue ? "รายบุคคล" : "ระดับครัวเรือน",
                    Recipient = rowMemberId.HasValue ? (Convert.ToString(r["person_name"]) ?? "สมาชิก") : vm.HouseholdLabel,
                    Category = Convert.ToString(r["assistance_type"]) ?? "การช่วยเหลือ",
                    ProgramName = Convert.ToString(r["program_name"]) ?? "",
                    Details = JoinDetails(Convert.ToString(r["description"]), Convert.ToString(r["provider"]) is { Length: > 0 } provider ? "หน่วยงาน: " + provider : null),
                    Amount = r["amount"] is DBNull ? null : Convert.ToDecimal(r["amount"]),
                    AssistanceDate = r["assistance_date"] is DBNull ? null : Convert.ToDateTime(r["assistance_date"]),
                    Status = Convert.ToString(r["status"]) ?? "ไม่ระบุสถานะ",
                    Source = "assistance_history"
                });
            }
        }
        catch (MySqlException ex) when (ex.Number is 1146 or 1054)
        {
            vm.HistoryTableAvailable = false;
            vm.Notice = "ยังไม่ได้สร้างตาราง assistance_history สำหรับบันทึกความช่วยเหลือทั่วไป กรุณารันไฟล์ Database/001_Create_Assistance_History.sql; ระหว่างนี้ระบบจะแสดงข้อมูลคำร้องซ่อมและสถานะสวัสดิการจากตารางเดิม";
        }

        // Existing household repair requests. Status is shown as recorded; a request is not assumed to be completed.
        const string houseRepairSql = """
            SELECT request_id, repair_type, budget, status, request_date, finished_date
            FROM repair_house_request
            WHERE house_id = @houseId
            ORDER BY request_date DESC, request_id DESC
            """;
        await using (var cmd = new MySqlCommand(houseRepairSql, cn))
        {
            cmd.Parameters.AddWithValue("@houseId", houseId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Records.Add(new AssistanceRecord
                {
                    AssistanceId = Convert.ToInt64(r["request_id"]),
                    Scope = "ระดับครัวเรือน",
                    Recipient = vm.HouseholdLabel,
                    Category = "คำร้องซ่อมบ้าน",
                    ProgramName = Convert.ToString(r["repair_type"]) ?? "ซ่อมแซมบ้าน",
                    Details = r["finished_date"] is DBNull ? "ข้อมูลจากคำร้องซ่อมบ้านเดิม" : "วันที่ดำเนินการเสร็จสิ้น: " + ToThaiDate(Convert.ToDateTime(r["finished_date"])),
                    Amount = r["budget"] is DBNull ? null : Convert.ToDecimal(r["budget"]),
                    AssistanceDate = r["request_date"] is DBNull ? null : Convert.ToDateTime(r["request_date"]),
                    FinishedDate = r["finished_date"] is DBNull ? null : Convert.ToDateTime(r["finished_date"]),
                    Status = Convert.ToString(r["status"]) ?? "ไม่ระบุสถานะ",
                    Source = "repair_house_request"
                });
            }
        }

        const string medicalRepairSql = """
            SELECT r.request_id, r.member_id, r.device_type, r.detail, r.status, r.received_date,
                   TRIM(CONCAT(COALESCE(px.name, ''), COALESCE(m.first_name, ''), ' ', COALESCE(m.last_name, ''))) AS member_name
            FROM repair_medical_request r
            INNER JOIN t_house_member m ON m.member_id = r.member_id
            LEFT JOIN m_prefix px ON px.prefix_id = m.prefix_id
            WHERE m.house_id = @houseId
              AND (@memberId IS NULL OR r.member_id = @memberId)
            ORDER BY r.received_date DESC, r.request_id DESC
            """;
        await using (var cmd = new MySqlCommand(medicalRepairSql, cn))
        {
            cmd.Parameters.AddWithValue("@houseId", houseId);
            cmd.Parameters.AddWithValue("@memberId", memberId is null ? DBNull.Value : memberId.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Records.Add(new AssistanceRecord
                {
                    AssistanceId = Convert.ToInt64(r["request_id"]),
                    Scope = "รายบุคคล",
                    Recipient = Convert.ToString(r["member_name"]) ?? "สมาชิก",
                    Category = "คำร้องอุปกรณ์การแพทย์",
                    ProgramName = Convert.ToString(r["device_type"]) ?? "อุปกรณ์การแพทย์",
                    Details = Convert.ToString(r["detail"]) ?? "",
                    AssistanceDate = r["received_date"] is DBNull ? null : Convert.ToDateTime(r["received_date"]),
                    Status = Convert.ToString(r["status"]) ?? "ไม่ระบุสถานะ",
                    Source = "repair_medical_request"
                });
            }
        }

        const string welfareSql = """
            SELECT m.member_id,
                   TRIM(CONCAT(COALESCE(px.name, ''), COALESCE(m.first_name, ''), ' ', COALESCE(m.last_name, ''))) AS member_name,
                   COALESCE(ws.name_th, '-') AS welfare_name, mw.has_welfare_card,
                   mw.is_new_added, mw.updated_at
            FROM member_welfare mw
            INNER JOIN t_house_member m ON m.member_id = mw.member_id
            LEFT JOIN m_prefix px ON px.prefix_id = m.prefix_id
            LEFT JOIN m_welfare_status ws ON ws.welfare_status_id = mw.welfare_status_id
            WHERE m.house_id = @houseId
              AND (@memberId IS NULL OR m.member_id = @memberId)
            ORDER BY m.member_id
            """;
        await using (var cmd = new MySqlCommand(welfareSql, cn))
        {
            cmd.Parameters.AddWithValue("@houseId", houseId);
            cmd.Parameters.AddWithValue("@memberId", memberId is null ? DBNull.Value : memberId.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.WelfareStatuses.Add(new WelfareSnapshot
                {
                    MemberId = Convert.ToInt64(r["member_id"]),
                    MemberName = Convert.ToString(r["member_name"]) ?? "",
                    WelfareName = Convert.ToString(r["welfare_name"]) ?? "-",
                    HasWelfareCard = Convert.ToInt32(r["has_welfare_card"]) == 1,
                    IsNewAdded = Convert.ToInt32(r["is_new_added"]) == 1,
                    UpdatedAt = r["updated_at"] is DBNull ? null : Convert.ToDateTime(r["updated_at"])
                });
            }
        }

        vm.Records = vm.Records.OrderByDescending(x => x.AssistanceDate ?? DateTime.MinValue).ToList();
        return vm;
    }

    public async Task CreateAsync(AssistanceCreateViewModel input, int? createdBy)
    {
        await using var cn = factory.Create();
        await cn.OpenAsync();

        await using (var checkHouse = new MySqlCommand(
            "SELECT COUNT(*) FROM t_house WHERE house_id = @houseId AND is_active = 1", cn))
        {
            checkHouse.Parameters.AddWithValue("@houseId", input.HouseId);
            if (Convert.ToInt64(await checkHouse.ExecuteScalarAsync()) == 0)
                throw new InvalidOperationException("ไม่พบครัวเรือนที่เลือก");
        }

        if (input.MemberId.HasValue)
        {
            await using var checkMember = new MySqlCommand(
                "SELECT COUNT(*) FROM t_house_member WHERE member_id = @memberId AND house_id = @houseId AND is_active = 1", cn);
            checkMember.Parameters.AddWithValue("@memberId", input.MemberId.Value);
            checkMember.Parameters.AddWithValue("@houseId", input.HouseId);
            if (Convert.ToInt64(await checkMember.ExecuteScalarAsync()) == 0)
                throw new InvalidOperationException("สมาชิกที่เลือกไม่ได้อยู่ในครัวเรือนนี้");
        }

        const string sql = """
            INSERT INTO assistance_history
                (house_id, member_id, assistance_type, program_name, description, amount,
                 assistance_date, status, provider, created_by, created_at)
            VALUES
                (@houseId, @memberId, @type, @program, @description, @amount,
                 @date, @status, @provider, @createdBy, CURRENT_TIMESTAMP)
            """;
        await using var cmd = new MySqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@houseId", input.HouseId);
        cmd.Parameters.AddWithValue("@memberId", input.MemberId is null ? DBNull.Value : input.MemberId.Value);
        cmd.Parameters.AddWithValue("@type", input.AssistanceType.Trim());
        cmd.Parameters.AddWithValue("@program", input.ProgramName.Trim());
        cmd.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(input.Description) ? DBNull.Value : input.Description.Trim());
        cmd.Parameters.AddWithValue("@amount", input.Amount is null ? DBNull.Value : input.Amount.Value);
        cmd.Parameters.AddWithValue("@date", input.AssistanceDate.Date);
        cmd.Parameters.AddWithValue("@status", input.Status.Trim());
        cmd.Parameters.AddWithValue("@provider", string.IsNullOrWhiteSpace(input.Provider) ? DBNull.Value : input.Provider.Trim());
        cmd.Parameters.AddWithValue("@createdBy", createdBy is null ? DBNull.Value : createdBy.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string JoinDetails(string? description, string? extra)
        => string.Join(" · ", new[] { description, extra }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public static string ToThaiDate(DateTime d) => $"{d.Day:00}/{d.Month:00}/{d.Year + 543}";

    public static AssistanceHistoryViewModel Sample(long houseId, long? memberId = null) => new()
    {
        HouseId = houseId,
        HouseCode = "001",
        HouseholdLabel = "ครัวเรือนรหัส 001",
        MemberId = memberId,
        MemberName = memberId switch { 1 => "นายสมชาย ใจดี", 2 => "นางสมพร ใจดี", 3 => "เด็กชายสมปอง ใจดี", 4 => "เด็กหญิงสมฤดี ใจดี", _ => "" },
        Village = "บ้านหนองไผ่", Tumbol = "นาอ้อ", Amphur = "เมือง", IsSampleData = true,
        HistoryTableAvailable = false,
        Notice = "กำลังแสดงข้อมูลจำลองเพื่อสาธิตหน้าจอ ไม่ใช่ประวัติจริงจาก Database; เมื่อต่อฐานข้อมูลได้จะแสดงข้อมูลจริงที่บันทึกไว้",
        Records = new List<AssistanceRecord>
        {
            new() { Scope="ระดับครัวเรือน", Recipient="ครัวเรือนรหัส 001", Category="ซ่อมแซมบ้าน", ProgramName="ซ่อมหลังคารั่ว", Details="ตัวอย่างข้อมูล · เปลี่ยนกระเบื้องหลังคาที่ชำรุด", AssistanceDate=new DateTime(2026,9,15), FinishedDate=new DateTime(2026,9,22), Amount=18000, Status="ดำเนินการเสร็จสิ้น", Source="ข้อมูลตัวอย่าง" },
            new() { Scope="รายบุคคล", Recipient="นายสมชาย ใจดี", Category="อุปกรณ์การแพทย์", ProgramName="รถเข็นผู้ป่วย", Details="ตัวอย่างข้อมูล · บันทึกว่ามีการส่งมอบอุปกรณ์แล้ว", AssistanceDate=new DateTime(2026,10,2), Amount=null, Status="ส่งมอบแล้ว", Source="ข้อมูลตัวอย่าง" },
            new() { Scope="ระดับครัวเรือน", Recipient="ครัวเรือนรหัส 001", Category="เงินช่วยเหลือ", ProgramName="เงินช่วยเหลือฉุกเฉิน", Details="ตัวอย่างข้อมูล · ใช้แสดงรูปแบบบันทึกการช่วยเหลือทั่วไป", AssistanceDate=new DateTime(2026,10,10), Amount=3000, Status="ได้รับแล้ว", Source="ข้อมูลตัวอย่าง" },
            new() { Scope="ระดับครัวเรือน", Recipient="ครัวเรือนรหัส 001", Category="สิ่งของช่วยเหลือ", ProgramName="ชุดอุปโภคบริโภค", Details="ตัวอย่างข้อมูล · ชุดสิ่งของจำเป็นสำหรับครอบครัว", AssistanceDate=new DateTime(2026,8,12), Amount=null, Status="ได้รับแล้ว", Source="ข้อมูลตัวอย่าง" },
            new() { Scope="ระดับครัวเรือน", Recipient="ครัวเรือนรหัส 001", Category="คำร้องซ่อมบ้าน", ProgramName="ซ่อมแซมห้องน้ำ", Details="ตัวอย่างข้อมูล · คำร้องอยู่ระหว่างดำเนินการ", AssistanceDate=new DateTime(2026,10,18), Amount=8500, Status="กำลังดำเนินการ", Source="ข้อมูลตัวอย่าง" }
        }.Where(x => memberId is null || x.Scope == "ระดับครัวเรือน" || x.Recipient == (memberId switch { 1 => "นายสมชาย ใจดี", 2 => "นางสมพร ใจดี", 3 => "เด็กชายสมปอง ใจดี", 4 => "เด็กหญิงสมฤดี ใจดี", _ => "" })).ToList(),
        WelfareStatuses = new List<WelfareSnapshot>
        {
            new() { MemberId=1, MemberName="นายสมชาย ใจดี", WelfareName="บัตรสวัสดิการแห่งรัฐ", HasWelfareCard=true, IsNewAdded=false, UpdatedAt=new DateTime(2026,10,20) },
            new() { MemberId=2, MemberName="นางสมพร ใจดี", WelfareName="บัตรสวัสดิการแห่งรัฐ", HasWelfareCard=true, IsNewAdded=true, UpdatedAt=new DateTime(2026,9,25) }
        }.Where(x => memberId is null || x.MemberId == memberId).ToList()
    };
}
