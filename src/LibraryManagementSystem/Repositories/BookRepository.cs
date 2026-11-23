using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.Data.Sqlite;

namespace LibraryManagementSystem.Repositories;

public class BookRepository : IBookRepository
{
    private readonly DatabaseContext _context;

    public BookRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Books WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapBook(reader);
        }
        return null;
    }

    public async Task<Book?> GetByIsbnAsync(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return null;
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Books WHERE ISBN = @isbn;";
        cmd.Parameters.AddWithValue("@isbn", isbn.Trim());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapBook(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync()
    {
        var list = new List<Book>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Books ORDER BY Title ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapBook(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(string? query, string? category = null, bool availableOnly = false)
    {
        var list = new List<Book>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();

        var sql = "SELECT * FROM Books WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += " AND (Title LIKE @q OR Author LIKE @q OR ISBN LIKE @q OR Description LIKE @q)";
            cmd.Parameters.AddWithValue("@q", $"%{query.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "All Categories")
        {
            sql += " AND Category = @c";
            cmd.Parameters.AddWithValue("@c", category);
        }

        if (availableOnly)
        {
            sql += " AND CopiesAvailable > 0";
        }

        sql += " ORDER BY Title ASC;";
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapBook(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<string>> GetAllCategoriesAsync()
    {
        var categories = new List<string>();
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category FROM Books ORDER BY Category ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(reader.GetString(0));
        }
        return categories;
    }

    public async Task<int> AddAsync(Book book)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO Books (Title, Author, ISBN, Category, PublicationYear, TotalCopies, CopiesAvailable, LocationRack, Description, CreatedAt)
        VALUES (@t, @a, @i, @c, @y, @tot, @avail, @loc, @d, @created);
        SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@t", book.Title);
        cmd.Parameters.AddWithValue("@a", book.Author);
        cmd.Parameters.AddWithValue("@i", string.IsNullOrWhiteSpace(book.ISBN) ? DBNull.Value : book.ISBN.Trim());
        cmd.Parameters.AddWithValue("@c", book.Category);
        cmd.Parameters.AddWithValue("@y", book.PublicationYear);
        cmd.Parameters.AddWithValue("@tot", book.TotalCopies);
        cmd.Parameters.AddWithValue("@avail", book.CopiesAvailable);
        cmd.Parameters.AddWithValue("@loc", book.LocationRack ?? string.Empty);
        cmd.Parameters.AddWithValue("@d", book.Description ?? string.Empty);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));

        var result = await cmd.ExecuteScalarAsync();
        var id = Convert.ToInt32(result);
        book.Id = id;
        return id;
    }

    public async Task<bool> UpdateAsync(Book book)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Books
        SET Title = @t, Author = @a, ISBN = @i, Category = @c, PublicationYear = @y,
            TotalCopies = @tot, CopiesAvailable = @avail, LocationRack = @loc,
            Description = @d, UpdatedAt = @updated
        WHERE Id = @id;";

        cmd.Parameters.AddWithValue("@id", book.Id);
        cmd.Parameters.AddWithValue("@t", book.Title);
        cmd.Parameters.AddWithValue("@a", book.Author);
        cmd.Parameters.AddWithValue("@i", string.IsNullOrWhiteSpace(book.ISBN) ? DBNull.Value : book.ISBN.Trim());
        cmd.Parameters.AddWithValue("@c", book.Category);
        cmd.Parameters.AddWithValue("@y", book.PublicationYear);
        cmd.Parameters.AddWithValue("@tot", book.TotalCopies);
        cmd.Parameters.AddWithValue("@avail", book.CopiesAvailable);
        cmd.Parameters.AddWithValue("@loc", book.LocationRack ?? string.Empty);
        cmd.Parameters.AddWithValue("@d", book.Description ?? string.Empty);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Books WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> AdjustCopiesAvailableAsync(int bookId, int delta)
    {
        using var connection = await _context.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Books
        SET CopiesAvailable = CopiesAvailable + @delta, UpdatedAt = @updated
        WHERE Id = @id AND CopiesAvailable + @delta >= 0 AND CopiesAvailable + @delta <= TotalCopies;";

        cmd.Parameters.AddWithValue("@id", bookId);
        cmd.Parameters.AddWithValue("@delta", delta);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    private static Book MapBook(SqliteDataReader reader)
    {
        return new Book
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Author = reader.GetString(reader.GetOrdinal("Author")),
            ISBN = reader.IsDBNull(reader.GetOrdinal("ISBN")) ? string.Empty : reader.GetString(reader.GetOrdinal("ISBN")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            PublicationYear = reader.GetInt32(reader.GetOrdinal("PublicationYear")),
            TotalCopies = reader.GetInt32(reader.GetOrdinal("TotalCopies")),
            CopiesAvailable = reader.GetInt32(reader.GetOrdinal("CopiesAvailable")),
            LocationRack = reader.IsDBNull(reader.GetOrdinal("LocationRack")) ? "" : reader.GetString(reader.GetOrdinal("LocationRack")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
            CreatedAt = DateTime.TryParse(reader.GetString(reader.GetOrdinal("CreatedAt")), out var c) ? c : DateTime.UtcNow,
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : (DateTime.TryParse(reader.GetString(reader.GetOrdinal("UpdatedAt")), out var u) ? u : null)
        };
    }
}
