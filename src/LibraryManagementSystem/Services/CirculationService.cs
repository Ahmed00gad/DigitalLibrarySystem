using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public interface ICirculationService
{
    Task<Loan> BorrowBookAsync(int bookId, int memberId, int durationDays = 14, string notes = "");
    Task<Loan> ReturnBookAsync(int loanId, bool finePaid = false, string? notes = null);
    Task<IReadOnlyList<Loan>> GetActiveLoansAsync();
    Task<IReadOnlyList<Loan>> GetOverdueLoansAsync();
    Task<IReadOnlyList<Loan>> SearchLoansAsync(string? query, LoanStatus? status = null);
    Task<int> RecalculateOverdueFinesAsync();
}

public class CirculationService : ICirculationService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IBookRepository _bookRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IFineCalculatorService _fineCalculator;

    public CirculationService(
        ILoanRepository loanRepository,
        IBookRepository bookRepository,
        IMemberRepository memberRepository,
        IFineCalculatorService fineCalculator)
    {
        _loanRepository = loanRepository;
        _bookRepository = bookRepository;
        _memberRepository = memberRepository;
        _fineCalculator = fineCalculator;
    }

    public async Task<Loan> BorrowBookAsync(int bookId, int memberId, int durationDays = 14, string notes = "")
    {
        // 1. Validate Book
        var book = await _bookRepository.GetByIdAsync(bookId)
            ?? throw new EntityNotFoundException("Book", bookId);

        if (book.CopiesAvailable <= 0)
        {
            throw new BookNotAvailableException(book.Id, book.Title);
        }

        // 2. Validate Member
        var member = await _memberRepository.GetByIdAsync(memberId)
            ?? throw new EntityNotFoundException("Member", memberId);

        if (!member.IsActive)
        {
            throw new ValidationException($"Member '{member.FullName}' is inactive and cannot borrow books.");
        }

        // 3. Check borrow limit
        var activeLoansCount = await _memberRepository.GetActiveLoansCountAsync(memberId);
        if (activeLoansCount >= member.MaxBorrowLimit)
        {
            throw new MemberLimitExceededException(member.Id, member.FullName, member.MaxBorrowLimit);
        }

        // 4. Check for unpaid overdue fines on other loans
        var memberLoans = await _loanRepository.GetLoansByMemberIdAsync(memberId);
        var hasUnpaidOverdue = memberLoans.Any(l => !l.IsReturned && l.IsOverdue && l.FineAmount > 0);
        if (hasUnpaidOverdue)
        {
            throw new ValidationException($"Member '{member.FullName}' has active overdue books with unpaid fines. Please return overdue books first.");
        }

        // 5. Decrement available copies
        var adjusted = await _bookRepository.AdjustCopiesAvailableAsync(bookId, -1);
        if (!adjusted)
        {
            throw new BookNotAvailableException(book.Id, book.Title);
        }

        // 6. Create Loan
        var loan = new Loan
        {
            BookId = bookId,
            MemberId = memberId,
            BorrowDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(durationDays > 0 ? durationDays : 14),
            IsReturned = false,
            Notes = notes ?? string.Empty
        };

        var loanId = await _loanRepository.AddAsync(loan);
        loan.Id = loanId;
        loan.BookTitle = book.Title;
        loan.BookISBN = book.ISBN;
        loan.MemberName = member.FullName;
        loan.MemberEmail = member.Email;

        return loan;
    }

    public async Task<Loan> ReturnBookAsync(int loanId, bool finePaid = false, string? notes = null)
    {
        var loan = await _loanRepository.GetByIdAsync(loanId)
            ?? throw new EntityNotFoundException("Loan", loanId);

        if (loan.IsReturned)
        {
            throw new ValidationException("This book has already been marked as returned.");
        }

        var member = await _memberRepository.GetByIdAsync(loan.MemberId);
        var membershipType = member?.MembershipType ?? MembershipType.Regular;

        // Calculate final fine
        var returnDate = DateTime.Today;
        var fine = _fineCalculator.CalculateFine(loan.DueDate, returnDate, membershipType);

        // Record return in database
        await _loanRepository.RecordReturnAsync(loanId, returnDate, fine, finePaid);

        // Increment available copies
        await _bookRepository.AdjustCopiesAvailableAsync(loan.BookId, 1);

        loan.IsReturned = true;
        loan.ReturnDate = returnDate;
        loan.FineAmount = fine;
        loan.FinePaid = finePaid;
        if (!string.IsNullOrEmpty(notes))
        {
            loan.Notes = notes;
        }

        return loan;
    }

    public async Task<IReadOnlyList<Loan>> GetActiveLoansAsync()
    {
        return await _loanRepository.GetActiveLoansAsync();
    }

    public async Task<IReadOnlyList<Loan>> GetOverdueLoansAsync()
    {
        return await _loanRepository.GetOverdueLoansAsync();
    }

    public async Task<IReadOnlyList<Loan>> SearchLoansAsync(string? query, LoanStatus? status = null)
    {
        return await _loanRepository.SearchLoansAsync(query, status);
    }

    public async Task<int> RecalculateOverdueFinesAsync()
    {
        var activeLoans = await _loanRepository.GetActiveLoansAsync();
        int updatedCount = 0;

        foreach (var loan in activeLoans)
        {
            if (loan.IsOverdue)
            {
                var member = await _memberRepository.GetByIdAsync(loan.MemberId);
                var membershipType = member?.MembershipType ?? MembershipType.Regular;
                var currentFine = _fineCalculator.CalculateFine(loan.DueDate, DateTime.Today, membershipType);

                if (currentFine != loan.FineAmount)
                {
                    await _loanRepository.UpdateFineAsync(loan.Id, currentFine);
                    updatedCount++;
                }
            }
        }

        return updatedCount;
    }
}
