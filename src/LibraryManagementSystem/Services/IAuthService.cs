using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    Task<User?> LoginAsync(string username, string password);
    void Logout();
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
