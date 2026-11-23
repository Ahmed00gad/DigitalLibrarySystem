using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services;

public interface IFineCalculatorService
{
    decimal DailyRate { get; set; }
    int GracePeriodDays { get; set; }

    int CalculateOverdueDays(DateTime dueDate, DateTime? returnDate = null);
    decimal CalculateFine(DateTime dueDate, DateTime? returnDate = null, MembershipType membershipType = MembershipType.Regular);
}

public class FineCalculatorService : IFineCalculatorService
{
    public decimal DailyRate { get; set; } = 0.50m;
    public int GracePeriodDays { get; set; } = 1;

    public int CalculateOverdueDays(DateTime dueDate, DateTime? returnDate = null)
    {
        var effectiveReturn = returnDate ?? DateTime.Today;
        if (effectiveReturn <= dueDate)
        {
            return 0;
        }

        var totalDaysLate = (effectiveReturn.Date - dueDate.Date).Days;
        return Math.Max(0, totalDaysLate);
    }

    public decimal CalculateFine(DateTime dueDate, DateTime? returnDate = null, MembershipType membershipType = MembershipType.Regular)
    {
        var overdueDays = CalculateOverdueDays(dueDate, returnDate);

        // Faculty gets 5 extra grace period days
        var grace = GracePeriodDays;
        if (membershipType == MembershipType.Faculty)
        {
            grace += 5;
        }

        if (overdueDays <= grace)
        {
            return 0.00m;
        }

        var chargeableDays = overdueDays - grace;
        var baseFine = chargeableDays * DailyRate;

        // Apply membership discounts
        return membershipType switch
        {
            MembershipType.Premium => 0.00m,                     // 100% fine waiver for Premium members
            MembershipType.Faculty => Math.Round(baseFine * 0.25m, 2), // 75% discount for Faculty
            MembershipType.Student => Math.Round(baseFine * 0.50m, 2), // 50% discount for Students
            _ => Math.Round(baseFine, 2)                         // Standard for Regular
        };
    }
}
