using System.Text.Json.Serialization;

namespace PersoNet.Contracts.Account;

/// <summary>Where an employee's sign-in account stands.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AccountState>))]
public enum AccountState
{
    /// <summary>No account with the employee's e-mail yet.</summary>
    None = 0,

    /// <summary>Created, but the employee has not set a password from the link yet.</summary>
    Invited,

    /// <summary>The employee can sign in.</summary>
    Active,

    /// <summary>Blocked by HR: signing in is refused.</summary>
    Blocked,
}

/// <summary>
/// The portal account of an employee. Account and employee record are linked by the work e-mail, so
/// <see cref="Email"/> is the employee's e-mail; without one there can be no account.
/// </summary>
public sealed record EmployeeAccountResponse(Guid EmployeeId, string? Email, AccountState State);

/// <summary>
/// A one-time link the employee opens to set their password. HR passes it on until the server sends e-mail;
/// it stops working at <see cref="ExpiresAt"/> or once used.
/// </summary>
public sealed record AccountLinkResponse(EmployeeAccountResponse Account, Uri Link, DateTimeOffset ExpiresAt);

/// <summary>A sign-in account as the login server reports it to PersoNet.Api.</summary>
public sealed record IdentityAccountResponse(Guid Id, string Email, AccountState State);

/// <summary>Asks the login server for an account with this e-mail (created if missing) and a link to set its password.</summary>
public sealed record IdentityAccountLinkRequest(string Email, string DisplayName);

/// <summary>The login server's answer to <see cref="IdentityAccountLinkRequest"/>.</summary>
public sealed record IdentityAccountLinkResponse(IdentityAccountResponse Account, Uri Link, DateTimeOffset ExpiresAt);
