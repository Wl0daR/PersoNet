using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Tenants;
using PersoNet.Domain.WorkTime;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class WorkDayConfiguration : IEntityTypeConfiguration<WorkDay>
{
    public void Configure(EntityTypeBuilder<WorkDay> builder)
    {
        builder.ToTable("work_days");

        builder.HasKey(day => day.Id);
        builder.Property(day => day.Id).ValueGeneratedNever();

        // Stored as text so the database stays readable and reordering the enums cannot corrupt data.
        builder.Property(day => day.Absence).HasConversion<string>().HasMaxLength(20);
        builder.Property(day => day.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(day => day.Problem).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(day => day.Note).HasMaxLength(WorkDay.NoteMaxLength);
        builder.Ignore(day => day.IsWork);
        builder.Ignore(day => day.WorkedMinutes);

        // One record per employee and day.
        builder.HasIndex(day => new { day.TenantId, day.EmployeeId, day.Date }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(day => day.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(day => day.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClockEventConfiguration : IEntityTypeConfiguration<ClockEvent>
{
    public void Configure(EntityTypeBuilder<ClockEvent> builder)
    {
        builder.ToTable("clock_events");

        builder.HasKey(clock => clock.Id);
        builder.Property(clock => clock.Id).ValueGeneratedNever();

        // The company's local time as the reader showed it, without a time zone.
        builder.Property(clock => clock.OccurredAt).HasColumnType("timestamp without time zone");
        builder.Property(clock => clock.Direction).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(clock => clock.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(clock => clock.Device).HasMaxLength(ClockEvent.DeviceMaxLength);
        builder.Ignore(clock => clock.Date);

        // The same event imported twice is stored once.
        builder.HasIndex(clock => new { clock.TenantId, clock.EmployeeId, clock.OccurredAt, clock.Direction }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(clock => clock.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(clock => clock.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
