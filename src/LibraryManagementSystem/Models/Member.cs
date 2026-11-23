namespace LibraryManagementSystem.Models;

public class Member : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public MembershipType MembershipType { get; set; } = MembershipType.Regular;
    public DateTime MembershipDate { get; set; } = DateTime.Today;
    public int MaxBorrowLimit { get; set; } = 3;
    public bool IsActive { get; set; } = true;

    public static int GetDefaultLimit(MembershipType type) => type switch
    {
        MembershipType.Student => 3,
        MembershipType.Regular => 5,
        MembershipType.Faculty => 10,
        MembershipType.Premium => 8,
        _ => 3
    };

    public override string ToString() => $"{FullName} ({MembershipType}) - {Email}";
}
