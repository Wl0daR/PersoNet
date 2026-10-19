using System.Globalization;
using PersoNet.Contracts.Safety;

namespace PersoNet.Desktop.Presentation.Safety;

/// <summary>How urgent a medical certificate or a training is, for the colour of its label.</summary>
public enum SafetyTone
{
    None,
    Ok,
    Warning,
    Critical,
}

/// <summary>Polish names and the validity of examinations and trainings, as both the card and the report show them.</summary>
public static class SafetyTexts
{
    /// <summary>A certificate or training that runs out within this many days is shown as running out.</summary>
    public const int ExpiringSoonDays = 30;

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string Name(MedicalExamKind kind) => kind switch
    {
        MedicalExamKind.Initial => "Badanie wstępne",
        MedicalExamKind.Periodic => "Badanie okresowe",
        MedicalExamKind.Control => "Badanie kontrolne",
        _ => kind.ToString(),
    };

    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", Polish);

    /// <summary>"co 3 lata", "co 18 miesięcy" or "bezterminowo".</summary>
    public static string Validity(int? months) => months switch
    {
        null => "bezterminowo",
        var m when m % 12 == 0 => $"co {Years(m.Value / 12)}",
        1 => "co miesiąc",
        var m => $"co {m} {(m % 10 is >= 2 and <= 4 && m % 100 is < 12 or > 14 ? "miesiące" : "miesięcy")}",
    };

    /// <summary>The state of the latest certificate or training on <paramref name="today"/>: text and tone.</summary>
    public static (string Text, SafetyTone Tone) State(DateOnly? validUntil, DateOnly today, bool isCurrent)
    {
        if (!isCurrent)
        {
            return ("zastąpione nowszym", SafetyTone.None);
        }

        if (validUntil is not { } until)
        {
            return ("bezterminowo", SafetyTone.Ok);
        }

        var days = until.DayNumber - today.DayNumber;
        return days switch
        {
            < 0 => ("nieważne", SafetyTone.Critical),
            0 => ("ważne do dziś", SafetyTone.Warning),
            <= ExpiringSoonDays => ($"ważne jeszcze {(days == 1 ? "1 dzień" : $"{days} dni")}", SafetyTone.Warning),
            _ => ("ważne", SafetyTone.Ok),
        };
    }

    private static string Years(int years) => years switch
    {
        1 => "rok",
        _ when years % 10 is >= 2 and <= 4 && years % 100 is < 12 or > 14 => $"{years} lata",
        _ => $"{years} lat",
    };
}
