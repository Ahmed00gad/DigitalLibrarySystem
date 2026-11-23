using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.UI.Dialogs;

public partial class BorrowDialog : Form
{
    private readonly IBookService _bookService;
    private readonly IMemberService _memberService;
    private readonly ICirculationService _circulationService;
    private readonly int? _preselectedBookId;


    private List<Member> _members = new();
    private List<Book> _books = new();

    public Loan? CreatedLoan { get; private set; }

    public BorrowDialog(
        IBookService bookService,
        IMemberService memberService,
        ICirculationService circulationService,
        int? preselectedBookId = null)
    {
        _bookService = bookService;
        _memberService = memberService;
        _circulationService = circulationService;
        _preselectedBookId = preselectedBookId;

        InitializeComponent();
        ConfigureLoanDates();
        cmbMembers.SelectedIndexChanged += CmbMembers_SelectedIndexChanged;
        cmbBooks.SelectedIndexChanged += CmbBooks_SelectedIndexChanged;
        btnBorrow.Click += BtnBorrow_Click;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnEnglishCalendar.Click += (_, _) => ShowEnglishCalendar();
        Shown += async (s, e) => await LoadDataAsync();
    }




    private void ConfigureLoanDates()
    {
        dtpDueDate.MinDate = DateTime.Today.AddDays(1);
        dtpDueDate.MaxDate = DateTime.Today.AddDays(90);
        dtpDueDate.Value = DateTime.Today.AddDays(14);
        void UpdateNotice() => lblDueNotice.Text =
            $"Return by: {dtpDueDate.Value.ToString("dddd, dd MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US"))}  |  No fine if returned on time";
        bool syncing = false;
        numDuration.ValueChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            try
            {
                DateTime target = DateTime.Today.AddDays((double)numDuration.Value);
                if (target >= dtpDueDate.MinDate && target <= dtpDueDate.MaxDate)
                    dtpDueDate.Value = target;
            }
            finally { syncing = false; }
        };
        dtpDueDate.ValueChanged += (_, _) =>
        {
            UpdateNotice();
            if (syncing) return;
            syncing = true;
            try
            {
                int days = (dtpDueDate.Value.Date - DateTime.Today).Days;
                if (days >= numDuration.Minimum && days <= numDuration.Maximum)
                    numDuration.Value = days;
            }
            finally { syncing = false; }
        };
        UpdateNotice();
    }

    // The native Windows date dropdown follows the operating system language.
    // This entirely managed calendar uses explicit English month and weekday labels.
    private void ShowEnglishCalendar()
    {
        var en = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        DateTime selected = dtpDueDate.Value.Date;
        DateTime visible = new DateTime(selected.Year, selected.Month, 1);
        using var dialog = new Form
        {
            Text = "Choose Due Date",
            // A DPI-independent client area prevents the day grid from being clipped at 150% scaling.
            ClientSize = new Size(595, 555),
            MinimumSize = new Size(610, 595),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            AutoScaleMode = AutoScaleMode.None
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        var prev = new Button { Text = "<", Dock = DockStyle.Fill };
        var next = new Button { Text = ">", Dock = DockStyle.Fill };
        var title = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11, FontStyle.Bold) };
        top.Controls.Add(prev, 0, 0);
        top.Controls.Add(title, 1, 0);
        top.Controls.Add(next, 2, 0);
        root.Controls.Add(top, 0, 0);

        var calendar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 7,
            Margin = new Padding(0, 10, 0, 8)
        };
        for (int i = 0; i < 7; i++)
            calendar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        for (int i = 0; i < 7; i++)
            calendar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7));
        root.Controls.Add(calendar, 0, 1);

        var footer = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Allowed dates: {dtpDueDate.MinDate.ToString("dd MMM yyyy", en)} - {dtpDueDate.MaxDate.ToString("dd MMM yyyy", en)}",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 9f),
            AutoEllipsis = true
        };
        root.Controls.Add(footer, 0, 2);
        dialog.Controls.Add(root);

        void Refresh()
        {
            title.Text = visible.ToString("MMMM yyyy", en);
            calendar.SuspendLayout();
            calendar.Controls.Clear();
            string[] weekdays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            for (int col = 0; col < 7; col++)
                calendar.Controls.Add(new Label
                {
                    Text = weekdays[col], Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                }, col, 0);
            int start = (int)visible.DayOfWeek;
            int count = DateTime.DaysInMonth(visible.Year, visible.Month);
            for (int day = 1; day <= count; day++)
            {
                int cell = start + day - 1;
                DateTime date = visible.AddDays(day - 1);
                var dayButton = new Button
                {
                    Text = day.ToString(en),
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2),
                    Font = new Font("Segoe UI", 10f),
                    FlatStyle = FlatStyle.Flat,
                    Enabled = date >= dtpDueDate.MinDate.Date && date <= dtpDueDate.MaxDate.Date,
                    BackColor = date == selected ? Color.FromArgb(199, 210, 254) : Color.White
                };
                dayButton.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
                dayButton.Click += (s, e) =>
                {
                    dtpDueDate.Value = date;
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };
                calendar.Controls.Add(dayButton, cell % 7, cell / 7 + 1);
            }
            calendar.ResumeLayout();
            prev.Enabled = visible > new DateTime(dtpDueDate.MinDate.Year, dtpDueDate.MinDate.Month, 1);
            next.Enabled = visible < new DateTime(dtpDueDate.MaxDate.Year, dtpDueDate.MaxDate.Month, 1);
        }
        prev.Click += (s, e) => { visible = visible.AddMonths(-1); Refresh(); };
        next.Click += (s, e) => { visible = visible.AddMonths(1); Refresh(); };
        Refresh();
        dialog.ShowDialog(this);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            btnBorrow.Enabled = false;
            _members = (await _memberService.SearchMembersAsync(null, null, activeOnly: true)).ToList();
            _books = (await _bookService.SearchBooksAsync(null, null, availableOnly: false)).ToList();

            // Populate Members
            cmbMembers.Items.Clear();
            foreach (var m in _members)
            {
                cmbMembers.Items.Add($"{m.FullName} ({m.MembershipType}) - {m.Email}");
            }
            if (cmbMembers.Items.Count > 0) cmbMembers.SelectedIndex = 0;

            // Populate Books
            cmbBooks.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < _books.Count; i++)
            {
                var b = _books[i];
                string availStatus = b.CopiesAvailable > 0 ? $"{b.CopiesAvailable} copies avail" : "UNAVAILABLE (0 copies)";
                cmbBooks.Items.Add($"\"{b.Title}\" [{availStatus}] - {b.ISBN}");

                if (_preselectedBookId.HasValue && b.Id == _preselectedBookId.Value)
                {
                    selectedIndex = i;
                }
            }

            if (cmbBooks.Items.Count > 0)
            {
                cmbBooks.SelectedIndex = selectedIndex;
                UpdateSelectedBookUi();
            }
        }
        catch (Exception ex)
        {
            btnBorrow.Enabled = false;
            MessageBox.Show($"Error loading data: {ex.Message}", "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CmbMembers_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbMembers.SelectedIndex >= 0 && cmbMembers.SelectedIndex < _members.Count)
        {
            var m = _members[cmbMembers.SelectedIndex];
            lblMemberInfo.Text = $"Type: {m.MembershipType}  |  Max: {m.MaxBorrowLimit} books\nMember since: {m.MembershipDate:yyyy-MM-dd}";
        }
    }

    private void CmbBooks_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateSelectedBookUi();
    }

    private void UpdateSelectedBookUi()
    {
        if (cmbBooks.SelectedIndex >= 0 && cmbBooks.SelectedIndex < _books.Count)
        {
            var b = _books[cmbBooks.SelectedIndex];
            if (b.CopiesAvailable <= 0)
            {
                lblBookInfo.Text = $"⚠️ UNAVAILABLE: \"{b.Title}\" has 0 copies available for loan.";
                lblBookInfo.ForeColor = Color.FromArgb(239, 68, 68); // Red
                btnBorrow.Enabled = false;
            }
            else
            {
                lblBookInfo.Text = $"Available: {b.CopiesAvailable} of {b.TotalCopies} copies\nShelf: {b.LocationRack}  |  Category: {b.Category}";
                lblBookInfo.ForeColor = Color.FromArgb(16, 185, 129); // Green
                btnBorrow.Enabled = true;
            }
        }
    }

    private async void BtnBorrow_Click(object? sender, EventArgs e)
    {
        if (cmbMembers.SelectedIndex < 0 || cmbMembers.SelectedIndex >= _members.Count)
        {
            MessageBox.Show("Please select a borrower member.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (cmbBooks.SelectedIndex < 0 || cmbBooks.SelectedIndex >= _books.Count)
        {
            MessageBox.Show("Please select an available book to borrow.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedMember = _members[cmbMembers.SelectedIndex];
        var selectedBook = _books[cmbBooks.SelectedIndex];

        if (selectedBook.CopiesAvailable <= 0)
        {
            MessageBox.Show($"Book '{selectedBook.Title}' currently has 0 available copies in stock.", "Book Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Prevent duplicate submissions (Requirement 10)
        btnBorrow.Enabled = false;

        try
        {
            CreatedLoan = await _circulationService.BorrowBookAsync(
                selectedBook.Id,
                selectedMember.Id,
                (int)numDuration.Value,
                txtNotes.Text.Trim()
            );

            MessageBox.Show(
                $"Book successfully borrowed!\n\n" +
                $"📖 Book: \"{selectedBook.Title}\"\n" +
                $"👤 Borrower: {selectedMember.FullName} ({selectedMember.MembershipType})\n" +
                $"📅 Due Date: {CreatedLoan.DueDate.ToString("dddd, dd MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US"))}\n\n" +
                $"The book's available copies have been decreased by 1.",
                "Borrowing Successful",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot Issue Loan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            btnBorrow.Enabled = true;
        }
    }
}
