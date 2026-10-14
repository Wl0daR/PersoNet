using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.WorkTime;

/// <summary>
/// Dialog of one day of the timesheet: the hours worked (from, to, break) or an absence, with a note. Shows the clock
/// events of the day, so HR sees what the reader recorded before correcting it.
/// </summary>
public sealed partial class WorkDayDialogViewModel : DialogViewModel
{
    public const int NoteMaxLength = 200;

    private readonly IWorkTimeApi _api;
    private readonly Guid _employeeId;
    private readonly TimesheetDay _day;

    public WorkDayDialogViewModel(IWorkTimeApi api, Guid employeeId, TimesheetDay day)
    {
        ArgumentNullException.ThrowIfNull(day);

        _api = api;
        _employeeId = employeeId;
        _day = day;

        IsAbsence = day.Absence is not null;
        Absence = WorkTimeTexts.Absences.FirstOrDefault(choice => choice.Kind == day.Absence) ?? WorkTimeTexts.Absences[0];
        Note = day.Note ?? string.Empty;
        BreakText = day.BreakMinutes > 0 ? day.BreakMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";

        // A day with nothing yet starts from the plan: 8:00 and the planned hours.
        var start = day.Start ?? (day.PlannedHours > 0 ? new TimeOnly(8, 0) : null);
        var end = day.End ?? (day.Start is null && start is { } planned ? planned.AddMinutes((double)(day.PlannedHours * 60)) : null);
        StartText = start is { } from ? WorkTimeTexts.Time(from) : string.Empty;
        EndText = end is { } to ? WorkTimeTexts.Time(to) : string.Empty;
    }

    public override string Title => $"Czas pracy – {WorkTimeTexts.LongDay(_day.Date)}";

    public static IReadOnlyList<AbsenceChoice> Absences => WorkTimeTexts.Absences;

    /// <summary>The kind of day and the plan, e.g. "Dzień roboczy, plan 8:00".</summary>
    public string DayInfo => _day.Kind switch
    {
        TimesheetDayKind.Working => _day.PlannedHours > 0 ? $"Dzień roboczy, plan {WorkTimeTexts.Duration(_day.PlannedHours)}" : "Dzień roboczy, bez umowy o pracę",
        TimesheetDayKind.DayOff => "Sobota, dzień wolny z pięciodniowego tygodnia pracy",
        TimesheetDayKind.Sunday => _day.HolidayName is { } name ? $"Niedziela, {name}" : "Niedziela",
        _ => $"Święto: {_day.HolidayName}",
    };

    /// <summary>The reader's events of the day, e.g. "07:55 wejście (Wejście główne) · 16:05 wyjście".</summary>
    public string? EventsText => _day.Events.Count == 0 ? null
        : string.Join(" · ", _day.Events.Select(e =>
            $"{WorkTimeTexts.Time(TimeOnly.FromDateTime(e.OccurredAt))} {(e.Direction == ClockEventDirection.In ? "wejście" : "wyjście")}"
            + (e.Device is { } device ? $" ({device})" : string.Empty)));

    public bool HasEvents => _day.Events.Count > 0;

    /// <summary>Hourly leave on the day, which stays as it is.</summary>
    public string? LeaveText => _day.Leave is { } leave ? $"{LeaveTexts.KindName(leave)}: {WorkTimeTexts.Duration(_day.AbsenceHours)} godz." : null;

    public bool HasLeave => _day.Leave is not null;

    /// <summary>The day has something to remove: what was typed, or the clock's hours to take again.</summary>
    public bool CanClear => _day.IsRecorded;

    public string ClearText => _day.Events.Count > 0 && _day.IsManual ? "Przywróć z odbić" : "Usuń wpis";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWork))]
    public partial bool IsAbsence { get; set; }

    public bool IsWork
    {
        get => !IsAbsence;
        set => IsAbsence = !value;
    }

    [ObservableProperty]
    public partial string StartText { get; set; }

    [ObservableProperty]
    public partial string EndText { get; set; }

    [ObservableProperty]
    public partial string BreakText { get; set; }

    [ObservableProperty]
    public partial AbsenceChoice Absence { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; }

    /// <summary>The month's timesheet after a save, so the card shows the new totals.</summary>
    public TimesheetResponse? Saved { get; private set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SetError(nameof(Note), Note.Trim().Length > NoteMaxLength ? $"Uwagi mogą mieć najwyżej {NoteMaxLength} znaków." : null);

        SaveWorkDayRequest request;
        if (IsAbsence)
        {
            SetError(nameof(StartText), null);
            SetError(nameof(EndText), null);
            SetError(nameof(BreakText), null);
            request = new SaveWorkDayRequest(null, null, 0, Absence.Kind, Note);
        }
        else
        {
            var start = WorkTimeTexts.ParseTime(StartText);
            var end = WorkTimeTexts.ParseTime(EndText);
            var hasBreak = int.TryParse(BreakText.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var breakMinutes)
                || string.IsNullOrWhiteSpace(BreakText);

            SetError(nameof(StartText), start is null ? "Podaj godzinę, np. 8:00." : null);
            SetError(nameof(EndText), end is null ? "Podaj godzinę, np. 16:00. Wcześniejsza niż początek oznacza następny dzień." : null);
            SetError(nameof(BreakText), hasBreak ? null : "Podaj liczbę minut, np. 30, albo 0.");
            request = new SaveWorkDayRequest(start, end, breakMinutes, null, Note);
        }

        if (HasErrors)
        {
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            Saved = await _api.SaveDayAsync(_employeeId, _day.Date, request, cancellationToken);
            HasChanges = true;
            Close(true);
        });
    }

    [RelayCommand]
    public Task ClearAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        Saved = await _api.ClearDayAsync(_employeeId, _day.Date, cancellationToken);
        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "worktime.times_required" => nameof(StartText),
            "worktime.same_times" => nameof(EndText),
            "worktime.break_invalid" => nameof(BreakText),
            "worktime.note_too_long" => nameof(Note),
            "worktime.absence_unknown" => nameof(Absence),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }
}
