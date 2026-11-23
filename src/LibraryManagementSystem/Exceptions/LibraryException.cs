namespace LibraryManagementSystem.Exceptions;

public class LibraryException : Exception
{
    public LibraryException(string message) : base(message) { }
    public LibraryException(string message, Exception innerException) : base(message, innerException) { }
}

public class BookNotAvailableException : LibraryException
{
    public int BookId { get; }
    public BookNotAvailableException(int bookId, string bookTitle) 
        : base($"Book '{bookTitle}' (ID: {bookId}) is currently not available for borrowing.")
    {
        BookId = bookId;
    }
}

public class MemberLimitExceededException : LibraryException
{
    public int MemberId { get; }
    public int CurrentLimit { get; }

    public MemberLimitExceededException(int memberId, string memberName, int limit)
        : base($"Member '{memberName}' (ID: {memberId}) has reached the maximum borrowing limit of {limit} books.")
    {
        MemberId = memberId;
        CurrentLimit = limit;
    }
}

public class ValidationException : LibraryException
{
    public List<string> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new List<string> { message };
    }

    public ValidationException(List<string> errors) 
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }
}

public class EntityNotFoundException : LibraryException
{
    public EntityNotFoundException(string entityName, object id)
        : base($"{entityName} with ID '{id}' was not found.") { }
}
