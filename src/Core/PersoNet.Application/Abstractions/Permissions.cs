namespace PersoNet.Application.Abstractions;

/// <summary>
/// What a user may do. Endpoints require permissions, never roles (see docs/adr/0004).
/// </summary>
public static class Permissions
{
    /// <summary>Create and change departments and positions.</summary>
    public const string OrganizationManage = "organization.manage";

    public static IReadOnlyList<string> All { get; } = [OrganizationManage];
}
