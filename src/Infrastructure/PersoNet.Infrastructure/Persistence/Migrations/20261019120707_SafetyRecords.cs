using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersoNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SafetyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medical_exams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medical_exams", x => x.id);
                    table.ForeignKey(
                        name: "fk_medical_exams_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_exams_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    validity_months = table.Column<int>(type: "integer", nullable: true),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    department_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_courses", x => x.id);
                    table.ForeignKey(
                        name: "fk_training_courses_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_trainings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_trainings", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_trainings_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_trainings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_trainings_training_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "training_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_trainings_course_id",
                table: "employee_trainings",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_trainings_employee_id_course_id_completed_on",
                table: "employee_trainings",
                columns: new[] { "employee_id", "course_id", "completed_on" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_trainings_tenant_id",
                table: "employee_trainings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_trainings_valid_until",
                table: "employee_trainings",
                column: "valid_until");

            migrationBuilder.CreateIndex(
                name: "ix_medical_exams_employee_id_exam_date",
                table: "medical_exams",
                columns: new[] { "employee_id", "exam_date" });

            migrationBuilder.CreateIndex(
                name: "ix_medical_exams_tenant_id",
                table: "medical_exams",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_medical_exams_valid_until",
                table: "medical_exams",
                column: "valid_until");

            migrationBuilder.CreateIndex(
                name: "ix_training_courses_tenant_id",
                table: "training_courses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_courses_tenant_id_normalized_name",
                table: "training_courses",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            migrationBuilder.EnableTenantIsolation("training_courses");
            migrationBuilder.EnableTenantIsolation("employee_trainings");
            migrationBuilder.EnableTenantIsolation("medical_exams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_trainings");

            migrationBuilder.DropTable(
                name: "medical_exams");

            migrationBuilder.DropTable(
                name: "training_courses");
        }
    }
}
