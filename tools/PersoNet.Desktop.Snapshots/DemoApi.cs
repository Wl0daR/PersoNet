using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Authentication;

namespace PersoNet.Desktop.Snapshots;

/// <summary>In-memory company with realistic names and lengths, so the screenshots show real-world text.</summary>
internal sealed class DemoApi : IEmployeesApi, IOrganizationApi, ISignInService
{
    private readonly List<DepartmentResponse> _departments = [];
    private readonly List<PositionResponse> _positions = [];
    private readonly List<EmployeeResponse> _employees = [];
    private readonly List<ContractResponse> _contracts = [];

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
        AddEmployee("P0004", "Małgorzata", "Wójcik", dev, dev1, kier, new DateOnly(2021, 9, 1));
        AddEmployee("P0005", "Piotr", "Kamiński", dev, dev1, kier, new DateOnly(2022, 2, 1), new DateOnly(2025, 8, 31));
        AddEmployee("P0006", "Katarzyna", "Lewandowska", sales, seller, prezes, new DateOnly(2023, 4, 3));
        AddEmployee("P0007", "Tomasz", "Zieliński", null, null, null, new DateOnly(2026, 10, 1));
        AddEmployee("P0008", "Aleksandra", "Szymańska", hr, hrSpec, kadry, new DateOnly(2024, 11, 4));
        AddEmployee("P0009", "Michał", "Woźniak", it, lead, prezes, new DateOnly(2017, 5, 8));
        AddEmployee("P0010", "Magdalena", "Dąbrowska", sales, seller, prezes, new DateOnly(2020, 7, 1));
        for (var i = 11; i <= 64; i++)
        {
            AddEmployee($"P{i:0000}", $"Imię{i}", $"Nazwisko{i}", sales, seller, prezes, new DateOnly(2025, 1, 1));
        }

        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.Probation, new DateOnly(2018, 6, 1), new DateOnly(2018, 8, 31), 1m));
        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.FixedTerm, new DateOnly(2018, 9, 1), new DateOnly(2020, 8, 31), 1m));
        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.Indefinite, new DateOnly(2020, 9, 1), null, 0.75m));
        _contracts.Add(new(Guid.CreateVersion7(), kadry, ContractKind.Mandate, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30), null));
    }

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
