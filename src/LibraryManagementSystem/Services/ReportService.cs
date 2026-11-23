using System.Text;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public interface IReportService
{
    Task<LibraryStats> GetStatisticsAsync();
    Task<string> GenerateSummaryReportTextAsync();
    Task ExportBooksToCsvAsync(string filePath);
    Task ExportLoansToCsvAsync(string filePath);
}

public class ReportService : IReportService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IBookRepository _bookRepository;

    public ReportService(ILoanRepository loanRepository, IBookRepository bookRepository)
    {
        _loanRepository = loanRepository;
        _bookRepository = bookRepository;
    }

    public async Task<LibraryStats> GetStatisticsAsync()
    {
        return await _loanRepository.GetLibraryStatsAsync();
    }

    public async Task<string> GenerateSummaryReportTextAsync()
    {
        var stats = await GetStatisticsAsync();
        var sb = new StringBuilder();

        sb.AppendLine("=================================================");
        sb.AppendLine("           DIGITAL LIBRARY EXECUTIVE REPORT      ");
        sb.AppendLine("=================================================");
        sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("--- INVENTORY & COPIES ---");
        sb.AppendLine($"Total Unique Book Titles: {stats.TotalBooks}");
        sb.AppendLine($"Total Physical Copies:    {stats.TotalCopies}");
        sb.AppendLine($"Available Copies:         {stats.AvailableCopies}");
        sb.AppendLine($"Borrowed Copies:          {stats.BorrowedCopies}");
        sb.AppendLine();
        sb.AppendLine("--- MEMBERSHIP STATS ---");
        sb.AppendLine($"Total Registered Members: {stats.TotalMembers}");
        sb.AppendLine($"Active Members:           {stats.ActiveMembers}");
        sb.AppendLine();
        sb.AppendLine("--- CIRCULATION & OVERDUE ---");
        sb.AppendLine($"Active Loans:             {stats.ActiveLoans}");
        sb.AppendLine($"Overdue Loans:            {stats.OverdueLoans}");
        sb.AppendLine($"Total Fines Accrued:      ${stats.TotalFinesAccrued:F2}");
        sb.AppendLine($"Total Fines Collected:    ${stats.TotalFinesCollected:F2}");
        sb.AppendLine();
        sb.AppendLine("--- CATEGORY BREAKDOWN ---");
        foreach (var kvp in stats.BooksByCategory)
        {
            sb.AppendLine($"  - {kvp.Key,-24}: {kvp.Value,4} titles");
        }
        sb.AppendLine();
        sb.AppendLine("--- TOP BORROWED BOOKS ---");
        int rank = 1;
        foreach (var top in stats.TopBorrowedBooks)
        {
            sb.AppendLine($"  {rank++}. {top.Title} by {top.Author} [{top.Category}] - {top.BorrowCount} borrows");
        }
        sb.AppendLine("=================================================");

        return sb.ToString();
    }

    public async Task ExportBooksToCsvAsync(string filePath)
    {
        var books = await _bookRepository.GetAllAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,Title,Author,ISBN,Category,PublicationYear,TotalCopies,CopiesAvailable,LocationRack");

        foreach (var b in books)
        {
            sb.AppendLine($"{b.Id},\"{EscapeCsv(b.Title)}\",\"{EscapeCsv(b.Author)}\",\"{EscapeCsv(b.ISBN)}\",\"{EscapeCsv(b.Category)}\",{b.PublicationYear},{b.TotalCopies},{b.CopiesAvailable},\"{EscapeCsv(b.LocationRack)}\"");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportLoansToCsvAsync(string filePath)
    {
        var loans = await _loanRepository.GetAllAsync();
        var sb = new StringBuilder();
        sb.AppendLine("LoanId,BookTitle,BookISBN,MemberName,MemberEmail,BorrowDate,DueDate,ReturnDate,Status,OverdueDays,FineAmount,FinePaid");

        foreach (var l in loans)
        {
            sb.AppendLine($"{l.Id},\"{EscapeCsv(l.BookTitle)}\",\"{EscapeCsv(l.BookISBN)}\",\"{EscapeCsv(l.MemberName)}\",\"{EscapeCsv(l.MemberEmail)}\",{l.BorrowDate:yyyy-MM-dd},{l.DueDate:yyyy-MM-dd},{(l.ReturnDate.HasValue ? l.ReturnDate.Value.ToString("yyyy-MM-dd") : "")},{l.Status},{l.OverdueDays},{l.FineAmount:F2},{l.FinePaid}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }
}
