using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformBackofficeAndFormalSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string migratedAtUtc = "2026-03-29T21:36:57Z";

            migrationBuilder.DropIndex(
                name: "uq_plan_entitlements__plan_module",
                table: "plan_entitlements");

            migrationBuilder.DropIndex(
                name: "IX_auth_refresh_tokens_user_id",
                table: "auth_refresh_tokens");

            migrationBuilder.AddColumn<long>(
                name: "commercial_plan_id",
                table: "plan_entitlements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "base_monthly_fee",
                table: "company_subscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "commercial_plan_id",
                table: "company_subscriptions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "plan_name",
                table: "company_subscriptions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_active_employee",
                table: "company_subscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "client_type",
                table: "auth_refresh_tokens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    diff_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_operators",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_operators", x => x.id);
                    table.ForeignKey(
                        name: "fk_platform_operators__auth_users",
                        column: x => x.user_id,
                        principalTable: "auth_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1002L,
                column: "commercial_plan_id",
                value: -3000L);

            migrationBuilder.UpdateData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1001L,
                column: "commercial_plan_id",
                value: -3000L);

            migrationBuilder.UpdateData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1000L,
                column: "commercial_plan_id",
                value: -3000L);

            migrationBuilder.Sql(
                $"""
                INSERT INTO commercial_plans (
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    description,
                    base_monthly_fee,
                    price_per_active_employee,
                    status,
                    is_system_plan,
                    concurrency_token,
                    public_id,
                    created_utc)
                SELECT
                    legacy.plan_code,
                    legacy.plan_code,
                    legacy.plan_code,
                    legacy.plan_code,
                    'Legacy plan migrated while formalizing company subscriptions.',
                    0,
                    0,
                    'Active',
                    FALSE,
                    (
                        substr(md5('legacy-plan-concurrency:' || legacy.plan_code), 1, 8) || '-' ||
                        substr(md5('legacy-plan-concurrency:' || legacy.plan_code), 9, 4) || '-' ||
                        substr(md5('legacy-plan-concurrency:' || legacy.plan_code), 13, 4) || '-' ||
                        substr(md5('legacy-plan-concurrency:' || legacy.plan_code), 17, 4) || '-' ||
                        substr(md5('legacy-plan-concurrency:' || legacy.plan_code), 21, 12)
                    )::uuid,
                    (
                        substr(md5('legacy-plan-public:' || legacy.plan_code), 1, 8) || '-' ||
                        substr(md5('legacy-plan-public:' || legacy.plan_code), 9, 4) || '-' ||
                        substr(md5('legacy-plan-public:' || legacy.plan_code), 13, 4) || '-' ||
                        substr(md5('legacy-plan-public:' || legacy.plan_code), 17, 4) || '-' ||
                        substr(md5('legacy-plan-public:' || legacy.plan_code), 21, 12)
                    )::uuid,
                    TIMESTAMPTZ '{migratedAtUtc}'
                FROM (
                    SELECT DISTINCT UPPER(TRIM(plan_code)) AS plan_code
                    FROM company_subscriptions
                    WHERE COALESCE(TRIM(plan_code), '') <> ''

                    UNION

                    SELECT DISTINCT UPPER(TRIM(plan_code)) AS plan_code
                    FROM plan_entitlements
                    WHERE COALESCE(TRIM(plan_code), '') <> ''
                ) AS legacy
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM commercial_plans existing_plan
                    WHERE existing_plan.normalized_code = legacy.plan_code);
                """);

            migrationBuilder.Sql(
                """
                UPDATE auth_refresh_tokens
                SET client_type = 'Core'
                WHERE client_type = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE plan_entitlements entitlement
                SET commercial_plan_id = plan.id,
                    plan_code = plan.code
                FROM commercial_plans plan
                WHERE entitlement.commercial_plan_id = 0
                  AND plan.normalized_code = UPPER(TRIM(entitlement.plan_code));
                """);

            migrationBuilder.Sql(
                """
                UPDATE company_subscriptions subscription
                SET commercial_plan_id = plan.id,
                    plan_code = plan.code,
                    plan_name = plan.name,
                    base_monthly_fee = plan.base_monthly_fee,
                    price_per_active_employee = plan.price_per_active_employee
                FROM commercial_plans plan
                WHERE subscription.commercial_plan_id = 0
                  AND plan.normalized_code = UPPER(TRIM(subscription.plan_code));
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM company_subscriptions
                        WHERE commercial_plan_id = 0
                    ) THEN
                        RAISE EXCEPTION 'Migration AddPlatformBackofficeAndFormalSubscriptionPlans could not backfill company_subscriptions.commercial_plan_id.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plan_entitlements
                        WHERE commercial_plan_id = 0
                    ) THEN
                        RAISE EXCEPTION 'Migration AddPlatformBackofficeAndFormalSubscriptionPlans could not backfill plan_entitlements.commercial_plan_id.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE auth_refresh_tokens ALTER COLUMN client_type DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN commercial_plan_id DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN plan_name DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN base_monthly_fee DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN price_per_active_employee DROP DEFAULT;
                ALTER TABLE plan_entitlements ALTER COLUMN commercial_plan_id DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements__plan_module",
                table: "plan_entitlements",
                columns: new[] { "commercial_plan_id", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__commercial_plan_id",
                table: "company_subscriptions",
                column: "commercial_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_refresh_tokens__user_client_revoked",
                table: "auth_refresh_tokens",
                columns: new[] { "user_id", "client_type", "revoked_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs__actor_created",
                table: "platform_audit_logs",
                columns: new[] { "actor_user_id", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs__entity",
                table: "platform_audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs__event_created",
                table: "platform_audit_logs",
                columns: new[] { "event_type", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_platform_audit_logs__public_id",
                table: "platform_audit_logs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_operators__active_role",
                table: "platform_operators",
                columns: new[] { "is_active", "role" });

            migrationBuilder.CreateIndex(
                name: "uq_platform_operators__public_id",
                table: "platform_operators",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_platform_operators__user_id",
                table: "platform_operators",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_company_subscriptions__commercial_plans",
                table: "company_subscriptions",
                column: "commercial_plan_id",
                principalTable: "commercial_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plan_entitlements__commercial_plans",
                table: "plan_entitlements",
                column: "commercial_plan_id",
                principalTable: "commercial_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_company_subscriptions__commercial_plans",
                table: "company_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "fk_plan_entitlements__commercial_plans",
                table: "plan_entitlements");

            migrationBuilder.DropTable(
                name: "platform_audit_logs");

            migrationBuilder.DropTable(
                name: "platform_operators");

            migrationBuilder.DropIndex(
                name: "uq_plan_entitlements__plan_module",
                table: "plan_entitlements");

            migrationBuilder.DropIndex(
                name: "ix_company_subscriptions__commercial_plan_id",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_auth_refresh_tokens__user_client_revoked",
                table: "auth_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "commercial_plan_id",
                table: "plan_entitlements");

            migrationBuilder.DropColumn(
                name: "base_monthly_fee",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "commercial_plan_id",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "plan_name",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "price_per_active_employee",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "client_type",
                table: "auth_refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements__plan_module",
                table: "plan_entitlements",
                columns: new[] { "plan_code", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_refresh_tokens_user_id",
                table: "auth_refresh_tokens",
                column: "user_id");
        }
    }
}
