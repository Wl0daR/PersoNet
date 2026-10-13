using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersoNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "hours",
                table: "leave_requests",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_hourly",
                table: "leave_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Requests made before hours were kept: a day takes the employee's working day then (8 hours times the working
            // time of the contract, full time when there is none). The owner skips row-level security only while it is not forced.
            migrationBuilder.Sql("""
                ALTER TABLE leave_requests NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE contracts NO FORCE ROW LEVEL SECURITY;
                UPDATE leave_requests AS r
                SET hours = r.days * ROUND(8 * COALESCE((
                    SELECT c.working_time FROM contracts AS c
                    WHERE c.employee_id = r.employee_id AND c.start_date <= r.start_date AND (c.end_date IS NULL OR c.end_date >= r.start_date)
                    ORDER BY c.start_date DESC LIMIT 1), 1), 2);
                ALTER TABLE contracts FORCE ROW LEVEL SECURITY;
                ALTER TABLE leave_requests FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hours",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "is_hourly",
                table: "leave_requests");
        }
    }
}
