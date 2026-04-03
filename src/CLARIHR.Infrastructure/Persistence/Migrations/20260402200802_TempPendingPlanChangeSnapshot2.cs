using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySubscriptionPlanChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_subscription_plan_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    current_commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    current_commercial_plan_version_id = table.Column<long>(type: "bigint", nullable: false),
                    current_plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    current_plan_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    current_plan_version_number = table.Column<int>(type: "integer", nullable: false),
                    current_base_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_price_per_active_employee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    target_commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    target_commercial_plan_version_id = table.Column<long>(type: "bigint", nullable: false),
                    target_plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_plan_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    target_plan_version_number = table.Column<int>(type: "integer", nullable: false),
                    target_base_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    target_price_per_active_employee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    target_periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    estimated_next_charge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    active_employee_count = table.Column<int>(type: "integer", nullable: false),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_subscription_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_subscription_plan_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_subscription_plan_changes__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_subscription_plan_changes__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_subscription_plan_changes__current_commercial_plans",
                        column: x => x.current_commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_subscription_plan_changes__current_plan_versions",
                        column: x => x.current_commercial_plan_version_id,
                        principalTable: "commercial_plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_subscription_plan_changes__target_commercial_plans",
                        column: x => x.target_commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_subscription_plan_changes__target_plan_versions",
                        column: x => x.target_commercial_plan_version_id,
                        principalTable: "commercial_plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_plan_changes__company_requested",
                table: "company_subscription_plan_changes",
                columns: new[] { "company_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_plan_changes__status_effective_date",
                table: "company_subscription_plan_changes",
                columns: new[] { "status", "effective_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_plan_changes__subscription_id",
                table: "company_subscription_plan_changes",
                column: "company_subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_subscription_plan_changes_current_commercial_plan_id",
                table: "company_subscription_plan_changes",
                column: "current_commercial_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_subscription_plan_changes_current_commercial_plan_v~",
                table: "company_subscription_plan_changes",
                column: "current_commercial_plan_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_subscription_plan_changes_target_commercial_plan_id",
                table: "company_subscription_plan_changes",
                column: "target_commercial_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_subscription_plan_changes_target_commercial_plan_ve~",
                table: "company_subscription_plan_changes",
                column: "target_commercial_plan_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_subscription_plan_changes__company_scheduled",
                table: "company_subscription_plan_changes",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "uq_company_subscription_plan_changes__public_id",
                table: "company_subscription_plan_changes",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_subscription_plan_changes");
        }
    }
}
