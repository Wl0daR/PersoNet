using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Employees.Import;
using AbsenceKind = PersoNet.Contracts.Absences.AbsenceKind;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class AttendanceImportTests : IDisposable
{
    // The earlier system's employees 2 and 4 (and 99 of another company).
    private const string Employees = """
        CREATE TABLE IF NOT EXISTS `Employees` (`EmployeeID` int(11), `FirstName` varchar(50), `LastName` varchar(50), `HireDate` date);
        INSERT INTO `Employees` (`EmployeeID`, `FirstName`, `LastName`, `HireDate`) VALUES
        	(2, 'Łukasz', 'Kowalski', '2007-08-07'),
        	(4, 'Ewa', 'Zielińska', '2020-02-01');
        """;

    // As HeidiSQL exports obecnosc: days since 30.12.1899 (45719 is Monday 3 March 2025), times as a fraction of the day.
    private const string Attendance = """
        -- Zrzut struktury tabela Kadry.obecnosc
        CREATE TABLE IF NOT EXISTS `obecnosc` (
          `id` int(11) NOT NULL AUTO_INCREMENT,
          `idprac` int(11) DEFAULT NULL,
          `dzien` int(11) DEFAULT NULL,
          `od` double DEFAULT NULL,
          `do` double DEFAULT NULL,
          `inne` tinytext,
          `razem` int(11) DEFAULT NULL,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

        INSERT INTO `inne_tabele` (`id`) VALUES (1);
        REPLACE INTO `obecnosc` (`id`, `idprac`, `dzien`, `od`, `do`, `inne`, `razem`) VALUES
        	(1, 2, 45719, 45719.25, 45719.5833333333, NULL, 480),
        	(2, 2, 45720, 45720.9166666667, 45721.25, '', 480),
        	(3, 4, 45719, 0, 0, 'DEL', 0),
        	(4, 4, 45720, 0, 0, 'DEL', 0),
        	(5, 4, 45721, 0, 0, 'DEL', 0);
        REPLACE INTO `obecnosc` (`id`, `idprac`, `dzien`, `od`, `do`, `inne`, `razem`) VALUES
        	(6, 4, 45723, 0, 0, 'ZP', 0),
        	(7, 4, 45724, 0, 0, 'ZO', 0),
        	(8, 4, 45725, 0, 0, '06:00', 0),
        	(9, 4, 45726, 0, 0, NULL, 0),
        	(10, 99, 45719, 45719.25, 45719.5833333333, NULL, 480);
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("personet-attendance-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_streaming_reader_takes_only_rows_of_the_table_with_their_line_in_the_file()
    {
        var rows = SqlDumpReader.StreamTable(new StringReader(Attendance), AttendanceSqlImport.Table).ToList();

        rows.Count.ShouldBe(10);
        rows[0].Line.ShouldBe(15);
        rows[0].Values["idprac"].ShouldBe("2");
        rows[5].Line.ShouldBe(21);
        rows[5].Values["inne"].ShouldBe("ZP");
    }

    [Fact]
    public void Days_worked_become_work_days_and_days_of_a_kind_in_a_row_one_period()
    {
        var import = AttendanceSqlImport.Read(new StringReader(Attendance));

        var plan = import.Plan(new HashSet<string>(["2", "4"]));

        import.Count.ShouldBe(10);
        plan.Days.ShouldBe(
        [
            new ImportWorkDayRow(15, "2", new DateOnly(2025, 3, 3), new TimeOnly(6, 0), new TimeOnly(14, 0)),
            new ImportWorkDayRow(16, "2", new DateOnly(2025, 3, 4), new TimeOnly(22, 0), new TimeOnly(6, 0)),
        ]);
        plan.Absences.ShouldBe(
        [
            new ImportAbsenceRow(17, "4", AbsenceKind.BusinessTrip, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 5)),
            new ImportAbsenceRow(22, "4", AbsenceKind.OtherExcused, new DateOnly(2025, 3, 8), new DateOnly(2025, 3, 8), "Kod ZO z poprzedniego systemu"),
        ]);
        plan.Leave.ShouldBe([new ImportLeaveRow(21, "4", LeaveKind.PaidRelease, new DateOnly(2025, 3, 7), new DateOnly(2025, 3, 7))]);
        plan.Problems.ShouldHaveSingleItem().Message.ShouldBe("Nieczytelny wpis „06:00” w dniu 09.03.2025.");
        plan.Empty.ShouldBe(1);
        plan.OtherEmployersRows.ShouldBe(1);
    }

    [Fact]
    public async Task Attendance_is_previewed_and_sent_after_the_employees_absences_before_days()
    {
        var employees = Path.Combine(_folder, "table-Employees.sql");
        var attendance = Path.Combine(_folder, "table-obecnosc.sql");
        await File.WriteAllTextAsync(employees, Employees, Cancellation);
        await File.WriteAllTextAsync(attendance, Attendance, Cancellation);
        var files = new FakeFileService();
        files.OpenPaths.AddRange([employees, attendance]);
        var absences = new FakeAbsencesApi();
        var workTime = new FakeWorkTimeApi();
        var viewModel = new EmployeeImportDialogViewModel(new FakeEmployeesApi(), absences, workTime, new FakeSafetyApi(), files);

        await viewModel.ChooseFileAsync();
        await viewModel.ImportAsync(Cancellation);

        viewModel.Details.ShouldNotBeNull().ShouldContain("z obecności dni pracy: 2, zwolnienia płatne: 1, nieobecności: 2, wiersze obecności z błędami: 1");
        absences.Imported.Select(request => (request.Leave.Count, request.Absences.Count)).ShouldBe([(1, 0), (0, 2)]);
        workTime.ImportedDays.ShouldHaveSingleItem().Days.Count.ShouldBe(2);
        viewModel.ResultText.ShouldNotBeNull().ShouldContain("Obecności: dni pracy 2, zwolnienia płatne 1, nieobecności 2.");
        viewModel.ResultText.ShouldContain("wiersz 23, pracownik 4: Nieczytelny wpis „06:00”");
        viewModel.Progress.ShouldBeNull();
    }
}
