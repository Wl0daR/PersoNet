namespace PersoNet.Desktop.Presentation.Authentication;

/// <summary>
/// Signs the HR user in through PersoNet.Identity. Implemented by the desktop host (system browser + local callback).
/// </summary>
public interface ISignInService
{
    /// <summary>
    /// Opens the login page and waits for the result. Throws <see cref="OperationCanceledException"/> when cancelled.
    /// </summary>
    Task<SignedInUser> SignInAsync(CancellationToken cancellationToken);

    /// <summary>Forgets the tokens of the current user.</summary>
    Task SignOutAsync(CancellationToken cancellationToken);
}

public sealed record SignedInUser(string Name, string? Email);
