using LibraryManagementSystem.Data;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class BookCrudTests
{
    private string _dbFile = null!;
    private DatabaseContext _context = null!;
    private BookRepository _bookRepo = null!;
    private MemberRepository _memberRepo = null!;
    private LoanRepository _loanRepo = null!;
    private BookService _bookService = null!;
    private CirculationService _circulationService = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dbFile = $"test_crud_{Guid.NewGuid():N}.db";
        _context = new DatabaseContext(_dbFile);
        var initializer = new DatabaseInitializer(_context);
        await initializer.InitializeAsync();

        _bookRepo = new BookRepository(_context);
        _memberRepo = new MemberRepository(_context);
        _loanRepo = new LoanRepository(_context);
        _bookService = new BookService(_bookRepo, _loanRepo);
        _circulationService = new CirculationService(_loanRepo, _bookRepo, _memberRepo, new FineCalculatorService());
    }

    [TestCleanup]
    public void Cleanup()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile))
        {
            try { File.Delete(_dbFile); } catch { }
        }
    }

    [TestMethod]
    public async Task Create_AddBook_SavesToDatabaseAndAssignsId()
    {
        var newBook = new Book
        {
            Title = "Domain-Driven Design",
            Author = "Eric Evans",
            ISBN = "978-0321125217",
            Category = "Software Engineering",
            PublicationYear = 2003,
            TotalCopies = 4,
            CopiesAvailable = 4,
            LocationRack = "DDD-01",
            Description = "Tackling complexity in the heart of software."
        };

        var created = await _bookService.AddBookAsync(newBook);

        Assert.IsNotNull(created);
        Assert.IsTrue(created.Id > 0);

        var fetched = await _bookService.GetBookByIdAsync(created.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("Domain-Driven Design", fetched.Title);
        Assert.AreEqual("Eric Evans", fetched.Author);
        Assert.AreEqual("978-0321125217", fetched.ISBN);
        Assert.AreEqual(4, fetched.CopiesAvailable);
    }

    [TestMethod]
    public async Task Create_DuplicateIsbn_ThrowsValidationException()
    {
        var book1 = new Book
        {
            Title = "First Book",
            Author = "Author A",
            ISBN = "978-1111111111",
            Category = "General",
            TotalCopies = 2,
            CopiesAvailable = 2
        };
        await _bookService.AddBookAsync(book1);

        var book2 = new Book
        {
            Title = "Duplicate ISBN Book",
            Author = "Author B",
            ISBN = "978-1111111111", // Duplicate
            Category = "General",
            TotalCopies = 1,
            CopiesAvailable = 1
        };

        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await _bookService.AddBookAsync(book2);
        });
    }

    [TestMethod]
    public async Task Read_SearchBooks_FindsByTitleAuthorOrCategory()
    {
        var byTitle = await _bookService.SearchBooksAsync("Clean Code");
        Assert.IsTrue(byTitle.Any(b => b.Title.Contains("Clean Code")));

        var byAuthor = await _bookService.SearchBooksAsync("Martin Fowler");
        Assert.IsTrue(byAuthor.Any(b => b.Author == "Martin Fowler"));

        var byCategory = await _bookService.SearchBooksAsync(null, "Philosophy");
        Assert.IsTrue(byCategory.All(b => b.Category == "Philosophy"));
    }

    [TestMethod]
    public async Task Update_EditBook_ModifiesDatabaseRecordSuccessfully()
    {
        // Book 1 is Clean Code
        var book = await _bookService.GetBookByIdAsync(1);
        Assert.IsNotNull(book);

        book.Title = "Clean Code (2nd Revised Edition)";
        book.LocationRack = "CS-UPDATED";
        book.TotalCopies = 8;
        book.CopiesAvailable = 7;

        bool updated = await _bookService.UpdateBookAsync(book);
        Assert.IsTrue(updated);

        var refreshed = await _bookService.GetBookByIdAsync(1);
        Assert.IsNotNull(refreshed);
        Assert.AreEqual("Clean Code (2nd Revised Edition)", refreshed.Title);
        Assert.AreEqual("CS-UPDATED", refreshed.LocationRack);
        Assert.AreEqual(8, refreshed.TotalCopies);
        Assert.AreEqual(7, refreshed.CopiesAvailable);
    }

    [TestMethod]
    public async Task Delete_BookWithoutLoans_DeletesSuccessfully()
    {
        // Add a fresh book with no loan history
        var freshBook = new Book
        {
            Title = "Temporary Book",
            Author = "Temp Author",
            ISBN = "978-9999999999",
            Category = "General",
            TotalCopies = 2,
            CopiesAvailable = 2
        };
        var added = await _bookService.AddBookAsync(freshBook);

        bool deleted = await _bookService.DeleteBookAsync(added.Id);
        Assert.IsTrue(deleted);

        var fetched = await _bookService.GetBookByIdAsync(added.Id);
        Assert.IsNull(fetched);
    }

    [TestMethod]
    public async Task Delete_BookWithActiveLoan_ThrowsValidationException()
    {
        // Book 1 has an active loan in seed data
        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await _bookService.DeleteBookAsync(1);
        });
    }
}
