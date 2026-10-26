using MySqlConnector;
namespace Logbook.Data;

public sealed class MySqlConnectionFactory(IConfiguration configuration)
{
    public MySqlConnection Create()
    {
        var cs = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ยังไม่ได้ตั้งค่า ConnectionStrings:DefaultConnection ใน appsettings.json");
        return new MySqlConnection(cs);
    }
}
