using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id).ValueGeneratedNever();

        builder.Property(employee => employee.Number).HasMaxLength(Employee.NumberMaxLength).IsRequired();
        builder.Property(employee => employee.FirstName).HasMaxLength(Employee.NameMaxLength).IsRequired();
        builder.Property(employee => employee.LastName).HasMaxLength(Employee.NameMaxLength).IsRequired();
        builder.Property(employee => employee.Email).HasMaxLength(Employee.EmailMaxLength);
        builder.Property(employee => employee.Phone).HasMaxLength(Employee.PhoneMaxLength);
        builder.Property(employee => employee.SearchText).HasMaxLength(512).IsRequired();
        builder.Ignore(employee => employee.FullName);

        builder.HasIndex(employee => new { employee.TenantId, employee.Number }).IsUnique();
        builder.HasIndex(employee => new { employee.TenantId, employee.Email }).IsUnique().HasFilter("email IS NOT NULL");
        builder.HasIndex(employee => new { employee.TenantId, employee.LastName, employee.FirstName });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(employee => employee.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(employee => employee.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Position>().WithMany().HasForeignKey(employee => employee.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(employee => employee.ManagerId).OnDelete(DeleteBehavior.Restrict);
    }
}
