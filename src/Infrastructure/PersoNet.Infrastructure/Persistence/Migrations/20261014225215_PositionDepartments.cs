using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersoNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PositionDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "department_ids",
                table: "positions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            // Existing positions get no departments, so they keep showing for every department.
            migrationBuilder.Sql("ALTER TABLE positions ALTER COLUMN department_ids DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "department_ids",
                table: "positions");
        }
    }
}
