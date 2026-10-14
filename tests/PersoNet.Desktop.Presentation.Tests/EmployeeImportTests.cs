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
        _files.OpenPath = path;
        _api.ImportRejects[26] = "Numer telefonu może mieć najwyżej 30 znaków.";
        var viewModel = new EmployeeImportDialogViewModel(_api, _files);

        viewModel.ImportCommand.CanExecute(null).ShouldBeFalse();
        viewModel.ChooseFile();

        viewModel.FileName.ShouldBe("kadry.sql");
        viewModel.Rows.Count.ShouldBe(4);
        viewModel.Rows[0].Name.ShouldBe("Nowak Jacek");
        viewModel.Rows[2].HasProblem.ShouldBeTrue();
        viewModel.Summary.ShouldBe("Znaleziono: 4 pracowników, w tym 1 z błędami. Te zostaną pominięte; resztę można zaimportować.");

        await viewModel.ImportAsync(Cancellation);

        _api.Imports.Single().Rows.Select(row => row.Number).ShouldBe(["1", "2", "4"]);
        _api.Employees.Count.ShouldBe(2);
        viewModel.Rows[3].Problem.ShouldBe("Numer telefonu może mieć najwyżej 30 znaków.");
        viewModel.ResultText.ShouldBe("Zaimportowano: 2 pracowników, z wymiarem urlopu na ten rok: 2. Już byli w PersoNet: 0. Pominięto z powodu błędów: 2.");
        viewModel.HasChanges.ShouldBeTrue();
        viewModel.ImportCommand.CanExecute(null).ShouldBeFalse();
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
