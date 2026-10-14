using PersoNet.Application.Common;
using PersoNet.Contracts.Account;

namespace PersoNet.Application.Accounts;

/// <summary>
/// The sign-in accounts, which live in the login server (PersoNet.Identity), seen from the caller's company.
/// </summary>
public interface IAccountDirectory
{
    /// <summary>The account with this e-mail, or null when the company has none.</summary>
    Task<IdentityAccountResponse?> FindAsync(string email, CancellationToken cancellationToken);

    /// <summary>A link to set the password; the account is created first when there is none.</summary>
    Task<Result<IdentityAccountLinkResponse>> CreateLinkAsync(IdentityAccountLinkRequest request, CancellationToken cancellationToken);

    Task<Result<IdentityAccountResponse>> SetBlockedAsync(Guid accountId, bool blocked, CancellationToken cancellationToken);
}
