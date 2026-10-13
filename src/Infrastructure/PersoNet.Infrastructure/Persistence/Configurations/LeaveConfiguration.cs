using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();

        // Stored as text so the database stays readable and reordering the enums cannot corrupt data.
        builder.Property(request => request.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.Note).HasMaxLength(LeaveRequest.NoteMaxLength);
        builder.Property(request => request.DecisionNote).HasMaxLength(LeaveRequest.NoteMaxLength);
        builder.Property(request => request.Hours).HasPrecision(6, 2);
        builder.Ignore(request => request.IsActive);
        builder.Ignore(request => request.UsesAnnualLeave);

        builder.HasIndex(request => new { request.EmployeeId, request.StartDate });
        builder.HasIndex(request => new { request.Status, request.StartDate });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(request => request.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(request => request.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LeaveEntitlementConfiguration : IEntityTypeConfiguration<LeaveEntitlement>
{
    public void Configure(EntityTypeBuilder<LeaveEntitlement> builder)
    {
        builder.ToTable("leave_entitlements");

        builder.HasKey(entitlement => entitlement.Id);
        builder.Property(entitlement => entitlement.Id).ValueGeneratedNever();
        builder.Ignore(entitlement => entitlement.Total);
        builder.Ignore(entitlement => entitlement.TotalHours);

        // One entitlement per employee and year.
        builder.HasIndex(entitlement => new { entitlement.TenantId, entitlement.EmployeeId, entitlement.Year }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(entitlement => entitlement.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(entitlement => entitlement.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
