using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.UI;

public partial class LoginForm : Form
{
    private readonly DatabaseContext _dbContext;
    private readonly IAuthService _authService;

    public User? AuthenticatedUser { get; private set; }

    public LoginForm(DatabaseContext? dbContext = null)
    {
        InitializeComponent();
        _dbContext = dbContext ?? new DatabaseContext("library.db");
        var userRepo = new UserRepository(_dbContext);
        _authService = new AuthService(userRepo);

        txtUsername.KeyDown += OnInputKeyDown;
        txtPassword.KeyDown += OnInputKeyDown;
        chkShowPassword.CheckedChanged += (s, e) =>
            txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
        btnLogin.Click += async (s, e) => await PerformLoginAsync();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        lblError.Visible = false;
    }

    private async Task PerformLoginAsync()
    {
        var username = txtUsername.Text.Trim();
        var password = txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username or email.");
            txtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password.");
            txtPassword.Focus();
            return;
        }

        btnLogin.Enabled = false;
        btnLogin.Text = "Verifying...";
        lblError.Visible = false;

        try
        {
            var user = await _authService.LoginAsync(username, password);
            if (user == null)
            {
                ShowError("Invalid username or password. Please try again.");
                txtPassword.SelectAll();
                txtPassword.Focus();
            }
            else
            {
                AuthenticatedUser = user;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Login error: {ex.Message}");
        }
        finally
        {
            btnLogin.Enabled = true;
            btnLogin.Text = "🔓  Log In / تسجيل الدخول";
        }
    }

    private void ShowError(string msg)
    {
        lblError.Text = $"❌  {msg}";
        lblError.Visible = true;
    }
}
