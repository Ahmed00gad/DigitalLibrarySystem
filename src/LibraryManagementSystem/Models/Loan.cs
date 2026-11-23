namespace LibraryManagementSystem.Models;

public class Loan : BaseEntity
{
    public int BookId { get; set; }
    public int MemberId { get; set; }
    public DateTime BorrowDate { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);
    public DateTime? ReturnDate { get; set; }
    public decimal FineAmount { get; set; } = 0.00m;
    public bool FinePaid { get; set; } = false;
    public bool IsReturned { get; set; } = false;
    public string Notes { get; set; } = string.Empty;

    // Navigation / Display properties (hydrated on view)
    public string BookTitle { get; set; } = string.Empty;
    public string BookISBN { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;

    public bool IsOverdue => !IsReturned && DateTime.Today > DueDate;
    public int OverdueDays => IsOverdue ? (DateTime.Today - DueDate).Days : 0;

    public LoanStatus Status => IsReturned ? LoanStatus.Returned :
                                IsOverdue ? LoanStatus.Overdue :
                                LoanStatus.Active;
}
