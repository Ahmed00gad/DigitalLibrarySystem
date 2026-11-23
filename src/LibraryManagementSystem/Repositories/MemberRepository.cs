using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.Data.Sqlite;

namespace LibraryManagementSystem.Repositories;

public class MemberRepository : IMemberRepository
{
    private readonly DatabaseContext _context;

    public MemberRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<Member?> GetByIdAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Members WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapMember(reader);
        }
        return null;
    }

    public async Task<Member?> GetByEmailAsync(string email)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Members WHERE LOWER(Email) = LOWER(@e);";
        cmd.Parameters.AddWithValue("@e", email.Trim());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapMember(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<Member>> GetAllAsync()
    {
        var list = new List<Member>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Members ORDER BY FullName ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapMember(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Member>> SearchAsync(string? query, MembershipType? type = null, bool activeOnly = false)
    {
        var list = new List<Member>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();

        var sql = "SELECT * FROM Members WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += " AND (FullName LIKE @q OR Email LIKE @q OR PhoneNumber LIKE @q)";
            cmd.Parameters.AddWithValue("@q", $"%{query.Trim()}%");
        }

        if (type.HasValue)
        {
            sql += " AND MembershipType = @t";
            cmd.Parameters.AddWithValue("@t", type.Value.ToString());
        }

        if (activeOnly)
        {
            sql += " AND IsActive = 1";
        }

        sql += " ORDER BY FullName ASC;";
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapMember(reader));
        }
        return list;
    }

    public async Task<int> GetActiveLoansCountAsync(int memberId)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Loans WHERE MemberId = @mid AND IsReturned = 0;";
        cmd.Parameters.AddWithValue("@mid", memberId);

        var count = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(count);
    }

    public async Task<int> AddAsync(Member member)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO Members (FullName, Email, PhoneNumber, MembershipType, MembershipDate, MaxBorrowLimit, IsActive, CreatedAt)
        VALUES (@name, @email, @phone, @type, @date, @limit, @active, @created);
        SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@name", member.FullName.Trim());
        cmd.Parameters.AddWithValue("@email", member.Email.Trim());
        cmd.Parameters.AddWithValue("@phone", member.PhoneNumber?.Trim() ?? string.Empty);
        cmd.Parameters.AddWithValue("@type", member.MembershipType.ToString());
        cmd.Parameters.AddWithValue("@date", member.MembershipDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@limit", member.MaxBorrowLimit);
        cmd.Parameters.AddWithValue("@active", member.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));

        var result = await cmd.ExecuteScalarAsync();
        var id = Convert.ToInt32(result);
        member.Id = id;
        return id;
    }

    public async Task<bool> UpdateAsync(Member member)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Members
        SET FullName = @name, Email = @email, PhoneNumber = @phone, MembershipType = @type,
            MaxBorrowLimit = @limit, IsActive = @active, UpdatedAt = @updated
        WHERE Id = @id;";

        cmd.Parameters.AddWithValue("@id", member.Id);
        cmd.Parameters.AddWithValue("@name", member.FullName.Trim());
        cmd.Parameters.AddWithValue("@email", member.Email.Trim());
        cmd.Parameters.AddWithValue("@phone", member.PhoneNumber?.Trim() ?? string.Empty);
        cmd.Parameters.AddWithValue("@type", member.MembershipType.ToString());
        cmd.Parameters.AddWithValue("@limit", member.MaxBorrowLimit);
        cmd.Parameters.AddWithValue("@active", member.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Members WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    private static Member MapMember(SqliteDataReader reader)
    {
        var typeStr = reader.GetString(reader.GetOrdinal("MembershipType"));
        Enum.TryParse<MembershipType>(typeStr, out var mType);

        return new Member
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            FullName = reader.GetString(reader.GetOrdinal("FullName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? "" : reader.GetString(reader.GetOrdinal("PhoneNumber")),
            MembershipType = mType,
            MembershipDate = DateTime.TryParse(reader.GetString(reader.GetOrdinal("MembershipDate")), out var md) ? md : DateTime.Today,
            MaxBorrowLimit = reader.GetInt32(reader.GetOrdinal("MaxBorrowLimit")),
            IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
            CreatedAt = DateTime.TryParse(reader.GetString(reader.GetOrdinal("CreatedAt")), out var c) ? c : DateTime.UtcNow,
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : (DateTime.TryParse(reader.GetString(reader.GetOrdinal("UpdatedAt")), out var u) ? u : null)
        };
    }
}
