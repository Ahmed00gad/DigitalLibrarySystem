using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.UI.Dialogs;

public partial class BookDialog : Form
{
    private readonly Book _book;
    private readonly bool _isEdit;

    private TextBox txtTitle = null!;
    private TextBox txtAuthor = null!;
    private TextBox txtISBN = null!;
    private ComboBox cmbCategory = null!;
    private NumericUpDown numYear = null!;
    private NumericUpDown numTotalCopies = null!;
    private NumericUpDown numAvailableCopies = null!;
    private TextBox txtLocation = null!;
    private TextBox txtDescription = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;
    private ErrorProvider errorProvider = null!;

    public Book Book => _book;

    public BookDialog() : this(null) { }

    public BookDialog(Book? bookToEdit)
    {
        _isEdit = bookToEdit != null;
        _book = bookToEdit ?? new Book();

        InitializeComponent();
        Text = _isEdit ? "Edit Book" : "Add New Book";
        headerTitle.Text = _isEdit ? "Edit Book Details" : "Register New Book";
        btnSave.Text = _isEdit ? "Update Book" : "Add Book";
        btnSave.Click += BtnSave_Click;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        numTotalCopies.ValueChanged += (s, e) =>
        {
            if (!_isEdit) numAvailableCopies.Value = numTotalCopies.Value;
            if (numAvailableCopies.Value > numTotalCopies.Value) numAvailableCopies.Value = numTotalCopies.Value;
        };
        numYear.Maximum = DateTime.Now.Year + 1;
        PopulateFields();
    }

    private void PopulateFields()
    {
        if (_isEdit)
        {
            txtTitle.Text = _book.Title;
            txtAuthor.Text = _book.Author;
            txtISBN.Text = _book.ISBN;
            cmbCategory.Text = _book.Category;
            numYear.Value = Math.Max(1000, Math.Min(DateTime.Now.Year + 1, _book.PublicationYear));
            numTotalCopies.Value = Math.Clamp(_book.TotalCopies, (int)numTotalCopies.Minimum, (int)numTotalCopies.Maximum);
            numAvailableCopies.Value = Math.Clamp(_book.CopiesAvailable, (int)numAvailableCopies.Minimum, (int)numAvailableCopies.Maximum);
            txtLocation.Text = _book.LocationRack;
            txtDescription.Text = _book.Description;
        }
        else
        {
            cmbCategory.SelectedIndex = 0;
            numYear.Value = DateTime.Now.Year;
            numTotalCopies.Value = 1;
            numAvailableCopies.Value = 1;
            txtLocation.Text = "A-1";
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        errorProvider.Clear();
        bool valid = true;

        if (string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            errorProvider.SetError(txtTitle, "Title is required");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(txtAuthor.Text))
        {
            errorProvider.SetError(txtAuthor, "Author is required");
            valid = false;
        }

        if (!string.IsNullOrWhiteSpace(txtISBN.Text) &&
    !BookService.IsValidIsbn(txtISBN.Text.Trim()))
        {
            errorProvider.SetError(txtISBN, "Invalid ISBN format (10 or 13 digits)");
            valid = false;
        }
        if (string.IsNullOrWhiteSpace(cmbCategory.Text))
        {
            errorProvider.SetError(cmbCategory, "Category is required");
            valid = false;
        }

        if (numAvailableCopies.Value > numTotalCopies.Value)
        {
            errorProvider.SetError(numAvailableCopies, "Available copies cannot exceed total copies");
            valid = false;
        }

        if (_isEdit && (int)numTotalCopies.Value - (int)numAvailableCopies.Value != _book.TotalCopies - _book.CopiesAvailable)
        {
            errorProvider.SetError(numAvailableCopies, "Changing the number of checked-out copies is not allowed here. Return or issue loans through Circulation.");
            valid = false;
        }

        if (!valid) return;

        _book.Title = txtTitle.Text.Trim();
        _book.Author = txtAuthor.Text.Trim();
        _book.ISBN = txtISBN.Text.Trim();
        _book.Category = cmbCategory.Text.Trim();
        _book.PublicationYear = (int)numYear.Value;
        _book.TotalCopies = (int)numTotalCopies.Value;
        _book.CopiesAvailable = (int)numAvailableCopies.Value;
        _book.LocationRack = txtLocation.Text.Trim();
        _book.Description = txtDescription.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}
