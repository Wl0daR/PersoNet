using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Audit;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.UserName).HasMaxLength(256);
        builder.Property(entry => entry.EntityType).HasMaxLength(64);
        builder.Property(entry => entry.Action).HasConversion<string>().HasMaxLength(16);
        builder.Property(entry => entry.Changes).HasColumnType("jsonb");
        builder.Property(entry => entry.State).HasColumnType("jsonb");

        // The card shows an employee's history newest first; a record's history is looked up by its id.
        builder.HasIndex(entry => new { entry.EmployeeId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(entry => entry.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
