using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PersoNet.Identity.Data;
using PersoNet.Identity.Seeding;

namespace PersoNet.Identity.Pages.Account;

/// <summary>
/// "Ustaw hasło": the page behind the link HR gives a new employee (or anyone who forgot their password).
/// The link carries a one-time password reset token.
/// </summary>
public sealed class ActivateModel(UserManager<ApplicationUser> users, IOptions<WebClientOptions> web) : PageModel
{
    private const string InvalidLink = "Ten link wygasł albo został już użyty. Poproś dział kadr o nowy.";

    [BindProperty(Name = "user", SupportsGet = true)]
    public Guid UserId { get; set; }

    [BindProperty(Name = "code", SupportsGet = true)]
    public string? Code { get; set; }

    [BindProperty]
    public ActivateInput Input { get; set; } = new();

    public string? Email { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool Done { get; private set; }

    /// <summary>The employee portal, where the person goes after setting the password.</summary>
    public Uri? PortalUri => web.Value.RedirectUri is { } redirect ? new Uri(redirect.GetLeftPart(UriPartial.Authority) + "/") : null;

    public async Task OnGetAsync()
    {
        var user = await FindUserAsync();
        if (user is null || Token is not { } token
            || !await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, UserManager<ApplicationUser>.ResetPasswordTokenPurpose, token))
        {
            ErrorMessage = InvalidLink;
            return;
        }

        Email = user.Email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await FindUserAsync();
        if (user is null || Token is not { } token)
        {
            ErrorMessage = InvalidLink;
            return Page();
        }

        Email = user.Email;
        if (Input.Password != Input.Confirm)
        {
            ModelState.AddModelError("Input.Confirm", "Hasła różnią się. Wpisz to samo hasło dwa razy.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await users.ResetPasswordAsync(user, token, Input.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                Email = null;
                ErrorMessage = InvalidLink;
                return Page();
            }

            ModelState.AddModelError("Input.Password", PasswordRules);
            return Page();
        }

        // Opening the link from the e-mail address HR entered proves it belongs to this person.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
        }

        Done = true;
        return Page();
    }

    public const string PasswordRules = "Hasło musi mieć co najmniej 12 znaków, w tym wielką i małą literę, cyfrę oraz znak specjalny.";

    private string? Token
    {
        get
        {
            try
            {
                return string.IsNullOrEmpty(Code) ? null : Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    private Task<ApplicationUser?> FindUserAsync() => UserId == Guid.Empty ? Task.FromResult<ApplicationUser?>(null) : users.FindByIdAsync(UserId.ToString());

    public sealed class ActivateInput
    {
        [Required(ErrorMessage = "Wpisz nowe hasło.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wpisz hasło jeszcze raz.")]
        [DataType(DataType.Password)]
        public string Confirm { get; set; } = string.Empty;
    }
}
