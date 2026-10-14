using PersoNet.Contracts.Leave;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Contracts.Company;

/// <summary>
/// The company's data and settings. <see cref="PortalLeaveKinds"/> are the kinds of leave employees request in the
/// portal themselves; HR registers the others. <see cref="WorkTimeSource"/> is how working time is recorded.
/// </summary>
public sealed record CompanyResponse(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Regon,
    string? Street,
    string? PostalCode,
    string? City,
    string? Email,
    string? Phone,
    IReadOnlyList<LeaveKind> PortalLeaveKinds,
    WorkTimeSourceKind WorkTimeSource);

/// <summary>The company's data; NIP and REGON may be typed with spaces, dashes or a "PL" prefix.</summary>
public sealed record SaveCompanyDetailsRequest(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Regon,
    string? Street,
    string? PostalCode,
    string? City,
    string? Email,
    string? Phone);

/// <summary>All kinds of leave employees may request in the portal; the ones left out are for HR only.</summary>
public sealed record SaveLeaveSettingsRequest(IReadOnlyList<LeaveKind> PortalLeaveKinds);

/// <summary>How the company records working time.</summary>
public sealed record SaveWorkTimeSettingsRequest(WorkTimeSourceKind Source);
