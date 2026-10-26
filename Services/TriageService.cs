using MySqlConnector;
using Logbook.Data;
using Logbook.Models;

namespace Logbook.Services;

/// <summary>
/// Rule-based support triage. This is a review aid, not an automatic decision on eligibility.
/// All group types defined in m_vulnerable_type are treated as eligible for screening;
/// urgency is inferred from recorded needs/requests, dependency flags and previous support.
/// </summary>
public sealed class TriageService(MySqlConnectionFactory factory)
{
    public async Task<TriageViewModel> GetAsync(string? search = null, string? priority = null, int? groupId = null)
    {
        var vm = new TriageViewModel { Search = search, Priority = string.IsNullOrWhiteSpace(priority) ? "All" : priority, GroupId = groupId };
        await using var cn = factory.Create();
        await cn.OpenAsync();

        const string groupSql = "SELECT vulnerable_type_id, name_th FROM m_vulnerable_type ORDER BY name_th";
        await using (var gc = new MySqlCommand(groupSql, cn))
        await using (var gr = await gc.ExecuteReaderAsync())
        {
            while (await gr.ReadAsync())
                vm.GroupOptions.Add(new VulnerableGroupOption { Id = Convert.ToInt32(gr["vulnerable_type_id"]), Name = Convert.ToString(gr["name_th"]) ?? "ไม่ระบุชื่อกลุ่ม" });
        }

        const string sql = """
            SELECT h.house_id, h.house_code,
                   COALESCE(v.name_th, '-') AS village_name,
                   COALESCE(t.name_th, '-') AS tumbol_name,
                   COALESCE(a.name_th, '-') AS amphur_name,
                   COALESCE(mc.member_count, 0) AS member_count,
                   COALESCE(vg.vulnerable_member_count, 0) AS vulnerable_member_count,
                   COALESCE(vg.vulnerable_type_count, 0) AS vulnerable_type_count,
                   COALESCE(vg.vulnerable_types, '-') AS vulnerable_types,
                   COALESCE(vg.high_dependency_count, 0) AS high_dependency_count,
                   COALESCE(rq.open_request_count, 0) AS open_request_count,
                   COALESCE(wf.welfare_card_count, 0) AS welfare_card_count
            FROM t_house h
            LEFT JOIN m_village v ON v.village_id = h.village_id
            LEFT JOIN m_tumbol t ON t.tumbol_id = v.tumbol_id
            LEFT JOIN m_amphur a ON a.amphur_id = t.amphur_id
            LEFT JOIN (
                SELECT house_id, COUNT(*) AS member_count
                FROM t_house_member WHERE is_active = 1 GROUP BY house_id
            ) mc ON mc.house_id = h.house_id
            INNER JOIN (
                SELECT m.house_id,
                       COUNT(DISTINCT mv.member_id) AS vulnerable_member_count,
                       COUNT(DISTINCT mv.vulnerable_type_id) AS vulnerable_type_count,
                       GROUP_CONCAT(DISTINCT vt.name_th ORDER BY vt.name_th SEPARATOR ' · ') AS vulnerable_types,
                       COUNT(DISTINCT CASE WHEN COALESCE(m.is_disabled,0)=1
                                      OR COALESCE(m.is_chronic_patient,0)=1
                                      OR COALESCE(m.is_self_reliance,1)=0
                                      THEN m.member_id END) AS high_dependency_count
                FROM member_vulnerable mv
                INNER JOIN t_house_member m ON m.member_id = mv.member_id AND m.is_active = 1
                LEFT JOIN m_vulnerable_type vt ON vt.vulnerable_type_id = mv.vulnerable_type_id
                GROUP BY m.house_id
            ) vg ON vg.house_id = h.house_id
            LEFT JOIN (
                SELECT request_set.house_id,
                       SUM(CASE WHEN LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%เสร็จ%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%ส่งมอบ%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%ได้รับแล้ว%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%ปิด%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%ยกเลิก%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%complete%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%completed%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%delivered%'
                                     OR LOWER(TRIM(COALESCE(request_set.status,''))) LIKE '%cancel%'
                                THEN 0 ELSE 1 END) AS open_request_count
                FROM (
                    SELECT house_id, status FROM repair_house_request
                    UNION ALL
                    SELECT m.house_id, r.status
                    FROM repair_medical_request r
                    INNER JOIN t_house_member m ON m.member_id = r.member_id
                ) request_set
                GROUP BY request_set.house_id
            ) rq ON rq.house_id = h.house_id
            LEFT JOIN (
                SELECT m.house_id,
                       COUNT(DISTINCT CASE WHEN mw.has_welfare_card = 1 THEN mw.member_id END) AS welfare_card_count
                FROM member_welfare mw
                INNER JOIN t_house_member m ON m.member_id = mw.member_id
                GROUP BY m.house_id
            ) wf ON wf.house_id = h.house_id
            WHERE h.is_active = 1
              AND (@searchPattern IS NULL
                   OR h.house_code LIKE @searchPattern
                   OR COALESCE(v.name_th,'') LIKE @searchPattern
                   OR COALESCE(t.name_th,'') LIKE @searchPattern
                   OR COALESCE(a.name_th,'') LIKE @searchPattern
                   OR COALESCE(vg.vulnerable_types,'') LIKE @searchPattern)
              AND (@groupId IS NULL OR EXISTS (
                    SELECT 1 FROM member_vulnerable mv2
                    INNER JOIN t_house_member m2 ON m2.member_id = mv2.member_id AND m2.is_active = 1
                    WHERE m2.house_id = h.house_id AND mv2.vulnerable_type_id = @groupId))
            ORDER BY h.house_code
            """;

        var pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        await using (var cmd = new MySqlCommand(sql, cn))
        {
            cmd.Parameters.Add(new MySqlParameter("@searchPattern", MySqlDbType.VarChar) { Value = pattern is null ? DBNull.Value : pattern });
            cmd.Parameters.AddWithValue("@groupId", groupId is null ? DBNull.Value : groupId.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.AllRows.Add(new TriageHouseholdRow
                {
                    HouseId = Convert.ToInt64(r["house_id"]),
                    HouseCode = Convert.ToString(r["house_code"]) ?? "",
                    Village = Convert.ToString(r["village_name"]) ?? "-",
                    Tumbol = Convert.ToString(r["tumbol_name"]) ?? "-",
                    Amphur = Convert.ToString(r["amphur_name"]) ?? "-",
                    MemberCount = Convert.ToInt32(r["member_count"]),
                    VulnerableMemberCount = Convert.ToInt32(r["vulnerable_member_count"]),
                    VulnerableTypeCount = Convert.ToInt32(r["vulnerable_type_count"]),
                    VulnerableTypes = Convert.ToString(r["vulnerable_types"]) ?? "-",
                    HighDependencyCount = Convert.ToInt32(r["high_dependency_count"]),
                    OpenRequestCount = Convert.ToInt32(r["open_request_count"]),
                    WelfareCardCount = Convert.ToInt32(r["welfare_card_count"])
                });
            }
        }

        // This table is optional in existing databases. Never interpret a missing table as proof that aid was not received.
        try
        {
            const string aidSql = """
                SELECT house_id, COUNT(*) AS aid_count, MAX(assistance_date) AS last_date
                FROM assistance_history
                GROUP BY house_id
                """;
            await using var cmd = new MySqlCommand(aidSql, cn);
            await using var r = await cmd.ExecuteReaderAsync();
            var aidByHouse = new Dictionary<long, (int Count, DateTime? LastDate)>();
            while (await r.ReadAsync())
                aidByHouse[Convert.ToInt64(r["house_id"])] = (Convert.ToInt32(r["aid_count"]), r["last_date"] is DBNull ? null : Convert.ToDateTime(r["last_date"]));
            foreach (var row in vm.AllRows)
                if (aidByHouse.TryGetValue(row.HouseId, out var aid))
                {
                    row.AssistanceCount = aid.Count;
                    row.LastAssistanceDate = aid.LastDate;
                }
            vm.AssistanceHistoryAvailable = true;
        }
        catch (MySqlException ex) when (ex.Number is 1146 or 1054)
        {
            vm.AssistanceHistoryAvailable = false;
            vm.Notice = "ยังไม่มีตาราง assistance_history จึงใช้เฉพาะคำร้องเดิมและสถานะบัตรสวัสดิการประกอบการคัดกรอง ไม่ถือว่าการไม่มีประวัติในตารางนี้แปลว่าไม่เคยได้รับความช่วยเหลือ";
        }

        foreach (var row in vm.AllRows) ApplyPriority(row, vm.AssistanceHistoryAvailable);
        vm.AllRows = vm.AllRows
            .OrderBy(x => x.SortRank)
            .ThenByDescending(x => x.OpenRequestCount)
            .ThenByDescending(x => x.HighDependencyCount)
            .ThenByDescending(x => x.VulnerableMemberCount)
            .ThenBy(x => x.HouseCode)
            .ToList();

        vm.Rows = vm.Priority switch
        {
            "Red" => vm.AllRows.Where(x => x.PriorityCode == "Red").ToList(),
            "Yellow" => vm.AllRows.Where(x => x.PriorityCode == "Yellow").ToList(),
            "Green" => vm.AllRows.Where(x => x.PriorityCode == "Green").ToList(),
            _ => vm.AllRows.ToList()
        };
        return vm;
    }

    private static void ApplyPriority(TriageHouseholdRow row, bool historyTableAvailable)
    {
        var hasRecordedSupport = row.AssistanceCount > 0 || row.WelfareCardCount > 0;
        var red = (row.OpenRequestCount > 0 && row.HighDependencyCount > 0)
                  || row.HighDependencyCount >= 2
                  || (row.VulnerableMemberCount >= 3 && row.VulnerableTypeCount >= 2);
        var yellow = row.OpenRequestCount > 0
                     || row.VulnerableMemberCount >= 2
                     || row.VulnerableTypeCount >= 2
                     || (historyTableAvailable && !hasRecordedSupport);

        if (red)
        {
            row.PriorityCode = "Red";
            row.PriorityLabel = "ช่วยเหลือเร่งด่วน";
            var reasons = new List<string>();
            if (row.OpenRequestCount > 0 && row.HighDependencyCount > 0) reasons.Add("มีคำร้องที่ยังไม่เสร็จและมีสมาชิกที่ต้องพึ่งพิง");
            if (row.HighDependencyCount >= 2) reasons.Add("มีสมาชิกที่ต้องพึ่งพิงตั้งแต่ 2 คนขึ้นไป");
            if (row.VulnerableMemberCount >= 3 && row.VulnerableTypeCount >= 2) reasons.Add("มีสมาชิกเปราะบางหลายคนและพบหลายประเภท");
            row.PriorityReason = string.Join("; ", reasons);
            row.SortRank = 0;
        }
        else if (yellow)
        {
            row.PriorityCode = "Yellow";
            row.PriorityLabel = "ควรช่วยเหลือ";
            var reasons = new List<string>();
            if (row.OpenRequestCount > 0) reasons.Add($"มีคำร้องค้าง/อยู่ระหว่างดำเนินการ {row.OpenRequestCount} รายการ");
            if (row.VulnerableMemberCount >= 2) reasons.Add($"มีสมาชิกในกลุ่มเปราะบาง {row.VulnerableMemberCount} คน");
            if (row.VulnerableTypeCount >= 2) reasons.Add($"พบกลุ่มเปราะบาง {row.VulnerableTypeCount} ประเภท");
            if (historyTableAvailable && !hasRecordedSupport) reasons.Add("ยังไม่พบประวัติช่วยเหลือหรือบัตรสวัสดิการในข้อมูลที่อ่านได้");
            row.PriorityReason = string.Join("; ", reasons.DefaultIfEmpty("พบสมาชิกในกลุ่มเปราะบาง ควรประเมินความต้องการเพิ่มเติม"));
            row.SortRank = 1;
        }
        else if (hasRecordedSupport && row.OpenRequestCount == 0 && row.HighDependencyCount == 0)
        {
            row.PriorityCode = "Green";
            row.PriorityLabel = "ให้คำแนะนำ/ติดตาม";
            row.PriorityReason = "มีประวัติการช่วยเหลือหรือข้อมูลสวัสดิการ และไม่พบสัญญาณเร่งด่วนจากฟิลด์ที่ตรวจสอบ ควรติดตามตามรอบ";
            row.SortRank = 2;
        }
        else
        {
            row.PriorityCode = "Yellow";
            row.PriorityLabel = "ควรช่วยเหลือ";
            row.PriorityReason = "พบสมาชิกในกลุ่มเปราะบาง แต่ข้อมูลยังไม่เพียงพอที่จะยืนยันว่าความต้องการได้รับการตอบสนองแล้ว ควรตรวจสอบกับครัวเรือน";
            row.SortRank = 1;
        }
    }

    public static TriageViewModel Sample(string? search = null, string? priority = null, int? groupId = null)
    {
        var vm = new TriageViewModel
        {
            Search = search, Priority = string.IsNullOrWhiteSpace(priority) ? "All" : priority,
            GroupId = groupId, AssistanceHistoryAvailable = true,
            Notice = "ตัวอย่างเพื่อสาธิตวิธีจัดลำดับเท่านั้น ไม่ใช่ข้อมูลจริง และไม่ใช่คำวินิจฉัยสิทธิ์ช่วยเหลือ",
            GroupOptions = new List<VulnerableGroupOption>
            {
                new(){Id=1,Name="ผู้สูงอายุ"}, new(){Id=2,Name="คนพิการ"}, new(){Id=3,Name="เด็กและเยาวชน"},
                new(){Id=4,Name="ผู้ป่วยเรื้อรัง"}, new(){Id=5,Name="ครอบครัวรายได้น้อย"}
            },
            AllRows = new List<TriageHouseholdRow>
            {
                new(){HouseId=1,HouseCode="001",Village="บ้านหนองไผ่",Tumbol="นาอ้อ",Amphur="เมือง",MemberCount=4,VulnerableMemberCount=2,VulnerableTypeCount=2,VulnerableTypes="ผู้สูงอายุ · คนพิการ",HighDependencyCount=1,OpenRequestCount=1,AssistanceCount=1,WelfareCardCount=1,PriorityCode="Red",PriorityLabel="ช่วยเหลือเร่งด่วน",PriorityReason="มีคำร้องค้างและมีสมาชิกที่ต้องพึ่งพิง",SortRank=0},
                new(){HouseId=2,HouseCode="002",Village="บ้านดอน",Tumbol="โพนสูง",Amphur="วานรนิวาส",MemberCount=3,VulnerableMemberCount=2,VulnerableTypeCount=2,VulnerableTypes="เด็กและเยาวชน · ครอบครัวรายได้น้อย",HighDependencyCount=0,OpenRequestCount=0,AssistanceCount=0,WelfareCardCount=0,PriorityCode="Yellow",PriorityLabel="ควรช่วยเหลือ",PriorityReason="มีสมาชิกหลายคน/หลายประเภทในกลุ่มเปราะบางและยังไม่พบประวัติช่วยเหลือในตัวอย่าง",SortRank=1},
                new(){HouseId=3,HouseCode="003",Village="บ้านโคก",Tumbol="นาอ้อ",Amphur="เมือง",MemberCount=2,VulnerableMemberCount=1,VulnerableTypeCount=1,VulnerableTypes="ผู้สูงอายุ",HighDependencyCount=0,OpenRequestCount=0,AssistanceCount=2,WelfareCardCount=1,PriorityCode="Green",PriorityLabel="ให้คำแนะนำ/ติดตาม",PriorityReason="มีประวัติช่วยเหลือแล้วและไม่พบสัญญาณเร่งด่วนในตัวอย่าง",SortRank=2}
            }
        };
        if (!string.IsNullOrWhiteSpace(search))
            vm.AllRows = vm.AllRows.Where(x => string.Join(" ",x.HouseCode,x.Village,x.Tumbol,x.Amphur,x.VulnerableTypes).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (groupId.HasValue && vm.GroupOptions.FirstOrDefault(x=>x.Id==groupId)?.Name is string gn)
            vm.AllRows = vm.AllRows.Where(x=>x.VulnerableTypes.Contains(gn,StringComparison.OrdinalIgnoreCase)).ToList();
        vm.Rows = vm.Priority switch { "Red" => vm.AllRows.Where(x=>x.PriorityCode=="Red").ToList(), "Yellow" => vm.AllRows.Where(x=>x.PriorityCode=="Yellow").ToList(), "Green" => vm.AllRows.Where(x=>x.PriorityCode=="Green").ToList(), _ => vm.AllRows.ToList() };
        return vm;
    }
}
