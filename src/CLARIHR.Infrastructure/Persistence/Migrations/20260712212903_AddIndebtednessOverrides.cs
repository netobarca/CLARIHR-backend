using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndebtednessOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pf_rd_indebtedness_overrides",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recurring_deduction_id = table.Column<long>(type: "bigint", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acknowledged_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    base_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    monthly_load = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    new_installment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    projected_percent = table.Column<decimal>(type: "numeric(11,4)", nullable: false),
                    limit_percent = table.Column<decimal>(type: "numeric(11,4)", nullable: false),
                    limit_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pf_rd_indebtedness_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_pf_rd_indebt_overrides__recurring_deduction",
                        column: x => x.recurring_deduction_id,
                        principalTable: "personnel_file_recurring_deductions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pf_rd_indebt_overrides__deduction_utc",
                table: "pf_rd_indebtedness_overrides",
                columns: new[] { "recurring_deduction_id", "acknowledged_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_pf_rd_indebtedness_overrides__public_id",
                table: "pf_rd_indebtedness_overrides",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pf_rd_indebtedness_overrides");
        }
    }
}
