using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Tests;

[TestClass]
public class ValidationTests
{
    [TestMethod]
    public void ValidateBook_WithValidData_DoesNotThrow()
    {
        var book = new Book
        {
            Title = "Clean Code",
            Author = "Robert C. Martin",
            ISBN = "978-0132350884",
            Category = "Computer Science",
            PublicationYear = 2008,
            TotalCopies = 5,
            CopiesAvailable = 5
        };

        BookService.ValidateBook(book); // Should not throw
    }

    [TestMethod]
    public void ValidateBook_WithEmptyTitle_ThrowsValidationException()
    {
        var book = new Book
        {
            Title = "",
            Author = "Author",
            ISBN = "978-0132350884",
            Category = "Tech",
            TotalCopies = 1,
            CopiesAvailable = 1
        };

        Assert.Throws<ValidationException>(() => BookService.ValidateBook(book));
    }

    [TestMethod]
    public void ValidateBook_WithInvalidIsbn_ThrowsValidationException()
    {
        var book = new Book
        {
            Title = "Valid Title",
            Author = "Author",
            ISBN = "123-abc", // invalid ISBN
            Category = "Tech",
            TotalCopies = 1,
            CopiesAvailable = 1
        };

        Assert.Throws<ValidationException>(() => BookService.ValidateBook(book));
    }

    [TestMethod]
    public void ValidateBook_WhenAvailableCopiesExceedsTotalCopies_ThrowsValidationException()
    {
        var book = new Book
        {
            Title = "Valid Title",
            Author = "Author",
            ISBN = "978-0132350884",
            Category = "Tech",
            TotalCopies = 2,
            CopiesAvailable = 5 // invalid
        };

        Assert.Throws<ValidationException>(() => BookService.ValidateBook(book));
    }

    [TestMethod]
    public void ValidateMember_WithValidData_DoesNotThrow()
    {
        var member = new Member
        {
            FullName = "Ahmed Hassan",
            Email = "ahmed.hassan@test.com",
            PhoneNumber = "+201012345678",
            MembershipType = MembershipType.Student,
            MaxBorrowLimit = 3
        };

        MemberService.ValidateMember(member); // Should not throw
    }

    [TestMethod]
    public void ValidateMember_WithInvalidEmail_ThrowsValidationException()
    {
        var member = new Member
        {
            FullName = "Ahmed Hassan",
            Email = "not-an-email",
            MembershipType = MembershipType.Student
        };

        Assert.Throws<ValidationException>(() => MemberService.ValidateMember(member));
    }

    [TestMethod]
    public void IsValidIsbn_RecognizesBoth10And13DigitFormats()
    {
        Assert.IsTrue(BookService.IsValidIsbn("978-0132350884"));
        Assert.IsTrue(BookService.IsValidIsbn("0132350882"));
        Assert.IsTrue(BookService.IsValidIsbn("0-201-63361-2"));
        Assert.IsFalse(BookService.IsValidIsbn("12345"));
        Assert.IsFalse(BookService.IsValidIsbn(""));
    }
}
