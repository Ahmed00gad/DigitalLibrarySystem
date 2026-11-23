using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.Data.Sqlite;

namespace LibraryManagementSystem.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DatabaseContext _context;

    public UserRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE LOWER(Username) = LOWER(@u);";
        cmd.Parameters.AddWithValue("@u", username.Trim());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Users WHERE LOWER(Username) = LOWER(@u);";
        cmd.Parameters.AddWithValue("@u", username.Trim());

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync()
    {
        var list = new List<User>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users ORDER BY Username ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapUser(reader));
        }
        return list;
    }

    public async Task<int> AddAsync(User user)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt, UpdatedAt)
            VALUES (@u, @p, @name, @role, @active, @created, @updated);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@u", user.Username.Trim());
        cmd.Parameters.AddWithValue("@p", user.PasswordHash);
        cmd.Parameters.AddWithValue("@name", user.FullName.Trim());
        cmd.Parameters.AddWithValue("@role", user.Role);
        cmd.Parameters.AddWithValue("@active", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@updated", DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        user.Id = Convert.ToInt32(id);
        return user.Id;
    }

    public async Task<bool> UpdateAsync(User user)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Users
            SET Username = @u,
                PasswordHash = @p,
                FullName = @name,
                Role = @role,
                IsActive = @active,
                UpdatedAt = @updated
            WHERE Id = @id;";

        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@u", user.Username.Trim());
        cmd.Parameters.AddWithValue("@p", user.PasswordHash);
        cmd.Parameters.AddWithValue("@name", user.FullName.Trim());
        cmd.Parameters.AddWithValue("@role", user.Role);
        cmd.Parameters.AddWithValue("@active", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Users WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    private static User MapUser(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            FullName = reader.GetString(reader.GetOrdinal("FullName")),
            Role = reader.GetString(reader.GetOrdinal("Role")),
            IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
            CreatedAt = DateTime.TryParse(reader.GetString(reader.GetOrdinal("CreatedAt")), out var ca) ? ca : DateTime.UtcNow,
            UpdatedAt = !reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) && DateTime.TryParse(reader.GetString(reader.GetOrdinal("UpdatedAt")), out var ua) ? ua : null
        };
    }
}
