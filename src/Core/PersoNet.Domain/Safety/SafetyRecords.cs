using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Safety;

/// <summary>Kind of occupational medical examination (Kodeks pracy, art. 229).</summary>
public enum MedicalExamType
{
    /// <summary>Badanie wstępne: before starting work.</summary>
    Initial = 1,

    /// <summary>Badanie okresowe: repeated while employed, as the doctor decides.</summary>
    Periodic = 2,

    /// <summary>Badanie kontrolne: after a sickness of more than 30 days.</summary>
    Control = 3,
}

/// <summary>
/// An occupational medical examination of an employee with the date the doctor's certificate is valid until, so HR knows
/// when to send the person to the next one. Without a valid certificate the employee may not work.
/// </summary>
public sealed class MedicalExam : Entity, ITenantOwned
{
    public const int NoteMaxLength = 500;

    // Used by EF Core.
    private MedicalExam()
    {
    }

    private MedicalExam(Guid tenantId, Guid employeeId, DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public MedicalExamType Type { get; private set; }

    public DateOnly ExamDate { get; private set; }

    /// <summary>Last day the certificate is valid; null when the doctor gave no date.</summary>
    public DateOnly? ValidUntil { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MedicalExam Create(Guid tenantId, Guid employeeId, MedicalExamType type, DateOnly examDate, DateOnly? validUntil, string? note, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Medical exam must belong to a tenant and an employee.");
        }

        var exam = new MedicalExam(tenantId, employeeId, createdAt);
        exam.Change(type, examDate, validUntil, note);
        return exam;
    }

    public void Change(MedicalExamType type, DateOnly examDate, DateOnly? validUntil, string? note)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Unknown medical exam type.");
        }

        if (validUntil < examDate)
        {
            throw new DomainException("A certificate cannot expire before the examination.");
        }

        Type = type;
        ExamDate = examDate;
        ValidUntil = validUntil;
        Note = SafetyNotes.Clean(note, NoteMaxLength);
    }
}

/// <summary>A training an employee completed, with the date it is valid until when it has to be repeated.</summary>
public sealed class EmployeeTraining : Entity, ITenantOwned
{
    public const int NoteMaxLength = 500;

    // Used by EF Core.
    private EmployeeTraining()
    {
    }

    private EmployeeTraining(Guid tenantId, Guid employeeId, DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public Guid CourseId { get; private set; }

    public DateOnly CompletedOn { get; private set; }

    /// <summary>Last day the training is valid; null when it does not expire.</summary>
    public DateOnly? ValidUntil { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static EmployeeTraining Create(Guid tenantId, Guid employeeId, Guid courseId, DateOnly completedOn, DateOnly? validUntil, string? note, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Training must belong to a tenant and an employee.");
        }

        var training = new EmployeeTraining(tenantId, employeeId, createdAt);
        training.Change(courseId, completedOn, validUntil, note);
        return training;
    }

    public void Change(Guid courseId, DateOnly completedOn, DateOnly? validUntil, string? note)
    {
        if (courseId == Guid.Empty)
        {
            throw new DomainException("Training must be of a course.");
        }

        if (validUntil < completedOn)
        {
            throw new DomainException("A training cannot expire before it was completed.");
        }

        CourseId = courseId;
        CompletedOn = completedOn;
        ValidUntil = validUntil;
        Note = SafetyNotes.Clean(note, NoteMaxLength);
    }
}

internal static class SafetyNotes
{
    public static string? Clean(string? note, int maxLength)
    {
        var cleaned = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return cleaned is { Length: var length } && length > maxLength
            ? throw new DomainException($"A note can have at most {maxLength} characters.")
            : cleaned;
    }
}
