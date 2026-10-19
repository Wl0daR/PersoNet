using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Employees.Import;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class SafetyImportTests : IDisposable
{
    // Employees 2 and 4 of the division PVC (1) and its department MAGAZYN PVC (2); 99 has no card.
    private const string Employees = """
        CREATE TABLE IF NOT EXISTS `Employees` (`EmployeeID` int(11), `FirstName` varchar(50), `LastName` varchar(50), `HireDate` date, `DepartmentID` int(11));
        INSERT INTO `Employees` (`EmployeeID`, `FirstName`, `LastName`, `HireDate`, `DepartmentID`) VALUES
        	(2, 'Łukasz', 'Kowalski', '2007-08-07', 2),
        	(4, 'Ewa', 'Zielińska', '2020-02-01', 2);
        CREATE TABLE IF NOT EXISTS `wydzialy` (`id` int(10) NOT NULL, `nazwa` varchar(100) DEFAULT NULL);
        REPLACE INTO `wydzialy` (`id`, `nazwa`) VALUES (1, 'PVC');
        REPLACE INTO `Departments` (`DepartmentID`, `DepartmentName`, `DepartmentType`) VALUES (2, 'MAGAZYN PVC', 1);
        """;

    // As HeidiSQL exports them: dates as days since 30.12.1899 (45719 is 3 March 2025), validity in days.
    private const string Safety = """
        CREATE TABLE IF NOT EXISTS `szkolenia_definicje` (`id` int(11), `nazwa` varchar(50), `waznosc` varchar(50), `czy_wygasa` int(11), `czy_obowiazkowe` int(11), `opis` varchar(500));
        REPLACE INTO `szkolenia_definicje` (`id`, `nazwa`, `waznosc`, `czy_wygasa`, `czy_obowiazkowe`, `opis`) VALUES
        	(1, 'BHP', '1095', 1, 1, ''),
        	(4, 'ABC Montażu', '730', 0, 1, 'Podstawy montażu'),
        	(67, 'SUWNICE', '3650', 1, 0, ''),
        	(70, 'Nieużywane', '0', 0, 0, '');
        CREATE TABLE IF NOT EXISTS `szkolenia_rel` (`id` int(11), `id_szkolenia` int(11), `id_wydzialu` int(11), `id_grupy` int(11));
        REPLACE INTO `szkolenia_rel` (`id`, `id_szkolenia`, `id_wydzialu`, `id_grupy`) VALUES
        	(1, 67, 1, 0),
        	(2, 67, 0, 13),
        	(3, 4, 1, 0);
        CREATE TABLE IF NOT EXISTS `grupy` (`id` int(11), `nazwa` varchar(50));
        REPLACE INTO `grupy` (`id`, `nazwa`) VALUES (13, 'MAGAZYN PVC');
        CREATE TABLE IF NOT EXISTS `szkolenia_prac` (`id` int(10), `idszkolenia` int(10), `idprac` int(10), `data` int(10), `wygasa` int(10), `uwagi` varchar(250));
        REPLACE INTO `szkolenia_prac` (`id`, `idszkolenia`, `idprac`, `data`, `wygasa`, `uwagi`) VALUES
        	(3, 1, 2, 45719, 46815, 'wstępne'),
        	(4, 4, 2, 45719, 46449, ''),
        	(5, 1, 99, 45719, 46815, ''),
        	(6, 8, 4, 45719, 46815, ''),
        	(7, 1, 4, NULL, NULL, '');
        CREATE TABLE IF NOT EXISTS `badania` (`id` int(10), `idprac` int(10), `data_badania` int(10), `wygasa` int(10), `czy_aktywne` tinyint(4), `uwagi` varchar(250));
        REPLACE INTO `badania` (`id`, `idprac`, `data_badania`, `wygasa`, `czy_aktywne`, `uwagi`) VALUES
        	(3, 2, 45719, 46449, 1, 'Memo1\r\n'),
        	(5, 4, 45000, 45730, 1, 'BADANIE WSTĘPNE'),
        	(7, 99, 45719, 46449, 1, '');
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("personet-safety-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Trainings_and_examinations_of_the_company_people_are_read_with_the_list_of_trainings_they_need()
    {
        var company = EmployeeSqlImport.Read([Employees]);
        var plan = SafetySqlImport.Read([Safety]).Plan(company.Plan(company.Employers[0]));

        // Every 3 years for everyone; for good in the division PVC; every 10 years in the group = department MAGAZYN PVC.
        plan.Courses.Select(course => (course.Name, course.ValidityMonths, course.IsMandatory, string.Join(",", course.DepartmentCodes ?? []), course.Description))
            .ShouldBe(
            [
                ("BHP", 36, true, "", null),
                ("ABC Montażu", null, true, "W1", "Podstawy montażu"),
                ("SUWNICE", 120, false, "D2", null),
            ]);
        plan.Trainings.ShouldBe(
        [
            new ImportTrainingRow(16, "2", "BHP", new DateOnly(2025, 3, 3), new DateOnly(2028, 3, 3), "wstępne"),
            new ImportTrainingRow(17, "2", "ABC Montażu", new DateOnly(2025, 3, 3), null, null),
        ]);
        plan.Exams.ShouldBe(
        [
            new ImportMedicalExamRow(23, "2", MedicalExamKind.Periodic, new DateOnly(2025, 3, 3), new DateOnly(2027, 3, 3), null),
            new ImportMedicalExamRow(24, "4", MedicalExamKind.Initial, new DateOnly(2023, 3, 15), new DateOnly(2025, 3, 14), "BADANIE WSTĘPNE"),
        ]);
        plan.Problems.Select(problem => (problem.Line, problem.Number)).ShouldBe([(19, "4"), (20, "4")]);
        plan.OtherEmployersRows.ShouldBe(2);
    }

    [Fact]
    public async Task The_records_are_previewed_and_sent_after_the_employees_the_list_of_trainings_first()
    {
        var employees = Path.Combine(_folder, "table-Employees.sql");
        var safetyFile = Path.Combine(_folder, "table-szkolenia.sql");
        await File.WriteAllTextAsync(employees, Employees, Cancellation);
        await File.WriteAllTextAsync(safetyFile, Safety, Cancellation);
        var files = new FakeFileService();
        files.OpenPaths.AddRange([employees, safetyFile]);
        var safety = new FakeSafetyApi();
        var viewModel = new EmployeeImportDialogViewModel(new FakeEmployeesApi(), new FakeAbsencesApi(), new FakeWorkTimeApi(), safety, files);

        await viewModel.ChooseFileAsync();
        await viewModel.ImportAsync(Cancellation);

        viewModel.Details.ShouldNotBeNull().ShouldContain("szkolenia_definicje, szkolenia_rel, szkolenia_prac, badania, grupy");
        viewModel.Details.ShouldContain("lista szkoleń: 3, ukończone szkolenia: 2, badania lekarskie: 2, wiersze badań i szkoleń z błędami: 2");
        safety.Imported.Select(request => (request.Courses.Count, request.Trainings.Count, request.Exams.Count)).ShouldBe([(3, 0, 0), (0, 2, 0), (0, 0, 2)]);
        viewModel.ResultText.ShouldNotBeNull().ShouldContain("Badania i szkolenia: lista szkoleń 3, ukończone szkolenia 2, badania lekarskie 2.");
        viewModel.ResultText.ShouldContain("wiersz 19, pracownik 4: Szkolenia nie ma na liście szkoleń");
    }
}
