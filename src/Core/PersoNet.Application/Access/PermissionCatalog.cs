using PersoNet.Application.Abstractions;
using PersoNet.Contracts.Access;

namespace PersoNet.Application.Access;

/// <summary>Polish names of the permissions, in the order HR sees them when setting up a role.</summary>
public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionResponse> All { get; } =
    [
        Describe(Permissions.EmployeesRead, "Pracownicy", "Przeglądanie kartotek", "Lista pracowników, kartoteki i umowy."),
        Describe(Permissions.EmployeesManage, "Pracownicy", "Zmiany w kartotekach", "Zatrudnianie, zmiana danych i umów, koniec zatrudnienia."),
        Describe(Permissions.AuditRead, "Pracownicy", "Historia zmian", "Kto i kiedy zmienił dane pracownika."),
        Describe(Permissions.LeaveRead, "Urlopy", "Przeglądanie urlopów", "Wnioski, wymiar i saldo urlopu wszystkich pracowników."),
        Describe(Permissions.LeaveManage, "Urlopy", "Decyzje urlopowe", "Wpisywanie, zatwierdzanie, odrzucanie i anulowanie wniosków oraz ustalanie wymiaru."),
        Describe(Permissions.WorkTimeRead, "Czas pracy", "Przeglądanie czasu pracy", "Karty ewidencji czasu pracy: godziny, nadgodziny, nieobecności i odbicia z czytników."),
        Describe(Permissions.WorkTimeManage, "Czas pracy", "Ewidencja czasu pracy", "Wpisywanie i poprawianie dni pracy, import odbić z systemu RCP i identyfikatory pracowników w tym systemie."),
        Describe(Permissions.SafetyRead, "BHP", "Przeglądanie badań i szkoleń", "Badania lekarskie i szkolenia pracowników oraz lista szkoleń w firmie."),
        Describe(Permissions.SafetyManage, "BHP", "Badania i szkolenia", "Wpisywanie badań lekarskich i odbytych szkoleń oraz prowadzenie listy szkoleń."),
        Describe(Permissions.ReportsRead, "Raporty", "Raporty kadrowe", "Stan zatrudnienia, wykorzystanie urlopów, kończące się umowy oraz badania i szkolenia do odnowienia w całej firmie, z eksportem do Excela."),
        Describe(Permissions.OrganizationManage, "Firma", "Struktura firmy", "Dodawanie i zmiana działów oraz stanowisk."),
        Describe(Permissions.SettingsManage, "Firma", "Ustawienia firmy", "Dane firmy, rodzaje urlopów, o które pracownicy proszą w portalu, i źródło czasu pracy."),
        Describe(Permissions.RolesManage, "Firma", "Role i uprawnienia", "Tworzenie ról i nadawanie ich pracownikom."),
        Describe(Permissions.AccountsManage, "Firma", "Konta w portalu", "Zakładanie i blokowanie kont pracowników. Ma je tylko administrator kadr."),
    ];

    /// <summary>The Polish name of a permission, or the code itself for one the application no longer knows.</summary>
    public static string NameOf(string code) => All.FirstOrDefault(permission => permission.Code == code)?.Name ?? code;

    private static PermissionResponse Describe(string code, string group, string name, string description) =>
        new(code, group, name, description, Permissions.Assignable.Contains(code));
}
