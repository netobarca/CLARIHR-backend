using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToPersonnelFileContractHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_custom_field_definitions");

            migrationBuilder.DropColumn(
                name: "custom_data",
                table: "personnel_files");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "personnel_file_contract_histories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "personnel_file_contract_histories");

            migrationBuilder.AddColumn<string>(
                name: "custom_data",
                table: "personnel_files",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "personnel_file_custom_field_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    field_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    normalized_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_custom_field_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_custom_field_definitions__tenant_active_sort",
                table: "personnel_file_custom_field_definitions",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_custom_field_definitions__public_id",
                table: "personnel_file_custom_field_definitions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_custom_field_definitions__tenant_key",
                table: "personnel_file_custom_field_definitions",
                columns: new[] { "tenant_id", "normalized_key" },
                unique: true);
        }
    }
}
