namespace PersoNet.Desktop.Presentation.Access;

/// <summary>
/// What the signed-in user may do, as the API reported after sign-in. Screens and tabs the user has no permission for are
/// hidden instead of failing; the API checks every call anyway.
/// </summary>
public sealed class UserAccess
{
    public const string EmployeesRead = "employees.read";
    public const string EmployeesManage = "employees.manage";
    public const string LeaveRead = "leave.read";
    public const string LeaveManage = "leave.manage";
    public const string OrganizationManage = "organization.manage";
    public const string AccountsManage = "accounts.manage";
    public const string AuditRead = "audit.read";
    public const string RolesManage = "roles.manage";
    public const string SettingsManage = "settings.manage";
    public const string ReportsRead = "reports.read";
    public const string WorkTimeRead = "worktime.read";
    public const string WorkTimeManage = "worktime.manage";
    public const string SafetyRead = "safety.read";
    public const string SafetyManage = "safety.manage";

    private HashSet<string> _permissions = [];

    /// <summary>Raised after sign-in and sign-out, when the permissions change.</summary>
    public event EventHandler? Changed;

    public IReadOnlySet<string> Permissions => _permissions;

    public bool Can(string permission) => _permissions.Contains(permission);

    public void Set(IEnumerable<string> permissions)
    {
        _permissions = permissions.ToHashSet(StringComparer.Ordinal);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear() => Set([]);
}
