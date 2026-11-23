using LibraryManagementSystem.Models;
using Microsoft.Data.Sqlite;

namespace LibraryManagementSystem.Data;

public class DatabaseInitializer
{
    private readonly DatabaseContext _context;

    public DatabaseInitializer(DatabaseContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync()
    {
        using var connection = await _context.OpenConnectionAsync();

        // Enable foreign keys
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
            await pragmaCmd.ExecuteNonQueryAsync();
        }

        // Create Tables
        var schemaSql = @"
        CREATE TABLE IF NOT EXISTS Books (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Author TEXT NOT NULL,
            ISBN TEXT UNIQUE,
            Category TEXT NOT NULL,
            PublicationYear INTEGER NOT NULL,
            TotalCopies INTEGER NOT NULL,
            CopiesAvailable INTEGER NOT NULL,
            LocationRack TEXT,
            Description TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Members (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FullName TEXT NOT NULL,
            Email TEXT NOT NULL UNIQUE,
            PhoneNumber TEXT,
            MembershipType TEXT NOT NULL,
            MembershipDate TEXT NOT NULL,
            MaxBorrowLimit INTEGER NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Loans (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            BookId INTEGER NOT NULL,
            MemberId INTEGER NOT NULL,
            BorrowDate TEXT NOT NULL,
            DueDate TEXT NOT NULL,
            ReturnDate TEXT,
            FineAmount REAL NOT NULL DEFAULT 0.0,
            FinePaid INTEGER NOT NULL DEFAULT 0,
            IsReturned INTEGER NOT NULL DEFAULT 0,
            Notes TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT,
            FOREIGN KEY (BookId) REFERENCES Books(Id) ON DELETE RESTRICT,
            FOREIGN KEY (MemberId) REFERENCES Members(Id) ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL,
            FullName TEXT NOT NULL,
            Role TEXT NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT
        );

        CREATE INDEX IF NOT EXISTS IX_Books_ISBN ON Books(ISBN);
        CREATE INDEX IF NOT EXISTS IX_Books_Category ON Books(Category);
        CREATE INDEX IF NOT EXISTS IX_Members_Email ON Members(Email);
        CREATE INDEX IF NOT EXISTS IX_Loans_BookId ON Loans(BookId);
        CREATE INDEX IF NOT EXISTS IX_Loans_MemberId ON Loans(MemberId);
        CREATE INDEX IF NOT EXISTS IX_Loans_Status ON Loans(IsReturned, DueDate);
        CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
        ";

        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = schemaSql;
            await createCmd.ExecuteNonQueryAsync();
        }

        // Upgrade databases created with the old NOT NULL ISBN column.
        // This preserves IDs and every Loans.BookId reference.
        await UpgradeOptionalIsbnAsync(connection);
        await SeedDefaultDataAsync(connection);
        await SeedUsersAsync(connection);
    }


    // SQLite cannot remove NOT NULL in-place; rebuild only Books in one transaction.
    // Take a backup of library.db before running a migration on real data.
    private static async Task UpgradeOptionalIsbnAsync(SqliteConnection connection)
    {
        bool isbnRequired = false;
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(Books);";
            using var reader = await info.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "ISBN")
                {
                    isbnRequired = reader.GetInt32(3) != 0;
                    break;
                }
            }
        }
        if (!isbnRequired) return;

        // Must disable FK checks BEFORE beginning the migration transaction.
        using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys = OFF;";
            await foreignKeys.ExecuteNonQueryAsync();
        }

        try
        {
            using var transaction = connection.BeginTransaction();
            using (var migrate = connection.CreateCommand())
            {
                migrate.Transaction = transaction;
                migrate.CommandText = @"
                    CREATE TABLE Books_OptionalIsbn_New (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Author TEXT NOT NULL,
                        ISBN TEXT UNIQUE,
                        Category TEXT NOT NULL,
                        PublicationYear INTEGER NOT NULL,
                        TotalCopies INTEGER NOT NULL,
                        CopiesAvailable INTEGER NOT NULL,
                        LocationRack TEXT,
                        Description TEXT,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT
                    );
                    INSERT INTO Books_OptionalIsbn_New
                        (Id, Title, Author, ISBN, Category, PublicationYear,
                         TotalCopies, CopiesAvailable, LocationRack, Description, CreatedAt, UpdatedAt)
                    SELECT Id, Title, Author, NULLIF(TRIM(ISBN), ''), Category,
                           PublicationYear, TotalCopies, CopiesAvailable,
                           LocationRack, Description, CreatedAt, UpdatedAt
                    FROM Books;
                    DROP TABLE Books;
                    ALTER TABLE Books_OptionalIsbn_New RENAME TO Books;
                    CREATE INDEX IF NOT EXISTS IX_Books_ISBN ON Books(ISBN);
                    CREATE INDEX IF NOT EXISTS IX_Books_Category ON Books(Category);";
                await migrate.ExecuteNonQueryAsync();
            }
            using (var verify = connection.CreateCommand())
            {
                verify.Transaction = transaction;
                verify.CommandText = "PRAGMA foreign_key_check;";
                using var reader = await verify.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    throw new InvalidOperationException("Database migration cancelled: foreign key check failed.");
            }
            transaction.Commit();
        }
        finally
        {
            using var restore = connection.CreateCommand();
            restore.CommandText = "PRAGMA foreign_keys = ON;";
            await restore.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedDefaultDataAsync(SqliteConnection connection)
    {
        // Check if books exist
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Books;";
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        if (count > 0) return; // Already seeded

        using var transaction = connection.BeginTransaction();
        try
        {
            // Seed Books
            var books = new[]
            {
                ("Clean Code: A Handbook of Agile Software Craftsmanship", "Robert C. Martin", "978-0132350884", "Computer Science", 2008, 5, 4, "CS-101", "A code of conduct for professional programmers."),
                ("Design Patterns: Elements of Reusable Object-Oriented Software", "Erich Gamma et al.", "978-0201633610", "Computer Science", 1994, 3, 2, "CS-102", "Foundational patterns for software engineering."),
                ("Introduction to Algorithms (CLRS)", "Thomas H. Cormen", "978-0262033848", "Computer Science", 2009, 4, 3, "CS-103", "Comprehensive textbook covering modern algorithmic theory."),
                ("Artificial Intelligence: A Modern Approach", "Stuart Russell, Peter Norvig", "978-0136042594", "Artificial Intelligence", 2020, 4, 3, "AI-201", "The authoritative reference in AI and machine learning."),
                ("Deep Learning", "Ian Goodfellow, Yoshua Bengio", "978-0262035613", "Artificial Intelligence", 2016, 3, 2, "AI-202", "Comprehensive guide to deep neural networks."),
                ("Machine Learning Yearning", "Andrew Ng", "978-0999999991", "Artificial Intelligence", 2018, 5, 5, "AI-203", "Technical strategy for AI projects in the machine learning era."),
                ("The Pragmatic Programmer", "David Thomas, Andrew Hunt", "978-0135957059", "Software Engineering", 2019, 4, 4, "SE-301", "Your journey to mastery in software craft."),
                ("Refactoring: Improving the Design of Existing Code", "Martin Fowler", "978-0134757599", "Software Engineering", 2018, 3, 3, "SE-302", "Principles of code refactoring and clean architecture."),
                ("C# 12 in a Nutshell", "Joseph Albahari", "978-1098147440", "Programming", 2023, 6, 5, "PRG-401", "The definitive desktop reference for C# developers."),
                ("To Kill a Mockingbird", "Harper Lee", "978-0061120084", "Literature", 1960, 4, 3, "LIT-501", "Classic Pulitzer prize-winning novel of warmth and humor."),
                ("1984", "George Orwell", "978-0451524935", "Literature", 1949, 5, 4, "LIT-502", "A startling and haunting dystopian vision."),
                ("A Brief History of Time", "Stephen Hawking", "978-0553380163", "Science", 1988, 4, 4, "SCI-601", "Landmark volume in science writing from the Big Bang to black holes."),
                ("Sapiens: A Brief History of Humankind", "Yuval Noah Harari", "978-0062316097", "History", 2014, 5, 4, "HIS-701", "How an undistinguished ape became the ruler of planet Earth."),
                ("Meditations", "Marcus Aurelius", "978-0140449334", "Philosophy", 2006, 3, 3, "PHI-801", "Timeless personal writings of the Roman Emperor."),
                ("Linear Algebra and Its Applications", "David C. Lay", "978-0321982384", "Mathematics", 2015, 4, 3, "MTH-901", "Fundamental concepts of vectors, matrices, and linear spaces.")
            };

            foreach (var b in books)
            {
                using var insertBook = connection.CreateCommand();
                insertBook.Transaction = transaction;
                insertBook.CommandText = @"
                INSERT INTO Books (Title, Author, ISBN, Category, PublicationYear, TotalCopies, CopiesAvailable, LocationRack, Description, CreatedAt)
                VALUES (@t, @a, @i, @c, @y, @tot, @avail, @loc, @d, @created);";
                insertBook.Parameters.AddWithValue("@t", b.Item1);
                insertBook.Parameters.AddWithValue("@a", b.Item2);
                insertBook.Parameters.AddWithValue("@i", b.Item3);
                insertBook.Parameters.AddWithValue("@c", b.Item4);
                insertBook.Parameters.AddWithValue("@y", b.Item5);
                insertBook.Parameters.AddWithValue("@tot", b.Item6);
                insertBook.Parameters.AddWithValue("@avail", b.Item7);
                insertBook.Parameters.AddWithValue("@loc", b.Item8);
                insertBook.Parameters.AddWithValue("@d", b.Item9);
                insertBook.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                await insertBook.ExecuteNonQueryAsync();
            }

            // Seed Members
            var members = new[]
            {
                ("Ahmed Hassan", "ahmed.hassan@university.edu", "+201012345678", MembershipType.Student, 3),
                ("Dr. Sarah Mohamed", "s.mohamed@faculty.edu", "+201123456789", MembershipType.Faculty, 10),
                ("Omar Khaled", "omar.khaled@gmail.com", "+201234567890", MembershipType.Regular, 5),
                ("Mariam Youssef", "mariam.y@outlook.com", "+201098765432", MembershipType.Premium, 8),
                ("Mahmoud Ali", "mahmoud.ali@tech.com", "+201155566778", MembershipType.Regular, 5)
            };

            foreach (var m in members)
            {
                using var insertMember = connection.CreateCommand();
                insertMember.Transaction = transaction;
                insertMember.CommandText = @"
                INSERT INTO Members (FullName, Email, PhoneNumber, MembershipType, MembershipDate, MaxBorrowLimit, IsActive, CreatedAt)
                VALUES (@name, @email, @phone, @type, @date, @limit, 1, @created);";
                insertMember.Parameters.AddWithValue("@name", m.Item1);
                insertMember.Parameters.AddWithValue("@email", m.Item2);
                insertMember.Parameters.AddWithValue("@phone", m.Item3);
                insertMember.Parameters.AddWithValue("@type", m.Item4.ToString());
                insertMember.Parameters.AddWithValue("@date", DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd"));
                insertMember.Parameters.AddWithValue("@limit", m.Item5);
                insertMember.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                await insertMember.ExecuteNonQueryAsync();
            }

            // Seed Loans (Active, Returned, and Overdue)
            var now = DateTime.Today;
            var loans = new[]
            {
                // Active loan (within due date)
                (1, 1, now.AddDays(-5), now.AddDays(9), (DateTime?)null, 0.0m, false, false, "Standard semester loan"),
                // Overdue loan (due 4 days ago)
                (2, 3, now.AddDays(-18), now.AddDays(-4), (DateTime?)null, 2.00m, false, false, "First overdue reminder notice sent"),
                // Active loan
                (4, 2, now.AddDays(-2), now.AddDays(12), (DateTime?)null, 0.0m, false, false, "Research reference loan"),
                // Returned loan (returned on time)
                (10, 4, now.AddDays(-30), now.AddDays(-16), (DateTime?)now.AddDays(-18), 0.0m, true, true, "Returned in pristine condition"),
                // Returned loan with fine paid
                (11, 1, now.AddDays(-40), now.AddDays(-26), (DateTime?)now.AddDays(-20), 3.00m, true, true, "Returned late, fine fully settled")
            };

            foreach (var l in loans)
            {
                using var insertLoan = connection.CreateCommand();
                insertLoan.Transaction = transaction;
                insertLoan.CommandText = @"
                INSERT INTO Loans (BookId, MemberId, BorrowDate, DueDate, ReturnDate, FineAmount, FinePaid, IsReturned, Notes, CreatedAt)
                VALUES (@bid, @mid, @bdate, @ddate, @rdate, @fine, @paid, @ret, @notes, @created);";
                insertLoan.Parameters.AddWithValue("@bid", l.Item1);
                insertLoan.Parameters.AddWithValue("@mid", l.Item2);
                insertLoan.Parameters.AddWithValue("@bdate", l.Item3.ToString("yyyy-MM-dd"));
                insertLoan.Parameters.AddWithValue("@ddate", l.Item4.ToString("yyyy-MM-dd"));
                insertLoan.Parameters.AddWithValue("@rdate", l.Item5.HasValue ? l.Item5.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                insertLoan.Parameters.AddWithValue("@fine", (double)l.Item6);
                insertLoan.Parameters.AddWithValue("@paid", l.Item7 ? 1 : 0);
                insertLoan.Parameters.AddWithValue("@ret", l.Item8 ? 1 : 0);
                insertLoan.Parameters.AddWithValue("@notes", l.Item9);
                insertLoan.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                await insertLoan.ExecuteNonQueryAsync();
            }

            // Seed Default Settings
            var settings = new[]
            {
                ("DefaultDailyFineRate", "0.50"),
                ("GracePeriodDays", "1"),
                ("DefaultBorrowDurationDays", "14"),
                ("ServerPort", "8888")
            };

            foreach (var s in settings)
            {
                using var insertSetting = connection.CreateCommand();
                insertSetting.Transaction = transaction;
                insertSetting.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k, @v);";
                insertSetting.Parameters.AddWithValue("@k", s.Item1);
                insertSetting.Parameters.AddWithValue("@v", s.Item2);
                await insertSetting.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task SeedUsersAsync(SqliteConnection connection)
    {
        // Remove legacy accounts if present
        using (var cleanCmd = connection.CreateCommand())
        {
            cleanCmd.CommandText = "DELETE FROM Users WHERE LOWER(Username) IN ('omar', 'librarian');";
            await cleanCmd.ExecuteNonQueryAsync();
        }

        static string Hash(string plain)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var b = System.Text.Encoding.UTF8.GetBytes(plain);
            return Convert.ToHexString(sha.ComputeHash(b)).ToLowerInvariant();
        }

        // Ensure 'admin' exists
        using (var checkAdmin = connection.CreateCommand())
        {
            checkAdmin.CommandText = "SELECT COUNT(1) FROM Users WHERE LOWER(Username) = 'admin';";
            if (Convert.ToInt32(await checkAdmin.ExecuteScalarAsync()) == 0)
            {
                using var insertAdmin = connection.CreateCommand();
                insertAdmin.CommandText = @"
                INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                VALUES ('admin', @p, 'System Administrator', 'Admin', 1, @created);";
                insertAdmin.Parameters.AddWithValue("@p", Hash("admin123"));
                insertAdmin.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                await insertAdmin.ExecuteNonQueryAsync();
            }
        }

        // Ensure 'user' exists with password '123456'
        using (var checkUser = connection.CreateCommand())
        {
            checkUser.CommandText = "SELECT COUNT(1) FROM Users WHERE LOWER(Username) = 'user';";
            if (Convert.ToInt32(await checkUser.ExecuteScalarAsync()) == 0)
            {
                using var insertUser = connection.CreateCommand();
                insertUser.CommandText = @"
                INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                VALUES ('user', @p, 'Library User', 'User', 1, @created);";
                insertUser.Parameters.AddWithValue("@p", Hash("123456"));
                insertUser.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                await insertUser.ExecuteNonQueryAsync();
            }
            else
            {
                using var updatePass = connection.CreateCommand();
                updatePass.CommandText = "UPDATE Users SET PasswordHash = @p WHERE LOWER(Username) = 'user';";
                updatePass.Parameters.AddWithValue("@p", Hash("123456"));
                await updatePass.ExecuteNonQueryAsync();
            }
        }
    }
}
