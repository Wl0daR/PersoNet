using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.Leave;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Company;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Company;

/// <summary>
/// The company's data and leave settings. A company that never saved them has the defaults: its registered name and
/// every kind of leave in the portal. The record is created on the first save.
/// </summary>
public sealed class CompanyService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<Result<CompanyResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await CurrentAsync(tracked: false, cancellationToken);
        return settings.IsSuccess ? ToResponse(settings.Value) : settings.Error!;
    }

    /// <summary>Whether employees may request this kind of leave in the portal themselves.</summary>
    public async Task<Result> CheckPortalLeaveKindAsync(LeaveKind kind, CancellationToken cancellationToken)
    {
        var settings = await CurrentAsync(tracked: false, cancellationToken);
        if (!settings.IsSuccess)
        {
            return settings.Error!;
        }

        return settings.Value.AllowsInPortal((LeaveType)kind) ? Result.Success() : CompanyErrors.LeaveKindNotInPortal;
    }

    public async Task<Result<CompanyResponse>> SaveDetailsAsync(SaveCompanyDetailsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Check(request) is { } error)
        {
            return error;
        }

        var settings = await CurrentAsync(tracked: true, cancellationToken);
        if (!settings.IsSuccess)
        {
            return settings.Error!;
        }

        try
        {
            settings.Value.ChangeDetails(new CompanyDetails(
                request.Name, request.LegalName, request.TaxId, request.Regon, request.Street, request.PostalCode, request.City, request.Email, request.Phone));
        }
        catch (DomainException exception)
        {
            return CompanyErrors.Invalid(exception);
        }

        // The registered name follows, so every place that shows the company uses the same name.
        var company = await db.Tenants.FirstAsync(company => company.Id == settings.Value.TenantId, cancellationToken);
        company.Rename(settings.Value.Name);

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(settings.Value);
    }

    public async Task<Result<CompanyResponse>> SaveLeaveSettingsAsync(SaveLeaveSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PortalLeaveKinds.Any(kind => !Enum.IsDefined((LeaveType)kind)))
        {
            return CompanyErrors.UnknownLeaveKind;
        }

        var settings = await CurrentAsync(tracked: true, cancellationToken);
        if (!settings.IsSuccess)
        {
            return settings.Error!;
        }

        settings.Value.ChangePortalLeaveTypes(request.PortalLeaveKinds.Select(kind => (LeaveType)kind));
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(settings.Value);
    }

    /// <summary>The saved settings, or new defaults (added to the context when they will be saved).</summary>
    private async Task<Result<CompanySettings>> CurrentAsync(bool tracked, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return CompanyErrors.NoTenant;
        }

        var query = tracked ? db.CompanySettings : db.CompanySettings.AsNoTracking();
        if (await query.FirstOrDefaultAsync(cancellationToken) is { } saved)
        {
            return saved;
        }

        var name = await db.Tenants.AsNoTracking().Where(company => company.Id == tenantId).Select(company => company.Name).FirstOrDefaultAsync(cancellationToken);
        if (name is null)
        {
            return CompanyErrors.NoTenant;
        }

        var defaults = CompanySettings.CreateDefault(tenantId, name);
        if (tracked)
        {
            db.CompanySettings.Add(defaults);
        }

        return defaults;
    }

    private static AppError? Check(SaveCompanyDetailsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return CompanyErrors.NameRequired;
        }

        if (request.Name.Trim().Length > CompanySettings.NameMaxLength)
        {
            return CompanyErrors.NameTooLong;
        }

        if (CompanySettings.Digits(request.TaxId) is { } taxId && !CompanySettings.IsValidTaxId(taxId))
        {
            return CompanyErrors.TaxIdInvalid;
        }

        if (CompanySettings.Digits(request.Regon) is { } regon && !CompanySettings.IsValidRegon(regon))
        {
            return CompanyErrors.RegonInvalid;
        }

        if (!string.IsNullOrWhiteSpace(request.PostalCode) && !CompanySettings.IsValidPostalCode(request.PostalCode.Trim()))
        {
            return CompanyErrors.PostalCodeInvalid;
        }

        return !string.IsNullOrWhiteSpace(request.Email) && !CompanySettings.IsValidEmail(request.Email.Trim())
            ? CompanyErrors.EmailInvalid
            : null;
    }

    private static CompanyResponse ToResponse(CompanySettings settings) => new(
        settings.Name,
        settings.LegalName,
        settings.TaxId,
        settings.Regon,
        settings.Street,
        settings.PostalCode,
        settings.City,
        settings.Email,
        settings.Phone,
        settings.PortalLeaveTypes.Select(type => (LeaveKind)type).ToList());
}
