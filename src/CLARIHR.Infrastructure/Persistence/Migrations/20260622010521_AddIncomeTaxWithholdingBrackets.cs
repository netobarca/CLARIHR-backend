using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeTaxWithholdingBrackets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "income_tax_withholding_brackets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pay_period_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bracket_order = table.Column<int>(type: "integer", nullable: false),
                    lower_bound = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    upper_bound = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    fixed_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    rate_percent = table.Column<decimal>(type: "numeric(11,8)", nullable: false),
                    excess_over = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_income_tax_withholding_brackets", x => x.id);
                    table.CheckConstraint("ck_income_tax_withholding_brackets__bounds", "upper_bound is null or upper_bound >= lower_bound");
                });

            migrationBuilder.CreateIndex(
                name: "ix_income_tax_withholding_brackets__tenant_period_active_order",
                table: "income_tax_withholding_brackets",
                columns: new[] { "tenant_id", "pay_period_code", "is_active", "bracket_order" });

            migrationBuilder.CreateIndex(
                name: "uq_income_tax_withholding_brackets__public_id",
                table: "income_tax_withholding_brackets",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "income_tax_withholding_brackets");
        }
    }
}
