using LibraryManagementSystem.Data;
using LibraryManagementSystem.UI;

namespace LibraryManagementSystem;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Global Exception Handling (Requirement 9: Exception Handling)
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        
        Application.ThreadException += (sender, args) =>
        {
            MessageBox.Show(
                $"An unexpected application error occurred:\n\n{args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}",
                "Library System Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(
                $"A critical system exception occurred:\n\n{ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}",
                "Critical Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Stop);
        };

        ApplicationConfiguration.Initialize();

        // 1. Ensure SQLite database schema & seed users exist before login
        var dbContext = new DatabaseContext("library.db");
        var dbInit = new DatabaseInitializer(dbContext);
        dbInit.InitializeAsync().GetAwaiter().GetResult();

        // 2. Main Login & Application Loop (supports Logout and Re-login)
        while (true)
        {
            using var loginForm = new LoginForm(dbContext);
            var result = loginForm.ShowDialog();

            if (result != DialogResult.OK || loginForm.AuthenticatedUser == null)
            {
                break; // User cancelled or closed the login window
            }

            using var mainForm = new MainForm(loginForm.AuthenticatedUser);
            Application.Run(mainForm);

            if (!mainForm.IsLoggedOut)
            {
                break; // User closed MainForm normally (X button)
            }
        }
    }
}
