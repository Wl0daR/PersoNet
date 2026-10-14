using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersoNet.Identity.Data;

namespace PersoNet.Identity.Pages.Account;

public sealed class LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Only local URLs, so the page cannot be used to redirect to another site.
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
        }

        if (result.IsLockedOut)
        {
            LoginLog.LockedOut(logger);
            var user = await signInManager.UserManager.FindByEmailAsync(Input.Email);
            ErrorMessage = user?.LockoutEnd == DateTimeOffset.MaxValue
                ? "Konto zostało zablokowane przez dział kadr. Skontaktuj się z nim, jeśli to pomyłka."
                : "Konto zostało czasowo zablokowane po kilku nieudanych próbach. Spróbuj ponownie później.";
        }
        else
        {
            ErrorMessage = "Nieprawidłowy e-mail lub hasło.";
        }

        return Page();
    }

    public sealed class LoginInput
    {
        [Required(ErrorMessage = "Podaj adres e-mail.")]
        [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Podaj hasło.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}

internal static partial class LoginLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "An account was locked out after failed sign-in attempts.")]
    public static partial void LockedOut(ILogger logger);
}
