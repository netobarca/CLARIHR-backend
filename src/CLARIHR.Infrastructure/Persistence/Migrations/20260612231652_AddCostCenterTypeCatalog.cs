using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCenterTypeCatalog : Migration
    {
        /// <summary>
        /// Replaces the cost_centers.type enum-string column with a tenant-scoped cost_center_types
        /// catalog + FK (mirror of work_center_types). Hand-staged instead of the scaffolded
        /// drop-then-add so existing rows survive: create catalog → seed the four legacy enum values
        /// per company → add nullable FK → backfill by (tenant, legacy string) → NOT NULL + FK →
        /// drop the legacy column.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cost_center_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cost_center_types", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cost_center_types__tenant_active",
                table: "cost_center_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_cost_center_types__tenant_name",
                table: "cost_center_types",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_cost_center_types__public_id",
                table: "cost_center_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_cost_center_types__tenant_code",
                table: "cost_center_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            // Seed the four legacy CostCenterType enum values as catalog rows for every existing
            // company (companies.public_id == tenant_id), plus any cost-center tenant missing a
            // company row, so the FK backfill below always finds its target. New companies start
            // with an empty catalog and create their own types (mirror of work_center_types).
            migrationBuilder.Sql(
                """
                INSERT INTO cost_center_types (
                    code, normalized_code, name, normalized_name, description,
                    is_active, concurrency_token, public_id, created_utc, tenant_id)
                SELECT
                    v.code, v.code, v.name, v.normalized_name, v.description,
                    TRUE, gen_random_uuid(), gen_random_uuid(), now(), t.tenant_id
                FROM (
                    SELECT public_id AS tenant_id FROM companies
                    UNION
                    SELECT DISTINCT tenant_id FROM cost_centers
                ) AS t
                CROSS JOIN (VALUES
                    ('SALARY-EXPENSE', 'Gasto salarial', 'GASTO SALARIAL', 'Centros de costo de gasto salarial.'),
                    ('EMPLOYER-CONTRIBUTION', 'Aporte patronal', 'APORTE PATRONAL', 'Centros de costo de aportes patronales.'),
                    ('PROVISION-RESERVE', 'Provision y reserva', 'PROVISION Y RESERVA', 'Centros de costo de provision o reserva.'),
                    ('MIXED', 'Mixto', 'MIXTO', 'Centros de costo mixtos.')
                ) AS v(code, name, normalized_name, description)
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.AddColumn<long>(
                name: "cost_center_type_id",
                table: "cost_centers",
                type: "bigint",
                nullable: true);

            // Backfill each cost center to its tenant's catalog row matching the legacy enum string.
            migrationBuilder.Sql(
                """
                UPDATE cost_centers cc
                SET cost_center_type_id = t.id
                FROM cost_center_types t
                WHERE t.tenant_id = cc.tenant_id
                  AND t.normalized_code = CASE cc.type
                        WHEN 'SalaryExpense' THEN 'SALARY-EXPENSE'
                        WHEN 'EmployerContribution' THEN 'EMPLOYER-CONTRIBUTION'
                        WHEN 'ProvisionReserve' THEN 'PROVISION-RESERVE'
                        ELSE 'MIXED'
                      END;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "cost_center_type_id",
                table: "cost_centers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "ix_cost_centers__tenant_type_active",
                table: "cost_centers");

            migrationBuilder.DropColumn(
                name: "type",
                table: "cost_centers");

            migrationBuilder.CreateIndex(
                name: "ix_cost_centers__tenant_type_active",
                table: "cost_centers",
                columns: new[] { "tenant_id", "cost_center_type_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_centers_cost_center_type_id",
                table: "cost_centers",
                column: "cost_center_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cost_centers__cost_center_types",
                table: "cost_centers",
                column: "cost_center_type_id",
                principalTable: "cost_center_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "cost_centers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Reverse-map the catalog reference to the legacy enum string; anything that is not one
            // of the four seeded codes degrades to 'Mixed' (the legacy catch-all).
            migrationBuilder.Sql(
                """
                UPDATE cost_centers cc
                SET type = CASE t.normalized_code
                        WHEN 'SALARY-EXPENSE' THEN 'SalaryExpense'
                        WHEN 'EMPLOYER-CONTRIBUTION' THEN 'EmployerContribution'
                        WHEN 'PROVISION-RESERVE' THEN 'ProvisionReserve'
                        ELSE 'Mixed'
                      END
                FROM cost_center_types t
                WHERE t.id = cc.cost_center_type_id;
                """);

            migrationBuilder.Sql("UPDATE cost_centers SET type = 'Mixed' WHERE type IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "cost_centers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_cost_centers__cost_center_types",
                table: "cost_centers");

            migrationBuilder.DropIndex(
                name: "ix_cost_centers__tenant_type_active",
                table: "cost_centers");

            migrationBuilder.DropIndex(
                name: "IX_cost_centers_cost_center_type_id",
                table: "cost_centers");

            migrationBuilder.DropColumn(
                name: "cost_center_type_id",
                table: "cost_centers");

            migrationBuilder.DropTable(
                name: "cost_center_types");

            migrationBuilder.CreateIndex(
                name: "ix_cost_centers__tenant_type_active",
                table: "cost_centers",
                columns: new[] { "tenant_id", "type", "is_active" });
        }
    }
}
