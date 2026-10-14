using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Account;

namespace PersoNet.Application.Accounts;

/// <summary>
/// HR gives employees access to the portal. The account belongs to the employee whose work e-mail it has, the same
/// rule the portal uses to find "me"; the accounts themselves are kept by the login server (<see cref="IAccountDirectory"/>).
/// </summary>
public sealed class AccountService(IAppDbContext db, IAccountDirectory directory, TimeProvider time)
{
    public async Task<Result<EmployeeAccountResponse>> GetAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return AccountErrors.EmployeeNotFound;
        }

        var account = employee.Email is { } email ? await directory.FindAsync(email, cancellationToken) : null;
        return new EmployeeAccountResponse(employeeId, employee.Email, account?.State ?? AccountState.None);
    }

    /// <summary>
    /// A one-time link for the employee to set their password: the first one creates the account, a later one lets them
    /// choose a new password when they forgot it.
    /// </summary>
    public async Task<Result<AccountLinkResponse>> CreateLinkAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return AccountErrors.EmployeeNotFound;
        }

        if (employee.Email is not { } email)
        {
            return AccountErrors.EmailRequired;
        }

        if (employee.TerminationDate is { } lastDay && lastDay < DateOnly.FromDateTime(time.GetLocalNow().DateTime))
        {
            return AccountErrors.EmployeeTerminated;
        }

        if ((await directory.FindAsync(email, cancellationToken))?.State == AccountState.Blocked)
        {
            return AccountErrors.Blocked;
        }

        var link = await directory.CreateLinkAsync(new IdentityAccountLinkRequest(email, $"{employee.FirstName} {employee.LastName}"), cancellationToken);
        return link.IsSuccess
            ? new AccountLinkResponse(new EmployeeAccountResponse(employeeId, email, link.Value.Account.State), link.Value.Link, link.Value.ExpiresAt)
            : link.Error!;
    }

    /// <summary>Blocks or unblocks signing in; a block also ends the sessions the employee has open.</summary>
    public async Task<Result<EmployeeAccountResponse>> SetBlockedAsync(Guid employeeId, bool blocked, CancellationToken cancellationToken)
    {
        var employee = await FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return AccountErrors.EmployeeNotFound;
        }

        var account = employee.Email is { } email ? await directory.FindAsync(email, cancellationToken) : null;
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        var changed = await directory.SetBlockedAsync(account.Id, blocked, cancellationToken);
        return changed.IsSuccess
            ? new EmployeeAccountResponse(employeeId, employee.Email, changed.Value.State)
            : changed.Error!;
    }

    private Task<EmployeeRow?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        db.Employees.AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new EmployeeRow(employee.FirstName, employee.LastName, employee.Email, employee.TerminationDate))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record EmployeeRow(string FirstName, string LastName, string? Email, DateOnly? TerminationDate);
}
