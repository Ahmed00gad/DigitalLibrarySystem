namespace LibraryManagementSystem.Models;

public class LibraryStats
{
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int BorrowedCopies { get; set; }
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public decimal TotalFinesAccrued { get; set; }
    public decimal TotalFinesCollected { get; set; }
    public Dictionary<string, int> BooksByCategory { get; set; } = new();
    public List<TopBookStat> TopBorrowedBooks { get; set; } = new();
    public Dictionary<string, int> MonthlyLoanTrends { get; set; } = new();
}

public class TopBookStat
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
}
