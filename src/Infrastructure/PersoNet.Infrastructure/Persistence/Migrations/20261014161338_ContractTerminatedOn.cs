using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersoNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractTerminatedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "terminated_on",
                table: "contracts",
                type: "date",
                nullable: true);

            // Contracts of people who have already left end on their last day of work, as new terminations do. The owner skips
            // row-level security only while it is not forced.
            migrationBuilder.Sql("""
                ALTER TABLE contracts NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE employees NO FORCE ROW LEVEL SECURITY;
                UPDATE contracts AS c
                SET terminated_on = e.termination_date
                FROM employees AS e
                WHERE e.id = c.employee_id
                  AND e.termination_date IS NOT NULL
                  AND c.start_date <= e.termination_date
                  AND (c.end_date IS NULL OR c.end_date > e.termination_date);
                ALTER TABLE employees FORCE ROW LEVEL SECURITY;
                ALTER TABLE contracts FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "terminated_on",
                table: "contracts");
        }
    }
}
