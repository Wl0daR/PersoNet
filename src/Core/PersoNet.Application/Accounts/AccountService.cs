using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Audit;
using PersoNet.Application.Common;
using PersoNet.Contracts.Account;
using PersoNet.Domain.Audit;

namespace PersoNet.Application.Accounts;

/// <summary>
/// HR gives employees access to the portal. The account belongs to the employee whose work e-mail it has, the same
/// rule the portal uses to find "me"; the accounts themselves are kept by the login server (<see cref="IAccountDirectory"/>),
/// so what HR does with them is written to the employee's history here.
/// </summary>
public sealed class AccountService(IAppDbContext db, IAccountDirectory directory, IAuditActor actor, TimeProvider time)
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

        var before = await directory.FindAsync(email, cancellationToken);
        if (before?.State == AccountState.Blocked)
        {
            return AccountErrors.Blocked;
        }

        var link = await directory.CreateLinkAsync(new IdentityAccountLinkRequest(email, $"{employee.FirstName} {employee.LastName}"), cancellationToken);
        if (!link.IsSuccess)
        {
            return link.Error!;
        }

        var account = link.Value.Account;
        await RecordAsync(employee, account, before?.State ?? AccountState.None, AuditTexts.AccountEventLink, cancellationToken);
        return new AccountLinkResponse(new EmployeeAccountResponse(employeeId, email, account.State), link.Value.Link, link.Value.ExpiresAt);
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
        if (!changed.IsSuccess)
        {
            return changed.Error!;
        }

        if (changed.Value.State != account.State)
        {
            var action = blocked ? AuditTexts.AccountEventBlock : AuditTexts.AccountEventUnblock;
            await RecordAsync(employee, changed.Value, account.State, action, cancellationToken);
        }

        return new EmployeeAccountResponse(employeeId, employee.Email, changed.Value.State);
    }

    // The account lives on the login server, so its changes are not seen by the automatic audit of saved data.
    private async Task RecordAsync(EmployeeRow employee, IdentityAccountResponse account, AccountState before, string action, CancellationToken cancellationToken)
    {
        var changes = before == account.State ? [] : new[] { new AuditChange("State", before.ToString(), account.State.ToString()) };
        var state = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Email"] = account.Email,
            [AuditTexts.AccountEventProperty] = action,
            ["State"] = account.State.ToString(),
        };

        db.AuditLog.Add(AuditEntry.Record(
            employee.TenantId,
            time.GetUtcNow(),
            actor.UserId,
            actor.Name,
            AuditTexts.AccountEntity,
            account.Id,
            employee.Id,
            before == AccountState.None ? AuditAction.Created : AuditAction.Changed,
            JsonSerializer.Serialize(changes, AuditJson.Options),
            JsonSerializer.Serialize(state, AuditJson.Options)));

        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<EmployeeRow?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        db.Employees.AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new EmployeeRow(employee.Id, employee.TenantId, employee.FirstName, employee.LastName, employee.Email, employee.TerminationDate))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record EmployeeRow(Guid Id, Guid TenantId, string FirstName, string LastName, string? Email, DateOnly? TerminationDate);
}
