using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.UI.Dialogs;

public partial class ReturnDialog : Form
{
    private readonly Loan _loan;
    private readonly ICirculationService _circulationService;
    private readonly IFineCalculatorService _fineCalculator;
    private readonly IMemberService _memberService;
    private bool _submitting;

    public decimal CalculatedFine { get; private set; }

    public ReturnDialog(
        Loan loan,
        ICirculationService circulationService,
        IFineCalculatorService fineCalculator,
        IMemberService memberService)
    {
        _loan = loan;
        _circulationService = circulationService;
        _fineCalculator = fineCalculator;
        _memberService = memberService;

        InitializeComponent();
        lblBook.Text = _loan.BookTitle;
        lblMember.Text = _loan.MemberName;
        lblBorrowDate.Text = _loan.BorrowDate.ToString("yyyy-MM-dd");
        lblDueDate.Text = _loan.DueDate.ToString("yyyy-MM-dd");

        // Subscribe only once. Do not register handlers in catch/click blocks.
        btnConfirmReturn.Click += BtnConfirmReturn_Click;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnConfirmReturn.Enabled = false;
        Shown += async (_, _) => await ComputeFineAndDisplayAsync();
    }

    private async Task ComputeFineAndDisplayAsync()
    {
        btnConfirmReturn.Enabled = false;
        chkFinePaid.Enabled = true;
        try
        {
            var member = await _memberService.GetMemberByIdAsync(_loan.MemberId);
            if (member is null)
                throw new InvalidOperationException($"Member ID {_loan.MemberId} could not be found. Return cancelled to avoid an incorrect fine.");

            var membershipType = member.MembershipType;
            var overdueDays = _fineCalculator.CalculateOverdueDays(_loan.DueDate, DateTime.Today);
            CalculatedFine = _fineCalculator.CalculateFine(_loan.DueDate, DateTime.Today, membershipType);

            if (overdueDays > 0)
            {
                lblOverdueStatus.Text = $"OVERDUE by {overdueDays} day(s) (Member Type: {membershipType})";
                lblOverdueStatus.ForeColor = Color.FromArgb(220, 38, 38);
                lblFineAmount.Text = $"Calculated Fine: ${CalculatedFine:F2}";
                lblFineAmount.ForeColor = Color.FromArgb(71, 85, 105);
                chkFinePaid.Enabled = CalculatedFine > 0;
            }
            else
            {
                lblOverdueStatus.Text = "On Time (No Overdue)";
                lblOverdueStatus.ForeColor = Color.FromArgb(22, 163, 74);
                lblFineAmount.Text = "Calculated Fine: $0.00 (On Time)";
                lblFineAmount.ForeColor = Color.FromArgb(22, 163, 74);
                chkFinePaid.Enabled = false;
            }
            btnConfirmReturn.Enabled = true;
        }
        catch (Exception ex)
        {
            lblOverdueStatus.Text = "Unable to calculate fine";
            lblOverdueStatus.ForeColor = Color.FromArgb(220, 38, 38);
            lblFineAmount.Text = "Please check the database connection.";
            btnConfirmReturn.Enabled = false;
            MessageBox.Show($"Unable to calculate the return fine: {ex.Message}",
                "Calculation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnConfirmReturn_Click(object? sender, EventArgs e)
    {
        if (_submitting || !btnConfirmReturn.Enabled) return;
        _submitting = true;
        btnConfirmReturn.Enabled = false;
        try
        {
            await _circulationService.ReturnBookAsync(_loan.Id, chkFinePaid.Checked, txtNotes.Text.Trim());

            string fineNotice = CalculatedFine > 0
                ? $"\nFine of ${CalculatedFine:F2} marked as {(chkFinePaid.Checked ? "PAID" : "UNPAID")}."
                : "";
            MessageBox.Show($"Book '{_loan.BookTitle}' returned successfully!{fineNotice}",
                "Return Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error returning book: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _submitting = false;
            if (!IsDisposed && DialogResult != DialogResult.OK)
                btnConfirmReturn.Enabled = true;
        }
    }
}
