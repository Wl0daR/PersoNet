using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersoNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clock_id",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_time_source",
                table: "company_settings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.CreateTable(
                name: "clock_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    device = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clock_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_clock_events_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clock_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_minutes = table.Column<int>(type: "integer", nullable: false),
                    absence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    problem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_days", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_days_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_days_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employees_tenant_id_clock_id",
                table: "employees",
                columns: new[] { "tenant_id", "clock_id" },
                unique: true,
                filter: "clock_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clock_events_employee_id",
                table: "clock_events",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_clock_events_tenant_id",
                table: "clock_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_clock_events_tenant_id_employee_id_occurred_at_direction",
                table: "clock_events",
                columns: new[] { "tenant_id", "employee_id", "occurred_at", "direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_days_employee_id",
                table: "work_days",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_days_tenant_id",
                table: "work_days",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_days_tenant_id_employee_id_date",
                table: "work_days",
                columns: new[] { "tenant_id", "employee_id", "date" },
                unique: true);

            migrationBuilder.EnableTenantIsolation("clock_events");
            migrationBuilder.EnableTenantIsolation("work_days");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clock_events");

            migrationBuilder.DropTable(
                name: "work_days");

            migrationBuilder.DropIndex(
                name: "ix_employees_tenant_id_clock_id",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "clock_id",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "work_time_source",
                table: "company_settings");
        }
    }
}
