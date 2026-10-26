using MySqlConnector;
using Logbook.Data;
using Logbook.Models;

namespace Logbook.Services;

public sealed class HouseholdService(MySqlConnectionFactory factory)
{
    public async Task<List<HouseholdRow>> GetAsync(string? search = null, string? amphur = null, string? tumbol = null)
    {
        await using var cn = factory.Create();
        await cn.OpenAsync();
        const string sql = """
            SELECT h.house_id, h.house_code, h.survey_year,
                   COALESCE(v.name_th, '-') AS village_name,
                   COALESCE(t.name_th, '-') AS tumbol_name,
                   COALESCE(a.name_th, '-') AS amphur_name,
                   (SELECT COUNT(*) FROM t_house_member hm WHERE hm.house_id = h.house_id AND hm.is_active = 1) AS member_count
            FROM t_house h
            LEFT JOIN m_village v ON v.village_id = h.village_id
            LEFT JOIN m_tumbol t ON t.tumbol_id = v.tumbol_id
            LEFT JOIN m_amphur a ON a.amphur_id = t.amphur_id
            WHERE h.is_active = 1
              AND (@search IS NULL OR h.house_code LIKE CONCAT('%', @search, '%') OR h.house_no LIKE CONCAT('%', @search, '%') OR v.name_th LIKE CONCAT('%', @search, '%'))
              AND (@amphur IS NULL OR a.name_th = @amphur)
              AND (@tumbol IS NULL OR t.name_th = @tumbol)
            ORDER BY h.house_id DESC LIMIT 100
            """;
        await using var cmd = new MySqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());
        cmd.Parameters.AddWithValue("@amphur", string.IsNullOrWhiteSpace(amphur) ? DBNull.Value : amphur.Trim());
        cmd.Parameters.AddWithValue("@tumbol", string.IsNullOrWhiteSpace(tumbol) ? DBNull.Value : tumbol.Trim());
        await using var r = await cmd.ExecuteReaderAsync();
        var rows = new List<HouseholdRow>();
        while (await r.ReadAsync()) rows.Add(new HouseholdRow
        {
            HouseId = Convert.ToInt64(r["house_id"]),
            HouseCode = Convert.ToString(r["house_code"]) ?? "",
            SurveyYear = Convert.ToInt32(r["survey_year"]),
            Village = Convert.ToString(r["village_name"]) ?? "-",
            Tumbol = Convert.ToString(r["tumbol_name"]) ?? "-",
            Amphur = Convert.ToString(r["amphur_name"]) ?? "-",
            MemberCount = Convert.ToInt32(r["member_count"])
        });
        return rows;
    }

    public async Task<HouseholdDetailsViewModel?> GetDetailsAsync(long id)
    {
        await using var cn = factory.Create();
        await cn.OpenAsync();
        const string sql = """
            SELECT h.house_id, h.house_code, h.survey_year, COALESCE(h.house_no, '-') AS house_no,
                   COALESCE(h.house_nearby, '') AS nearby,
                   COALESCE(v.name_th, '-') AS village_name,
                   COALESCE(t.name_th, '-') AS tumbol_name,
                   COALESCE(a.name_th, '-') AS amphur_name,
                   COALESCE(p.name_th, '-') AS province_name,
                   (SELECT COUNT(*) FROM t_house_member hm WHERE hm.house_id = h.house_id AND hm.is_active = 1) AS member_count
            FROM t_house h
            LEFT JOIN m_village v ON v.village_id = h.village_id
            LEFT JOIN m_tumbol t ON t.tumbol_id = v.tumbol_id
            LEFT JOIN m_amphur a ON a.amphur_id = t.amphur_id
            LEFT JOIN m_province p ON p.province_id = a.province_id
            WHERE h.house_id = @id AND h.is_active = 1 LIMIT 1
            """;
        await using var cmd = new MySqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        var vm = new HouseholdDetailsViewModel
        {
            HouseId = Convert.ToInt64(r["house_id"]), HouseCode = Convert.ToString(r["house_code"]) ?? "",
            SurveyYear = Convert.ToInt32(r["survey_year"]), HouseNo = Convert.ToString(r["house_no"]) ?? "-",
            Location = Convert.ToString(r["nearby"]) ?? "", Village = Convert.ToString(r["village_name"]) ?? "-",
            Tumbol = Convert.ToString(r["tumbol_name"]) ?? "-", Amphur = Convert.ToString(r["amphur_name"]) ?? "-",
            Province = Convert.ToString(r["province_name"]) ?? "-", MemberCount = Convert.ToInt32(r["member_count"])
        };
        await r.CloseAsync();
        const string memberSql = """
            SELECT m.member_id,
                   TRIM(CONCAT(COALESCE(px.name, ''), COALESCE(m.first_name, ''), ' ', COALESCE(m.last_name, ''))) AS full_name,
                   m.age, COALESCE(g.name, '-') AS gender_name, COALESCE(o.name, '-') AS occupation_name
            FROM t_house_member m
            LEFT JOIN m_prefix px ON px.prefix_id = m.prefix_id
            LEFT JOIN m_gender g ON g.gender_id = m.gender_id
            LEFT JOIN m_occupation o ON o.occupation_id = m.occupation_id
            WHERE m.house_id = @id AND m.is_active = 1 ORDER BY m.member_id LIMIT 100
            """;
        await using var memberCmd = new MySqlCommand(memberSql, cn);
        memberCmd.Parameters.AddWithValue("@id", id);
        await using var mr = await memberCmd.ExecuteReaderAsync();
        while (await mr.ReadAsync()) vm.Members.Add(new HouseholdMemberRow
        {
            MemberId = Convert.ToInt64(mr["member_id"]), FullName = Convert.ToString(mr["full_name"]) ?? "",
            Age = mr["age"] is DBNull ? null : Convert.ToInt32(mr["age"]),
            Gender = Convert.ToString(mr["gender_name"]) ?? "-", Occupation = Convert.ToString(mr["occupation_name"]) ?? "-"
        });
        return vm;
    }

    public static List<HouseholdRow> SampleRows() => new()
    {
        new(){HouseId=1,HouseCode="001",SurveyYear=2569,Village="บ้านหนองไผ่",Tumbol="นาอ้อ",Amphur="เมือง",MemberCount=4},
        new(){HouseId=2,HouseCode="002",SurveyYear=2569,Village="บ้านดอน",Tumbol="นาอ้อ",Amphur="เมือง",MemberCount=3},
        new(){HouseId=3,HouseCode="003",SurveyYear=2569,Village="บ้านโคก",Tumbol="โพนสูง",Amphur="วานรนิวาส",MemberCount=5},
        new(){HouseId=4,HouseCode="004",SurveyYear=2569,Village="บ้านกลาง",Tumbol="กุดเรือคำ",Amphur="วานรนิวาส",MemberCount=2},
        new(){HouseId=5,HouseCode="005",SurveyYear=2569,Village="บ้านเหนือ",Tumbol="หนองสนม",Amphur="สว่างแดนดิน",MemberCount=3}
    };

    public static HouseholdDetailsViewModel SampleDetails(long id) => new()
    {
        HouseId=id,HouseCode=id.ToString("000"),SurveyYear=2569,HouseNo="12/3",Location="ใกล้วัดประจำหมู่บ้าน",
        Village="บ้านหนองไผ่",Tumbol="นาอ้อ",Amphur="เมือง",Province="สกลนคร",MemberCount=4,IsSampleData=true,
        Members=new(){
          new(){MemberId=1,FullName="นายสมชาย ใจดี",Age=45,Gender="ชาย",Occupation="เกษตรกร"},
          new(){MemberId=2,FullName="นางสมพร ใจดี",Age=42,Gender="หญิง",Occupation="แม่บ้าน"},
          new(){MemberId=3,FullName="เด็กชายสมปอง ใจดี",Age=12,Gender="ชาย",Occupation="นักเรียน"},
          new(){MemberId=4,FullName="เด็กหญิงสมฤดี ใจดี",Age=9,Gender="หญิง",Occupation="นักเรียน"}
        }
    };
}
