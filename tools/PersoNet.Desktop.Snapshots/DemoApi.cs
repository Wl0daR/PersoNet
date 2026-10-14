using PersoNet.ApiClient.Access;
using PersoNet.ApiClient.Company;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.ApiClient.Reports;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.Access;
using PersoNet.Contracts.Account;
using PersoNet.Contracts.Audit;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.Organization;
using PersoNet.Contracts.Reports;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Snapshots;

/// <summary>In-memory company with realistic names and lengths, so the screenshots show real-world text.</summary>
internal sealed partial class DemoApi : IEmployeesApi, IOrganizationApi, ILeaveApi, IAccessApi, ICompanyApi, IReportsApi, IWorkTimeApi, ISignInService, IThemeService
{
    private static readonly IReadOnlyList<PermissionResponse> Catalog =
    [
        new("employees.read", "Pracownicy", "Przeglądanie kartotek", "Lista pracowników, kartoteki i umowy.", true),
        new("employees.manage", "Pracownicy", "Zmiany w kartotekach", "Zatrudnianie, zmiana danych i umów, koniec zatrudnienia.", true),
        new("audit.read", "Pracownicy", "Historia zmian", "Kto i kiedy zmienił dane pracownika.", true),
        new("leave.read", "Urlopy", "Przeglądanie urlopów", "Wnioski, wymiar i saldo urlopu wszystkich pracowników.", true),
        new("leave.manage", "Urlopy", "Decyzje urlopowe", "Wpisywanie, zatwierdzanie, odrzucanie i anulowanie wniosków oraz ustalanie wymiaru.", true),
        new("worktime.read", "Czas pracy", "Przeglądanie czasu pracy", "Karty ewidencji czasu pracy: godziny, nadgodziny, nieobecności i odbicia z czytników.", true),
        new("worktime.manage", "Czas pracy", "Ewidencja czasu pracy", "Wpisywanie i poprawianie dni pracy, import odbić z systemu RCP i identyfikatory pracowników w tym systemie.", true),
        new("reports.read", "Raporty", "Raporty kadrowe", "Stan zatrudnienia, wykorzystanie urlopów i kończące się umowy w całej firmie, z eksportem do Excela.", true),
        new("organization.manage", "Firma", "Struktura firmy", "Dodawanie i zmiana działów oraz stanowisk.", true),
        new("settings.manage", "Firma", "Ustawienia firmy", "Dane firmy oraz rodzaje urlopów, o które pracownicy proszą w portalu.", true),
        new("roles.manage", "Firma", "Role i uprawnienia", "Tworzenie ról i nadawanie ich pracownikom.", true),
        new("accounts.manage", "Firma", "Konta w portalu", "Zakładanie i blokowanie kont pracowników. Ma je tylko administrator kadr.", false),
    ];

    private static readonly IReadOnlyList<RoleResponse> DemoRoles =
    [
        new(Guid.NewGuid(), "Kadrowa", "Prowadzi kartoteki i urlopy, bez zmian w strukturze firmy.", ["employees.manage", "employees.read", "leave.manage", "leave.read"], 2),
        new(Guid.NewGuid(), "Kierownik działu", "Widzi urlopy i decyduje o wnioskach.", ["leave.manage", "leave.read"], 5),
        new(Guid.NewGuid(), "Księgowość", null, ["employees.read", "leave.read"], 1),
    ];

    public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CurrentUserResponse(Guid.NewGuid(), Guid.NewGuid(), "Anna Kowalska", "anna.kowalska@firma.pl", [.. Catalog.Select(permission => permission.Code)]));

    private CompanyResponse _company = new(
        "Hurtownia Mazowsze", "Hurtownia Mazowsze Spółka z ograniczoną odpowiedzialnością", "5261043838", "142130400",
        "ul. Marszałkowska 142 lok. 7", "00-061", "Warszawa", "kadry@hurtownia-mazowsze.pl", null,
        [LeaveKind.Annual, LeaveKind.OnDemand, LeaveKind.ChildCare, LeaveKind.ForceMajeure],
        WorkTimeSourceKind.RogerPrMaster);

    public Task<CompanyResponse> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_company);

    public Task<CompanyResponse> SaveDetailsAsync(SaveCompanyDetailsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<CompanyResponse> SaveLeaveSettingsAsync(SaveLeaveSettingsRequest request, CancellationToken cancellationToken)
    {
        _company = _company with { PortalLeaveKinds = request.PortalLeaveKinds };
        return Task.FromResult(_company);
    }

    public Task<CompanyResponse> SaveWorkTimeSettingsAsync(SaveWorkTimeSettingsRequest request, CancellationToken cancellationToken)
    {
        _company = _company with { WorkTimeSource = request.Source };
        return Task.FromResult(_company);
    }

    public Task<IReadOnlyList<PermissionResponse>> ListPermissionsAsync(CancellationToken cancellationToken) => Task.FromResult(Catalog);

    public Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken) => Task.FromResult(DemoRoles);

    public Task<RoleResponse> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<RoleResponse> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<RoleResponse>> GetEmployeeRolesAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoleResponse>>([DemoRoles[0]]);

    public Task<IReadOnlyList<RoleResponse>> SetEmployeeRolesAsync(Guid employeeId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private readonly List<DepartmentResponse> _departments = [];
    private readonly List<PositionResponse> _positions = [];
    private readonly List<EmployeeResponse> _employees = [];
    private readonly List<ContractResponse> _contracts = [];
    private readonly List<LeaveRequestResponse> _leave = [];
    private readonly Dictionary<(Guid EmployeeId, int Year), (int Days, int CarriedOver)> _entitlements = [];

    // The snapshots switch the theme themselves; the menu only shows it.
    public AppTheme Theme { get; private set; } = AppTheme.System;

    public void SetTheme(AppTheme theme) => Theme = theme;

    public DemoApi()
    {
        var board = AddDepartment("Zarząd", "ZAR", null);
        var hr = AddDepartment("Dział Kadr i Płac", "KAD", board);
        var it = AddDepartment("Dział Informatyki", "IT", board);
        AddDepartment("Zespół Utrzymania Infrastruktury i Bezpieczeństwa Sieci", "IT-INF", it);
        var dev = AddDepartment("Zespół Rozwoju Oprogramowania", "IT-DEV", it);
        var sales = AddDepartment("Dział Sprzedaży", "SPR", board);
        AddDepartment("Archiwum", "ARCH", null, active: false);

        var ceo = AddPosition("Prezes Zarządu");
        var hrSpec = AddPosition("Specjalista ds. kadr");
        var dev1 = AddPosition("Programista .NET");
        var lead = AddPosition("Kierownik zespołu");
        var seller = AddPosition("Przedstawiciel handlowy");
        AddPosition("Starszy specjalista ds. wynagrodzeń i rozliczeń z ZUS");
        AddPosition("Stażysta", active: false);

        var prezes = AddEmployee("P0001", "Jan", "Nowak", board, ceo, null, new DateOnly(2015, 3, 1));
        var kadry = AddEmployee("P0002", "Anna", "Kowalska", hr, hrSpec, prezes, new DateOnly(2018, 6, 1));
        var kier = AddEmployee("P0003", "Krzysztof", "Wiśniewski-Grabowski", dev, lead, prezes, new DateOnly(2019, 1, 15));
        var wojcik = AddEmployee("P0004", "Małgorzata", "Wójcik", dev, dev1, kier, new DateOnly(2021, 9, 1));
        AddEmployee("P0005", "Piotr", "Kamiński", dev, dev1, kier, new DateOnly(2022, 2, 1), new DateOnly(2025, 8, 31));
        var lewandowska = AddEmployee("P0006", "Katarzyna", "Lewandowska", sales, seller, prezes, new DateOnly(2023, 4, 3));
        AddEmployee("P0007", "Tomasz", "Zieliński", null, null, null, new DateOnly(2026, 10, 1));
        AddEmployee("P0008", "Aleksandra", "Szymańska", hr, hrSpec, kadry, new DateOnly(2024, 11, 4));
        var wozniak = AddEmployee("P0009", "Michał", "Woźniak", it, lead, prezes, new DateOnly(2017, 5, 8));
        var dabrowska = AddEmployee("P0010", "Magdalena", "Dąbrowska", sales, seller, prezes, new DateOnly(2020, 7, 1));
        for (var i = 11; i <= 64; i++)
        {
            AddEmployee($"P{i:0000}", $"Imię{i}", $"Nazwisko{i}", sales, seller, prezes, new DateOnly(2025, 1, 1));
        }

        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.Probation, new DateOnly(2018, 6, 1), new DateOnly(2018, 8, 31), 1m));
        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.FixedTerm, new DateOnly(2018, 9, 1), new DateOnly(2020, 8, 31), 1m));
        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.Indefinite, new DateOnly(2020, 9, 1), null, 0.75m));
        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.Mandate, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30), null));

        // Contracts of the others, some ending soon, so the reports have something to show whenever they are rendered.
        var today = DateOnly.FromDateTime(DateTime.Today);
        _contracts.Add(new(Guid.CreateVersion7(), prezes, ContractKind.Indefinite, new DateOnly(2015, 3, 1), null, 1m));
        _contracts.Add(new(Guid.CreateVersion7(), kier, ContractKind.Indefinite, new DateOnly(2019, 1, 15), null, 1m));
        _contracts.Add(new(Guid.CreateVersion7(), wojcik, ContractKind.FixedTerm, new DateOnly(2021, 9, 1), today.AddDays(18), 1m));
        _contracts.Add(new(Guid.CreateVersion7(), lewandowska, ContractKind.FixedTerm, new DateOnly(2023, 4, 3), today.AddDays(45), 1m));
        _contracts.Add(new(Guid.CreateVersion7(), lewandowska, ContractKind.Indefinite, today.AddDays(46), null, 1m));
        _contracts.Add(new(Guid.CreateVersion7(), wozniak, ContractKind.Indefinite, new DateOnly(2017, 5, 8), null, 1m));
        _contracts.Add(new(Guid.CreateVersion7(), dabrowska, ContractKind.FixedTerm, new DateOnly(2020, 7, 1), today.AddDays(32), 0.5m));
        _contracts.Add(new(Guid.CreateVersion7(), dabrowska, ContractKind.Mandate, today.AddDays(-60), today.AddDays(5), null));
        foreach (var other in _employees.Where(employee => string.CompareOrdinal(employee.Number, "P0011") >= 0))
        {
            _contracts.Add(new(Guid.CreateVersion7(), other.Id, ContractKind.Indefinite, other.HireDate, null, 1m));
        }

        // Leave in the current year, so the screenshots look the same whenever they are taken. Days are working days.
        var year = DateTime.Today.Year;
        foreach (var employee in _employees.Where(employee => employee.TerminationDate is null))
        {
            _entitlements[(employee.Id, year)] = (employee.HireDate.Year < 2017 ? 26 : 20, 0);
        }

        _entitlements[(kadry, year)] = (26, 4);
        AddLeave(wojcik, LeaveKind.Annual, new DateOnly(year, 11, 2), new DateOnly(year, 11, 6), 5, LeaveState.Pending, "Wyjazd rodzinny, zastępstwo uzgodnione z zespołem.");
        AddLeave(lewandowska, LeaveKind.OnDemand, new DateOnly(year, 10, 16), new DateOnly(year, 10, 16), 1, LeaveState.Pending, null);
        AddLeave(kier, LeaveKind.Annual, new DateOnly(year, 12, 21), new DateOnly(year, 12, 31), 7, LeaveState.Pending, null);
        AddLeave(kadry, LeaveKind.Annual, new DateOnly(year, 7, 6), new DateOnly(year, 7, 17), 10, LeaveState.Approved, null);
        AddLeave(kadry, LeaveKind.ChildCare, new DateOnly(year, 3, 12), new DateOnly(year, 3, 12), 1, LeaveState.Approved, null);
        AddLeave(kadry, LeaveKind.Annual, new DateOnly(year, 8, 24), new DateOnly(year, 8, 28), 5, LeaveState.Rejected, null,
            "W tym tygodniu zamykamy listy płac za sierpień. Proszę o inny termin.");
        // In the current month too, so the calendar of the card has something to show whenever it is rendered.
        var firstMonday = new DateOnly(year, DateTime.Today.Month, 1);
        while (firstMonday.DayOfWeek != DayOfWeek.Monday)
        {
            firstMonday = firstMonday.AddDays(1);
        }

        AddLeave(kadry, LeaveKind.Annual, firstMonday, firstMonday.AddDays(4), 5, LeaveState.Approved, "Wyjazd w góry.");
        AddLeave(kadry, LeaveKind.ChildCare, firstMonday.AddDays(15), firstMonday.AddDays(15), 1, LeaveState.Pending, null, hours: 2m);
        // Others away this month too, so the leave calendar shows overlaps.
        AddLeave(wojcik, LeaveKind.Annual, firstMonday.AddDays(7), firstMonday.AddDays(11), 5, LeaveState.Approved, null);
        AddLeave(lewandowska, LeaveKind.OnDemand, firstMonday.AddDays(9), firstMonday.AddDays(9), 1, LeaveState.Approved, null);
        AddLeave(wozniak, LeaveKind.Annual, firstMonday.AddDays(14), firstMonday.AddDays(18), 5, LeaveState.Pending, null);
        AddLeave(dabrowska, LeaveKind.Unpaid, firstMonday.AddDays(2), firstMonday.AddDays(3), 2, LeaveState.Approved, null);
        AddLeave(wozniak, LeaveKind.Occasional, new DateOnly(year, 9, 18), new DateOnly(year, 9, 18), 1, LeaveState.Approved, "Ślub brata.");
        AddLeave(wojcik, LeaveKind.ChildCare, new DateOnly(year, 10, 22), new DateOnly(year, 10, 22), 1, LeaveState.Pending, "Wizyta u lekarza z synem.", hours: 3m);
        AddLeave(wozniak, LeaveKind.ForceMajeure, new DateOnly(year, 9, 3), new DateOnly(year, 9, 3), 1, LeaveState.Approved, "Awaria ogrzewania w domu.", hours: 2.5m);
        AddLeave(dabrowska, LeaveKind.Unpaid, new DateOnly(year, 10, 26), new DateOnly(year, 10, 30), 5, LeaveState.Cancelled, null);
    }

    public Guid FirstEmployeeWithLeave => _leave.First(request => request.State == LeaveState.Rejected).EmployeeId;

    public Guid FirstEmployeeWithContracts => _contracts[0].EmployeeId;

    public EmployeeListItem ListItem(Guid id) => ToListItem(_employees.Single(employee => employee.Id == id));

    // ISignInService

    public Task<SignedInUser> SignInAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SignedInUser("Anna Kowalska", "anna.kowalska@demo.personet.local"));

    public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // IEmployeesApi

    public Task<PagedResponse<EmployeeListItem>> ListAsync(EmployeeListQuery query, CancellationToken cancellationToken)
    {
        var all = _employees
            .Where(employee => query.IncludeFormer || employee.TerminationDate is null)
            .Where(employee => string.IsNullOrWhiteSpace(query.Search) || $"{employee.LastName} {employee.FirstName} {employee.Number}".Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(employee => employee.LastName, StringComparer.Create(new System.Globalization.CultureInfo("pl-PL"), false))
            .ToList();

        var page = all.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(ToListItem).ToList();
        return Task.FromResult(new PagedResponse<EmployeeListItem>(page, query.Page, query.PageSize, all.Count));
    }

    public Task<IReadOnlyList<EmployeeLookupItem>> LookupAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeLookupItem>>(_employees
            .Where(employee => employee.TerminationDate is null)
            .Select(employee => new EmployeeLookupItem(employee.Id, employee.Number, $"{employee.LastName} {employee.FirstName}"))
            .ToList());

    public Task<EmployeeResponse> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_employees.Single(employee => employee.Id == id));

    public Task<EmployeeResponse> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeResponse> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportEmployeesResponse> ImportAsync(ImportEmployeesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportOrganizationResponse> ImportOrganizationAsync(ImportOrganizationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeResponse> TerminateAsync(Guid id, DateOnly lastDay, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeResponse> ReinstateAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeResponse> RehireAsync(Guid id, DateOnly hireDate, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeAccountResponse> GetAccountAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult(new EmployeeAccountResponse(employeeId, _employees.Single(employee => employee.Id == employeeId).Email, AccountState.Active));

    public Task<IReadOnlyList<HistoryEntryResponse>> GetHistoryAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var at = new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.FromHours(2));
        return Task.FromResult<IReadOnlyList<HistoryEntryResponse>>(
        [
            new(Guid.NewGuid(), at.AddHours(6), "Anna Kowalska", "Konto w portalu", "Utworzono konto w portalu i link do ustawienia hasła", HistoryAction.Created,
                [new("Stan konta", "brak konta", "czeka na ustawienie hasła")]),
            new(Guid.NewGuid(), at.AddHours(5), "Marek Wiśniewski", "Wniosek urlopowy", "Zatwierdzono wniosek: urlop wypoczynkowy, 3 listopada 2026 – 7 listopada 2026", HistoryAction.Changed,
                [new("Stan", "czeka na decyzję", "zatwierdzony")]),
            new(Guid.NewGuid(), at.AddHours(1), "Jan Nowak", "Wniosek urlopowy", "Złożono wniosek: urlop wypoczynkowy, 3 listopada 2026 – 7 listopada 2026", HistoryAction.Created,
                [new("Rodzaj", null, "urlop wypoczynkowy"), new("Od", null, "3 listopada 2026"), new("Do", null, "7 listopada 2026"), new("Dni robocze", null, "5"), new("Godziny", null, "40"), new("Stan", null, "czeka na decyzję")]),
            new(Guid.NewGuid(), at, "Anna Kowalska", "Kartoteka", "Zmieniono dane", HistoryAction.Changed,
                [new("Telefon", "600 100 200", "600 100 300"), new("Stanowisko", "Księgowa", "Starsza księgowa")]),
            new(Guid.NewGuid(), at.AddDays(-30), "Anna Kowalska", "Umowa", "Dodano umowę: umowa o pracę na czas nieokreślony od 1 października 2026", HistoryAction.Created,
                [new("Rodzaj umowy", null, "umowa o pracę na czas nieokreślony"), new("Od", null, "1 października 2026"), new("Wymiar etatu", null, "1")]),
        ]);
    }

    public Task<AccountLinkResponse> CreateAccountLinkAsync(Guid employeeId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeAccountResponse> SetAccountBlockedAsync(Guid employeeId, bool blocked, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ContractResponse>> ListContractsAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ContractResponse>>(_contracts.Where(contract => contract.EmployeeId == employeeId).OrderByDescending(contract => contract.StartDate).ToList());

    public Task<ContractResponse> CreateContractAsync(Guid employeeId, SaveContractRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ContractResponse> UpdateContractAsync(Guid employeeId, Guid contractId, SaveContractRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken) => throw new NotSupportedException();

    // IOrganizationApi

    public Task<IReadOnlyList<DepartmentResponse>> GetDepartmentsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DepartmentResponse>>(_departments.Where(department => includeInactive || department.IsActive).ToList());

    public Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SetDepartmentActiveAsync(Guid id, bool active, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PositionResponse>>(_positions.Where(position => includeInactive || position.IsActive).ToList());

    public Task<PositionResponse> CreatePositionAsync(CreatePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PositionResponse> UpdatePositionAsync(Guid id, UpdatePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SetPositionActiveAsync(Guid id, bool active, CancellationToken cancellationToken) => throw new NotSupportedException();

    // ILeaveApi

    public Task<IReadOnlyList<LeaveRequestResponse>> ListAsync(LeaveState? state, int? year, Guid? employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LeaveRequestResponse>>(_leave
            .Where(request => state is null || request.State == state)
            .Where(request => year is null || request.StartDate.Year == year)
            .Where(request => employeeId is null || request.EmployeeId == employeeId)
            .OrderBy(request => request.State == LeaveState.Pending ? 0 : 1)
            .ThenByDescending(request => request.StartDate)
            .ToList());

    public Task<LeaveRequestResponse> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<LeaveRequestResponse> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<LeaveRequestResponse> RejectAsync(Guid id, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<LeaveRequestResponse> CancelAsync(Guid id, string? note, CancellationToken cancellationToken) => throw new NotSupportedException();

    // Fixed-date holidays only; enough for the months the screenshots show.
    public Task<IReadOnlyList<HolidayResponse>> GetHolidaysAsync(int year, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HolidayResponse>>(
        [
            new(new DateOnly(year, 1, 1), "Nowy Rok"),
            new(new DateOnly(year, 1, 6), "Trzech Króli"),
            new(new DateOnly(year, 5, 1), "Święto Pracy"),
            new(new DateOnly(year, 5, 3), "Święto Konstytucji 3 Maja"),
            new(new DateOnly(year, 8, 15), "Wniebowzięcie Najświętszej Maryi Panny"),
            new(new DateOnly(year, 11, 1), "Wszystkich Świętych"),
            new(new DateOnly(year, 11, 11), "Narodowe Święto Niepodległości"),
            new(new DateOnly(year, 12, 24), "Wigilia Bożego Narodzenia"),
            new(new DateOnly(year, 12, 25), "Boże Narodzenie (pierwszy dzień)"),
            new(new DateOnly(year, 12, 26), "Boże Narodzenie (drugi dzień)"),
        ]);

    public Task<LeaveBalanceResponse> GetBalanceAsync(Guid employeeId, int year, CancellationToken cancellationToken)
    {
        var annual = _leave.Where(request => request.EmployeeId == employeeId && request.StartDate.Year == year
            && request.Kind is LeaveKind.Annual or LeaveKind.OnDemand).ToList();
        var used = annual.Where(request => request.State == LeaveState.Approved).Sum(request => request.Hours);
        var pending = annual.Where(request => request.State == LeaveState.Pending).Sum(request => request.Hours);
        var onDemand = annual.Where(request => request.Kind == LeaveKind.OnDemand && request.State is LeaveState.Approved or LeaveState.Pending).Sum(request => request.Days);

        if (!_entitlements.TryGetValue((employeeId, year), out var entitlement))
        {
            return Task.FromResult(new LeaveBalanceResponse(employeeId, year, false, 0, 0, 0, used, pending, 0, onDemand, 8m));
        }

        var total = (entitlement.Days + entitlement.CarriedOver) * 8m;
        return Task.FromResult(new LeaveBalanceResponse(employeeId, year, true, entitlement.Days, entitlement.CarriedOver, total, used, pending, total - used - pending, onDemand, 8m));
    }

    public Task<LeaveCalendarResponse> GetCalendarAsync(int year, int month, Guid? departmentId, CancellationToken cancellationToken)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var included = departmentId is { } root ? Subtree(root) : null;

        var people = _employees
            .Where(employee => employee.HireDate <= last && (employee.TerminationDate is null || employee.TerminationDate >= first))
            .Where(employee => included is null || (employee.DepartmentId is { } id && included.Contains(id)))
            .OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName)
            .Select(employee => new LeaveCalendarPerson(
                employee.Id,
                employee.FirstName,
                employee.LastName,
                _departments.FirstOrDefault(department => department.Id == employee.DepartmentId)?.Name,
                _positions.FirstOrDefault(position => position.Id == employee.PositionId)?.Name,
                [.. _leave
                    .Where(request => request.EmployeeId == employee.Id && request.StartDate <= last && request.EndDate >= first
                        && request.State is LeaveState.Approved or LeaveState.Pending)
                    .OrderBy(request => request.StartDate)
                    .Select(request => new LeaveCalendarEntry(request.Id, request.Kind, request.StartDate, request.EndDate, request.State, request.IsHourly, request.Hours))]))
            .ToList();
        var holidays = GetHolidaysAsync(year, cancellationToken).Result.Where(holiday => holiday.Date.Month == month).ToList();

        return Task.FromResult(new LeaveCalendarResponse(year, month, holidays, people));
    }

    private HashSet<Guid> Subtree(Guid root)
    {
        var included = new HashSet<Guid> { root };
        for (var added = true; added;)
        {
            added = false;
            foreach (var department in _departments.Where(d => d.ParentId is { } parent && included.Contains(parent) && !included.Contains(d.Id)))
            {
                added = included.Add(department.Id) || added;
            }
        }

        return included;
    }

    public Task<LeaveBalanceResponse> SetEntitlementAsync(Guid employeeId, int year, SaveLeaveEntitlementRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    // IReportsApi: the same rules as the server, over the demo data.
    public Task<HeadcountReport> GetHeadcountAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var rows = _employees
            .Where(employee => employee.HireDate <= day && (employee.TerminationDate is null || employee.TerminationDate >= day))
            .OrderBy(employee => employee.LastName, StringComparer.CurrentCulture).ThenBy(employee => employee.FirstName, StringComparer.CurrentCulture)
            .Select(employee =>
            {
                var contract = _contracts
                    .Where(c => c.EmployeeId == employee.Id && c.StartDate <= day && (c.EndDate is null || c.EndDate >= day))
                    .OrderByDescending(c => c.Type is ContractKind.Probation or ContractKind.FixedTerm or ContractKind.Indefinite)
                    .ThenByDescending(c => c.StartDate)
                    .FirstOrDefault();
                return new HeadcountPerson(employee.Id, employee.Number, employee.FirstName, employee.LastName, DepartmentName(employee.DepartmentId),
                    _positions.FirstOrDefault(position => position.Id == employee.PositionId)?.Name, employee.HireDate, contract?.Type, contract?.EndDate, contract?.WorkingTime);
            })
            .ToList();

        static decimal FullTime(HeadcountPerson person) =>
            person.ContractKind is ContractKind.Probation or ContractKind.FixedTerm or ContractKind.Indefinite ? person.WorkingTime ?? 0m : 0m;

        var departments = rows.GroupBy(row => row.DepartmentName)
            .OrderBy(group => group.Key is null).ThenBy(group => group.Key, StringComparer.CurrentCulture)
            .Select(group => new HeadcountDepartment(group.Key, group.Count(), group.Sum(FullTime)))
            .ToList();
        return Task.FromResult(new HeadcountReport(day, rows.Count, rows.Sum(FullTime), departments, rows));
    }

    public Task<LeaveUsageReport> GetLeaveUsageAsync(int year, CancellationToken cancellationToken)
    {
        var first = new DateOnly(year, 1, 1);
        var last = new DateOnly(year, 12, 31);
        var rows = _employees
            .Where(employee => employee.HireDate <= last && (employee.TerminationDate is null || employee.TerminationDate >= first))
            .OrderBy(employee => employee.LastName, StringComparer.CurrentCulture).ThenBy(employee => employee.FirstName, StringComparer.CurrentCulture)
            .Select(employee =>
            {
                var has = _entitlements.TryGetValue((employee.Id, year), out var entitlement);
                var annual = _leave.Where(request => request.EmployeeId == employee.Id && request.StartDate.Year == year
                    && request.Kind is LeaveKind.Annual or LeaveKind.OnDemand && request.State is LeaveState.Approved or LeaveState.Pending).ToList();
                var total = (entitlement.Days + entitlement.CarriedOver) * 8m;
                var used = annual.Where(request => request.State == LeaveState.Approved).Sum(request => request.Hours);
                var pending = annual.Where(request => request.State == LeaveState.Pending).Sum(request => request.Hours);
                return new LeaveUsageRow(employee.Id, employee.Number, employee.FirstName, employee.LastName, DepartmentName(employee.DepartmentId),
                    has, entitlement.Days, entitlement.CarriedOver, total, used, pending, total - used - pending,
                    annual.Where(request => request.Kind == LeaveKind.OnDemand).Sum(request => request.Days), 8m);
            })
            .ToList();
        return Task.FromResult(new LeaveUsageReport(year, rows));
    }

    public Task<ContractsEndingReport> GetContractsEndingAsync(DateOnly from, int days, CancellationToken cancellationToken)
    {
        var to = from.AddDays(days - 1);
        var rows = _contracts
            .Where(contract => contract.EndDate is { } end && end >= from && end <= to)
            .Select(contract => (Contract: contract, Employee: _employees.Single(employee => employee.Id == contract.EmployeeId)))
            .OrderBy(row => row.Contract.EndDate).ThenBy(row => row.Employee.LastName, StringComparer.CurrentCulture)
            .Select(row => new ContractEndingRow(row.Contract.Id, row.Employee.Id, row.Employee.Number, row.Employee.FirstName, row.Employee.LastName,
                DepartmentName(row.Employee.DepartmentId), _positions.FirstOrDefault(position => position.Id == row.Employee.PositionId)?.Name,
                row.Contract.Type, row.Contract.StartDate, row.Contract.EndDate!.Value, row.Contract.WorkingTime,
                _contracts.Any(next => next.EmployeeId == row.Contract.EmployeeId && next.Id != row.Contract.Id && next.StartDate > row.Contract.StartDate
                    && (next.EndDate is null || next.EndDate > row.Contract.EndDate))))
            .ToList();
        return Task.FromResult(new ContractsEndingReport(from, to, rows));
    }

    private string? DepartmentName(Guid? id) => _departments.FirstOrDefault(department => department.Id == id)?.Name;

    private void AddLeave(Guid employeeId, LeaveKind kind, DateOnly start, DateOnly end, int days, LeaveState state, string? note, string? decisionNote = null, decimal? hours = null)
    {
        var employee = _employees.Single(employee => employee.Id == employeeId);
        var created = new DateTimeOffset(start.AddDays(-14).ToDateTime(new TimeOnly(9, 30)));
        _leave.Add(new LeaveRequestResponse(Guid.CreateVersion7(), employeeId, employee.Number, $"{employee.LastName} {employee.FirstName}", kind, start, end, days,
            hours ?? days * 8m, hours is not null, note,
            state, created, state == LeaveState.Pending ? null : created.AddDays(1), decisionNote));
    }

    private Guid AddDepartment(string name, string code, Guid? parentId, bool active = true)
    {
        var department = new DepartmentResponse(Guid.CreateVersion7(), name, code, parentId, active);
        _departments.Add(department);
        return department.Id;
    }

    private Guid AddPosition(string name, bool active = true)
    {
        var position = new PositionResponse(Guid.CreateVersion7(), name, active);
        _positions.Add(position);
        return position.Id;
    }

    private Guid AddEmployee(string number, string firstName, string lastName, Guid? departmentId, Guid? positionId, Guid? managerId, DateOnly hireDate, DateOnly? terminationDate = null)
    {
        var employee = new EmployeeResponse(Guid.CreateVersion7(), number, firstName, lastName,
            $"{Ascii(firstName)}.{Ascii(lastName)}@demo.personet.local".ToLowerInvariant(), "+48 600 100 200", hireDate, terminationDate, departmentId, positionId, managerId);
        _employees.Add(employee);
        return employee.Id;
    }

    private EmployeeListItem ToListItem(EmployeeResponse employee) => new(
        employee.Id,
        employee.Number,
        employee.FirstName,
        employee.LastName,
        employee.Email,
        _departments.FirstOrDefault(department => department.Id == employee.DepartmentId)?.Name,
        _positions.FirstOrDefault(position => position.Id == employee.PositionId)?.Name,
        _employees.FirstOrDefault(manager => manager.Id == employee.ManagerId) is { } manager ? $"{manager.LastName} {manager.FirstName}" : null,
        employee.HireDate,
        employee.TerminationDate);

    private static string Ascii(string text) => new(text.Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
        .Select(c => c switch { 'ł' => 'l', 'Ł' => 'L', '-' => '-', _ => c })
        .ToArray());
}
