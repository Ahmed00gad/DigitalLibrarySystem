using LibraryManagementSystem.Data;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class CirculationServiceTests
{
    private string _dbFile = null!;
    private DatabaseContext _context = null!;
    private BookRepository _bookRepo = null!;
    private MemberRepository _memberRepo = null!;
    private LoanRepository _loanRepo = null!;
    private FineCalculatorService _fineCalculator = null!;
    private CirculationService _circulationService = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dbFile = $"test_library_{Guid.NewGuid():N}.db";
        _context = new DatabaseContext(_dbFile);
        var initializer = new DatabaseInitializer(_context);
        await initializer.InitializeAsync();

        _bookRepo = new BookRepository(_context);
        _memberRepo = new MemberRepository(_context);
        _loanRepo = new LoanRepository(_context);
        _fineCalculator = new FineCalculatorService { DailyRate = 0.50m, GracePeriodDays = 1 };
        _circulationService = new CirculationService(_loanRepo, _bookRepo, _memberRepo, _fineCalculator);
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
    public async Task BorrowBook_WhenAvailable_DecrementsCopiesAndCreatesLoan()
    {
        // Book 6 has 5 total copies and 5 available copies
        var bookBefore = await _bookRepo.GetByIdAsync(6);
        Assert.IsNotNull(bookBefore);
        int initialAvail = bookBefore.CopiesAvailable;

        // Member 4 is Mariam Youssef (Active Premium member)
        var loan = await _circulationService.BorrowBookAsync(6, 4, 14, "Unit test loan");

        Assert.IsNotNull(loan);
        Assert.IsTrue(loan.Id > 0);
        Assert.IsFalse(loan.IsReturned);

        var bookAfter = await _bookRepo.GetByIdAsync(6);
        Assert.AreEqual(initialAvail - 1, bookAfter!.CopiesAvailable);
    }

    [TestMethod]
    public async Task BorrowBook_WhenNoCopiesAvailable_ThrowsBookNotAvailableException()
    {
        // Add a book with 0 available copies
        var zeroBook = new Book
        {
            Title = "Out of Stock Book",
            Author = "Author",
            ISBN = "978-0000000001",
            Category = "General",
            TotalCopies = 1,
            CopiesAvailable = 0
        };
        int bookId = await _bookRepo.AddAsync(zeroBook);

        // Member 4
        await Assert.ThrowsAsync<BookNotAvailableException>(async () =>
        {
            await _circulationService.BorrowBookAsync(bookId, 4, 14);
        });
    }

    [TestMethod]
    public async Task BorrowBook_WhenMemberIsInactive_ThrowsValidationException()
    {
        // Create inactive member
        var inactiveMember = new Member
        {
            FullName = "Inactive Member",
            Email = "inactive@test.com",
            IsActive = false,
            MaxBorrowLimit = 5
        };
        int memberId = await _memberRepo.AddAsync(inactiveMember);

        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await _circulationService.BorrowBookAsync(6, memberId, 14);
        });
    }

    [TestMethod]
    public async Task ReturnBook_MarksLoanAsReturnedAndIncrementsCopies()
    {
        // First borrow book
        var loan = await _circulationService.BorrowBookAsync(6, 4, 14);
        var bookAfterBorrow = await _bookRepo.GetByIdAsync(6);
        int availAfterBorrow = bookAfterBorrow!.CopiesAvailable;

        // Return book
        var returnedLoan = await _circulationService.ReturnBookAsync(loan.Id, finePaid: true, "Returned successfully");

        Assert.IsTrue(returnedLoan.IsReturned);
        Assert.IsNotNull(returnedLoan.ReturnDate);

        var bookAfterReturn = await _bookRepo.GetByIdAsync(6);
        Assert.AreEqual(availAfterBorrow + 1, bookAfterReturn!.CopiesAvailable);
    }

    [TestMethod]
    public async Task DirectCatalogBorrow_WithCustomDuration_CorrectlySetsDueDateAndDecrementsCopies()
    {
        // Select an existing book (e.g. Clean Code, ID 1)
        var book = await _bookRepo.GetByIdAsync(1);
        Assert.IsNotNull(book);
        int initialCopies = book.CopiesAvailable;
        Assert.IsTrue(initialCopies > 0);

        int durationDays = 21;
        var loan = await _circulationService.BorrowBookAsync(1, 4, durationDays, "Direct catalog borrow");

        Assert.IsNotNull(loan);
        Assert.AreEqual(1, loan.BookId);
        Assert.AreEqual(4, loan.MemberId);
        Assert.AreEqual(DateTime.Today.AddDays(durationDays).Date, loan.DueDate.Date);

        var bookAfter = await _bookRepo.GetByIdAsync(1);
        Assert.AreEqual(initialCopies - 1, bookAfter!.CopiesAvailable);
    }

    [TestMethod]
    public async Task DirectCatalogBorrow_WhenLastCopyBorrowed_NextBorrowThrowsBookNotAvailable()
    {
        // Create a book with only 1 available copy
        var singleCopyBook = new Book
        {
            Title = "Rare Limited Edition",
            Author = "Special Author",
            ISBN = "978-9999999999",
            Category = "Special",
            PublicationYear = 2024,
            TotalCopies = 1,
            CopiesAvailable = 1,
            LocationRack = "Vault-1"
        };
        int bookId = await _bookRepo.AddAsync(singleCopyBook);

        // First borrow directly from catalog: succeeds
        var loan1 = await _circulationService.BorrowBookAsync(bookId, 4, 14, "First borrow");
        Assert.IsNotNull(loan1);

        var bookAfterFirst = await _bookRepo.GetByIdAsync(bookId);
        Assert.AreEqual(0, bookAfterFirst!.CopiesAvailable);

        // Second borrow attempt: must throw BookNotAvailableException
        await Assert.ThrowsAsync<BookNotAvailableException>(async () =>
        {
            await _circulationService.BorrowBookAsync(bookId, 2, 14, "Attempt second borrow");
        });
    }

    [TestMethod]
    public async Task DirectCatalogBorrow_WhenMemberExceedsLimit_ThrowsMemberLimitExceededException()
    {
        // Create member with limit of 1
        var strictMember = new Member
        {
            FullName = "Strict Member",
            Email = "strict@test.com",
            MembershipType = MembershipType.Student,
            MaxBorrowLimit = 1,
            IsActive = true
        };
        int memberId = await _memberRepo.AddAsync(strictMember);

        // First loan succeeds
        await _circulationService.BorrowBookAsync(6, memberId, 14);

        // Second loan exceeds limit
        await Assert.ThrowsAsync<MemberLimitExceededException>(async () =>
        {
            await _circulationService.BorrowBookAsync(7, memberId, 14);
        });
    }
}
