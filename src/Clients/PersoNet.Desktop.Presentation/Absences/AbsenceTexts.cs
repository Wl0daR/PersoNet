using PersoNet.Contracts.Absences;

namespace PersoNet.Desktop.Presentation.Absences;

/// <summary>Polish names of the absence kinds.</summary>
public static class AbsenceTexts
{
    public static string Name(AbsenceKind kind) => kind switch
    {
        AbsenceKind.Sickness => "Choroba (L4)",
        AbsenceKind.ChildCareAllowance => "Opieka nad chorym dzieckiem",
        AbsenceKind.FamilyCareAllowance => "Opieka nad chorym członkiem rodziny",
        AbsenceKind.Rehabilitation => "Świadczenie rehabilitacyjne",
        AbsenceKind.WorkAccident => "Choroba po wypadku przy pracy",
        AbsenceKind.Unexcused => "Nieobecność nieusprawiedliwiona",
        AbsenceKind.OtherExcused => "Inna nieobecność usprawiedliwiona",
        AbsenceKind.BusinessTrip => "Delegacja",
        AbsenceKind.Training => "Szkolenie",
        AbsenceKind.MedicalExam => "Badania lekarskie",
        AbsenceKind.TradeFair => "Targi",
        AbsenceKind.RemoteWork => "Praca zdalna (home office)",
        _ => kind.ToString(),
    };

    /// <summary>Sickness and the allowances paid by ZUS instead of wages.</summary>
    public static bool IsSickness(AbsenceKind kind) => kind is >= AbsenceKind.Sickness and <= AbsenceKind.WorkAccident;

    public static string Days(int days) => days == 1 ? "1 dzień" : $"{days} dni";
}
