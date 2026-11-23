namespace LibraryManagementSystem.Models;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Librarian"; // "Admin", "Librarian"
    public bool IsActive { get; set; } = true;
}
