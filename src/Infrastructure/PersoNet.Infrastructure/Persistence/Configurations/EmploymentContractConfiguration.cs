using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class EmploymentContractConfiguration : IEntityTypeConfiguration<EmploymentContract>
{
    public void Configure(EntityTypeBuilder<EmploymentContract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.Id).ValueGeneratedNever();

        // Stored as text so the database stays readable and reordering the enum cannot corrupt data.
        builder.Property(contract => contract.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(contract => contract.WorkingTime).HasPrecision(4, 3);
        builder.Ignore(contract => contract.IsEmployment);
        builder.Ignore(contract => contract.LastDay);

        builder.HasIndex(contract => new { contract.EmployeeId, contract.StartDate });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(contract => contract.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(contract => contract.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
