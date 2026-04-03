using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySubscriptionStatusChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_subscription_status_change_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    current_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_subscription_status_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_subscription_status_change_requests__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_subscription_status_change_requests__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_status_change_requests__company_requested",
                table: "company_subscription_status_change_requests",
                columns: new[] { "company_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_status_change_requests__status_effective_date",
                table: "company_subscription_status_change_requests",
                columns: new[] { "status", "effective_date_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_company_subscription_status_change_requests__public_id",
                table: "company_subscription_status_change_requests",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_subscription_status_change_requests__subscription_scheduled",
                table: "company_subscription_status_change_requests",
                columns: new[] { "company_subscription_id", "status" },
                unique: true,
                filter: "status = 'Scheduled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_subscription_status_change_requests");
        }
    }
}
