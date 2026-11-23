using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class FineCalculatorServiceTests
{
    private FineCalculatorService _calculator = null!;

    [TestInitialize]
    public void Setup()
    {
        _calculator = new FineCalculatorService
        {
            DailyRate = 1.00m,
            GracePeriodDays = 1
        };
    }

    [TestMethod]
    public void CalculateOverdueDays_WhenReturnedBeforeDueDate_ReturnsZero()
    {
        var due = DateTime.Today;
        var returnDate = DateTime.Today.AddDays(-2);

        var days = _calculator.CalculateOverdueDays(due, returnDate);

        Assert.AreEqual(0, days);
    }

    [TestMethod]
    public void CalculateOverdueDays_WhenReturnedOnDueDate_ReturnsZero()
    {
        var due = DateTime.Today;
        var returnDate = DateTime.Today;

        var days = _calculator.CalculateOverdueDays(due, returnDate);

        Assert.AreEqual(0, days);
    }

    [TestMethod]
    public void CalculateOverdueDays_WhenReturnedAfterDueDate_ReturnsCorrectDays()
    {
        var due = DateTime.Today.AddDays(-5);
        var returnDate = DateTime.Today;

        var days = _calculator.CalculateOverdueDays(due, returnDate);

        Assert.AreEqual(5, days);
    }

    [TestMethod]
    public void CalculateFine_WithinGracePeriod_ReturnsZeroFine()
    {
        // 1 day late with 1 day grace period -> 0 fine
        var due = DateTime.Today.AddDays(-1);
        var returnDate = DateTime.Today;

        var fine = _calculator.CalculateFine(due, returnDate, MembershipType.Regular);

        Assert.AreEqual(0.00m, fine);
    }

    [TestMethod]
    public void CalculateFine_RegularMember_CalculatesStandardFine()
    {
        // 4 days late, 1 grace day = 3 chargeable days @ $1.00 = $3.00
        var due = DateTime.Today.AddDays(-4);
        var returnDate = DateTime.Today;

        var fine = _calculator.CalculateFine(due, returnDate, MembershipType.Regular);

        Assert.AreEqual(3.00m, fine);
    }

    [TestMethod]
    public void CalculateFine_StudentMember_Applies50PercentDiscount()
    {
        // 5 days late, 1 grace day = 4 chargeable days @ $1.00 = $4.00 * 50% = $2.00
        var due = DateTime.Today.AddDays(-5);
        var returnDate = DateTime.Today;

        var fine = _calculator.CalculateFine(due, returnDate, MembershipType.Student);

        Assert.AreEqual(2.00m, fine);
    }

    [TestMethod]
    public void CalculateFine_PremiumMember_Waiver100PercentFine()
    {
        // Premium member gets 100% fine waiver ($0.00)
        var due = DateTime.Today.AddDays(-20);
        var returnDate = DateTime.Today;

        var fine = _calculator.CalculateFine(due, returnDate, MembershipType.Premium);

        Assert.AreEqual(0.00m, fine);
    }

    [TestMethod]
    public void CalculateFine_FacultyMember_ReceivesExtraGraceAnd75PercentDiscount()
    {
        // Faculty gets 1 + 5 = 6 grace days.
        // 10 days late - 6 grace days = 4 chargeable days @ $1.00 = $4.00 * 25% = $1.00
        var due = DateTime.Today.AddDays(-10);
        var returnDate = DateTime.Today;

        var fine = _calculator.CalculateFine(due, returnDate, MembershipType.Faculty);

        Assert.AreEqual(1.00m, fine);
    }
}
