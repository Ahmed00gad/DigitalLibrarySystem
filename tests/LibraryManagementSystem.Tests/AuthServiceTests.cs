using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class AuthServiceTests
{
    private class MockUserRepository : IUserRepository
    {
        private readonly List<User> _users = new();

        public MockUserRepository(IEnumerable<User> initial)
        {
            _users.AddRange(initial);
        }

        public Task<User?> GetByIdAsync(int id) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByUsernameAsync(string username) =>
            Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> UsernameExistsAsync(string username) =>
            Task.FromResult(_users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<User>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<User>>(_users.ToList());

        public Task<int> AddAsync(User entity)
        {
            entity.Id = _users.Count + 1;
            _users.Add(entity);
            return Task.FromResult(entity.Id);
        }

        public Task<bool> UpdateAsync(User entity)
        {
            var existing = _users.FirstOrDefault(u => u.Id == entity.Id);
            if (existing == null) return Task.FromResult(false);
            existing.Username = entity.Username;
            existing.PasswordHash = entity.PasswordHash;
            existing.FullName = entity.FullName;
            existing.Role = entity.Role;
            existing.IsActive = entity.IsActive;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(int id)
        {
            var count = _users.RemoveAll(u => u.Id == id);
            return Task.FromResult(count > 0);
        }
    }

    private AuthService _authService = null!;
    private MockUserRepository _mockRepo = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepo = new MockUserRepository(new[]
        {
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918", // SHA256 of "admin"
                FullName = "System Administrator",
                Role = "Admin",
                IsActive = true
            },
            new User
            {
                Id = 2,
                Username = "inactive_user",
                PasswordHash = "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918",
                FullName = "Disabled User",
                Role = "Librarian",
                IsActive = false
            }
        });

        _authService = new AuthService(_mockRepo);
    }

    [TestMethod]
    public async Task LoginAsync_ValidCredentials_ReturnsUserAndSetsCurrentUser()
    {
        var user = await _authService.LoginAsync("admin", "admin");

        Assert.IsNotNull(user);
        Assert.AreEqual("admin", user.Username);
        Assert.AreEqual("Admin", user.Role);
        Assert.AreEqual(user, _authService.CurrentUser);
    }

    [TestMethod]
    public async Task LoginAsync_CaseInsensitiveUsername_Succeeds()
    {
        var user = await _authService.LoginAsync("ADMIN", "admin");

        Assert.IsNotNull(user);
        Assert.AreEqual("admin", user.Username);
    }

    [TestMethod]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var user = await _authService.LoginAsync("admin", "wrong_password");

        Assert.IsNull(user);
        Assert.IsNull(_authService.CurrentUser);
    }

    [TestMethod]
    public async Task LoginAsync_NonExistentUser_ReturnsNull()
    {
        var user = await _authService.LoginAsync("unknown_user", "password");

        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        var user = await _authService.LoginAsync("inactive_user", "admin");

        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task Logout_ClearsCurrentUser()
    {
        await _authService.LoginAsync("admin", "admin");
        Assert.IsNotNull(_authService.CurrentUser);

        _authService.Logout();
        Assert.IsNull(_authService.CurrentUser);
    }

    [TestMethod]
    public void HashPassword_ProducesConsistentSha256Hex()
    {
        var hash1 = _authService.HashPassword("secret123");
        var hash2 = _authService.HashPassword("secret123");

        Assert.AreEqual(hash1, hash2);
        Assert.AreEqual(64, hash1.Length);
        Assert.IsTrue(_authService.VerifyPassword("secret123", hash1));
        Assert.IsFalse(_authService.VerifyPassword("wrong", hash1));
    }
}
