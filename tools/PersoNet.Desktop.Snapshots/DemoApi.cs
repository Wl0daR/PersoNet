using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Snapshots;

/// <summary>In-memory company with realistic names and lengths, so the screenshots show real-world text.</summary>
internal sealed class DemoApi : IEmployeesApi, IOrganizationApi, ILeaveApi, ISignInService, IThemeService
{
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

    public Task<EmployeeResponse> TerminateAsync(Guid id, DateOnly lastDay, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeResponse> ReinstateAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

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

    public Task<LeaveBalanceResponse> SetEntitlementAsync(Guid employeeId, int year, SaveLeaveEntitlementRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

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
