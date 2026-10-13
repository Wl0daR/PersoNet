using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(department => department.Id);
        builder.Property(department => department.Id).ValueGeneratedNever();

        builder.Property(department => department.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(department => department.Code).HasMaxLength(Department.CodeMaxLength).IsRequired();
        builder.HasIndex(department => new { department.TenantId, department.Code }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(department => department.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(department => department.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
