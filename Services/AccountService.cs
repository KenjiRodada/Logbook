using MySqlConnector;
using Logbook.Data;

namespace Logbook.Services;

public sealed class AccountService(MySqlConnectionFactory factory)
{
    public async Task<LoginUser?> ValidateAsync(string username, string password)
    {
        await using var cn = factory.Create();
        await cn.OpenAsync();
        const string sql = """
            SELECT user_id, username, password_hash, full_name, role, is_active
            FROM web_users WHERE username = @username LIMIT 1
            """;
        await using var cmd = new MySqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@username", username);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || Convert.ToInt32(reader["is_active"]) != 1) return null;
        var hash = Convert.ToString(reader["password_hash"]) ?? "";
        if (string.IsNullOrWhiteSpace(hash) || !BCrypt.Net.BCrypt.Verify(password, hash)) return null;
        return new LoginUser
        {
            UserId = Convert.ToInt32(reader["user_id"]),
            Username = Convert.ToString(reader["username"]) ?? "",
            FullName = Convert.ToString(reader["full_name"]) ?? "",
            Role = Convert.ToString(reader["role"]) ?? "staff"
        };
    }
}

public sealed class LoginUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "staff";
}
