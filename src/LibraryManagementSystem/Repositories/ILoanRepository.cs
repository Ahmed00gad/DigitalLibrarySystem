using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface ILoanRepository : IRepository<Loan>
{
    Task<IReadOnlyList<Loan>> GetActiveLoansAsync();
    Task<IReadOnlyList<Loan>> GetOverdueLoansAsync();
    Task<IReadOnlyList<Loan>> GetLoansByMemberIdAsync(int memberId);
    Task<IReadOnlyList<Loan>> SearchLoansAsync(string? query, LoanStatus? status = null);
    Task<bool> RecordReturnAsync(int loanId, DateTime returnDate, decimal fineAmount, bool finePaid);
    Task<bool> UpdateFineAsync(int loanId, decimal fineAmount);
    Task<LibraryStats> GetLibraryStatsAsync();
}
