using System.Collections.Concurrent;
using PersoNet.Application.Accounts;
using PersoNet.Application.Common;
using PersoNet.Contracts.Account;

namespace PersoNet.Api.IntegrationTests;

/// <summary>
/// Stands in for the login server's accounts in API tests: an in-memory list with its rules (one account per e-mail).
/// </summary>
public sealed class FakeAccountDirectory : IAccountDirectory
{
    private readonly ConcurrentDictionary<string, IdentityAccountResponse> _accounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>E-mails taken by accounts of other companies.</summary>
    public ConcurrentBag<string> TakenElsewhere { get; } = [];

    public Task<IdentityAccountResponse?> FindAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(email));

    public Task<Result<IdentityAccountLinkResponse>> CreateLinkAsync(IdentityAccountLinkRequest request, CancellationToken cancellationToken)
    {
        if (TakenElsewhere.Contains(request.Email, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult<Result<IdentityAccountLinkResponse>>(AppError.Conflict("account.email_taken", "Ten adres e-mail jest już używany przez konto w innej firmie."));
        }

        var account = _accounts.GetOrAdd(request.Email, email => new IdentityAccountResponse(Guid.NewGuid(), email, AccountState.Invited));
        var link = new IdentityAccountLinkResponse(account, new Uri($"https://login.test/account/activate?user={account.Id}&code=x"), DateTimeOffset.UtcNow.AddDays(7));
        return Task.FromResult<Result<IdentityAccountLinkResponse>>(link);
    }

    public Task<Result<IdentityAccountResponse>> SetBlockedAsync(Guid accountId, bool blocked, CancellationToken cancellationToken)
    {
        var account = _accounts.Values.Single(a => a.Id == accountId);
        var changed = account with { State = blocked ? AccountState.Blocked : AccountState.Active };
        _accounts[account.Email] = changed;
        return Task.FromResult<Result<IdentityAccountResponse>>(changed);
    }
}
