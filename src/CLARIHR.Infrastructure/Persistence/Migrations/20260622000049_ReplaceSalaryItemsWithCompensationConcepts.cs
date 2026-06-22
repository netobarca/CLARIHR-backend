using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSalaryItemsWithCompensationConcepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_salary_items");

            migrationBuilder.CreateTable(
                name: "personnel_file_compensation_concepts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_position_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concept_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    deduction_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    calculation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    calculation_base_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    employer_rate = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    contribution_cap = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pay_period_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    counterparty_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_suggested = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_compensation_concepts", x => x.id);
                    table.CheckConstraint("ck_personnel_file_compensation_concepts__dates", "end_date is null or end_date >= start_date");
                    table.CheckConstraint("ck_personnel_file_compensation_concepts__value_non_negative", "value >= 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_compensation_concepts__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_compensation_concepts__tenant_assigned_position",
                table: "personnel_file_compensation_concepts",
                columns: new[] { "tenant_id", "assigned_position_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_compensation_concepts__tenant_file_nature_active",
                table: "personnel_file_compensation_concepts",
                columns: new[] { "tenant_id", "personnel_file_id", "nature", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_compensation_concepts_personnel_file_id",
                table: "personnel_file_compensation_concepts",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_compensation_concepts__public_id",
                table: "personnel_file_compensation_concepts",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_compensation_concepts");

            migrationBuilder.CreateTable(
                name: "personnel_file_salary_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    income_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pay_period_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_rubric_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_salary_items", x => x.id);
                    table.CheckConstraint("ck_personnel_file_salary_items__amount_non_negative", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_salary_items__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_salary_items__tenant_file_start_active",
                table: "personnel_file_salary_items",
                columns: new[] { "tenant_id", "personnel_file_id", "start_date", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_salary_items_personnel_file_id",
                table: "personnel_file_salary_items",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_salary_items__public_id",
                table: "personnel_file_salary_items",
                column: "public_id",
                unique: true);
        }
    }
}
