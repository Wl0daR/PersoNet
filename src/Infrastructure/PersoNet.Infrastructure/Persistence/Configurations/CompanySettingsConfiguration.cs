using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Company;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("company_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        builder.Property(settings => settings.Name).HasMaxLength(CompanySettings.NameMaxLength).IsRequired();
        builder.Property(settings => settings.LegalName).HasMaxLength(CompanySettings.LegalNameMaxLength);
        builder.Property(settings => settings.TaxId).HasMaxLength(10);
        builder.Property(settings => settings.Regon).HasMaxLength(14);
        builder.Property(settings => settings.Street).HasMaxLength(CompanySettings.StreetMaxLength);
        builder.Property(settings => settings.PostalCode).HasMaxLength(6);
        builder.Property(settings => settings.City).HasMaxLength(CompanySettings.CityMaxLength);
        builder.Property(settings => settings.Email).HasMaxLength(CompanySettings.EmailMaxLength);
        builder.Property(settings => settings.Phone).HasMaxLength(CompanySettings.PhoneMaxLength);

        // Stored as names, like the leave requests themselves, so reordering the enum cannot change the settings.
        builder.PrimitiveCollection(settings => settings.PortalLeaveTypes).IsRequired().ElementType().HasConversion<string>();

        builder.Property(settings => settings.WorkTimeSource).HasConversion<string>().HasMaxLength(20).IsRequired();

        // One record per company.
        builder.HasIndex(settings => settings.TenantId).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(settings => settings.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
