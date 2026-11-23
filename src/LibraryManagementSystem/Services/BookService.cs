using System.Text.RegularExpressions;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public interface IBookService
{
    Task<IReadOnlyList<Book>> GetAllBooksAsync();
    Task<Book?> GetBookByIdAsync(int id);
    Task<Book?> GetBookByIsbnAsync(string isbn);
    Task<IReadOnlyList<Book>> SearchBooksAsync(string? query, string? category = null, bool availableOnly = false);
    Task<IReadOnlyList<string>> GetCategoriesAsync();
    Task<Book> AddBookAsync(Book book);
    Task<bool> UpdateBookAsync(Book book);
    Task<bool> DeleteBookAsync(int id);
}

public class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;
    private readonly ILoanRepository _loanRepository;

    public BookService(IBookRepository bookRepository, ILoanRepository loanRepository)
    {
        _bookRepository = bookRepository;
        _loanRepository = loanRepository;
    }

    public async Task<IReadOnlyList<Book>> GetAllBooksAsync()
    {
        return await _bookRepository.GetAllAsync();
    }

    public async Task<Book?> GetBookByIdAsync(int id)
    {
        return await _bookRepository.GetByIdAsync(id);
    }

    public async Task<Book?> GetBookByIsbnAsync(string isbn)
    {
        return await _bookRepository.GetByIsbnAsync(isbn);
    }

    public async Task<IReadOnlyList<Book>> SearchBooksAsync(string? query, string? category = null, bool availableOnly = false)
    {
        return await _bookRepository.SearchAsync(query, category, availableOnly);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        return await _bookRepository.GetAllCategoriesAsync();
    }

    public async Task<Book> AddBookAsync(Book book)
    {
        ValidateBook(book);

        // ISBN is optional. Check uniqueness only when a real ISBN is supplied.
        book.ISBN = book.ISBN?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(book.ISBN))
        {
            var existing = await _bookRepository.GetByIsbnAsync(book.ISBN);
            if (existing != null)
            {
                throw new ValidationException($"A book with ISBN '{book.ISBN}' already exists in the catalog ({existing.Title}).");
            }
        }

        // Initially, available copies equals total copies unless specified
        if (book.CopiesAvailable == 0 && book.TotalCopies > 0)
        {
            book.CopiesAvailable = book.TotalCopies;
        }

        var id = await _bookRepository.AddAsync(book);
        book.Id = id;
        return book;
    }

    public async Task<bool> UpdateBookAsync(Book book)
    {
        ValidateBook(book);

        var existing = await _bookRepository.GetByIdAsync(book.Id)
            ?? throw new EntityNotFoundException("Book", book.Id);

        // Do not treat missing ISBN values as duplicate ISBNs.
        book.ISBN = book.ISBN?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(book.ISBN))
        {
            var existingByIsbn = await _bookRepository.GetByIsbnAsync(book.ISBN);
            if (existingByIsbn != null && existingByIsbn.Id != book.Id)
            {
                throw new ValidationException($"Another book with ISBN '{book.ISBN}' already exists ({existingByIsbn.Title}).");
            }
        }

        // Ensure available copies does not exceed total copies
        if (book.CopiesAvailable > book.TotalCopies)
        {
            throw new ValidationException("Available copies cannot exceed total copies.");
        }

        return await _bookRepository.UpdateAsync(book);
    }

    public async Task<bool> DeleteBookAsync(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id)
            ?? throw new EntityNotFoundException("Book", id);

        var activeLoans = await _loanRepository.SearchLoansAsync(null, LoanStatus.Active);
        if (activeLoans.Any(l => l.BookId == id))
        {
            throw new ValidationException($"Cannot delete book '{book.Title}' because it is currently borrowed by active members.");
        }

        var overdueLoans = await _loanRepository.SearchLoansAsync(null, LoanStatus.Overdue);
        if (overdueLoans.Any(l => l.BookId == id))
        {
            throw new ValidationException($"Cannot delete book '{book.Title}' because it is currently in an overdue loan.");
        }

        try
        {
            return await _bookRepository.DeleteAsync(id);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new ValidationException($"Cannot delete book '{book.Title}' because historical loan records exist for it. To preserve audit history, books with circulation history cannot be permanently deleted.");
        }
    }

    public static void ValidateBook(Book book)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(book.Title))
            errors.Add("Book title is required.");

        if (string.IsNullOrWhiteSpace(book.Author))
            errors.Add("Author name is required.");

        if (!string.IsNullOrWhiteSpace(book.ISBN) && !IsValidIsbn(book.ISBN))
            errors.Add("ISBN must be a valid 10 or 13-digit format (hyphens allowed).");

        if (string.IsNullOrWhiteSpace(book.Category))
            errors.Add("Category is required.");

        if (book.PublicationYear < 1000 || book.PublicationYear > DateTime.Now.Year + 1)
            errors.Add($"Publication year must be between 1000 and {DateTime.Now.Year + 1}.");

        if (book.TotalCopies < 1)
            errors.Add("Total copies must be at least 1.");

        if (book.CopiesAvailable < 0)
            errors.Add("Available copies cannot be negative.");

        if (book.CopiesAvailable > book.TotalCopies)
            errors.Add("Available copies cannot exceed total copies.");

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    public static bool IsValidIsbn(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return false;
        var clean = isbn.Replace("-", "").Replace(" ", "").Trim();
        if (clean.Length == 10)
        {
            return Regex.IsMatch(clean, @"^\d{9}[\dX]$", RegexOptions.IgnoreCase);
        }
        if (clean.Length == 13)
        {
            return Regex.IsMatch(clean, @"^\d{13}$");
        }
        return false;
    }
}
