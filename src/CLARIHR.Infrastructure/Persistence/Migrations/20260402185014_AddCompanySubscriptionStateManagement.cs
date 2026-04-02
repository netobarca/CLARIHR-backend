using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySubscriptionStateManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_company_subscriptions__company_active",
                table: "company_subscriptions");

            migrationBuilder.AddColumn<string>(
                name: "current_status_observations",
                table: "company_subscriptions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "current_status_origin",
                table: "company_subscriptions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "current_status_reason_code",
                table: "company_subscriptions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at_utc",
                table: "company_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "status_changed_at_utc",
                table: "company_subscriptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "company_subscription_status_transitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_subscription_status_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_subscription_status_transitions__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE company_subscriptions
                SET
                    status_changed_at_utc = CASE
                        WHEN status = 'Cancelled' THEN COALESCE(end_date_utc, modified_utc, activated_at_utc, created_utc, start_date_utc)
                        WHEN status = 'Scheduled' THEN COALESCE(activated_at_utc, created_utc, start_date_utc)
                        ELSE COALESCE(activated_at_utc, created_utc, start_date_utc)
                    END,
                    current_status_reason_code = CASE
                        WHEN status = 'Scheduled' THEN 'ActivationScheduled'
                        WHEN status = 'Cancelled' THEN 'CommercialCancellation'
                        WHEN status = 'Expired' THEN 'ExpirationReached'
                        WHEN status = 'Suspended' THEN 'ManualSuspension'
                        ELSE 'InitialAssignment'
                    END,
                    current_status_origin = 'SystemProcess',
                    current_status_observations = NULL
                WHERE current_status_reason_code = ''
                   OR current_status_origin = ''
                   OR status_changed_at_utc = TIMESTAMP '0001-01-01 00:00:00';
                """);

            migrationBuilder.Sql("""
                INSERT INTO company_subscription_status_transitions
                    (public_id, company_subscription_id, previous_status, new_status, reason_code, observations, changed_at_utc, origin, actor_user_public_id, created_utc, modified_utc)
                SELECT
                    (
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':INITIAL'), 1, 8) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':INITIAL'), 9, 4) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':INITIAL'), 13, 4) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':INITIAL'), 17, 4) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':INITIAL'), 21, 12)
                    )::uuid,
                    cs.id,
                    NULL,
                    CASE
                        WHEN cs.status = 'Cancelled'
                             AND cs.start_date_utc > COALESCE(cs.end_date_utc, cs.modified_utc, cs.activated_at_utc, cs.created_utc, cs.start_date_utc)
                            THEN 'Scheduled'
                        WHEN cs.status = 'Cancelled'
                            THEN 'Active'
                        ELSE cs.status
                    END,
                    CASE
                        WHEN cs.status = 'Scheduled' THEN 'ActivationScheduled'
                        WHEN cs.status = 'Cancelled'
                             AND cs.start_date_utc > COALESCE(cs.end_date_utc, cs.modified_utc, cs.activated_at_utc, cs.created_utc, cs.start_date_utc)
                            THEN 'ActivationScheduled'
                        ELSE 'InitialAssignment'
                    END,
                    NULL,
                    CASE
                        WHEN cs.status = 'Cancelled'
                            THEN COALESCE(cs.activated_at_utc, cs.created_utc, cs.start_date_utc)
                        ELSE cs.status_changed_at_utc
                    END,
                    'SystemProcess',
                    NULL,
                    CASE
                        WHEN cs.status = 'Cancelled'
                            THEN COALESCE(cs.activated_at_utc, cs.created_utc, cs.start_date_utc)
                        ELSE cs.status_changed_at_utc
                    END,
                    NULL
                FROM company_subscriptions cs;
                """);

            migrationBuilder.Sql("""
                INSERT INTO company_subscription_status_transitions
                    (public_id, company_subscription_id, previous_status, new_status, reason_code, observations, changed_at_utc, origin, actor_user_public_id, created_utc, modified_utc)
                SELECT
                    (
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':CURRENT'), 1, 8) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':CURRENT'), 9, 4) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':CURRENT'), 13, 4) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':CURRENT'), 17, 4) || '-' ||
                        SUBSTRING(MD5('COMPANY_SUBSCRIPTION_STATUS_TRANSITION:' || cs.id::text || ':CURRENT'), 21, 12)
                    )::uuid,
                    cs.id,
                    CASE
                        WHEN cs.start_date_utc > COALESCE(cs.end_date_utc, cs.modified_utc, cs.activated_at_utc, cs.created_utc, cs.start_date_utc)
                            THEN 'Scheduled'
                        ELSE 'Active'
                    END,
                    'Cancelled',
                    'CommercialCancellation',
                    'Legacy reconstructed cancellation transition.',
                    cs.status_changed_at_utc,
                    'SystemProcess',
                    NULL,
                    cs.status_changed_at_utc,
                    NULL
                FROM company_subscriptions cs
                WHERE cs.status = 'Cancelled';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__company_status_changed",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status_changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_live",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status IN ('Draft', 'Trial', 'Active', 'Suspended')");

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_status_transitions__subscription_changed",
                table: "company_subscription_status_transitions",
                columns: new[] { "company_subscription_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_company_subscription_status_transitions__public_id",
                table: "company_subscription_status_transitions",
                column: "public_id",
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE company_subscriptions ALTER COLUMN current_status_origin DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN current_status_reason_code DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN status_changed_at_utc DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_subscription_status_transitions");

            migrationBuilder.DropIndex(
                name: "ix_company_subscriptions__company_status_changed",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "uq_company_subscriptions__company_live",
                table: "company_subscriptions");

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_active",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.DropColumn(
                name: "current_status_observations",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "current_status_origin",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "current_status_reason_code",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "expires_at_utc",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "status_changed_at_utc",
                table: "company_subscriptions");
        }
    }
}
