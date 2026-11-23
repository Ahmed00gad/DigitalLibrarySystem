using System.Text.RegularExpressions;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public interface IMemberService
{
    Task<IReadOnlyList<Member>> GetAllMembersAsync();
    Task<Member?> GetMemberByIdAsync(int id);
    Task<Member?> GetMemberByEmailAsync(string email);
    Task<IReadOnlyList<Member>> SearchMembersAsync(string? query, MembershipType? type = null, bool activeOnly = false);
    Task<Member> AddMemberAsync(Member member);
    Task<bool> UpdateMemberAsync(Member member);
    Task<bool> DeleteMemberAsync(int id);
    Task<bool> ToggleMemberStatusAsync(int id);
}

public class MemberService : IMemberService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ILoanRepository _loanRepository;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MemberService(IMemberRepository memberRepository, ILoanRepository loanRepository)
    {
        _memberRepository = memberRepository;
        _loanRepository = loanRepository;
    }

    public async Task<IReadOnlyList<Member>> GetAllMembersAsync()
    {
        return await _memberRepository.GetAllAsync();
    }

    public async Task<Member?> GetMemberByIdAsync(int id)
    {
        return await _memberRepository.GetByIdAsync(id);
    }

    public async Task<Member?> GetMemberByEmailAsync(string email)
    {
        return await _memberRepository.GetByEmailAsync(email);
    }

    public async Task<IReadOnlyList<Member>> SearchMembersAsync(string? query, MembershipType? type = null, bool activeOnly = false)
    {
        return await _memberRepository.SearchAsync(query, type, activeOnly);
    }

    public async Task<Member> AddMemberAsync(Member member)
    {
        ValidateMember(member);

        var existing = await _memberRepository.GetByEmailAsync(member.Email);
        if (existing != null)
        {
            throw new ValidationException($"A member with email '{member.Email}' already exists ({existing.FullName}).");
        }

        if (member.MaxBorrowLimit <= 0)
        {
            member.MaxBorrowLimit = Member.GetDefaultLimit(member.MembershipType);
        }

        var id = await _memberRepository.AddAsync(member);
        member.Id = id;
        return member;
    }

    public async Task<bool> UpdateMemberAsync(Member member)
    {
        ValidateMember(member);

        var existing = await _memberRepository.GetByIdAsync(member.Id)
            ?? throw new EntityNotFoundException("Member", member.Id);

        var existingByEmail = await _memberRepository.GetByEmailAsync(member.Email);
        if (existingByEmail != null && existingByEmail.Id != member.Id)
        {
            throw new ValidationException($"Another member with email '{member.Email}' already exists ({existingByEmail.FullName}).");
        }

        return await _memberRepository.UpdateAsync(member);
    }

    public async Task<bool> DeleteMemberAsync(int id)
    {
        var member = await _memberRepository.GetByIdAsync(id)
            ?? throw new EntityNotFoundException("Member", id);

        var activeLoansCount = await _memberRepository.GetActiveLoansCountAsync(id);
        if (activeLoansCount > 0)
        {
            throw new ValidationException($"Cannot delete member '{member.FullName}' because they currently have {activeLoansCount} active borrowed books.");
        }

        try
        {
            return await _memberRepository.DeleteAsync(id);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new ValidationException($"Cannot delete member '{member.FullName}' because historical loan records exist for this member. You can deactivate the member instead using the Status button.");
        }
    }

    public async Task<bool> ToggleMemberStatusAsync(int id)
    {
        var member = await _memberRepository.GetByIdAsync(id)
            ?? throw new EntityNotFoundException("Member", id);

        member.IsActive = !member.IsActive;
        return await _memberRepository.UpdateAsync(member);
    }

    public static void ValidateMember(Member member)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(member.FullName))
            errors.Add("Member full name is required.");

        if (string.IsNullOrWhiteSpace(member.Email))
            errors.Add("Email address is required.");
        else if (!EmailRegex.IsMatch(member.Email.Trim()))
            errors.Add("Please enter a valid email address (e.g., user@domain.com).");

        if (member.MaxBorrowLimit < 1)
            errors.Add("Maximum borrow limit must be at least 1.");

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}
