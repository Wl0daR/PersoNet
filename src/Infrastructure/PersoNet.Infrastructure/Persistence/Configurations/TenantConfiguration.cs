using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();

        builder.Property(tenant => tenant.Name).HasMaxLength(Tenant.NameMaxLength).IsRequired();
        builder.Property(tenant => tenant.Subdomain).HasMaxLength(Tenant.SubdomainMaxLength).IsRequired();
        builder.HasIndex(tenant => tenant.Subdomain).IsUnique();
    }
}
