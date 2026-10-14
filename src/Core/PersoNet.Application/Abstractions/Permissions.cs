namespace PersoNet.Application.Abstractions;

/// <summary>
/// What a user may do. Endpoints require permissions, never roles (see docs/adr/0004). The company's HR administrator
/// has all of them; other people get them through the roles the company defines (<see cref="Domain.Access.Role"/>).
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

    /// <summary>See who changed an employee's data and when (the audit log).</summary>
    public const string AuditRead = "audit.read";

    /// <summary>Define roles and give them to people.</summary>
    public const string RolesManage = "roles.manage";

    /// <summary>Change the company's data and how it runs leave.</summary>
    public const string SettingsManage = "settings.manage";

    /// <summary>See company-wide reports (headcount, use of leave, contracts that end) and export them.</summary>
    public const string ReportsRead = "reports.read";

    /// <summary>See employees' timesheets: hours worked, overtime, absences and clock events.</summary>
    public const string WorkTimeRead = "worktime.read";

    /// <summary>Type and correct working days, import clock events and set employees' time clock identifiers.</summary>
    public const string WorkTimeManage = "worktime.manage";

    public static IReadOnlyList<string> All { get; } =
        [OrganizationManage, EmployeesRead, EmployeesManage, LeaveRead, LeaveManage, WorkTimeRead, WorkTimeManage, AccountsManage, AuditRead, RolesManage, SettingsManage, ReportsRead];

    /// <summary>
    /// Permissions a company role can grant. Managing portal accounts stays with the HR administrator, because the login
    /// server accepts it only from them.
    /// </summary>
    public static IReadOnlySet<string> Assignable { get; } = All.Where(code => code != AccountsManage).ToHashSet(StringComparer.Ordinal);
}
