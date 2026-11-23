using System.Text.RegularExpressions;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.UI.Dialogs;

public partial class MemberDialog : Form
{
    private readonly Member _member;
    private readonly bool _isEdit;

    private ErrorProvider errorProvider = null!;

    public Member Member => _member;

    public MemberDialog(Member? memberToEdit = null)
    {
        _isEdit = memberToEdit != null;
        _member = memberToEdit ?? new Member();

        InitializeComponent();
        Text = _isEdit ? "Edit Member" : "Register New Member";
        lblDialogTitle.Text = _isEdit ? "Edit Member Profile" : "Register New Member";
        btnSave.Text = _isEdit ? "Save Changes" : "Register Member";
        errorProvider = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink };
        cmbType.Items.AddRange(Enum.GetNames(typeof(MembershipType)));
        cmbType.SelectedIndexChanged += (_, _) => {
            if (!_isEdit && Enum.TryParse<MembershipType>(cmbType.SelectedItem?.ToString(), out var type))
                numBorrowLimit.Value = Math.Clamp(Member.GetDefaultLimit(type), (int)numBorrowLimit.Minimum, (int)numBorrowLimit.Maximum);
        };
        btnSave.Click += BtnSave_Click;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        PopulateFields();
    }



    private void PopulateFields()
    {
        if (_isEdit)
        {
            txtFullName.Text = _member.FullName;
            txtEmail.Text = _member.Email;
            txtPhone.Text = _member.PhoneNumber;
            cmbType.SelectedItem = _member.MembershipType.ToString();
            numBorrowLimit.Value = Math.Clamp(_member.MaxBorrowLimit, (int)numBorrowLimit.Minimum, (int)numBorrowLimit.Maximum);
            chkIsActive.Checked = _member.IsActive;
        }
        else
        {
            cmbType.SelectedItem = MembershipType.Regular.ToString();
            numBorrowLimit.Value = Member.GetDefaultLimit(MembershipType.Regular);
            chkIsActive.Checked = true;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        errorProvider.Clear();
        bool valid = true;

        if (string.IsNullOrWhiteSpace(txtFullName.Text))
        {
            errorProvider.SetError(txtFullName, "Full name is required");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(txtEmail.Text))
        {
            errorProvider.SetError(txtEmail, "Email address is required");
            valid = false;
        }
        else if (!Regex.IsMatch(txtEmail.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            errorProvider.SetError(txtEmail, "Invalid email format (e.g. user@domain.com)");
            valid = false;
        }

        if (!valid) return;

        _member.FullName = txtFullName.Text.Trim();
        _member.Email = txtEmail.Text.Trim();
        _member.PhoneNumber = txtPhone.Text.Trim();
        if (Enum.TryParse<MembershipType>(cmbType.SelectedItem?.ToString(), out var type))
        {
            _member.MembershipType = type;
        }
        _member.MaxBorrowLimit = (int)numBorrowLimit.Value;
        _member.IsActive = chkIsActive.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
