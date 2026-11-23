using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface IMemberRepository : IRepository<Member>
{
    Task<Member?> GetByEmailAsync(string email);
    Task<IReadOnlyList<Member>> SearchAsync(string? query, MembershipType? type = null, bool activeOnly = false);
    Task<int> GetActiveLoansCountAsync(int memberId);
}
