namespace LibraryManagementSystem.Models;

public class Book : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public int PublicationYear { get; set; } = DateTime.Now.Year;
    public int TotalCopies { get; set; } = 1;
    public int CopiesAvailable { get; set; } = 1;
    public string LocationRack { get; set; } = "A-1";
    public string Description { get; set; } = string.Empty;

    public bool IsAvailable => CopiesAvailable > 0;

    public override string ToString() => $"{Title} by {Author} ({ISBN})";
}
