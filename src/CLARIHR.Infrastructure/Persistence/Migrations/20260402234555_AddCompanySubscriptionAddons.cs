using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySubscriptionAddons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_commercial_addon_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    commercial_addon_id = table.Column<long>(type: "bigint", nullable: false),
                    addon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    addon_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    addon_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    billing_model = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    measurement_unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    minimum_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resulting_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    quantity_basis = table.Column<int>(type: "integer", nullable: false),
                    estimated_next_charge_impact = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_subscription_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_commercial_addon_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_commercial_addon_changes__commercial_addons",
                        column: x => x.commercial_addon_id,
                        principalTable: "commercial_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_commercial_addon_changes__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_commercial_addon_changes__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_commercial_addons",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    commercial_addon_id = table.Column<long>(type: "bigint", nullable: false),
                    addon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    addon_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    addon_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    billing_model = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    measurement_unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    minimum_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status_effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_commercial_addons", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_commercial_addons__commercial_addons",
                        column: x => x.commercial_addon_id,
                        principalTable: "commercial_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_commercial_addons__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_commercial_addons__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_company_commercial_addon_changes__company_requested",
                table: "company_commercial_addon_changes",
                columns: new[] { "company_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_commercial_addon_changes__status_effective_date",
                table: "company_commercial_addon_changes",
                columns: new[] { "status", "effective_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addon_changes_commercial_addon_id",
                table: "company_commercial_addon_changes",
                column: "commercial_addon_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addon_changes_company_subscription_id",
                table: "company_commercial_addon_changes",
                column: "company_subscription_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addon_changes__company_addon_scheduled",
                table: "company_commercial_addon_changes",
                columns: new[] { "company_id", "commercial_addon_id", "status" },
                unique: true,
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addon_changes__public_id",
                table: "company_commercial_addon_changes",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_commercial_addons__company_status",
                table: "company_commercial_addons",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addons_commercial_addon_id",
                table: "company_commercial_addons",
                column: "commercial_addon_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addons_company_subscription_id",
                table: "company_commercial_addons",
                column: "company_subscription_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addons__company_addon",
                table: "company_commercial_addons",
                columns: new[] { "company_id", "commercial_addon_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addons__public_id",
                table: "company_commercial_addons",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_commercial_addon_changes");

            migrationBuilder.DropTable(
                name: "company_commercial_addons");
        }
    }
}
