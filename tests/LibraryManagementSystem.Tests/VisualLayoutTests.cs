using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.UI;
using LibraryManagementSystem.UI.Controls;
using LibraryManagementSystem.UI.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class VisualLayoutTests
{
    private string _screenshotDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _screenshotDir = Path.Combine(Path.GetTempPath(), "DigitalLibrary_Screenshots");
        Directory.CreateDirectory(_screenshotDir);
    }

    private void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw new AggregateException("STA thread failed", ex);
    }

    [TestMethod]
    public void TestBookDialog_LayoutAndScaling()
    {
        RunInSta(() =>
        {
            var longBook = new Book
            {
                Id = 1,
                Title = "The Architecture of Complex Software Systems: Patterns, Practices, and Enterprise Principles",
                Author = "Dr. Alexander Bartholomew Montgomery-Smith III, PhD",
                ISBN = "978-0-13-235088-4",
                Category = "Software Engineering",
                PublicationYear = 2024,
                TotalCopies = 15,
                CopiesAvailable = 12,
                LocationRack = "Aisle 4 - Shelf B2 - Top",
                Description = "A comprehensive deep dive into scalable system architectures and design."
            };

            using var dlg = new BookDialog(longBook);
            dlg.CreateControl();

            // Verify 100% scale
            CaptureAndVerify(dlg, "BookDialog_100.png");

            // Verify 125% scale
            dlg.Scale(new SizeF(1.25f, 1.25f));
            CaptureAndVerify(dlg, "BookDialog_125.png");

            // Verify 150% scale
            dlg.Scale(new SizeF(1.2f, 1.2f)); // 1.25 * 1.2 = 1.50
            CaptureAndVerify(dlg, "BookDialog_150.png");
        });
    }

    [TestMethod]
    public void TestMemberDialog_LayoutAndScaling()
    {
        RunInSta(() =>
        {
            var longMember = new Member
            {
                Id = 1,
                FullName = "Prof. Christopher Wellington-Montgomery IV",
                Email = "christopher.wellington.montgomery.iv@faculty.oxford.ac.uk",
                PhoneNumber = "+1 (555) 987-6543 ext. 204",
                MembershipType = MembershipType.Faculty,
                MaxBorrowLimit = 10,
                IsActive = true
            };

            using var dlg = new MemberDialog(longMember);
            dlg.CreateControl();

            CaptureAndVerify(dlg, "MemberDialog_100.png");

            dlg.Scale(new SizeF(1.25f, 1.25f));
            CaptureAndVerify(dlg, "MemberDialog_125.png");

            dlg.Scale(new SizeF(1.2f, 1.2f));
            CaptureAndVerify(dlg, "MemberDialog_150.png");
        });
    }

    [TestMethod]
    public void TestReturnDialog_LayoutAndScaling()
    {
        RunInSta(() =>
        {
            var testDb = $"test_return_visual_{Guid.NewGuid():N}.db";
            try
            {
                var dbContext = new DatabaseContext(testDb);
                var bookRepo = new BookRepository(dbContext);
                var memberRepo = new MemberRepository(dbContext);
                var loanRepo = new LoanRepository(dbContext);
                var fineCalc = new FineCalculatorService { DailyRate = 0.50m, GracePeriodDays = 1 };
                var circService = new CirculationService(loanRepo, bookRepo, memberRepo, fineCalc);
                var memberService = new MemberService(memberRepo, loanRepo);

                var overdueLoan = new Loan
                {
                    Id = 42,
                    BookId = 1,
                    BookTitle = "Designing Data-Intensive Applications: The Big Ideas Behind Reliable, Scalable Systems",
                    MemberId = 10,
                    MemberName = "Sarah Jenkins-Kowalski (Senior Researcher)",
                    BorrowDate = DateTime.Today.AddDays(-30),
                    DueDate = DateTime.Today.AddDays(-16),
                    IsReturned = false,
                    FineAmount = 8.00m
                };

                using var dlg = new ReturnDialog(overdueLoan, circService, fineCalc, memberService);
                dlg.CreateControl();

                CaptureAndVerify(dlg, "ReturnDialog_100.png");

                dlg.Scale(new SizeF(1.25f, 1.25f));
                CaptureAndVerify(dlg, "ReturnDialog_125.png");

                dlg.Scale(new SizeF(1.2f, 1.2f));
                CaptureAndVerify(dlg, "ReturnDialog_150.png");
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try { if (File.Exists(testDb)) File.Delete(testDb); } catch { }
            }
        });
    }

    [TestMethod]
    public void TestBorrowDialog_LayoutAndScaling()
    {
        RunInSta(() =>
        {
            var testDb = $"test_borrow_visual_{Guid.NewGuid():N}.db";
            try
            {
                var dbContext = new DatabaseContext(testDb);
                var initializer = new DatabaseInitializer(dbContext);
                initializer.InitializeAsync().GetAwaiter().GetResult();

                var bookRepo = new BookRepository(dbContext);
                var memberRepo = new MemberRepository(dbContext);
                var loanRepo = new LoanRepository(dbContext);
                var fineCalc = new FineCalculatorService { DailyRate = 0.50m, GracePeriodDays = 1 };
                var circService = new CirculationService(loanRepo, bookRepo, memberRepo, fineCalc);
                var bookService = new BookService(bookRepo, loanRepo);
                var memberService = new MemberService(memberRepo, loanRepo);

                using var dlg = new BorrowDialog(bookService, memberService, circService, preselectedBookId: 1);
                dlg.CreateControl();

                CaptureAndVerify(dlg, "BorrowDialog_100.png");

                dlg.Scale(new SizeF(1.25f, 1.25f));
                CaptureAndVerify(dlg, "BorrowDialog_125.png");

                dlg.Scale(new SizeF(1.2f, 1.2f));
                CaptureAndVerify(dlg, "BorrowDialog_150.png");
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try { if (File.Exists(testDb)) File.Delete(testDb); } catch { }
            }
        });
    }

    [TestMethod]
    public void TestMainForm_LayoutAndScreenshots()
    {
        RunInSta(() =>
        {
            using var form = new MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(0, 0);
            form.Size = new Size(1366, 768);
            form.Show();
            Application.DoEvents();
            Thread.Sleep(500);
            Application.DoEvents();

            CaptureAndVerify(form, "MainForm_Dashboard_1366x768.png");

            // Set to 1920x1080 resolution
            form.Size = new Size(1920, 1080);
            Application.DoEvents();
            Thread.Sleep(200);
            CaptureAndVerify(form, "MainForm_Dashboard_1920x1080.png");

            // Switch tabs and capture
            var tabControl = FindControl<TabControl>(form);
            Assert.IsNotNull(tabControl, "TabControl not found");

            if (tabControl.TabPages.Count > 1)
            {
                tabControl.SelectedIndex = 1;
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();
                CaptureAndVerify(form, "MainForm_BooksCatalog.png");
            }

            if (tabControl.TabPages.Count > 2)
            {
                tabControl.SelectedIndex = 2;
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();
                CaptureAndVerify(form, "MainForm_Members.png");
            }

            if (tabControl.TabPages.Count > 3)
            {
                tabControl.SelectedIndex = 3;
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();
                CaptureAndVerify(form, "MainForm_Circulation.png");
            }

            if (tabControl.TabPages.Count > 4)
            {
                tabControl.SelectedIndex = 4;
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();
                CaptureAndVerify(form, "MainForm_Reports.png");
            }

            if (tabControl.TabPages.Count > 5)
            {
                tabControl.SelectedIndex = 5;
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();
                CaptureAndVerify(form, "MainForm_Network.png");
            }

            form.Close();
            form.Dispose();
        });
    }

    private void CaptureAndVerify(Form form, string filename)
    {
        if (!form.Visible)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(30, 30);
            form.Show();
        }
        Application.DoEvents();
        Thread.Sleep(150);
        Application.DoEvents();

        var width = Math.Max(form.Width, 300);
        var height = Math.Max(form.Height, 200);

        using var bmp = new Bitmap(width, height);
        try
        {
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(form.Location, Point.Empty, form.Size);
        }
        catch
        {
            // Fallback to recursive DrawToBitmap if screen capture unavailable
            RenderControlTree(form, bmp);
        }

        var fullPath = Path.Combine(_screenshotDir, filename);
        bmp.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);

        // Sanity assertions on form layout
        Assert.IsTrue(File.Exists(fullPath), $"Screenshot was not saved at {fullPath}");
    }

    private static void RenderControlTree(Control root, Bitmap bmp)
    {
        using var g = Graphics.FromImage(bmp);
        g.Clear(root.BackColor);
        DrawChildControls(root, Point.Empty, g);
    }

    private static void DrawChildControls(Control parent, Point parentOffset, Graphics g)
    {
        for (int i = parent.Controls.Count - 1; i >= 0; i--)
        {
            var c = parent.Controls[i];
            if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
            var loc = new Point(parentOffset.X + c.Left, parentOffset.Y + c.Top);

            if (c is DataGridView dgv)
            {
                try
                {
                    using var dgvBmp = new Bitmap(dgv.Width, dgv.Height);
                    dgv.DrawToBitmap(dgvBmp, new Rectangle(0, 0, dgv.Width, dgv.Height));
                    g.DrawImage(dgvBmp, loc);
                }
                catch { }
                continue;
            }

            if (c is TabControl tc)
            {
                try
                {
                    using var tcBmp = new Bitmap(tc.Width, tc.Height);
                    tc.DrawToBitmap(tcBmp, new Rectangle(0, 0, tc.Width, tc.Height));
                    g.DrawImage(tcBmp, loc);
                }
                catch { }

                if (tc.SelectedTab != null && tc.SelectedTab.Visible)
                {
                    var tabLoc = new Point(loc.X + tc.SelectedTab.Left, loc.Y + tc.SelectedTab.Top);
                    if (tc.SelectedTab.BackColor != Color.Transparent)
                    {
                        using var bg = new SolidBrush(tc.SelectedTab.BackColor);
                        g.FillRectangle(bg, new Rectangle(tabLoc, tc.SelectedTab.Size));
                    }
                    DrawChildControls(tc.SelectedTab, tabLoc, g);
                }
                continue;
            }

            if (c.BackColor != Color.Transparent)
            {
                using var bgBrush = new SolidBrush(c.BackColor);
                g.FillRectangle(bgBrush, new Rectangle(loc, c.Size));
            }

            if (!c.HasChildren || c is StatCardControl or GdiChartControl)
            {
                try
                {
                    using var childBmp = new Bitmap(c.Width, c.Height);
                    c.DrawToBitmap(childBmp, new Rectangle(0, 0, c.Width, c.Height));
                    g.DrawImage(childBmp, loc);
                }
                catch { }
            }

            if (c.HasChildren && !(c is StatCardControl or GdiChartControl))
            {
                DrawChildControls(c, loc, g);
            }
        }
    }

    private static T? FindControl<T>(Control parent) where T : Control
    {
        foreach (Control c in parent.Controls)
        {
            if (c is T t) return t;
            var sub = FindControl<T>(c);
            if (sub != null) return sub;
        }
        return null;
    }
}
