using Microsoft.AspNetCore.Identity;

namespace PersoNet.Identity.Data;

/// <summary>
/// A sign-in account. Each account belongs to exactly one company (tenant).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public Guid TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
