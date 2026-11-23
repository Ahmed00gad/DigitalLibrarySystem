using System.Net;
using System.Net.Sockets;
using System.Text;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Network;

public class LibraryNetworkServer
{
    private readonly IBookService _bookService;
    private readonly ICirculationService _circulationService;
    private readonly IReportService _reportService;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public int Port { get; private set; }

    public event Action<string>? LogMessage;
    public event Action<string>? ClientConnected;

    public LibraryNetworkServer(
        IBookService bookService,
        ICirculationService circulationService,
        IReportService reportService)
    {
        _bookService = bookService;
        _circulationService = circulationService;
        _reportService = reportService;
    }

    public void Start(int port = 8888)
    {
        if (_isRunning) return;

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _isRunning = true;

        LogMessage?.Invoke($"[Server] TCP Server started on 127.0.0.1:{port}. Waiting for client connections...");

        // Accept clients in background task
        Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cts?.Cancel();
        _listener?.Stop();
        LogMessage?.Invoke("[Server] TCP Server stopped.");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                if (_listener == null) break;
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                ClientConnected?.Invoke(endpoint);
                LogMessage?.Invoke($"[Server] Client connected from {endpoint}");

                _ = Task.Run(() => HandleClientAsync(client, endpoint, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (!_isRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Server Error] {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, string endpoint, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            try
            {
                await writer.WriteLineAsync("200 DIGITAL_LIBRARY_SERVER_READY (Type HELP for command list)");

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) break;

                    LogMessage?.Invoke($"[Client {endpoint}] REQ: {line}");

                    var response = await ProcessCommandAsync(line.Trim());
                    await writer.WriteLineAsync(response);
                    LogMessage?.Invoke($"[Server -> {endpoint}] RESP: {Truncate(response, 80)}");

                    if (line.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMessage?.Invoke($"[Client {endpoint}] Disconnected: {ex.Message}");
            }
        }
    }

    public async Task<string> ProcessCommandAsync(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return "400 EMPTY_COMMAND";

        var parts = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToUpperInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : "";

        try
        {
            switch (cmd)
            {
                case "PING":
                    return "PONG 1.0 (Digital Library Server Online)";

                case "HELP":
                    return "COMMANDS: PING | SEARCH <query> | CHECK <isbn> | STATS | OVERDUE | QUIT";

                case "SEARCH":
                    if (string.IsNullOrWhiteSpace(arg))
                        return "400 SEARCH requires query parameter. Usage: SEARCH <title or author>";
                    var searchResults = await _bookService.SearchBooksAsync(arg);
                    if (searchResults.Count == 0)
                        return "404 NO_MATCHES_FOUND";
                    var sbSearch = new StringBuilder();
                    sbSearch.Append($"200 FOUND {searchResults.Count} BOOKS: ");
                    foreach (var b in searchResults.Take(5))
                    {
                        sbSearch.Append($"[{b.Id}] \"{b.Title}\" by {b.Author} (Avail: {b.CopiesAvailable}/{b.TotalCopies}); ");
                    }
                    return sbSearch.ToString();

                case "CHECK":
                    if (string.IsNullOrWhiteSpace(arg))
                        return "400 CHECK requires ISBN. Usage: CHECK <isbn>";
                    var book = await _bookService.GetBookByIsbnAsync(arg);
                    if (book == null)
                        return $"404 BOOK_NOT_FOUND (ISBN: {arg})";
                    return $"200 \"{book.Title}\" by {book.Author} | Status: {(book.IsAvailable ? "AVAILABLE" : "OUT_OF_STOCK")} | Copies: {book.CopiesAvailable}/{book.TotalCopies} | Shelf: {book.LocationRack}";

                case "STATS":
                    var stats = await _reportService.GetStatisticsAsync();
                    return $"200 STATS: Books={stats.TotalBooks}, TotalCopies={stats.TotalCopies}, Available={stats.AvailableCopies}, ActiveMembers={stats.ActiveMembers}, ActiveLoans={stats.ActiveLoans}, Overdue={stats.OverdueLoans}, FinesAccrued=${stats.TotalFinesAccrued:F2}";

                case "OVERDUE":
                    var overdues = await _circulationService.GetOverdueLoansAsync();
                    if (overdues.Count == 0)
                        return "200 NO_OVERDUE_LOANS";
                    var sbOverdue = new StringBuilder($"200 OVERDUE_COUNT={overdues.Count}: ");
                    foreach (var o in overdues.Take(5))
                    {
                        sbOverdue.Append($"[Loan #{o.Id}] \"{o.BookTitle}\" borrowed by {o.MemberName} (Overdue: {o.OverdueDays} days, Fine: ${o.FineAmount:F2}); ");
                    }
                    return sbOverdue.ToString();

                case "QUIT":
                    return "200 GOODBYE";

                default:
                    return $"400 UNKNOWN_COMMAND '{cmd}'. Type HELP for options.";
            }
        }
        catch (Exception ex)
        {
            return $"500 SERVER_ERROR: {ex.Message}";
        }
    }

    private static string Truncate(string str, int maxLen)
    {
        if (str.Length <= maxLen) return str;
        return str.Substring(0, maxLen) + "...";
    }
}
