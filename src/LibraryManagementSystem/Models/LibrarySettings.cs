namespace LibraryManagementSystem.Models;

public class LibrarySettings
{
    public decimal DefaultDailyFineRate { get; set; } = 0.50m;
    public int GracePeriodDays { get; set; } = 1;
    public int DefaultBorrowDurationDays { get; set; } = 14;
    public int ServerPort { get; set; } = 8888;
    public bool AutoStartServer { get; set; } = false;
}
