namespace PersoNet.Desktop.Snapshots;

/// <summary>A small export of an earlier system's tables of employees and their structure, as HeidiSQL writes it.</summary>
internal static class DemoSql
{
    public const string Employees = """
        CREATE TABLE IF NOT EXISTS `Employees` (
          `EmployeeID` int(11) NOT NULL AUTO_INCREMENT,
          `FirstName` varchar(50) NOT NULL,
          `LastName` varchar(50) NOT NULL,
          `HireDate` date NOT NULL,
          `TerminationDate` date DEFAULT NULL,
          `PhoneNumber` varchar(20) DEFAULT NULL,
          `UrlopR` int(11) DEFAULT NULL,
          PRIMARY KEY (`EmployeeID`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

        REPLACE INTO `Employees` (`EmployeeID`, `FirstName`, `LastName`, `HireDate`, `TerminationDate`, `PhoneNumber`, `UrlopR`) VALUES
        	(101, 'Jacek', 'Nowak', '2010-09-06', '2011-09-16', '123-456-789', 26),
        	(102, 'Łukasz', 'Kowalski', '2007-08-07', NULL, '', 26),
        	(103, 'Marcin', 'Laskowski', '0000-00-00', '2010-11-29', '', 26),
        	(104, 'Ewa', 'Zielińska', '2018-03-01', NULL, '600-100-200', 26),
        	(105, 'Katarzyna', 'Wiśniewska', '2021-06-14', NULL, NULL, 20),
        	(106, 'Piotr', 'Wójcik', '2019-02-01', '2018-12-31', NULL, 26),
        	(107, 'Magdalena', 'Kamińska', '2023-09-01', NULL, '512-345-678', 20),
        	(108, 'Tomasz', 'Lewandowski', '2015-04-20', NULL, NULL, 26),
        	(109, 'Agnieszka', 'Dąbrowska', '2012-01-09', '2024-06-30', NULL, 26),
        	(110, 'Michał', 'Zając', '2024-10-01', NULL, NULL, 20);

        REPLACE INTO `wydzialy` (`id`, `nazwa`, `opis`) VALUES (1, 'PVC', NULL), (4, 'Biuro', NULL), (5, 'Prima', NULL);
        REPLACE INTO `Departments` (`DepartmentID`, `DepartmentName`, `DepartmentType`) VALUES
        	(2, 'MAGAZYN PVC', 1), (11, 'MONTAŻ OKIEN', 1), (16, 'Dział Księgowości', 4), (20, 'Dział Personalny', 4), (34, 'Księgowość PKL', 5);
        REPLACE INTO `Positions` (`PositionID`, `PositionName`, `DepartmentID`) VALUES
        	(1, 'Magazynier', 2), (2, 'Monter okien', 11), (3, 'Księgowa', 16), (4, 'Specjalista ds. kadr', 20), (5, 'Brygadzista', 11);
        REPLACE INTO `Companies` (`CompanyID`, `CompanyName`, `IsTemporaryEmployee`) VALUES (1, 'VETREX', 0), (6, 'Prima-KL', 0);
        REPLACE INTO `EmploymentHistory` (`HistoryID`, `EmployeeID`, `DepartmentID`, `PositionID`, `StartDate`, `EndDate`, `Workload`, `CompanyID`) VALUES
        	(1, 101, 2, 1, '2010-09-06', '2011-09-16', 8, 1),
        	(2, 102, 11, 5, '2007-08-07', NULL, 8, 1),
        	(3, 104, 16, 3, '2018-03-01', NULL, 8, 1),
        	(4, 105, 20, 4, '2021-06-14', NULL, 8, 1),
        	(5, 106, 11, 2, '2019-02-01', NULL, 8, 1),
        	(6, 107, 11, 2, '2023-09-01', NULL, 4, 1),
        	(7, 108, 2, 1, '2015-04-20', NULL, 8, 1),
        	(8, 109, 34, 3, '2012-01-09', '2024-06-30', 8, 6),
        	(9, 110, 11, 2, '2024-10-01', NULL, 8, 1);
        """;
}
