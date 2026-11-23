using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.Data.Sqlite;

namespace LibraryManagementSystem.Repositories;

public class LoanRepository : ILoanRepository
{
    private readonly DatabaseContext _context;

    public LoanRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<Loan?> GetByIdAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT l.*, b.Title AS BookTitle, b.ISBN AS BookISBN, m.FullName AS MemberName, m.Email AS MemberEmail
        FROM Loans l
        JOIN Books b ON l.BookId = b.Id
        JOIN Members m ON l.MemberId = m.Id
        WHERE l.Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapLoan(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<Loan>> GetAllAsync()
    {
        var list = new List<Loan>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT l.*, b.Title AS BookTitle, b.ISBN AS BookISBN, m.FullName AS MemberName, m.Email AS MemberEmail
        FROM Loans l
        JOIN Books b ON l.BookId = b.Id
        JOIN Members m ON l.MemberId = m.Id
        ORDER BY l.BorrowDate DESC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapLoan(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Loan>> GetActiveLoansAsync()
    {
        var list = new List<Loan>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT l.*, b.Title AS BookTitle, b.ISBN AS BookISBN, m.FullName AS MemberName, m.Email AS MemberEmail
        FROM Loans l
        JOIN Books b ON l.BookId = b.Id
        JOIN Members m ON l.MemberId = m.Id
        WHERE l.IsReturned = 0
        ORDER BY l.DueDate ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapLoan(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Loan>> GetOverdueLoansAsync()
    {
        var list = new List<Loan>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT l.*, b.Title AS BookTitle, b.ISBN AS BookISBN, m.FullName AS MemberName, m.Email AS MemberEmail
        FROM Loans l
        JOIN Books b ON l.BookId = b.Id
        JOIN Members m ON l.MemberId = m.Id
        WHERE l.IsReturned = 0 AND date(l.DueDate) < date('now')
        ORDER BY l.DueDate ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapLoan(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Loan>> GetLoansByMemberIdAsync(int memberId)
    {
        var list = new List<Loan>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT l.*, b.Title AS BookTitle, b.ISBN AS BookISBN, m.FullName AS MemberName, m.Email AS MemberEmail
        FROM Loans l
        JOIN Books b ON l.BookId = b.Id
        JOIN Members m ON l.MemberId = m.Id
        WHERE l.MemberId = @mid
        ORDER BY l.BorrowDate DESC;";
        cmd.Parameters.AddWithValue("@mid", memberId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapLoan(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Loan>> SearchLoansAsync(string? query, LoanStatus? status = null)
    {
        var list = new List<Loan>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();

        var sql = @"
        SELECT l.*, b.Title AS BookTitle, b.ISBN AS BookISBN, m.FullName AS MemberName, m.Email AS MemberEmail
        FROM Loans l
        JOIN Books b ON l.BookId = b.Id
        JOIN Members m ON l.MemberId = m.Id
        WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += " AND (b.Title LIKE @q OR b.ISBN LIKE @q OR m.FullName LIKE @q OR m.Email LIKE @q)";
            cmd.Parameters.AddWithValue("@q", $"%{query.Trim()}%");
        }

        if (status.HasValue)
        {
            if (status.Value == LoanStatus.Active)
                sql += " AND l.IsReturned = 0 AND date(l.DueDate) >= date('now')";
            else if (status.Value == LoanStatus.Overdue)
                sql += " AND l.IsReturned = 0 AND date(l.DueDate) < date('now')";
            else if (status.Value == LoanStatus.Returned)
                sql += " AND l.IsReturned = 1";
        }

        sql += " ORDER BY l.BorrowDate DESC;";
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapLoan(reader));
        }
        return list;
    }

    public async Task<int> AddAsync(Loan loan)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO Loans (BookId, MemberId, BorrowDate, DueDate, ReturnDate, FineAmount, FinePaid, IsReturned, Notes, CreatedAt)
        VALUES (@bid, @mid, @bdate, @ddate, @rdate, @fine, @paid, @ret, @notes, @created);
        SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@bid", loan.BookId);
        cmd.Parameters.AddWithValue("@mid", loan.MemberId);
        cmd.Parameters.AddWithValue("@bdate", loan.BorrowDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@ddate", loan.DueDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@rdate", loan.ReturnDate.HasValue ? loan.ReturnDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fine", (double)loan.FineAmount);
        cmd.Parameters.AddWithValue("@paid", loan.FinePaid ? 1 : 0);
        cmd.Parameters.AddWithValue("@ret", loan.IsReturned ? 1 : 0);
        cmd.Parameters.AddWithValue("@notes", loan.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));

        var result = await cmd.ExecuteScalarAsync();
        var id = Convert.ToInt32(result);
        loan.Id = id;
        return id;
    }

    public async Task<bool> UpdateAsync(Loan loan)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Loans
        SET BookId = @bid, MemberId = @mid, BorrowDate = @bdate, DueDate = @ddate,
            ReturnDate = @rdate, FineAmount = @fine, FinePaid = @paid,
            IsReturned = @ret, Notes = @notes, UpdatedAt = @updated
        WHERE Id = @id;";

        cmd.Parameters.AddWithValue("@id", loan.Id);
        cmd.Parameters.AddWithValue("@bid", loan.BookId);
        cmd.Parameters.AddWithValue("@mid", loan.MemberId);
        cmd.Parameters.AddWithValue("@bdate", loan.BorrowDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@ddate", loan.DueDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@rdate", loan.ReturnDate.HasValue ? loan.ReturnDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fine", (double)loan.FineAmount);
        cmd.Parameters.AddWithValue("@paid", loan.FinePaid ? 1 : 0);
        cmd.Parameters.AddWithValue("@ret", loan.IsReturned ? 1 : 0);
        cmd.Parameters.AddWithValue("@notes", loan.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Loans WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> RecordReturnAsync(int loanId, DateTime returnDate, decimal fineAmount, bool finePaid)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Loans
        SET ReturnDate = @rdate, FineAmount = @fine, FinePaid = @paid, IsReturned = 1, UpdatedAt = @updated
        WHERE Id = @id;";

        cmd.Parameters.AddWithValue("@id", loanId);
        cmd.Parameters.AddWithValue("@rdate", returnDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@fine", (double)fineAmount);
        cmd.Parameters.AddWithValue("@paid", finePaid ? 1 : 0);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UpdateFineAsync(int loanId, decimal fineAmount)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Loans
        SET FineAmount = @fine, UpdatedAt = @updated
        WHERE Id = @id;";

        cmd.Parameters.AddWithValue("@id", loanId);
        cmd.Parameters.AddWithValue("@fine", (double)fineAmount);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<LibraryStats> GetLibraryStatsAsync()
    {
        var stats = new LibraryStats();
        using var connection = await _context.OpenConnectionAsync();

        // Books count & copies
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(TotalCopies), 0), COALESCE(SUM(CopiesAvailable), 0) FROM Books;";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalBooks = reader.GetInt32(0);
                stats.TotalCopies = reader.GetInt32(1);
                stats.AvailableCopies = reader.GetInt32(2);
                stats.BorrowedCopies = stats.TotalCopies - stats.AvailableCopies;
            }
        }

        // Members count
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END), 0) FROM Members;";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalMembers = reader.GetInt32(0);
                stats.ActiveMembers = reader.GetInt32(1);
            }
        }

        // Active and Overdue Loans & Fines
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
            SELECT 
                COALESCE(SUM(CASE WHEN IsReturned = 0 THEN 1 ELSE 0 END), 0) AS ActiveLoans,
                COALESCE(SUM(CASE WHEN IsReturned = 0 AND date(DueDate) < date('now') THEN 1 ELSE 0 END), 0) AS OverdueLoans,
                COALESCE(SUM(FineAmount), 0.0) AS TotalFinesAccrued,
                COALESCE(SUM(CASE WHEN FinePaid = 1 THEN FineAmount ELSE 0.0 END), 0.0) AS TotalFinesCollected
            FROM Loans;";

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.ActiveLoans = reader.GetInt32(0);
                stats.OverdueLoans = reader.GetInt32(1);
                stats.TotalFinesAccrued = (decimal)reader.GetDouble(2);
                stats.TotalFinesCollected = (decimal)reader.GetDouble(3);
            }
        }

        // Books by Category
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Category, COUNT(*) FROM Books GROUP BY Category ORDER BY COUNT(*) DESC;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats.BooksByCategory[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Top 5 Borrowed Books
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
            SELECT b.Title, b.Author, b.Category, COUNT(l.Id) AS BorrowCount
            FROM Loans l
            JOIN Books b ON l.BookId = b.Id
            GROUP BY l.BookId
            ORDER BY BorrowCount DESC
            LIMIT 5;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats.TopBorrowedBooks.Add(new TopBookStat
                {
                    Title = reader.GetString(0),
                    Author = reader.GetString(1),
                    Category = reader.GetString(2),
                    BorrowCount = reader.GetInt32(3)
                });
            }
        }

        // Monthly Loan Trends (last 6 months)
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
            SELECT strftime('%Y-%m', BorrowDate) AS MonthYear, COUNT(*) 
            FROM Loans 
            GROUP BY MonthYear 
            ORDER BY MonthYear ASC 
            LIMIT 6;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var month = reader.GetString(0);
                stats.MonthlyLoanTrends[month] = reader.GetInt32(1);
            }
        }

        return stats;
    }

    private static Loan MapLoan(SqliteDataReader reader)
    {
        return new Loan
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            BookId = reader.GetInt32(reader.GetOrdinal("BookId")),
            MemberId = reader.GetInt32(reader.GetOrdinal("MemberId")),
            BorrowDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("BorrowDate"))),
            DueDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("DueDate"))),
            ReturnDate = reader.IsDBNull(reader.GetOrdinal("ReturnDate")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("ReturnDate"))),
            FineAmount = (decimal)reader.GetDouble(reader.GetOrdinal("FineAmount")),
            FinePaid = reader.GetInt32(reader.GetOrdinal("FinePaid")) == 1,
            IsReturned = reader.GetInt32(reader.GetOrdinal("IsReturned")) == 1,
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? "" : reader.GetString(reader.GetOrdinal("Notes")),
            BookTitle = reader.IsDBNull(reader.GetOrdinal("BookTitle")) ? "" : reader.GetString(reader.GetOrdinal("BookTitle")),
            BookISBN = reader.IsDBNull(reader.GetOrdinal("BookISBN")) ? "" : reader.GetString(reader.GetOrdinal("BookISBN")),
            MemberName = reader.IsDBNull(reader.GetOrdinal("MemberName")) ? "" : reader.GetString(reader.GetOrdinal("MemberName")),
            MemberEmail = reader.IsDBNull(reader.GetOrdinal("MemberEmail")) ? "" : reader.GetString(reader.GetOrdinal("MemberEmail")),
            CreatedAt = DateTime.TryParse(reader.GetString(reader.GetOrdinal("CreatedAt")), out var c) ? c : DateTime.UtcNow,
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : (DateTime.TryParse(reader.GetString(reader.GetOrdinal("UpdatedAt")), out var u) ? u : null)
        };
    }
}
