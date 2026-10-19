using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Safety;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class TrainingCourseConfiguration : IEntityTypeConfiguration<TrainingCourse>
{
    public void Configure(EntityTypeBuilder<TrainingCourse> builder)
    {
        builder.ToTable("training_courses");

        builder.HasKey(course => course.Id);
        builder.Property(course => course.Id).ValueGeneratedNever();

        builder.Property(course => course.Name).HasMaxLength(TrainingCourse.NameMaxLength).IsRequired();
        builder.Property(course => course.NormalizedName).HasMaxLength(TrainingCourse.NameMaxLength).IsRequired();
        builder.HasIndex(course => new { course.TenantId, course.NormalizedName }).IsUnique();
        builder.Property(course => course.Description).HasMaxLength(TrainingCourse.DescriptionMaxLength);
        builder.Property(course => course.DepartmentIds).IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(course => course.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EmployeeTrainingConfiguration : IEntityTypeConfiguration<EmployeeTraining>
{
    public void Configure(EntityTypeBuilder<EmployeeTraining> builder)
    {
        builder.ToTable("employee_trainings");

        builder.HasKey(training => training.Id);
        builder.Property(training => training.Id).ValueGeneratedNever();
        builder.Property(training => training.Note).HasMaxLength(EmployeeTraining.NoteMaxLength);

        builder.HasIndex(training => new { training.EmployeeId, training.CourseId, training.CompletedOn });
        builder.HasIndex(training => training.ValidUntil);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(training => training.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(training => training.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainingCourse>().WithMany().HasForeignKey(training => training.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MedicalExamConfiguration : IEntityTypeConfiguration<MedicalExam>
{
    public void Configure(EntityTypeBuilder<MedicalExam> builder)
    {
        builder.ToTable("medical_exams");

        builder.HasKey(exam => exam.Id);
        builder.Property(exam => exam.Id).ValueGeneratedNever();

        // Stored as text, like the other kinds, so the database stays readable.
        builder.Property(exam => exam.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(exam => exam.Note).HasMaxLength(MedicalExam.NoteMaxLength);

        builder.HasIndex(exam => new { exam.EmployeeId, exam.ExamDate });
        builder.HasIndex(exam => exam.ValidUntil);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(exam => exam.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(exam => exam.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
