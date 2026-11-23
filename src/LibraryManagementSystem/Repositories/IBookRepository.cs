using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface IBookRepository : IRepository<Book>
{
    Task<Book?> GetByIsbnAsync(string isbn);
    Task<IReadOnlyList<Book>> SearchAsync(string? query, string? category = null, bool availableOnly = false);
    Task<IReadOnlyList<string>> GetAllCategoriesAsync();
    Task<bool> AdjustCopiesAvailableAsync(int bookId, int delta);
}
