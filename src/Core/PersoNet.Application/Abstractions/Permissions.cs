namespace PersoNet.Application.Abstractions;

/// <summary>
/// What a user may do. Endpoints require permissions, never roles (see docs/adr/0004).
/// </summary>
public static class Permissions
{
    /// <summary>Create and change departments and positions.</summary>
    public const string OrganizationManage = "organization.manage";

    /// <summary>See employee cards and the employee list (personal data, so HR only for now).</summary>
    public const string EmployeesRead = "employees.read";

    /// <summary>Hire, edit and terminate employees.</summary>
    public const string EmployeesManage = "employees.manage";

    /// <summary>See leave requests, balances and entitlements of all employees.</summary>
    public const string LeaveRead = "leave.read";

    /// <summary>Register leave requests, approve, reject or cancel them, and set annual leave entitlements.</summary>
    public const string LeaveManage = "leave.manage";

    /// <summary>Give employees access to the portal (accounts, links to set the password) and block it.</summary>
    public const string AccountsManage = "accounts.manage";

    public static IReadOnlyList<string> All { get; } = [OrganizationManage, EmployeesRead, EmployeesManage, LeaveRead, LeaveManage, AccountsManage];
}
