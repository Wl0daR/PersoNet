using PersoNet.Desktop.Presentation.Employees.Import;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeeImportTests : IDisposable
{
    // As HeidiSQL exports the table of the earlier system.
    private const string Dump = """
        -- --------------------------------------------------------
        /*!40101 SET NAMES utf8 */;

        CREATE TABLE IF NOT EXISTS `Employees` (
          `EmployeeID` int(11) NOT NULL AUTO_INCREMENT,
          `FirstName` varchar(50) CHARACTER SET utf8 COLLATE utf8_polish_ci NOT NULL,
          `LastName` varchar(50) CHARACTER SET utf8 COLLATE utf8_polish_ci NOT NULL,
          `Gender` enum('Mężczyzna','Kobieta') DEFAULT NULL,
          `BirthDate` date NOT NULL,
          `HireDate` date NOT NULL,
          `TerminationDate` date DEFAULT NULL,
          `PhoneNumber` varchar(20) DEFAULT NULL,
          `StatusID` int(11) NOT NULL DEFAULT '1',
          `PositionID` int(11) DEFAULT NULL,
          `DepartmentID` int(11) DEFAULT NULL,
          `UrlopR` int(11) DEFAULT NULL,
          PRIMARY KEY (`EmployeeID`),
          KEY `StatusID` (`StatusID`)
        ) ENGINE=InnoDB AUTO_INCREMENT=2066 DEFAULT CHARSET=utf8;

        DELETE FROM `Employees`;
        REPLACE INTO `Employees` (`EmployeeID`, `FirstName`, `LastName`, `Gender`, `BirthDate`, `HireDate`, `TerminationDate`, `PhoneNumber`, `StatusID`, `PositionID`, `DepartmentID`, `UrlopR`) VALUES
        	(1, 'Jacek', 'Nowak', 'Mężczyzna', '1899-12-30', '2010-09-06', '2011-09-16', '123-456-789', 2, 2, 10, 26),
        	(2, 'Łukasz', 'O\'Brien; Kowalski', 'Mężczyzna', '1899-12-30', '2007-08-07', NULL, '', 2, 2, 10, 26),
        	(3, 'Marcin', 'Laskowski', 'Mężczyzna', '1899-12-30', '0000-00-00', '2010-11-29', '', 2, 2, 10, NULL);
        INSERT INTO `Employees` VALUES (4, 'Ewa', 'Zielińska', 'Kobieta', '1990-01-01', '2020-02-01', NULL, NULL, 1, NULL, NULL, 20);
        INSERT INTO `Positions` VALUES (1, 'Magazynier');
        """;

    private readonly FakeEmployeesApi _api = new();
    private readonly FakeFileService _files = new();
    private readonly string _folder = Directory.CreateTempSubdirectory("personet-import-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_reader_takes_rows_of_the_table_with_and_without_a_column_list()
    {
        var entries = EmployeeSqlImport.Read(Dump);

        entries.Select(entry => entry.Number).ShouldBe(["1", "2", "3", "4"]);
        var jacek = entries[0];
        (jacek.Line, jacek.FirstName, jacek.LastName, jacek.Phone).ShouldBe((23, "Jacek", "Nowak", "123-456-789"));
        (jacek.HireDate, jacek.TerminationDate, jacek.AnnualLeaveDays, jacek.Problem).ShouldBe((new DateOnly(2010, 9, 6), new DateOnly(2011, 9, 16), 26, null));
        (entries[1].LastName, entries[1].Phone, entries[1].TerminationDate).ShouldBe(("O'Brien; Kowalski", null, null));
        entries[2].Problem.ShouldBe("Brak daty zatrudnienia.");
        entries[2].Row.ShouldBeNull();
        (entries[3].FirstName, entries[3].HireDate, entries[3].AnnualLeaveDays).ShouldBe(("Ewa", new DateOnly(2020, 2, 1), 20));
    }

    [Fact]
    public void Values_without_a_column_list_or_create_table_cannot_be_read()
    {
        Should.Throw<FormatException>(() => EmployeeSqlImport.Read("INSERT INTO Employees VALUES (1, 'Ala', 'Nowak');"));
        EmployeeSqlImport.Read("INSERT INTO Positions VALUES (1, 'Magazynier');").ShouldBeEmpty();
    }

    [Fact]
    public async Task Hr_previews_the_file_and_imports_the_employees_without_problems()
    {
        var path = Path.Combine(_folder, "kadry.sql");
        await File.WriteAllTextAsync(path, Dump, Cancellation);
        _files.OpenPaths.Add(path);
        _api.ImportRejects[26] = "Numer telefonu może mieć najwyżej 30 znaków.";
        var viewModel = new EmployeeImportDialogViewModel(_api, _files);

        viewModel.ImportCommand.CanExecute(null).ShouldBeFalse();
        viewModel.ChooseFile();

        viewModel.FileName.ShouldBe("kadry.sql");
        viewModel.Rows.Count.ShouldBe(4);
        viewModel.Rows[0].Name.ShouldBe("Nowak Jacek");
        viewModel.Rows[2].HasProblem.ShouldBeTrue();
        viewModel.Summary.ShouldBe("Znaleziono: 4 pracowników, w tym 1 z błędami. Ci zostaną pominięci; resztę można zaimportować.");

        await viewModel.ImportAsync(Cancellation);

        _api.Imports.Single().Rows.Select(row => row.Number).ShouldBe(["1", "2", "4"]);
        _api.Employees.Count.ShouldBe(2);
        viewModel.Rows[3].Problem.ShouldBe("Numer telefonu może mieć najwyżej 30 znaków.");
        viewModel.ResultText.ShouldBe("Zaimportowano: 2 pracowników, z wymiarem urlopu na ten rok: 2. Umowy: 0. Bez zmian: 0. Pominięto z powodu błędów: 2.");
        viewModel.HasChanges.ShouldBeTrue();
        viewModel.ImportCommand.CanExecute(null).ShouldBeFalse();
    }

    // The other tables of the earlier system, as separate files: two employers, one using an agency.
    private const string Organization = """
        CREATE TABLE IF NOT EXISTS `wydzialy` (`id` int(10) NOT NULL, `nazwa` varchar(100) DEFAULT NULL, `opis` varchar(250) DEFAULT NULL);
        REPLACE INTO `wydzialy` (`id`, `nazwa`, `opis`) VALUES
        	(1, 'PVC', NULL),
        	(5, 'Prima', NULL);
        REPLACE INTO `Departments` (`DepartmentID`, `DepartmentName`, `DepartmentType`) VALUES
        	(2, 'MAGAZYN PVC', 1),
        	(29, 'Łuki', 3),
        	(34, 'Księgowość PKL', 5);
        REPLACE INTO `Positions` (`PositionID`, `PositionName`, `DepartmentID`) VALUES
        	(1, ' Magazynier', 2),
        	(2, 'Księgowa', 34),
        	(3, 'Operator', 0);
        REPLACE INTO `Companies` (`CompanyID`, `CompanyName`, `IsTemporaryEmployee`) VALUES
        	(1, 'VETREX', 0),
        	(2, 'Toolip', 1),
        	(6, 'Prima-KL', 0),
        	(10, 'Prima-KL Tymczasowi', 1);
        REPLACE INTO `EmploymentHistory` (`HistoryID`, `EmployeeID`, `DepartmentID`, `PositionID`, `StartDate`, `EndDate`, `Workload`, `IsDisability`, `JobCategoryID`, `Irregular_hours`, `IsManager`, `CompanyID`) VALUES
        	(1, 1, 2, 1, '2010-09-06', '2011-03-31', 8, 0, 2, 0, 0, 1),
        	(2, 1, 29, 3, '2011-04-01', '2011-09-16', 8, 0, 2, 0, 0, 1),
        	(3, 2, 2, 1, '2007-08-07', NULL, 4, 0, 2, 0, 0, 2),
        	(4, 4, 34, 2, '2020-02-01', NULL, 8, 0, 1, 0, 0, 10);
        """;

    [Fact]
    public void Each_employer_gets_its_departments_positions_people_and_contracts()
    {
        var import = EmployeeSqlImport.Read([Dump, Organization]);

        import.Employers.Select(employer => employer.Name).ShouldBe(["VETREX", "Prima-KL"]);
        import.FoundTables.ShouldBe(["Employees", "wydzialy", "Departments", "Positions", "EmploymentHistory", "Companies"]);

        var vetrex = import.Plan(import.Employers[0]);
        vetrex.Departments.ShouldBe([new("W1", "PVC", null), new("D2", "MAGAZYN PVC", "W1"), new("D29", "Łuki", null)]);
        vetrex.Employees.Select(entry => entry.Number).ShouldBe(["1", "2", "3"]);
        vetrex.OtherEmployersEmployees.ShouldBe(1);
        // Księgowa belongs to a Prima-KL department in the files, so for VETREX it is tied to none.
        vetrex.Positions.Select(position => (position.Name, string.Join(",", position.DepartmentCodes ?? []))).ShouldBe([("Księgowa", "")]);

        // The card's department 10 is not in the files, so the latest period's is taken; the card's position is.
        // Two periods back to back are one employment; the agency's worker counts towards VETREX, on half time.
        var jacek = vetrex.Employees[0];
        (jacek.DepartmentCode, jacek.DepartmentName, jacek.Position).ShouldBe(("D29", "Łuki", "Księgowa"));
        vetrex.Employees[2].DepartmentCode.ShouldBeNull();
        jacek.Contracts.ShouldBe([new(new DateOnly(2010, 9, 6), new DateOnly(2011, 9, 16), 1m)]);
        vetrex.Employees[1].Contracts.ShouldBe([new(new DateOnly(2007, 8, 7), null, 0.5m)]);
        vetrex.Employees[2].Contracts.ShouldBeEmpty();

        var prima = import.Plan(import.Employers[1]);
        prima.Departments.ShouldBe([new("W5", "Prima", null), new("D34", "Księgowość PKL", "W5")]);
        prima.Employees.Single().Row!.ShouldSatisfyAllConditions(
            row => row.Number.ShouldBe("4"),
            row => row.DepartmentCode.ShouldBe("D34"),
            row => row.Position.ShouldBe("Księgowa"));
    }

    [Fact]
    public async Task Hr_picks_several_files_and_the_company_and_imports_the_structure_first()
    {
        var employees = Path.Combine(_folder, "table-Employees.sql");
        var other = Path.Combine(_folder, "tabele.sql");
        var attendance = Path.Combine(_folder, "table-obecnosc.sql");
        await File.WriteAllTextAsync(employees, Dump, Cancellation);
        await File.WriteAllTextAsync(other, Organization, Cancellation);
        await File.WriteAllTextAsync(attendance, "CREATE TABLE IF NOT EXISTS `obecnosc` (`id` int);\nREPLACE INTO `obecnosc` (`id`) VALUES (1);", Cancellation);
        _files.OpenPaths.AddRange([employees, other, attendance]);
        var viewModel = new EmployeeImportDialogViewModel(_api, _files);

        viewModel.ChooseFile();

        viewModel.FileName.ShouldBe("table-Employees.sql, tabele.sql, table-obecnosc.sql");
        viewModel.HasEmployers.ShouldBeTrue();
        viewModel.Employer!.Name.ShouldBe("VETREX");
        viewModel.Rows.Count.ShouldBe(3);
        viewModel.Rows[0].Department.ShouldBe("Łuki");
        viewModel.Details.ShouldBe(
            "Wczytane tabele: Employees, wydzialy, Departments, Positions, EmploymentHistory, Companies. "
            + "Do zaimportowania działy: 3, stanowiska: 1, okresy zatrudnienia (umowy): 2, pracownicy innych firm, pominięci: 1.");

        viewModel.Employer = viewModel.Employers[1];
        viewModel.Rows.Select(row => row.Name).ShouldBe(["Zielińska Ewa"]);

        await viewModel.ImportAsync(Cancellation);

        var organization = _api.OrganizationImports.Single();
        organization.Departments.Select(department => department.Code).ShouldBe(["W5", "D34"]);
        organization.Positions.Select(position => (position.Name, string.Join(",", position.DepartmentCodes ?? []))).ShouldBe([("Księgowa", "D34")]);
        _api.Imports.Single().Rows.Single().DepartmentCode.ShouldBe("D34");
        viewModel.ResultText.ShouldStartWith("Nowe działy: 2, nowe stanowiska: 1. Zaimportowano: 1 pracownik");
    }

    [Fact]
    public async Task A_file_without_the_table_says_so()
    {
        var path = Path.Combine(_folder, "inne.sql");
        await File.WriteAllTextAsync(path, "INSERT INTO `Positions` VALUES (1, 'Magazynier');", Cancellation);
        var viewModel = new EmployeeImportDialogViewModel(_api, _files);

        viewModel.LoadFile(path);

        viewModel.Rows.ShouldBeEmpty();
        viewModel.ErrorMessage.ShouldNotBeNull().ShouldContain("tabeli Employees");
    }
}
