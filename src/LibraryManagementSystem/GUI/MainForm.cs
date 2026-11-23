using System.Globalization;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.UI.Controls;
using LibraryManagementSystem.UI.Dialogs;

namespace LibraryManagementSystem.UI;

public partial class MainForm : Form
{
    private readonly DatabaseContext _dbContext;
    private readonly IBookRepository _bookRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly ILoanRepository _loanRepo;

    private readonly IFineCalculatorService _fineCalculator;
    private readonly ICirculationService _circulationService;
    private readonly IBookService _bookService;
    private readonly IMemberService _memberService;
    private readonly IReportService _reportService;

    // Controls
    private System.Windows.Forms.Timer tmrBackgroundScan = null!;

    // Dashboard Controls

    // Books Tab Controls
    private bool _isBorrowInProgress;
    private readonly Font _actionButtonFont = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly Font _overdueEmphasisFont = new("Segoe UI", 10f, FontStyle.Bold);

    // Members Tab Controls

    // Loans tab controls are declared in MainForm.Designer.cs.

    // Reports Tab Controls


    // Status bar

    public bool IsLoggedOut { get; private set; }
    public User? CurrentUser { get; private set; }

    public MainForm() : this(null) { }

    public MainForm(User? user)
    {
        CurrentUser = user;

        // 1. Initialize Dependency Injection / Services
        _dbContext = new DatabaseContext("library.db");
        _bookRepo = new BookRepository(_dbContext);
        _memberRepo = new MemberRepository(_dbContext);
        _loanRepo = new LoanRepository(_dbContext);

        _fineCalculator = new FineCalculatorService { DailyRate = 0.50m, GracePeriodDays = 1 };
        _circulationService = new CirculationService(_loanRepo, _bookRepo, _memberRepo, _fineCalculator);
        _bookService = new BookService(_bookRepo, _loanRepo);
        _memberService = new MemberService(_memberRepo, _loanRepo);
        _reportService = new ReportService(_loanRepo, _bookRepo);

        InitializeComponent();

        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
        {
            ApplyModernGridTheme(dgvBooks);
            ApplyModernGridTheme(dgvMembers);
            ApplyModernGridTheme(dgvLoans);
            ApplyModernGridTheme(dgvTopBooks);
        }
        // Remove the networking tab from the running application.
        // The tab and its controls may still exist in MainForm.Designer.cs.
        foreach (TabPage page in tabMain.TabPages.Cast<TabPage>().ToArray())
        {
            if (page.Name.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
                page.Text.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
                page.Text.Contains("TCP", StringComparison.OrdinalIgnoreCase))
            {
                tabMain.TabPages.Remove(page);
                page.Dispose();
            }
        }

        foreach (Control control in Controls.Find("lblServerIndicator", true))
            control.Visible = false;

        tabMain.SelectedTab = pageDashboard;
        lblUserInfo.Text = $"👤 {(CurrentUser?.FullName ?? "Admin")} ({(CurrentUser?.Role ?? "Admin")})";
        WireDesignerShellEvents();
        WireBooksCatalogEvents();
        WireMembersEvents();
        WireCirculationEvents();
        WireReportsEvents();
        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
            PopulateTabContents();

        // 2. Setup Background Overdue Scan Timer (Multithreading / Async Feature)
        tmrBackgroundScan = new System.Windows.Forms.Timer { Interval = 45000 };
        tmrBackgroundScan.Tick += async (s, e) => await RunBackgroundScanAsync();
        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
            tmrBackgroundScan.Start();

        Shown += async (s, e) =>
        {
            await InitializeDatabaseAndLoadAsync();
        };

        FormClosing += (s, e) =>
        {
            tmrBackgroundScan.Stop();
            tmrBackgroundScan.Dispose();
            _actionButtonFont.Dispose();
            _overdueEmphasisFont.Dispose();
        };
    }

    // Main shell controls are declared and positioned in MainForm.Designer.cs.
    // The content of each tab remains generated at runtime until its individual
    // page has been safely migrated to Designer code.
    private void PopulateTabContents()
    {
        // Dashboard controls are created by the Windows Forms Designer.
        // Books Catalog is built in the WinForms Designer.
        // Members is created in MainForm.Designer.cs.
        // Circulation & Loans is now Designer-managed.
        // Reports & Analytics is editable in MainForm.Designer.cs.
    }

    private static void AttachTabContent(TabPage destination, TabPage source)
    {
        destination.SuspendLayout();
        while (source.Controls.Count > 0)
        {
            var child = source.Controls[0];
            source.Controls.RemoveAt(0);
            destination.Controls.Add(child);
        }
        destination.ResumeLayout(false);
        source.Dispose();
    }

    private void WireDesignerShellEvents()
    {
        btnQuickLogout.Click += (s, e) =>
        {
            if (MessageBox.Show("Are you sure you want to log out?", "Logout",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                IsLoggedOut = true;
                Close();
            }
        };
        btnQuickAddBook.Click += async (s, e) => await OpenAddBookDialogAsync();
        btnQuickBorrow.Click += async (s, e) => await OpenBorrowDialogAsync();
        tabMain.SelectedIndexChanged += async (s, e) => await RefreshActiveTabAsync();
        // Runtime behaviors for controls created by the visual Designer.
        dgvOverdueSummary.CellDoubleClick += async (sender, args) =>
        {
            if (args.RowIndex < 0) return;
            var value = dgvOverdueSummary.Rows[args.RowIndex].Cells["LoanId"].Value;
            if (value == null) return;
            var loan = await _loanRepo.GetByIdAsync(Convert.ToInt32(value));
            if (loan == null || loan.IsReturned) return;
            using var dialog = new ReturnDialog(loan, _circulationService, _fineCalculator, _memberService);
            if (dialog.ShowDialog(this) == DialogResult.OK)
                await RefreshActiveTabAsync();
        };
        dgvOverdueSummary.CellFormatting += (sender, args) =>
        {
            if (args.RowIndex < 0 || args.ColumnIndex < 0) return;
            var columnName = dgvOverdueSummary.Columns[args.ColumnIndex].Name;
            if (args.Value != null)
                dgvOverdueSummary.Rows[args.RowIndex].Cells[args.ColumnIndex].ToolTipText = args.Value.ToString();
            if (columnName == "DaysLate" || columnName == "Fine")
                args.CellStyle.Font = _overdueEmphasisFont;
        };

    }

    #region Tab 1: Dashboard

    #endregion

    // Books Catalog controls are created in MainForm.Designer.cs and can be edited visually.
    private void WireBooksCatalogEvents()
    {
        btnBookRefresh.Click += async (s, e) => await RefreshBooksListAsync();
        btnBookExportCsv.Click += async (s, e) => await ExportBooksCsvAsync();
        btnAddBook.Click += async (s, e) => await OpenAddBookDialogAsync();
        btnEditBook.Click += async (s, e) => await EditSelectedBookAsync();
        btnDeleteBook.Click += async (s, e) => await DeleteSelectedBookAsync();
        txtBookSearch.TextChanged += async (s, e) => await RefreshBooksListAsync();
        cmbBookCategoryFilter.SelectedIndexChanged += async (s, e) => await RefreshBooksListAsync();
        chkBookAvailableOnly.CheckedChanged += async (s, e) => await RefreshBooksListAsync();
        btnBookResetFilters.Click += async (s, e) =>
        {
            txtBookSearch.Clear();
            cmbBookCategoryFilter.SelectedIndex = 0;
            chkBookAvailableOnly.Checked = false;
            await RefreshBooksListAsync();
        };
        dgvBooks.CellFormatting += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                dgvBooks.Columns[e.ColumnIndex].Name == "colRowNumber")
            {
                e.Value = (e.RowIndex + 1).ToString();
                e.FormattingApplied = true;
            }
        };
        dgvBooks.Sorted += (s, e) => dgvBooks.InvalidateColumn(dgvBooks.Columns["colRowNumber"].Index);
        dgvBooks.CellFormatting += DgvBooks_CellFormatting;
        dgvBooks.CellPainting += DgvBooks_CellPainting;
        dgvBooks.CellMouseMove += DgvBooks_CellMouseMove;
        dgvBooks.CellMouseLeave += (s, e) => dgvBooks.Cursor = Cursors.Default;
        dgvBooks.CellClick += async (s, e) => await DgvBooks_CellClickAsync(e);
        dgvBooks.CellDoubleClick += async (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (dgvBooks.Columns[e.ColumnIndex].Name == "colActions")
                    return;

                await EditSelectedBookAsync();
            }
        };

        // Dynamically toggle Edit & Delete buttons on selection
        dgvBooks.SelectionChanged += (s, e) =>
        {
            bool hasSelection = dgvBooks.SelectedRows.Count > 0;
            btnEditBook.Enabled = hasSelection;
            btnDeleteBook.Enabled = hasSelection;
        };

        // Context Menu on Right-Click
        var bookContextMenu = new ContextMenuStrip();
        var menuAdd = new ToolStripMenuItem("➕ Add New Book...", null, async (s, e) => await OpenAddBookDialogAsync());
        var menuEdit = new ToolStripMenuItem("✏️ Edit Selected Book...", null, async (s, e) => await EditSelectedBookAsync());
        var menuDelete = new ToolStripMenuItem("🗑️ Delete Selected Book...", null, async (s, e) => await DeleteSelectedBookAsync());
        var menuLoan = new ToolStripMenuItem("📖 Borrow This Book...", null, async (s, e) =>
        {
            if (dgvBooks.SelectedRows.Count > 0 && dgvBooks.SelectedRows[0].Cells["Id"]?.Value is int bookId)
            {
                await OpenBorrowDialogForBookAsync(bookId);
            }
            else
            {
                await OpenBorrowDialogAsync();
            }
        });
        var menuRefresh = new ToolStripMenuItem("🔄 Refresh Catalog", null, async (s, e) => await RefreshBooksListAsync());

        bookContextMenu.Items.AddRange(new ToolStripItem[]
        {
            menuAdd,
            menuEdit,
            menuDelete,
            new ToolStripSeparator(),
            menuLoan,
            menuRefresh
        });

        dgvBooks.ContextMenuStrip = bookContextMenu;
        dgvBooks.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                var hit = dgvBooks.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0)
                {
                    dgvBooks.ClearSelection();
                    dgvBooks.Rows[hit.RowIndex].Selected = true;
                }
            }
        };

    }
    // Members controls are Designer-managed; event handlers remain in MainForm.cs.
    private void WireMembersEvents()
    {
        btnMemberRefresh.Click += async (s, e) => await RefreshMembersListAsync();
        btnAddMember.Click += async (s, e) => await OpenAddMemberDialogAsync();
        btnEditMember.Click += async (s, e) => await EditSelectedMemberAsync();
        btnDeleteMember.Click += async (s, e) => await DeleteSelectedMemberAsync();
        btnToggleMemberStatus.Click += async (s, e) => await ToggleSelectedMemberStatusAsync();
        txtMemberSearch.TextChanged += async (s, e) => await RefreshMembersListAsync();
        cmbMemberTypeFilter.SelectedIndexChanged += async (s, e) => await RefreshMembersListAsync();
        chkMemberActiveOnly.CheckedChanged += async (s, e) => await RefreshMembersListAsync();
        btnMemberResetFilters.Click += async (s, e) =>
        {
            txtMemberSearch.Clear();
            cmbMemberTypeFilter.SelectedIndex = 0;
            chkMemberActiveOnly.Checked = false;
            await RefreshMembersListAsync();
        };
        dgvMembers.DoubleClick += async (s, e) => await EditSelectedMemberAsync();
        dgvMembers.SelectionChanged += (s, e) =>
        {
            bool selected = dgvMembers.SelectedRows.Count > 0;
            btnEditMember.Enabled = selected;
            btnDeleteMember.Enabled = selected;
            btnToggleMemberStatus.Enabled = selected;
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Register New Member...", null, async (s, e) => await OpenAddMemberDialogAsync());
        menu.Items.Add("Edit Selected Member...", null, async (s, e) => await EditSelectedMemberAsync());
        menu.Items.Add("Toggle Active Status", null, async (s, e) => await ToggleSelectedMemberStatusAsync());
        menu.Items.Add("Delete Member...", null, async (s, e) => await DeleteSelectedMemberAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Refresh Members", null, async (s, e) => await RefreshMembersListAsync());
        dgvMembers.ContextMenuStrip = menu;
        dgvMembers.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = dgvMembers.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;
            dgvMembers.ClearSelection();
            dgvMembers.Rows[hit.RowIndex].Selected = true;
        };
    }

    #region Tab 4: Circulation & Loans
    // The layout and controls are in MainForm.Designer.cs.
    // Runtime behavior stays here, so Designer edits do not change business rules.
    private void WireCirculationEvents()
    {
        btnLoanRefresh.Click += async (s, e) => await RefreshLoansListAsync();
        btnLoanExportCsv.Click += async (s, e) => await ExportLoansCsvAsync();
        btnIssueLoan.Click += async (s, e) => await OpenBorrowDialogAsync();
        btnReturnBook.Click += async (s, e) => await OpenReturnDialogForSelectedLoanAsync();
        btnRecalcFines.Click += async (s, e) =>
        {
            int updated = await _circulationService.RecalculateOverdueFinesAsync();
            lblStatusText.Text = $"Recalculated fines on {updated} active overdue loans.";
            await RefreshLoansListAsync();
            await RefreshDashboardAsync();
        };
        txtLoanSearch.TextChanged += async (s, e) => await RefreshLoansListAsync();
        cmbLoanStatusFilter.SelectedIndexChanged += async (s, e) => await RefreshLoansListAsync();
        btnLoanResetFilters.Click += async (s, e) =>
        {
            txtLoanSearch.Clear();
            cmbLoanStatusFilter.SelectedIndex = 0;
            await RefreshLoansListAsync();
        };
        dgvLoans.DoubleClick += async (s, e) => await OpenReturnDialogForSelectedLoanAsync();
        dgvLoans.SelectionChanged += (s, e) =>
        {
            var row = dgvLoans.SelectedRows.Count > 0 ? dgvLoans.SelectedRows[0] : null;
            btnReturnBook.Enabled = row != null &&
                row.Cells["Status"]?.Value?.ToString() != "Returned";
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Borrow Book...", null, async (s, e) => await OpenBorrowDialogAsync());
        menu.Items.Add("Return Selected Book...", null, async (s, e) => await OpenReturnDialogForSelectedLoanAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Recalculate Fines", null, async (s, e) =>
        {
            await _circulationService.RecalculateOverdueFinesAsync();
            await RefreshLoansListAsync();
        });
        menu.Items.Add("Refresh Loans", null, async (s, e) => await RefreshLoansListAsync());
        dgvLoans.ContextMenuStrip = menu;
        dgvLoans.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = dgvLoans.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;
            dgvLoans.ClearSelection();
            dgvLoans.Rows[hit.RowIndex].Selected = true;
        };
    }
    #endregion

    #region Tab 5: Reports & Analytics
    // Report layout is generated in MainForm.Designer.cs; only behavior lives here.
    private void WireReportsEvents()
    {
        btnReportRefresh.Click += async (s, e) => await LoadExecutiveReportAsync();
        btnReportExportTxt.Click += async (s, e) => await ExportSummaryTextAsync();
        btnReportViewFull.Click += (s, e) => ShowFullExecutiveReport();
    }
    private async void ShowFullExecutiveReport()
    {
        try
        {
            var stats = await _reportService.GetStatisticsAsync();
            using var reportWindow = new Dialogs.ExecutiveReportDialog(stats);
            reportWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to open report: {ex.Message}", "Report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    #endregion

    #region Helper Grid Factory
    private static void ApplyModernGridTheme(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(226, 232, 240);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersHeight = 46;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(199, 210, 254);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.RowTemplate.Height = 40;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.Dock = DockStyle.Fill;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AllowUserToResizeColumns = true;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    private static DataGridView CreateStyledGrid()
    {
        var grid = new DataGridView
        {
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(241, 245, 249),
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ShowCellToolTips = true,
            ScrollBars = ScrollBars.Both
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
        grid.ColumnHeadersHeight = 40;

        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.25f);
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 231, 255);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 41, 59);
        grid.RowTemplate.Height = 36;

        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

        return grid;
    }
    // These data grids have more columns than fit on a laptop screen.
    // Display full headers with a horizontal scrollbar rather than compressing cells.
    private static void PrepareWideGrid(DataGridView grid)
    {
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.ColumnHeadersVisible = true;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 48;
        grid.RowTemplate.Height = 42;
        grid.ScrollBars = ScrollBars.Both;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    private static void SetColumnWidth(DataGridView grid, string name, int preferredWidth)
    {
        if (grid.Columns[name] is { } column)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = Math.Max(65, preferredWidth);
            column.MinimumWidth = Math.Min(preferredWidth, 85);
        }
    }

    // Automatically share available width without squeezing long names or dates.
    // When a window is genuinely too narrow, normal horizontal scrolling remains available.
    private static void FitGridToViewport(DataGridView grid)
    {
        grid.SuspendLayout();
        try
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ScrollBars = ScrollBars.Both;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeight = 48;
            grid.RowTemplate.Height = 40;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible) continue;
                if (column.Name is "colRowNumber" or "colActions")
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.Width = column.Name == "colRowNumber" ? 55 : 105;
                    continue;
                }
                int weight = column.Name switch
                {
                    "Title" or "Book" or "FullName" => 210,
                    "Author" or "Borrower" => 145,
                    "Email" => 210,
                    "Category" or "ISBN" or "Phone" => 135,
                    "MemberSince" or "BorrowDate" or "DueDate" or "ReturnDate" => 125,
                    "Status" or "Type" => 100,
                    "TotalBorrows" => 105,
                    _ => 85
                };
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column.FillWeight = weight;
                column.MinimumWidth = Math.Min(100, Math.Max(55, weight / 2));
            }
        }
        finally { grid.ResumeLayout(); }
    }

    #endregion

    #region Data Loading & Background Tasks
    private async Task InitializeDatabaseAndLoadAsync()
    {
        try
        {
            lblStatusText.Text = "Initializing database tables and seed records...";
            var initializer = new DatabaseInitializer(_dbContext);
            await initializer.InitializeAsync();

            await LoadCategoriesDropdownAsync();
            await RefreshActiveTabAsync();
            lblStatusText.Text = "Database initialized and ready.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database initialization failed: {ex.Message}", "Fatal Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadCategoriesDropdownAsync()
    {
        var categories = await _bookService.GetCategoriesAsync();
        cmbBookCategoryFilter.Items.Clear();
        cmbBookCategoryFilter.Items.Add("All Categories");
        foreach (var c in categories)
        {
            cmbBookCategoryFilter.Items.Add(c);
        }
        cmbBookCategoryFilter.SelectedIndex = 0;
    }

    private async Task RefreshActiveTabAsync()
    {
        int index = tabMain.SelectedIndex;
        if (index == 0) await RefreshDashboardAsync();
        else if (index == 1) await RefreshBooksListAsync();
        else if (index == 2) await RefreshMembersListAsync();
        else if (index == 3) await RefreshLoansListAsync();
        else if (index == 4) await LoadExecutiveReportAsync();
    }

    private async Task RefreshDashboardAsync()
    {
        try
        {
            var stats = await _reportService.GetStatisticsAsync();

            cardBooks.Value = stats.TotalBooks.ToString("N0");
            cardCopies.Value = $"{stats.AvailableCopies} / {stats.TotalCopies}";
            cardMembers.Value = $"{stats.ActiveMembers} / {stats.TotalMembers}";
            cardActiveLoans.Value = stats.ActiveLoans.ToString("N0");
            cardOverdue.Value = stats.OverdueLoans.ToString("N0");
            cardFines.Value = $"${stats.TotalFinesAccrued:F2}";

            // Update GDI+ Donut Chart
            chartCategories.SetData(stats.BooksByCategory);

            // Update GDI+ Bar Chart
            chartTrends.SetData(stats.MonthlyLoanTrends);

            // Populate Urgent Overdue Grid
            var overdues = await _circulationService.GetOverdueLoansAsync();
            lblOverdueHeaderBadge.Text = overdues.Count switch
            {
                0 => "✅ All loans currently on schedule",
                1 => "⚠️ 1 overdue loan requires action",
                _ => $"⚠️ {overdues.Count} overdue loans require action"
            };

            dgvOverdueSummary.DataSource = overdues.Select(o => new
            {
                LoanId = o.Id,
                Book = o.BookTitle,
                Borrower = o.MemberName,
                DueDate = o.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DaysLate = $"{o.OverdueDays} days late",
                Fine = $"${o.FineAmount:F2}"
            }).ToList();

            if (dgvOverdueSummary.Columns["LoanId"] is { } colOverdueId)
            {
                colOverdueId.Visible = false; // Keep ID bound for double-click return dialog, but hide to display the 5 requested columns cleanly
            }
            if (dgvOverdueSummary.Columns["Book"] is { } colOverdueBook)
            {
                colOverdueBook.MinimumWidth = 280;
                colOverdueBook.FillWeight = 160;
                colOverdueBook.HeaderText = "Book Title";
            }
            if (dgvOverdueSummary.Columns["Borrower"] is { } colOverdueBorrower)
            {
                colOverdueBorrower.MinimumWidth = 190;
                colOverdueBorrower.FillWeight = 120;
                colOverdueBorrower.HeaderText = "Borrower";
            }
            if (dgvOverdueSummary.Columns["DueDate"] is { } colOverdueDue)
            {
                colOverdueDue.Width = 130;
                colOverdueDue.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colOverdueDue.HeaderText = "Due Date";
                colOverdueDue.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvOverdueSummary.Columns["DaysLate"] is { } colOverdueDays)
            {
                colOverdueDays.Width = 145;
                colOverdueDays.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colOverdueDays.HeaderText = "Overdue Days";
                colOverdueDays.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvOverdueSummary.Columns["Fine"] is { } colOverdueFine)
            {
                colOverdueFine.Width = 135;
                colOverdueFine.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colOverdueFine.HeaderText = "Fine Amount";
                colOverdueFine.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            dgvOverdueSummary.ClearSelection();
        }
        catch (Exception ex)
        {
            lblStatusText.Text = $"Error refreshing dashboard: {ex.Message}";
        }
    }

    private async Task RefreshBooksListAsync()
    {
        try
        {
            var query = txtBookSearch.Text;
            var category = cmbBookCategoryFilter.SelectedItem?.ToString();
            var availOnly = chkBookAvailableOnly.Checked;

            var books = await _bookService.SearchBooksAsync(query, category, availOnly);
            dgvBooks.DataSource = books.Select(b => new
            {
                b.Id,
                b.Title,
                b.Author,
                b.ISBN,
                b.Category,
                Year = b.PublicationYear,
                Available = b.CopiesAvailable,
                Total = b.TotalCopies,
                Rack = b.LocationRack
            }).ToList();

            EnsureBookActionsColumn();
            FitGridToViewport(dgvBooks);

            if (lblBookCountBadge != null)
                lblBookCountBadge.Text = $"({books.Count} book{(books.Count == 1 ? "" : "s")} found)";

            lblStatusText.Text = $"Showing {books.Count} book title(s).";
        }
        catch (Exception ex)
        {
            lblStatusText.Text = $"Error loading books: {ex.Message}";
        }
    }

    private async Task RefreshMembersListAsync()
    {
        try
        {
            var query = txtMemberSearch.Text;
            MembershipType? type = null;
            if (cmbMemberTypeFilter.SelectedIndex > 0 && Enum.TryParse<MembershipType>(cmbMemberTypeFilter.SelectedItem?.ToString(), out var parsedType))
            {
                type = parsedType;
            }
            var activeOnly = chkMemberActiveOnly.Checked;

            var members = await _memberService.SearchMembersAsync(query, type, activeOnly);
            dgvMembers.DataSource = members.Select(m => new
            {
                m.Id,
                m.FullName,
                m.Email,
                Phone = m.PhoneNumber,
                Type = m.MembershipType.ToString(),
                MaxBorrow = m.MaxBorrowLimit,
                Status = m.IsActive ? "Active" : "Inactive",
                MemberSince = m.MembershipDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }).ToList();

            dgvMembers.ScrollBars = ScrollBars.Both;

            if (dgvMembers.Columns["Id"] is { } colMemId)
            {
                colMemId.Width = 50;
                colMemId.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemId.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvMembers.Columns["FullName"] is { } colMemName)
            {
                colMemName.MinimumWidth = 170;
                colMemName.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemName.Width = 185;
                colMemName.FillWeight = 130;
                colMemName.HeaderText = "Full Name";
            }
            if (dgvMembers.Columns["Email"] is { } colMemEmail)
            {
                colMemEmail.MinimumWidth = 190;
                colMemEmail.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemEmail.Width = 235;
                colMemEmail.FillWeight = 140;
                colMemEmail.HeaderText = "Email Address";
            }
            if (dgvMembers.Columns["Phone"] is { } colMemPhone)
            {
                colMemPhone.MinimumWidth = 120;
                colMemPhone.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemPhone.Width = 150;
                colMemPhone.FillWeight = 90;
                colMemPhone.HeaderText = "Phone";
            }
            if (dgvMembers.Columns["Type"] is { } colMemType)
            {
                colMemType.MinimumWidth = 105;
                colMemType.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemType.Width = 135;
                colMemType.FillWeight = 85;
                colMemType.HeaderText = "Membership";
            }
            if (dgvMembers.Columns["MaxBorrow"] is { } colMemMax)
            {
                colMemMax.Width = 170;
                colMemMax.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemMax.HeaderText = "Max Books";
                colMemMax.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
                colMemMax.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvMembers.Columns["Status"] is { } colMemStatus)
            {
                colMemStatus.Width = 85;
                colMemStatus.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemStatus.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvMembers.Columns["MemberSince"] is { } colMemSince)
            {
                colMemSince.Width = 110;
                colMemSince.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colMemSince.HeaderText = "Joined Date";
                colMemSince.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            // Explicit widths prevent unreadable compressed cells.
            SetColumnWidth(dgvMembers, "Id", 65);
            SetColumnWidth(dgvMembers, "FullName", 205);
            SetColumnWidth(dgvMembers, "Email", 275);
            SetColumnWidth(dgvMembers, "Phone", 165);
            SetColumnWidth(dgvMembers, "Type", 135);
            SetColumnWidth(dgvMembers, "MaxBorrow", 170);
            SetColumnWidth(dgvMembers, "Status", 110);
            SetColumnWidth(dgvMembers, "MemberSince", 135);
            dgvMembers.ColumnHeadersVisible = true;
            FitGridToViewport(dgvMembers);

            if (lblMemberCountBadge != null)
                lblMemberCountBadge.Text = $"({members.Count} member{(members.Count == 1 ? "" : "s")})";

            lblStatusText.Text = $"Showing {members.Count} member(s).";
        }
        catch (Exception ex)
        {
            lblStatusText.Text = $"Error loading members: {ex.Message}";
        }
    }

    private async Task RefreshLoansListAsync()
    {
        try
        {
            var query = txtLoanSearch.Text;
            LoanStatus? status = cmbLoanStatusFilter.SelectedIndex switch
            {
                1 => LoanStatus.Active,
                2 => LoanStatus.Overdue,
                3 => LoanStatus.Returned,
                _ => null
            };

            var loans = await _circulationService.SearchLoansAsync(query, status);
            dgvLoans.DataSource = loans.Select(l => new
            {
                LoanId = l.Id,
                Book = l.BookTitle,
                ISBN = l.BookISBN,
                Borrower = l.MemberName,
                BorrowDate = l.BorrowDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DueDate = l.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReturnDate = l.ReturnDate.HasValue ? l.ReturnDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "-",
                Status = l.Status.ToString(),
                OverdueDays = l.OverdueDays,
                Fine = $"${l.FineAmount:F2}",
                FinePaid = l.FinePaid ? "Yes" : "No"
            }).ToList();

            if (dgvLoans.Columns["LoanId"] is { } colLoanId)
            {
                colLoanId.Width = 60;
                colLoanId.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanId.HeaderText = "ID";
                colLoanId.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvLoans.Columns["Book"] is { } colLoanBook)
            {
                colLoanBook.MinimumWidth = 200;
                colLoanBook.FillWeight = 150;
                colLoanBook.HeaderText = "Book Title";
            }
            if (dgvLoans.Columns["ISBN"] is { } colLoanIsbn)
            {
                colLoanIsbn.MinimumWidth = 120;
                colLoanIsbn.FillWeight = 85;
            }
            if (dgvLoans.Columns["Borrower"] is { } colLoanBorrower)
            {
                colLoanBorrower.MinimumWidth = 150;
                colLoanBorrower.FillWeight = 110;
                colLoanBorrower.HeaderText = "Borrower Name";
            }
            if (dgvLoans.Columns["BorrowDate"] is { } colLoanBorrowDate)
            {
                colLoanBorrowDate.Width = 100;
                colLoanBorrowDate.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanBorrowDate.HeaderText = "Borrowed";
                colLoanBorrowDate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvLoans.Columns["DueDate"] is { } colLoanDueDate)
            {
                colLoanDueDate.Width = 100;
                colLoanDueDate.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanDueDate.HeaderText = "Due Date";
                colLoanDueDate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvLoans.Columns["ReturnDate"] is { } colLoanReturnDate)
            {
                colLoanReturnDate.Width = 100;
                colLoanReturnDate.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanReturnDate.HeaderText = "Returned";
                colLoanReturnDate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvLoans.Columns["Status"] is { } colLoanStatus)
            {
                colLoanStatus.Width = 90;
                colLoanStatus.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanStatus.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvLoans.Columns["OverdueDays"] is { } colLoanOverdue)
            {
                colLoanOverdue.Width = 85;
                colLoanOverdue.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanOverdue.HeaderText = "Days Late";
                colLoanOverdue.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgvLoans.Columns["Fine"] is { } colLoanFine)
            {
                colLoanFine.Width = 85;
                colLoanFine.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanFine.HeaderText = "Fine ($)";
                colLoanFine.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (dgvLoans.Columns["FinePaid"] is { } colLoanPaid)
            {
                colLoanPaid.Width = 80;
                colLoanPaid.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colLoanPaid.HeaderText = "Paid?";
                colLoanPaid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            // Date columns have room for the full ISO date: yyyy-MM-dd.
            SetColumnWidth(dgvLoans, "LoanId", 65);
            SetColumnWidth(dgvLoans, "Book", 275);
            SetColumnWidth(dgvLoans, "ISBN", 160);
            SetColumnWidth(dgvLoans, "Borrower", 185);
            SetColumnWidth(dgvLoans, "BorrowDate", 135);
            SetColumnWidth(dgvLoans, "DueDate", 135);
            SetColumnWidth(dgvLoans, "ReturnDate", 135);
            SetColumnWidth(dgvLoans, "Status", 115);
            SetColumnWidth(dgvLoans, "OverdueDays", 110);
            SetColumnWidth(dgvLoans, "Fine", 105);
            SetColumnWidth(dgvLoans, "FinePaid", 95);
            dgvLoans.ColumnHeadersVisible = true;
            FitGridToViewport(dgvLoans);

            if (lblLoanCountBadge != null)
                lblLoanCountBadge.Text = $"({loans.Count} loan record{(loans.Count == 1 ? "" : "s")})";

            lblStatusText.Text = $"Showing {loans.Count} loan record(s).";
        }
        catch (Exception ex)
        {
            lblStatusText.Text = $"Error loading circulation: {ex.Message}";
        }
    }

    private async Task LoadExecutiveReportAsync()
    {
        try
        {
            lblStatusText.Text = "Generating analytics and executive report...";
            var reportText = await _reportService.GenerateSummaryReportTextAsync();
            txtReportOutput.Text = reportText;
            txtReportOutput.SelectionStart = 0;
            txtReportOutput.SelectionLength = 0;
            txtReportOutput.ScrollToCaret();

            var stats = await _reportService.GetStatisticsAsync();
            dgvTopBooks.DataSource = stats.TopBorrowedBooks.Select(t => new
            {
                t.Title,
                t.Author,
                t.Category,
                TotalBorrows = t.BorrowCount
            }).ToList();

            if (dgvTopBooks.Columns["Title"] is { } colTopTitle)
            {
                colTopTitle.MinimumWidth = 200;
                colTopTitle.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colTopTitle.Width = 390;
                colTopTitle.FillWeight = 150;
            }
            if (dgvTopBooks.Columns["Author"] is { } colTopAuthor)
            {
                colTopAuthor.MinimumWidth = 140;
                colTopAuthor.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colTopAuthor.Width = 240;
                colTopAuthor.FillWeight = 110;
            }
            if (dgvTopBooks.Columns["Category"] is { } colTopCat)
            {
                colTopCat.MinimumWidth = 120;
                colTopCat.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colTopCat.Width = 200;
                colTopCat.FillWeight = 90;
            }
            if (dgvTopBooks.Columns["TotalBorrows"] is { } colTopBorrows)
            {
                colTopBorrows.Width = 190;
                colTopBorrows.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colTopBorrows.HeaderText = "Times Borrowed";
                colTopBorrows.MinimumWidth = 190;
                colTopBorrows.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
                colTopBorrows.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            FitGridToViewport(dgvTopBooks);
            lblStatusText.Text = "Report updated successfully.";
        }
        catch (Exception ex)
        {
            lblStatusText.Text = $"Error generating report: {ex.Message}";
        }
    }

    private async Task RunBackgroundScanAsync()
    {
        try
        {
            int updated = await _circulationService.RecalculateOverdueFinesAsync();
            if (updated > 0)
            {
                await RefreshDashboardAsync();
                lblStatusText.Text = $"[Background Worker] Auto-updated {updated} overdue loan fines at {DateTime.Now:HH:mm:ss}.";
            }
        }
        catch
        {
            // Background scan failure should not disrupt user experience
        }
    }
    #endregion

    #region Dialog Openers & Actions
    private async Task OpenAddBookDialogAsync()
    {
        using var dlg = new BookDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                await _bookService.AddBookAsync(dlg.Book);
                MessageBox.Show($"Book '{dlg.Book.Title}' registered successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadCategoriesDropdownAsync();
                await RefreshBooksListAsync();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Adding Book", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task EditSelectedBookAsync()
    {
        if (dgvBooks.SelectedRows.Count == 0)
        {
            MessageBox.Show("Please select a book from the list to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int bookId = Convert.ToInt32(dgvBooks.SelectedRows[0].Cells["Id"].Value);
        var book = await _bookService.GetBookByIdAsync(bookId);
        if (book == null) return;

        using var dlg = new BookDialog(book);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                await _bookService.UpdateBookAsync(dlg.Book);
                MessageBox.Show("Book updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshBooksListAsync();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Updating Book", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task DeleteSelectedBookAsync()
    {
        if (dgvBooks.SelectedRows.Count == 0)
        {
            MessageBox.Show("Please select a book to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int bookId = Convert.ToInt32(dgvBooks.SelectedRows[0].Cells["Id"].Value);
        string title = dgvBooks.SelectedRows[0].Cells["Title"].Value?.ToString() ?? "Book";

        var confirm = MessageBox.Show($"Are you sure you want to delete book '{title}'?\nThis action cannot be undone.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm == DialogResult.Yes)
        {
            try
            {
                await _bookService.DeleteBookAsync(bookId);
                MessageBox.Show("Book deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshBooksListAsync();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot Delete Book", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task OpenAddMemberDialogAsync()
    {
        using var dlg = new MemberDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                await _memberService.AddMemberAsync(dlg.Member);
                MessageBox.Show($"Member '{dlg.Member.FullName}' registered successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshMembersListAsync();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Registering Member", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task EditSelectedMemberAsync()
    {
        if (dgvMembers.SelectedRows.Count == 0)
        {
            MessageBox.Show("Please select a member from the list to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int memberId = Convert.ToInt32(dgvMembers.SelectedRows[0].Cells["Id"].Value);
        var member = await _memberService.GetMemberByIdAsync(memberId);
        if (member == null) return;

        using var dlg = new MemberDialog(member);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                await _memberService.UpdateMemberAsync(dlg.Member);
                MessageBox.Show("Member profile updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshMembersListAsync();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Updating Member", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task ToggleSelectedMemberStatusAsync()
    {
        if (dgvMembers.SelectedRows.Count == 0) return;

        int memberId = Convert.ToInt32(dgvMembers.SelectedRows[0].Cells["Id"].Value);
        await _memberService.ToggleMemberStatusAsync(memberId);
        await RefreshMembersListAsync();
    }

    private async Task DeleteSelectedMemberAsync()
    {
        if (dgvMembers.SelectedRows.Count == 0) return;

        int memberId = Convert.ToInt32(dgvMembers.SelectedRows[0].Cells["Id"].Value);
        string name = dgvMembers.SelectedRows[0].Cells["FullName"].Value?.ToString() ?? "Member";

        var confirm = MessageBox.Show($"Are you sure you want to delete member '{name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm == DialogResult.Yes)
        {
            try
            {
                await _memberService.DeleteMemberAsync(memberId);
                MessageBox.Show("Member deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshMembersListAsync();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot Delete Member", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task OpenBorrowDialogForBookAsync(int bookId)
    {
        await OpenBorrowDialogAsync(bookId);
    }

    private async Task OpenBorrowDialogAsync(int? preselectedBookId = null)
    {
        if (_isBorrowInProgress) return;
        _isBorrowInProgress = true;

        try
        {
            using var dlg = new BorrowDialog(_bookService, _memberService, _circulationService, preselectedBookId);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                await RefreshLoansListAsync();
                await RefreshBooksListAsync();
                await RefreshDashboardAsync();
            }
        }
        finally
        {
            _isBorrowInProgress = false;
        }
    }

    private void EnsureBookActionsColumn()
    {
        // Display-only sequence number. Database IDs remain unchanged for all operations.
        if (!dgvBooks.Columns.Contains("colRowNumber"))
        {
            var sequence = new DataGridViewTextBoxColumn
            {
                Name = "colRowNumber",
                HeaderText = "No.",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Width = 58,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            sequence.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvBooks.Columns.Add(sequence);
        }
        dgvBooks.Columns["colRowNumber"].DisplayIndex = 0;

        if (!dgvBooks.Columns.Contains("colActions"))
        {
            var colActions = new DataGridViewButtonColumn
            {
                Name = "colActions",
                HeaderText = "Actions",
                Text = "Borrow",
                UseColumnTextForButtonValue = false,
                Width = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            colActions.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvBooks.Columns.Add(colActions);
        }

        if (dgvBooks.Columns["colActions"] is { } colActionsExt)
        {
            colActionsExt.DisplayIndex = dgvBooks.Columns.Count - 1;
        }

        if (dgvBooks.Columns["Id"] is { } colId)
        {
            // Actual book key is retained in the bound row, but hidden from the catalog UI.
            colId.Visible = false;
            colId.Width = 45;
            colId.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colId.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colId.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (dgvBooks.Columns["Title"] is { } colTitle)
        {
            colTitle.MinimumWidth = 220;
            colTitle.FillWeight = 160;
        }
        if (dgvBooks.Columns["Author"] is { } colAuthor)
        {
            colAuthor.MinimumWidth = 140;
            colAuthor.FillWeight = 110;
        }
        if (dgvBooks.Columns["ISBN"] is { } colIsbn)
        {
            colIsbn.MinimumWidth = 125;
            colIsbn.FillWeight = 85;
        }
        if (dgvBooks.Columns["Category"] is { } colCat)
        {
            colCat.MinimumWidth = 125;
            colCat.FillWeight = 90;
        }
        if (dgvBooks.Columns["Year"] is { } colYear)
        {
            colYear.Width = 65;
            colYear.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colYear.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colYear.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (dgvBooks.Columns["Available"] is { } colAvail)
        {
            colAvail.Width = 115;
            colAvail.HeaderText = "Available";
            colAvail.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colAvail.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
            colAvail.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colAvail.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (dgvBooks.Columns["Total"] is { } colTotal)
        {
            colTotal.Width = 65;
            colTotal.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colTotal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colTotal.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (dgvBooks.Columns["Rack"] is { } colRack)
        {
            colRack.Width = 85;
            colRack.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colRack.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colRack.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
    }

    private void DgvBooks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvBooks.Columns[e.ColumnIndex].Name == "colActions")
        {
            if (dgvBooks.Rows[e.RowIndex].Cells["Available"]?.Value is int avail)
            {
                e.Value = avail > 0 ? "Borrow" : "Unavailable";
                e.FormattingApplied = true;
            }
        }
    }

    private void DgvBooks_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics == null) return;
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvBooks.Columns[e.ColumnIndex].Name == "colActions")
        {
            e.PaintBackground(e.ClipBounds, true);

            int avail = 0;
            if (dgvBooks.Rows[e.RowIndex].Cells["Available"]?.Value is int val)
            {
                avail = val;
            }

            var btnRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + 3, e.CellBounds.Width - 16, e.CellBounds.Height - 6);

            if (avail > 0)
            {
                // Active Emerald Green Button
                using var brush = new SolidBrush(Color.FromArgb(16, 185, 129));
                e.Graphics.FillRectangle(brush, btnRect);

                using var pen = new Pen(Color.FromArgb(5, 150, 105), 1f);
                e.Graphics.DrawRectangle(pen, btnRect);

                TextRenderer.DrawText(
                    e.Graphics,
                    "📖 Borrow",
                    _actionButtonFont,
                    btnRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                // Disabled Gray Button for Unavailable
                using var brush = new SolidBrush(Color.FromArgb(241, 245, 249));
                e.Graphics.FillRectangle(brush, btnRect);

                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
                e.Graphics.DrawRectangle(pen, btnRect);

                TextRenderer.DrawText(
                    e.Graphics,
                    "Unavailable",
                    _actionButtonFont,
                    btnRect,
                    Color.FromArgb(148, 163, 184),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            e.Handled = true;
        }
    }

    private void DgvBooks_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvBooks.Columns[e.ColumnIndex].Name == "colActions")
        {
            int avail = 0;
            if (dgvBooks.Rows[e.RowIndex].Cells["Available"]?.Value is int val)
                avail = val;

            dgvBooks.Cursor = avail > 0 ? Cursors.Hand : Cursors.Default;
        }
        else
        {
            dgvBooks.Cursor = Cursors.Default;
        }
    }

    private async Task DgvBooks_CellClickAsync(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        if (dgvBooks.Columns[e.ColumnIndex].Name == "colActions")
        {
            var row = dgvBooks.Rows[e.RowIndex];
            if (row.Cells["Id"]?.Value is int bookId)
            {
                int avail = 0;
                if (row.Cells["Available"]?.Value is int val)
                    avail = val;

                if (avail <= 0)
                {
                    var bookTitle = row.Cells["Title"]?.Value?.ToString() ?? "This book";
                    MessageBox.Show(
                        $"\"{bookTitle}\" currently has 0 available copies in stock and cannot be borrowed.",
                        "Book Unavailable",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                await OpenBorrowDialogForBookAsync(bookId);
            }
        }
    }

    private async Task OpenReturnDialogForSelectedLoanAsync()
    {
        if (dgvLoans.SelectedRows.Count == 0)
        {
            MessageBox.Show("Please select an active loan to return.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int loanId = Convert.ToInt32(dgvLoans.SelectedRows[0].Cells["LoanId"].Value);
        var loan = await _loanRepo.GetByIdAsync(loanId);
        if (loan == null) return;

        if (loan.IsReturned)
        {
            MessageBox.Show("This book has already been marked as returned.", "Already Returned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new ReturnDialog(loan, _circulationService, _fineCalculator, _memberService);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshLoansListAsync();
            await RefreshBooksListAsync();
            await RefreshDashboardAsync();
        }
    }

    private async Task ExportBooksCsvAsync()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"Books_Catalog_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            await _reportService.ExportBooksToCsvAsync(sfd.FileName);
            MessageBox.Show("Books catalog exported to CSV successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task ExportLoansCsvAsync()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"Loans_Report_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            await _reportService.ExportLoansToCsvAsync(sfd.FileName);
            MessageBox.Show("Loans report exported to CSV successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task ExportSummaryTextAsync()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Text Report (*.txt)|*.txt",
            FileName = $"Library_Executive_Summary_{DateTime.Now:yyyyMMdd}.txt"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            await File.WriteAllTextAsync(sfd.FileName, txtReportOutput.Text);
            MessageBox.Show("Executive report text file saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    #endregion

    private void MainForm_Load(object sender, EventArgs e)
    {

    }
}
