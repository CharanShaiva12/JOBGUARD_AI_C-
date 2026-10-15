using System.Data;
using Microsoft.Data.SqlClient;
using JobGuard.Models;

namespace JobGuard.Data;

public interface IUserRepository
{
    Task<int> RegisterUserAsync(User user);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(int userId);
    Task<bool> UpdateUserStatusAsync(int userId, bool isActive);
    Task<int> GetTotalUsersCountAsync();
    Task<List<User>> GetAllUsersAsync(int max = 100);
}

public class UserRepository : IUserRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public UserRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> RegisterUserAsync(User user)
    {
        const string sql = @"
            INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt)
            OUTPUT INSERTED.UserId
            VALUES (@FullName, @Email, @PasswordHash, @Role, @IsActive, @CreatedAt);";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150).Value = user.FullName;
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = user.Email.Trim().ToLowerInvariant();
        cmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = user.PasswordHash;
        cmd.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role;
        cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = user.IsActive;
        cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt;

        await conn.OpenAsync();
        var insertedId = (int?)await cmd.ExecuteScalarAsync();
        return insertedId ?? 0;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        const string sql = @"
            SELECT UserId, FullName, Email, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt
            FROM Users
            WHERE Email = @Email;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email.Trim().ToLowerInvariant();

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        return null;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        const string sql = @"
            SELECT UserId, FullName, Email, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt
            FROM Users
            WHERE UserId = @UserId;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        return null;
    }

    public async Task<bool> UpdateUserStatusAsync(int userId, bool isActive)
    {
        const string sql = @"
            UPDATE Users
            SET IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHERE UserId = @UserId;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;

        await conn.OpenAsync();
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> GetTotalUsersCountAsync()
    {
        const string sql = "SELECT COUNT(1) FROM Users;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);

        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<User>> GetAllUsersAsync(int max = 100)
    {
        var users = new List<User>();
        const string sql = @"
            SELECT TOP (@Max) UserId, FullName, Email, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt
            FROM Users
            ORDER BY CreatedAt DESC;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Max", SqlDbType.Int).Value = max;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(MapUser(reader));
        }

        return users;
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            FullName = reader.GetString(reader.GetOrdinal("FullName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            Role = reader.GetString(reader.GetOrdinal("Role")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }
}
