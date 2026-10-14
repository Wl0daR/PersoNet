using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using PersoNet.Contracts;
using PersoNet.Contracts.Account;
using PersoNet.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersoNet.Identity.Endpoints;

/// <summary>
/// Account administration for PersoNet.Api, called with the HR user's own access token: find an account by e-mail,
/// give out a link to set the password (creating the account first if needed), block and unblock. Every call stays
/// inside the caller's company.
/// </summary>
internal static class AccountAdminEndpoints
{
    public const string Path = "/api/accounts";

    public static IEndpointRouteBuilder MapAccountAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup(Path).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(Claims.Role, PersoNetAuth.Roles.HrAdmin)
            .RequireClaim(PersoNetAuth.TenantIdClaim));

        accounts.MapGet("/", FindAsync);
        accounts.MapPost("/link", CreateLinkAsync);
        accounts.MapPost("/{id:guid}/block", (Guid id, ClaimsPrincipal caller, UserManager<ApplicationUser> users, IOpenIddictTokenManager tokens, TimeProvider time) =>
            SetBlockedAsync(id, blocked: true, caller, users, tokens, time));
        accounts.MapPost("/{id:guid}/unblock", (Guid id, ClaimsPrincipal caller, UserManager<ApplicationUser> users, IOpenIddictTokenManager tokens, TimeProvider time) =>
            SetBlockedAsync(id, blocked: false, caller, users, tokens, time));

        return app;
    }

    private static async Task<IResult> FindAsync(string email, ClaimsPrincipal caller, UserManager<ApplicationUser> users, TimeProvider time)
    {
        var user = await users.FindByEmailAsync(email);
        return user is not null && user.TenantId == TenantOf(caller)
            ? TypedResults.Ok(ToResponse(user, time))
            : TypedResults.NotFound();
    }

    private static async Task<IResult> CreateLinkAsync(
        IdentityAccountLinkRequest request,
        ClaimsPrincipal caller,
        HttpContext context,
        UserManager<ApplicationUser> users,
        IOptions<DataProtectionTokenProviderOptions> tokenOptions,
        TimeProvider time)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status400BadRequest, "account.email_invalid", "Podaj poprawny adres e-mail.");
        }

        var tenantId = TenantOf(caller);
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = request.DisplayName.Trim(),
                TenantId = tenantId,
            };

            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                return Problem(StatusCodes.Status400BadRequest, "account.invalid", string.Join(" ", created.Errors.Select(error => error.Description)));
            }
        }
        else if (user.TenantId != tenantId)
        {
            // The e-mail is unique across companies; say so without revealing which company has it.
            return Problem(StatusCodes.Status409Conflict, "account.email_taken", "Ten adres e-mail jest już używany przez konto w innej firmie.");
        }

        // A password reset token: one use, for a limited time, and void once the password changes.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/account/activate?user={user.Id}&code={code}");

        return TypedResults.Ok(new IdentityAccountLinkResponse(ToResponse(user, time), link, time.GetUtcNow() + tokenOptions.Value.TokenLifespan));
    }

    private static async Task<IResult> SetBlockedAsync(
        Guid id, bool blocked, ClaimsPrincipal caller, UserManager<ApplicationUser> users, IOpenIddictTokenManager tokens, TimeProvider time)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != TenantOf(caller))
        {
            return TypedResults.NotFound();
        }

        if (blocked && caller.GetClaim(Claims.Subject) == user.Id.ToString())
        {
            return Problem(StatusCodes.Status400BadRequest, "account.block_self", "Nie możesz zablokować własnego konta.");
        }

        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, blocked ? DateTimeOffset.MaxValue : null);
        await users.ResetAccessFailedCountAsync(user);

        if (blocked)
        {
            // Signed-in apps keep working until their tokens expire unless the refresh tokens go too.
            await users.UpdateSecurityStampAsync(user);
            await foreach (var token in tokens.FindBySubjectAsync(user.Id.ToString()))
            {
                await tokens.TryRevokeAsync(token);
            }
        }

        return TypedResults.Ok(ToResponse(user, time));
    }

    private static IdentityAccountResponse ToResponse(ApplicationUser user, TimeProvider time) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.LockoutEnd > time.GetUtcNow() ? AccountState.Blocked
            : user.PasswordHash is null ? AccountState.Invited
            : AccountState.Active);

    private static Guid TenantOf(ClaimsPrincipal caller) => Guid.Parse(caller.GetClaim(PersoNetAuth.TenantIdClaim)!);

    private static ProblemHttpResult Problem(int status, string code, string detail) => TypedResults.Problem(
        detail: detail,
        statusCode: status,
        extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });
}
