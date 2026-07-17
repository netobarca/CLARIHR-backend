using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyLegalProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "payroll_compliance_gates_enabled",
                table: "company_preferences",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company_legal_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employer_nit_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    isss_employer_registration_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fiscal_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    economic_activity_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    legal_representative_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_legal_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_legal_profiles__companies",
                        column: x => x.tenant_id,
                        principalTable: "companies",
                        principalColumn: "public_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_company_legal_profiles__public_id",
                table: "company_legal_profiles",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_legal_profiles__tenant_id",
                table: "company_legal_profiles",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_legal_profiles");

            migrationBuilder.DropColumn(
                name: "payroll_compliance_gates_enabled",
                table: "company_preferences");
        }
    }
}
