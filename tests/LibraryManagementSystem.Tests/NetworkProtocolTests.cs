using LibraryManagementSystem.Data;
using LibraryManagementSystem.Network;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class NetworkProtocolTests
{
    private string _dbFile = null!;
    private DatabaseContext _context = null!;
    private LibraryNetworkServer _server = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dbFile = $"test_net_{Guid.NewGuid():N}.db";
        _context = new DatabaseContext(_dbFile);
        var initializer = new DatabaseInitializer(_context);
        await initializer.InitializeAsync();

        var bookRepo = new BookRepository(_context);
        var memberRepo = new MemberRepository(_context);
        var loanRepo = new LoanRepository(_context);
        var fineCalculator = new FineCalculatorService();
        var circulationService = new CirculationService(loanRepo, bookRepo, memberRepo, fineCalculator);
        var bookService = new BookService(bookRepo, loanRepo);
        var reportService = new ReportService(loanRepo, bookRepo);

        _server = new LibraryNetworkServer(bookService, circulationService, reportService);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFile))
        {
            try { File.Delete(_dbFile); } catch { }
        }
    }

    [TestMethod]
    public async Task ProcessCommand_Ping_ReturnsPong()
    {
        var response = await _server.ProcessCommandAsync("PING");
        Assert.IsTrue(response.StartsWith("PONG"));
    }

    [TestMethod]
    public async Task ProcessCommand_Help_ReturnsCommandList()
    {
        var response = await _server.ProcessCommandAsync("HELP");
        Assert.IsTrue(response.Contains("SEARCH"));
        Assert.IsTrue(response.Contains("CHECK"));
        Assert.IsTrue(response.Contains("STATS"));
    }

    [TestMethod]
    public async Task ProcessCommand_Stats_ReturnsValidMetrics()
    {
        var response = await _server.ProcessCommandAsync("STATS");
        Assert.IsTrue(response.StartsWith("200 STATS:"));
        Assert.IsTrue(response.Contains("Books="));
    }

    [TestMethod]
    public async Task ProcessCommand_Search_ReturnsMatchingBooks()
    {
        var response = await _server.ProcessCommandAsync("SEARCH Clean Code");
        Assert.IsTrue(response.StartsWith("200 FOUND"));
        Assert.IsTrue(response.Contains("Clean Code"));
    }

    [TestMethod]
    public async Task ProcessCommand_CheckIsbn_ReturnsAvailability()
    {
        var response = await _server.ProcessCommandAsync("CHECK 978-0132350884");
        Assert.IsTrue(response.StartsWith("200"));
        Assert.IsTrue(response.Contains("Clean Code"));
        Assert.IsTrue(response.Contains("AVAILABLE"));
    }

    [TestMethod]
    public async Task ProcessCommand_UnknownCommand_Returns400()
    {
        var response = await _server.ProcessCommandAsync("FOOBAR");
        Assert.IsTrue(response.StartsWith("400 UNKNOWN_COMMAND"));
    }
}
