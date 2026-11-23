using LibraryManagementSystem.Models;
using System.Drawing;
using System.Windows.Forms;
using LibraryManagementSystem.UI.Controls;

namespace LibraryManagementSystem.UI.Dialogs;

public partial class ExecutiveReportDialog : Form
{
    private readonly LibraryStats _stats;
    public ExecutiveReportDialog(LibraryStats stats)
    {
        _stats = stats;
        InitializeComponent();
        lblDate.Text = $"LIBRARY PERFORMANCE  •  {DateTime.Now:MMMM yyyy}";
        SetCard(lblBooksValue, stats.TotalBooks);
        SetCard(lblCopiesValue, stats.TotalCopies);
        SetCard(lblAvailableValue, stats.AvailableCopies);
        SetCard(lblMembersValue, stats.ActiveMembers);
        SetCard(lblLoansValue, stats.ActiveLoans);
        SetCard(lblOverdueValue, stats.OverdueLoans);
        lblFinesValue.Text = $"${stats.TotalFinesAccrued:N2}";
        lblCollectedValue.Text = $"${stats.TotalFinesCollected:N2}";
        chartCategories.SetData(stats.BooksByCategory);
        chartMonthly.SetData(stats.MonthlyLoanTrends);
        gridTopBooks.DataSource = stats.TopBorrowedBooks.Select(x => new { x.Title, x.Author, x.Category, TimesBorrowed = x.BorrowCount }).ToList();
        gridTopBooks.ClearSelection();
    }

    private static void SetCard(Label label, int value) => label.Text = value.ToString("N0");
}
