using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Absences;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> builder)
    {
        builder.ToTable("absences");

        builder.HasKey(absence => absence.Id);
        builder.Property(absence => absence.Id).ValueGeneratedNever();

        // Stored as text, like the leave kinds, so the database stays readable.
        builder.Property(absence => absence.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(absence => absence.Note).HasMaxLength(Absence.NoteMaxLength);
        builder.Ignore(absence => absence.CalendarDays);
        builder.Ignore(absence => absence.IsSickness);
        builder.Ignore(absence => absence.IsWorkElsewhere);

        builder.HasIndex(absence => new { absence.EmployeeId, absence.StartDate });
        builder.HasIndex(absence => new { absence.StartDate, absence.EndDate });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(absence => absence.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(absence => absence.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
