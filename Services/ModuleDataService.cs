using System.Globalization;
using System.Data;
using System.Text.RegularExpressions;
using MySqlConnector;
using Logbook.Data;
using Logbook.Models;

namespace Logbook.Services;

public sealed class ModuleDataService(MySqlConnectionFactory factory)
{
    private static readonly List<ReportDefinition> ReportDefinitions = new()
    {
        new(){ViewName="v_household_happiness_by_amphur",Title="ความอยู่ดีมีสุขรายอำเภอ",Description="สรุประดับความอยู่ดีมีสุขของครัวเรือนแยกตามอำเภอ"},
        new(){ViewName="v_member_welfare_list",Title="รายชื่อผู้มีสวัสดิการ",Description="รายชื่อบุคคลพร้อมสถานะบัตรสวัสดิการและพื้นที่"},
        new(){ViewName="v_over18_no_welfare_card",Title="อายุ 18 ปีขึ้นไปที่ไม่มีบัตรสวัสดิการ",Description="รายชื่อผู้ใหญ่ที่ยังไม่มีบัตรสวัสดิการ"},
        new(){ViewName="v_poor_households_by_village",Title="ครัวเรือนยากจนรายหมู่บ้าน",Description="จำนวนครัวเรือนยากจนแยกตามหมู่บ้าน"},
        new(){ViewName="v_population_by_amphur",Title="ประชากรรายอำเภอ",Description="สรุปประชากร ครัวเรือน เพศ ผู้สูงอายุ และผู้พิการ"},
        new(){ViewName="v_population_by_tumbol",Title="ประชากรรายตำบล",Description="สรุปประชากรและครัวเรือนแยกตามตำบล"},
        new(){ViewName="v_population_by_village",Title="ประชากรรายหมู่บ้าน",Description="สรุปประชากรและครัวเรือนแยกตามหมู่บ้าน"},
        new(){ViewName="v_ppp_by_village",Title="กลุ่ม PPPConnext รายหมู่บ้าน",Description="จำนวนบุคคลในกลุ่ม G1–G4 แยกตามหมู่บ้าน"},
        new(){ViewName="v_ppp_member",Title="การจัดกลุ่ม PPPConnext รายบุคคล",Description="ผลจัดกลุ่มรายบุคคลตามคะแนนเฉลี่ย"},
        new(){ViewName="v_summary_province",Title="สรุปภาพรวมระดับจังหวัด",Description="ภาพรวมประชากร ครัวเรือน ผู้สูงอายุ และผู้พิการ"},
        new(){ViewName="v_vulnerable_counts",Title="จำนวนกลุ่มเปราะบาง",Description="จำนวนบุคคลแยกตามประเภทกลุ่มเปราะบาง"},
        new(){ViewName="v_welfare_by_tumbol",Title="สถานะสวัสดิการรายตำบล",Description="สรุปสถานะบัตรสวัสดิการแยกตามตำบล"}
    };

    private static readonly Dictionary<string, (string Title, string Description, string Sql)> ModuleQueries = new()
    {
        ["Members"] = ("ข้อมูลสมาชิก", "รายชื่อสมาชิกครัวเรือน พร้อมอายุ เพศ อาชีพ และหมู่บ้าน", """
            SELECT m.member_id AS `รหัสสมาชิก`,
              TRIM(CONCAT(COALESCE(px.name,''), COALESCE(m.first_name,''), ' ', COALESCE(m.last_name,''))) AS `ชื่อ-นามสกุล`,
              COALESCE(m.age, 0) AS `อายุ`, COALESCE(g.name, '-') AS `เพศ`,
              COALESCE(o.name, '-') AS `อาชีพ`, h.house_code AS `รหัสบ้าน`,
              COALESCE(v.name_th, '-') AS `หมู่บ้าน`
            FROM t_house_member m
            INNER JOIN t_house h ON h.house_id=m.house_id
            LEFT JOIN m_prefix px ON px.prefix_id=m.prefix_id
            LEFT JOIN m_gender g ON g.gender_id=m.gender_id
            LEFT JOIN m_occupation o ON o.occupation_id=m.occupation_id
            LEFT JOIN m_village v ON v.village_id=h.village_id
            WHERE m.is_active=1
            ORDER BY m.member_id DESC LIMIT 100
            """),
        ["Health"] = ("ข้อมูลสุขภาพ", "รายการโรค/ภาวะสุขภาพที่บันทึกไว้ใน member_disease", """
            SELECT md.member_disease_id AS `รหัสรายการ`,
              TRIM(CONCAT(COALESCE(px.name,''), COALESCE(m.first_name,''), ' ', COALESCE(m.last_name,''))) AS `ชื่อสมาชิก`,
              COALESCE(d.name_th, '-') AS `โรค`, md.diagnosed_date AS `วันที่วินิจฉัย`,
              h.house_code AS `รหัสบ้าน`, COALESCE(v.name_th, '-') AS `หมู่บ้าน`
            FROM member_disease md
            INNER JOIN t_house_member m ON m.member_id=md.member_id
            LEFT JOIN m_prefix px ON px.prefix_id=m.prefix_id
            LEFT JOIN m_disease d ON d.disease_id=md.disease_id
            LEFT JOIN t_house h ON h.house_id=m.house_id
            LEFT JOIN m_village v ON v.village_id=h.village_id
            WHERE m.is_active=1
            ORDER BY md.diagnosed_date DESC, md.member_disease_id DESC LIMIT 100
            """),
        ["Income"] = ("รายได้และอาชีพ", "รายได้ อาชีพหลัก/รอง และเงินออมที่บันทึกใน member_income", """
            SELECT m.member_id AS `รหัสสมาชิก`,
              TRIM(CONCAT(COALESCE(px.name,''), COALESCE(m.first_name,''), ' ', COALESCE(m.last_name,''))) AS `ชื่อสมาชิก`,
              COALESCE(o1.name, '-') AS `อาชีพหลัก`, COALESCE(mi.main_income,0) AS `รายได้อาชีพหลัก`,
              COALESCE(o2.name, '-') AS `อาชีพรอง`, COALESCE(mi.second_income,0) AS `รายได้อาชีพรอง`,
              COALESCE(mi.other_income,0) AS `รายได้อื่น`, COALESCE(mi.total_income,0) AS `รายได้รวม`,
              COALESCE(mi.saving,0) AS `เงินออม`
            FROM member_income mi
            INNER JOIN t_house_member m ON m.member_id=mi.member_id
            LEFT JOIN m_prefix px ON px.prefix_id=m.prefix_id
            LEFT JOIN m_occupation o1 ON o1.occupation_id=mi.main_occupation_id
            LEFT JOIN m_occupation o2 ON o2.occupation_id=mi.second_occupation_id
            WHERE m.is_active=1
            ORDER BY mi.total_income DESC LIMIT 100
            """),
        ["Welfare"] = ("สวัสดิการ", "สถานะบัตรสวัสดิการปัจจุบันของสมาชิกใน member_welfare", """
            SELECT m.member_id AS `รหัสสมาชิก`,
              TRIM(CONCAT(COALESCE(px.name,''), COALESCE(m.first_name,''), ' ', COALESCE(m.last_name,''))) AS `ชื่อสมาชิก`,
              COALESCE(ws.name_th, '-') AS `ประเภทสวัสดิการ`,
              CASE WHEN mw.has_welfare_card=1 THEN 'มีบัตร' ELSE 'ไม่มีบัตร/ไม่ระบุ' END AS `สถานะบัตร`,
              CASE WHEN mw.is_new_added=1 THEN 'ใช่' ELSE 'ไม่ใช่' END AS `เพิ่มภายหลัง`,
              mw.updated_at AS `อัปเดตล่าสุด`
            FROM member_welfare mw
            INNER JOIN t_house_member m ON m.member_id=mw.member_id
            LEFT JOIN m_prefix px ON px.prefix_id=m.prefix_id
            LEFT JOIN m_welfare_status ws ON ws.welfare_status_id=mw.welfare_status_id
            WHERE m.is_active=1
            ORDER BY mw.updated_at DESC LIMIT 100
            """),
        ["Vulnerable"] = ("กลุ่มเปราะบาง", "ประเภทกลุ่มเปราะบาง วันที่ขึ้นทะเบียน และหมายเหตุ", """
            SELECT m.member_id AS `รหัสสมาชิก`,
              TRIM(CONCAT(COALESCE(px.name,''), COALESCE(m.first_name,''), ' ', COALESCE(m.last_name,''))) AS `ชื่อสมาชิก`,
              COALESCE(vt.name_th, '-') AS `ประเภทกลุ่มเปราะบาง`, mv.identified_date AS `วันที่ขึ้นทะเบียน`,
              COALESCE(mv.note, '-') AS `หมายเหตุ`
            FROM member_vulnerable mv
            INNER JOIN t_house_member m ON m.member_id=mv.member_id
            LEFT JOIN m_prefix px ON px.prefix_id=m.prefix_id
            LEFT JOIN m_vulnerable_type vt ON vt.vulnerable_type_id=mv.vulnerable_type_id
            WHERE m.is_active=1
            ORDER BY mv.identified_date DESC LIMIT 100
            """),
        ["Users"] = ("จัดการผู้ใช้", "รายชื่อบัญชีผู้ใช้งานและบทบาท โดยไม่แสดงรหัสผ่าน", """
            SELECT user_id AS `รหัสผู้ใช้`, username AS `ชื่อผู้ใช้`,
                   COALESCE(full_name, '-') AS `ชื่อ-นามสกุล`, role AS `บทบาท`,
                   COALESCE(amphur_id, '-') AS `รหัสอำเภอ`, COALESCE(tumbol_id, '-') AS `รหัสตำบล`,
                   CASE WHEN is_active=1 THEN 'ใช้งาน' ELSE 'ปิดใช้งาน' END AS `สถานะ`,
                   created_at AS `วันที่สร้าง`
            FROM web_users
            ORDER BY user_id DESC LIMIT 100
            """),
        ["Repair"] = ("คำร้องซ่อมและอุปกรณ์การแพทย์", "รวมคำร้องซ่อมบ้านและคำร้องอุปกรณ์การแพทย์ พร้อมสถานะตามฐานข้อมูล", """
            SELECT request_type AS `ประเภทคำร้อง`, house_code AS `รหัสบ้าน`, item_name AS `รายการ`,
                   request_status AS `สถานะ`, date_value AS `วันที่`, amount_value AS `งบประมาณ`
            FROM (
              SELECT 'ซ่อมบ้าน' AS request_type, h.house_code AS house_code,
                     r.repair_type AS item_name, r.status AS request_status,
                     r.request_date AS date_value, r.budget AS amount_value
              FROM repair_house_request r INNER JOIN t_house h ON h.house_id=r.house_id
              UNION ALL
              SELECT 'อุปกรณ์การแพทย์' AS request_type, h.house_code AS house_code,
                     CONCAT(COALESCE(m.first_name,''), ' ', COALESCE(m.last_name,''), ': ', r.device_type) AS item_name,
                     r.status AS request_status, r.received_date AS date_value, NULL AS amount_value
              FROM repair_medical_request r
              INNER JOIN t_house_member m ON m.member_id=r.member_id
              INNER JOIN t_house h ON h.house_id=m.house_id
            ) AS requests
            ORDER BY date_value DESC LIMIT 100
            """)
    };

    public async Task<ModuleDataViewModel> GetAsync(string moduleId, string? viewName = null, string? search = null)
    {
        var vm = CreateBase(moduleId);
        vm.Search = search;
        await using var cn = factory.Create();
        await cn.OpenAsync();

        if (moduleId == "Reports")
        {
            vm.Reports = ReportDefinitions.Select(r => new ReportDefinition
            {
                ViewName = r.ViewName, Title = r.Title, Description = r.Description
            }).ToList();

            if (string.IsNullOrWhiteSpace(viewName))
            {
                vm.Title = "รายงานและสถิติ";
                vm.Description = "เลือก View สำหรับเปิดดูข้อมูลสรุปจากฐานข้อมูล (สูงสุด 100 แถวต่อรายงาน)";
                vm.Columns = new() { "ชื่อรายงาน", "View", "จำนวนแถว" };
                foreach (var report in vm.Reports)
                {
                    try
                    {
                        await using var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM `{report.ViewName}`", cn);
                        report.RowCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
                    }
                    catch (MySqlException) { report.RowCount = null; }
                    vm.Rows.Add(new() { report.Title, report.ViewName, report.RowCount?.ToString("N0") ?? "เปิดอ่านไม่ได้" });
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    vm.Reports = vm.Reports.Where(report => $"{report.Title} {report.Description} {report.ViewName}".Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                    vm.Rows = vm.Rows.Where(row => string.Join(" ", row).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                return vm;
            }

            var selected = ReportDefinitions.FirstOrDefault(x => x.ViewName == viewName);
            if (selected is null)
            {
                vm.InformationNote = "ไม่พบรายงานที่เลือก";
                return vm;
            }
            vm.Title = selected.Title;
            vm.Description = selected.Description;
            vm.SelectedView = selected.ViewName;
            await ReadGridAsync(cn, $"SELECT * FROM `{selected.ViewName}` LIMIT 100", vm, search);
            return vm;
        }

        if (moduleId == "Settings")
        {
            vm.Title = "ข้อมูลอ้างอิงของระบบ";
            vm.Description = "จำนวนแถวของตาราง m_* ที่ใช้เป็นตัวเลือกและข้อมูลอ้างอิงในหน้าเว็บ";
            const string sql = """
                SELECT 'ภาค' AS `ข้อมูลอ้างอิง`, COUNT(*) AS `จำนวนรายการ` FROM m_region
                UNION ALL SELECT 'จังหวัด', COUNT(*) FROM m_province
                UNION ALL SELECT 'อำเภอ', COUNT(*) FROM m_amphur
                UNION ALL SELECT 'ตำบล', COUNT(*) FROM m_tumbol
                UNION ALL SELECT 'หมู่บ้าน', COUNT(*) FROM m_village
                UNION ALL SELECT 'คำนำหน้าชื่อ', COUNT(*) FROM m_prefix
                UNION ALL SELECT 'เพศ', COUNT(*) FROM m_gender
                UNION ALL SELECT 'อาชีพ', COUNT(*) FROM m_occupation
                UNION ALL SELECT 'ระดับการศึกษา', COUNT(*) FROM m_education
                UNION ALL SELECT 'โรค', COUNT(*) FROM m_disease
                UNION ALL SELECT 'ศาสนา', COUNT(*) FROM m_religion
                UNION ALL SELECT 'ประเภทกลุ่มเปราะบาง', COUNT(*) FROM m_vulnerable_type
                """;
            await ReadGridAsync(cn, sql, vm, search);
            return vm;
        }

        if (!ModuleQueries.TryGetValue(moduleId, out var query))
        {
            vm.InformationNote = moduleId switch
            {
                "HouseholdCreate" => "ใช้เมนูข้อมูลครัวเรือนเพื่อค้นหาและตรวจสอบข้อมูลบ้านก่อน การเพิ่ม/แก้ไขข้อมูลบ้านยังต้องกำหนดฟอร์มและกฎตรวจสอบให้ตรงกับข้อมูลจริงก่อนเปิดใช้งาน",
                "HouseholdEdit" => "เลือกครัวเรือนจากหน้ารายการ แล้วตรวจสอบข้อมูลก่อนแก้ไข การแก้ไขข้อมูลจริงยังไม่ได้เปิดใช้ในเวอร์ชันนี้",
                "MemberDetails" => "เปิดข้อมูลสมาชิกผ่านหน้ารายละเอียดครัวเรือน แล้วกดดูประวัติการช่วยเหลือรายบุคคล",
                _ => "เมนูนี้ยังไม่มีชุดข้อมูลที่กำหนดไว้"
            };
            return vm;
        }

        vm.Title = query.Title;
        vm.Description = query.Description;
        await ReadGridAsync(cn, query.Sql, vm, search);
        return vm;
    }

    private static async Task ReadGridAsync(MySqlConnection cn, string sql, ModuleDataViewModel vm, string? search = null)
    {
        var effectiveSql = sql;
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Search the query result, not just the first 100 rows. Query text is authored in this service;
            // the search phrase itself is always sent as a parameter.
            var unboundedSql = Regex.Replace(sql.Trim(), @"\s+LIMIT\s+\d+\s*$", "", RegexOptions.IgnoreCase).Trim();
            var schemaSql = $"SELECT * FROM ({unboundedSql}) AS search_result LIMIT 0";
            var columnNames = new List<string>();
            await using (var schemaCmd = new MySqlCommand(schemaSql, cn))
            await using (var schemaReader = await schemaCmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly))
            {
                columnNames = Enumerable.Range(0, schemaReader.FieldCount).Select(schemaReader.GetName).ToList();
            }

            if (columnNames.Count > 0)
            {
                var fields = string.Join(", ", columnNames.Select(name =>
                {
                    var safeName = name.Replace("`", "``", StringComparison.Ordinal);
                    return $"CAST(search_result.`{safeName}` AS CHAR)";
                }));
                effectiveSql = $"SELECT * FROM ({unboundedSql}) AS search_result WHERE CONCAT_WS(' ', {fields}) LIKE @search LIMIT 100";
            }
        }

        await using var cmd = new MySqlCommand(effectiveSql, cn);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.Add(new MySqlParameter("@search", MySqlDbType.VarChar) { Value = $"%{search.Trim()}%" });
        await using var r = await cmd.ExecuteReaderAsync();
        vm.Columns = Enumerable.Range(0, r.FieldCount).Select(i => r.GetName(i)).ToList();
        while (await r.ReadAsync())
        {
            var row = new List<string>();
            for (var i = 0; i < r.FieldCount; i++)
            {
                if (r.IsDBNull(i)) { row.Add("-"); continue; }
                var value = r.GetValue(i);
                if (value is DateTime dt) row.Add($"{dt.Day:00}/{dt.Month:00}/{dt.Year + 543}");
                else if (value is decimal dec) row.Add(dec.ToString("N2", CultureInfo.GetCultureInfo("th-TH")));
                else row.Add(Convert.ToString(value, CultureInfo.GetCultureInfo("th-TH")) ?? "-");
            }
            vm.Rows.Add(row);
        }
    }

    private static ModuleDataViewModel CreateBase(string moduleId)
    {
        var (title, description) = moduleId switch
        {
            "Members" => ("ข้อมูลสมาชิก", "รายชื่อสมาชิกครัวเรือน"),
            "Health" => ("ข้อมูลสุขภาพ", "รายการโรคที่บันทึกไว้"),
            "Income" => ("รายได้และอาชีพ", "รายได้และเงินออมของสมาชิก"),
            "Welfare" => ("สวัสดิการ", "สถานะบัตรสวัสดิการปัจจุบัน"),
            "Vulnerable" => ("กลุ่มเปราะบาง", "ข้อมูลประเภทกลุ่มเปราะบาง"),
            "Repair" => ("คำร้องซ่อม", "คำร้องซ่อมบ้านและอุปกรณ์การแพทย์"),
            "Users" => ("จัดการผู้ใช้", "บัญชีผู้ใช้งานระบบ"),
            "Settings" => ("ข้อมูลอ้างอิงของระบบ", "จำนวนรายการในตาราง master data"),
            "Reports" => ("รายงานและสถิติ", "View สรุปรายงานจากฐานข้อมูล"),
            "HouseholdCreate" => ("เพิ่มข้อมูลครัวเรือน", "แบบฟอร์มสำหรับเพิ่มข้อมูลครัวเรือนยังอยู่ระหว่างพัฒนา"),
            "HouseholdEdit" => ("แก้ไขข้อมูลครัวเรือน", "ตรวจสอบข้อมูลก่อนแก้ไข"),
            "MemberDetails" => ("รายละเอียดสมาชิก", "เปิดประวัติรายบุคคลจากหน้ารายละเอียดครัวเรือน"),
            _ => ("ระบบข้อมูล", "ข้อมูลระบบ Logbook")
        };
        return new ModuleDataViewModel { ModuleId = moduleId, Title = title, Description = description };
    }

    public static ModuleDataViewModel Sample(string moduleId, string? viewName = null, string? search = null)
    {
        var vm = CreateBase(moduleId);
        vm.IsSampleData = true;
        vm.DbMessage = "ยังเชื่อมต่อฐานข้อมูลไม่ได้ จึงแสดงข้อมูลตัวอย่างเพื่อไม่ให้หน้าเว็บว่าง ข้อมูลนี้ไม่ใช่ข้อมูลจริง";
        switch (moduleId)
        {
            case "Members":
                vm.Columns = new() { "รหัสสมาชิก", "ชื่อ-นามสกุล", "อายุ", "เพศ", "อาชีพ", "รหัสบ้าน", "หมู่บ้าน" };
                vm.Rows = new() {
                    new(){"1","นายสมชาย ใจดี","45","ชาย","เกษตรกร","001","บ้านหนองไผ่"},
                    new(){"2","นางสมพร ใจดี","42","หญิง","แม่บ้าน","001","บ้านหนองไผ่"},
                    new(){"3","เด็กชายสมปอง ใจดี","12","ชาย","นักเรียน","001","บ้านหนองไผ่"},
                    new(){"4","เด็กหญิงสมฤดี ใจดี","9","หญิง","นักเรียน","001","บ้านหนองไผ่"},
                    new(){"5","นายประเสริฐ ใจบุญ","61","ชาย","เกษตรกร","002","บ้านดอน"}
                };
                break;
            case "Health":
                vm.Columns = new() { "รหัสรายการ", "ชื่อสมาชิก", "โรค", "วันที่วินิจฉัย", "รหัสบ้าน", "หมู่บ้าน" };
                vm.Rows = new() {
                    new(){"1","นายสมชาย ใจดี","ความดันโลหิตสูง","05/02/2569","001","บ้านหนองไผ่"},
                    new(){"2","นางสมพร ใจดี","เบาหวาน","17/04/2569","001","บ้านหนองไผ่"},
                    new(){"3","นายประเสริฐ ใจบุญ","ข้อเข่าเสื่อม","22/06/2569","002","บ้านดอน"}
                };
                break;
            case "Income":
                vm.Columns = new() { "รหัสสมาชิก", "ชื่อสมาชิก", "อาชีพหลัก", "รายได้รวม", "เงินออม" };
                vm.Rows = new() {
                    new(){"1","นายสมชาย ใจดี","เกษตรกร","120000.00","15000.00"},
                    new(){"2","นางสมพร ใจดี","ค้าขาย","72000.00","8000.00"},
                    new(){"5","นายประเสริฐ ใจบุญ","เกษตรกร","56000.00","2500.00"}
                };
                break;
            case "Welfare":
                vm.Columns = new() { "รหัสสมาชิก", "ชื่อสมาชิก", "ประเภทสวัสดิการ", "สถานะบัตร", "เพิ่มภายหลัง", "อัปเดตล่าสุด" };
                vm.Rows = new() {
                    new(){"1","นายสมชาย ใจดี","บัตรสวัสดิการแห่งรัฐ","มีบัตร","ไม่ใช่","20/10/2569"},
                    new(){"2","นางสมพร ใจดี","บัตรสวัสดิการแห่งรัฐ","มีบัตร","ใช่","25/09/2569"}
                };
                break;
            case "Vulnerable":
                vm.Columns = new() { "รหัสสมาชิก", "ชื่อสมาชิก", "ประเภทกลุ่มเปราะบาง", "วันที่ขึ้นทะเบียน", "หมายเหตุ" };
                vm.Rows = new() {
                    new(){"5","นายประเสริฐ ใจบุญ","ผู้สูงอายุ","12/01/2569","ต้องติดตามเยี่ยมบ้าน"},
                    new(){"6","นางสายใจ ใจตรง","ผู้พิการ","08/03/2569","ตัวอย่างข้อมูลเท่านั้น"}
                };
                break;
            case "Repair":
                vm.Columns = new() { "ประเภทคำร้อง", "รหัสบ้าน", "รายการ", "สถานะ", "วันที่", "งบประมาณ" };
                vm.Rows = new() {
                    new(){"ซ่อมบ้าน","001","ซ่อมหลังคารั่ว","ดำเนินการเสร็จสิ้น","15/09/2569","18000.00"},
                    new(){"อุปกรณ์การแพทย์","001","นายสมชาย ใจดี: รถเข็นผู้ป่วย","ส่งมอบแล้ว","02/10/2569","-"},
                    new(){"ซ่อมบ้าน","001","ซ่อมแซมห้องน้ำ","กำลังดำเนินการ","18/10/2569","8500.00"}
                };
                break;
            case "Users":
                vm.Columns = new() { "รหัสผู้ใช้", "ชื่อผู้ใช้", "ชื่อ-นามสกุล", "บทบาท", "สถานะ" };
                vm.Rows = new() {
                    new(){"1","admin.demo","ผู้ดูแลระบบตัวอย่าง","admin","ใช้งาน"},
                    new(){"2","staff.demo","เจ้าหน้าที่ตัวอย่าง","staff","ใช้งาน"}
                };
                break;
            case "Settings":
                vm.Columns = new() { "ข้อมูลอ้างอิง", "จำนวนรายการ" };
                vm.Rows = new() {
                    new(){"จังหวัด","1"}, new(){"อำเภอ","18"}, new(){"ตำบล","125"}, new(){"หมู่บ้าน","245"},
                    new(){"คำนำหน้าชื่อ","8"}, new(){"เพศ","3"}, new(){"อาชีพ","55"}, new(){"โรค","420"}
                };
                break;
            case "Reports":
                vm.Reports = ReportDefinitions.Select(r => new ReportDefinition { ViewName=r.ViewName, Title=r.Title, Description=r.Description, RowCount=120 }).ToList();
                if (!string.IsNullOrWhiteSpace(viewName))
                {
                    var rep=ReportDefinitions.FirstOrDefault(x=>x.ViewName==viewName);
                    vm.Title=rep?.Title ?? "รายงานตัวอย่าง"; vm.SelectedView=viewName;
                    vm.Columns=new(){"พื้นที่","จำนวนครัวเรือน","จำนวนสมาชิก","หมายเหตุ"};
                    vm.Rows=new(){new(){"อำเภอเมือง","1,240","3,460","ข้อมูลจำลอง"},new(){"อำเภอวานรนิวาส","980","2,755","ข้อมูลจำลอง"},new(){"อำเภอสว่างแดนดิน","1,115","3,020","ข้อมูลจำลอง"}};
                }
                else
                {
                    vm.Columns=new(){"ชื่อรายงาน","View","จำนวนแถว"};
                    vm.Rows=vm.Reports.Select(r=>new List<string>{r.Title,r.ViewName,"120 (ตัวอย่าง)"}).ToList();
                }
                break;
            default:
                vm.InformationNote = "หน้าแก้ไขหรือเพิ่มข้อมูลนี้ยังไม่ได้เปิดใช้การบันทึกจริงในเวอร์ชันนี้";
                break;
        }
        vm.Search = search;
        if (!string.IsNullOrWhiteSpace(search) && vm.Rows.Count > 0)
            vm.Rows = vm.Rows.Where(row => string.Join(" ", row).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(search) && vm.Reports.Count > 0)
            vm.Reports = vm.Reports.Where(report => $"{report.Title} {report.Description} {report.ViewName}".Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        return vm;
    }
}
