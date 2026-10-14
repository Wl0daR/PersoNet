using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.WorkTime;

/// <summary>Whether a person came in or went out.</summary>
public enum ClockDirection
{
    In = 1,
    Out = 2,
}

/// <summary>How an event reached PersoNet.</summary>
public enum ClockEventSource
{
    /// <summary>Typed by HR, for example a forgotten clock-out.</summary>
    Manual = 1,

    /// <summary>Imported from a file exported by a time clock system.</summary>
    File = 2,

    /// <summary>Sent by a connected time clock system.</summary>
    Integration = 3,
}

/// <summary>
/// One clock-in or clock-out of an employee ("odbicie"), as the time clock system (RCP) recorded it. Events are kept as
/// they came, as evidence; the working days are worked out from them (<see cref="ClockPairing"/>).
/// </summary>
public sealed class ClockEvent : Entity, ITenantOwned
{
    public const int DeviceMaxLength = 100;

    // Used by EF Core.
    private ClockEvent()
    {
    }

    private ClockEvent(Guid tenantId, Guid employeeId, DateTime occurredAt, ClockDirection direction, ClockEventSource source, string? device, DateTimeOffset recordedAt)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        OccurredAt = occurredAt;
        Direction = direction;
        Source = source;
        Device = device;
        RecordedAt = recordedAt;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    /// <summary>Local wall-clock time of the event at the company, to the second, as the reader showed it.</summary>
    public DateTime OccurredAt { get; private set; }

    public ClockDirection Direction { get; private set; }

    public ClockEventSource Source { get; private set; }

    /// <summary>The reader or terminal, when the system says which.</summary>
    public string? Device { get; private set; }

    /// <summary>When PersoNet received the event.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public DateOnly Date => DateOnly.FromDateTime(OccurredAt);

    public static ClockEvent Record(
        Guid tenantId, Guid employeeId, DateTime occurredAt, ClockDirection direction, ClockEventSource source, string? device, DateTimeOffset recordedAt)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("A clock event must belong to a tenant and an employee.");
        }

        if (!Enum.IsDefined(direction) || !Enum.IsDefined(source))
        {
            throw new DomainException("Unknown kind of clock event.");
        }

        var trimmed = device?.Trim();
        if (trimmed?.Length > DeviceMaxLength)
        {
            throw new DomainException($"Device name cannot exceed {DeviceMaxLength} characters.");
        }

        return new ClockEvent(
            tenantId,
            employeeId,
            WholeSeconds(occurredAt),
            direction,
            source,
            string.IsNullOrEmpty(trimmed) ? null : trimmed,
            recordedAt);
    }

    /// <summary>The time without fractions of a second and without a time zone, as events are compared and stored.</summary>
    public static DateTime WholeSeconds(DateTime time) =>
        DateTime.SpecifyKind(new DateTime(time.Ticks - (time.Ticks % TimeSpan.TicksPerSecond)), DateTimeKind.Unspecified);
}
