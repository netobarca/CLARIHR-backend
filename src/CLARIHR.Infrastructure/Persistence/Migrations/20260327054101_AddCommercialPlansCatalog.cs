using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialPlansCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commercial_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    base_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_per_active_employee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_system_plan = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plan_limits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    limit_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_limit_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_plan_limits", x => x.id);
                    table.ForeignKey(
                        name: "fk_commercial_plan_limits__commercial_plans",
                        column: x => x.commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "commercial_plans",
                columns: new[] { "id", "base_monthly_fee", "code", "concurrency_token", "created_utc", "description", "is_system_plan", "modified_utc", "name", "normalized_code", "normalized_name", "price_per_active_employee", "public_id", "status" },
                values: new object[] { -3000L, 0m, "FREE", new Guid("00000000-0000-0000-0000-000000000902"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Canonical free commercial plan used during provisioning.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Free", "FREE", "FREE", 0m, new Guid("00000000-0000-0000-0000-000000000901"), "Active" });

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plan_limits__plan_limit_code",
                table: "commercial_plan_limits",
                columns: new[] { "commercial_plan_id", "normalized_limit_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commercial_plans__normalized_name",
                table: "commercial_plans",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_plans__status",
                table: "commercial_plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plans__normalized_code",
                table: "commercial_plans",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plans__public_id",
                table: "commercial_plans",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commercial_plan_limits");

            migrationBuilder.DropTable(
                name: "commercial_plans");
        }
    }
}
