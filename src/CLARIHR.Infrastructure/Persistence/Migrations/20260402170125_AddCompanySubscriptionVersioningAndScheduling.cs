using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySubscriptionVersioningAndScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_company_subscriptions__company_active",
                table: "company_subscriptions");

            migrationBuilder.AddColumn<DateTime>(
                name: "activated_at_utc",
                table: "company_subscriptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "activated_by_user_public_id",
                table: "company_subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "commercial_plan_version_id",
                table: "company_subscriptions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "company_subscriptions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "periodicity",
                table: "company_subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "plan_version_number",
                table: "company_subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "billable_since_utc",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_billable",
                table: "companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "commercial_plan_versions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    base_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_per_active_employee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_plan_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_commercial_plan_versions__commercial_plans",
                        column: x => x.commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "commercial_plan_versions",
                columns: new[] { "id", "base_monthly_fee", "commercial_plan_id", "created_utc", "currency_code", "effective_from_utc", "effective_to_utc", "modified_utc", "price_per_active_employee", "public_id", "version_number" },
                values: new object[] { -3001L, 0m, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "USD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), 0m, new Guid("cf0c879c-d6c7-3d5d-d1cf-903ef0f66cfb"), 1 });

            migrationBuilder.Sql("""
                INSERT INTO commercial_plan_versions
                    (public_id, commercial_plan_id, version_number, currency_code, base_monthly_fee, price_per_active_employee, effective_from_utc, effective_to_utc, created_utc, modified_utc)
                SELECT
                    (
                        SUBSTRING(MD5('COMMERCIAL_PLAN_VERSION:' || cp.id::text || ':1'), 1, 8) || '-' ||
                        SUBSTRING(MD5('COMMERCIAL_PLAN_VERSION:' || cp.id::text || ':1'), 9, 4) || '-' ||
                        SUBSTRING(MD5('COMMERCIAL_PLAN_VERSION:' || cp.id::text || ':1'), 13, 4) || '-' ||
                        SUBSTRING(MD5('COMMERCIAL_PLAN_VERSION:' || cp.id::text || ':1'), 17, 4) || '-' ||
                        SUBSTRING(MD5('COMMERCIAL_PLAN_VERSION:' || cp.id::text || ':1'), 21, 12)
                    )::uuid,
                    cp.id,
                    1,
                    'USD',
                    cp.base_monthly_fee,
                    cp.price_per_active_employee,
                    cp.created_utc,
                    NULL,
                    cp.created_utc,
                    cp.modified_utc
                FROM commercial_plans cp
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM commercial_plan_versions cpv
                    WHERE cpv.commercial_plan_id = cp.id
                );
                """);

            migrationBuilder.Sql("""
                UPDATE company_subscriptions cs
                SET
                    commercial_plan_version_id = cpv.id,
                    plan_version_number = cpv.version_number,
                    currency_code = cpv.currency_code,
                    periodicity = 'Monthly',
                    activated_by_user_public_id = '00000000-0000-0000-0000-000000000000',
                    activated_at_utc = COALESCE(cs.created_utc, cs.start_date_utc)
                FROM commercial_plan_versions cpv
                WHERE cpv.commercial_plan_id = cs.commercial_plan_id
                  AND cpv.version_number = 1;
                """);

            migrationBuilder.Sql("""
                UPDATE companies c
                SET
                    is_billable = COALESCE(summary.has_billable, FALSE),
                    billable_since_utc = summary.billable_since_utc
                FROM (
                    SELECT
                        company.id AS company_id,
                        COALESCE(BOOL_OR(NOT plan.is_system_plan), FALSE) AS has_billable,
                        MIN(CASE WHEN NOT plan.is_system_plan THEN subscription.start_date_utc END) AS billable_since_utc
                    FROM companies company
                    LEFT JOIN company_subscriptions subscription
                        ON subscription.company_id = company.id
                        AND subscription.status = 'Active'
                    LEFT JOIN commercial_plans plan
                        ON plan.id = subscription.commercial_plan_id
                    GROUP BY company.id
                ) summary
                WHERE summary.company_id = c.id;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE company_subscriptions ALTER COLUMN activated_at_utc DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN activated_by_user_public_id DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN commercial_plan_version_id DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN currency_code DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN periodicity DROP DEFAULT;
                ALTER TABLE company_subscriptions ALTER COLUMN plan_version_number DROP DEFAULT;
                ALTER TABLE companies ALTER COLUMN is_billable DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__commercial_plan_version_id",
                table: "company_subscriptions",
                column: "commercial_plan_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_active",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__status_start_date",
                table: "company_subscriptions",
                columns: new[] { "status", "start_date_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_scheduled",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_plan_versions__plan_effective_from",
                table: "commercial_plan_versions",
                columns: new[] { "commercial_plan_id", "effective_from_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plan_versions__plan_version_number",
                table: "commercial_plan_versions",
                columns: new[] { "commercial_plan_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plan_versions__public_id",
                table: "commercial_plan_versions",
                column: "public_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_company_subscriptions__commercial_plan_versions",
                table: "company_subscriptions",
                column: "commercial_plan_version_id",
                principalTable: "commercial_plan_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_company_subscriptions__commercial_plan_versions",
                table: "company_subscriptions");

            migrationBuilder.DropTable(
                name: "commercial_plan_versions");

            migrationBuilder.DropIndex(
                name: "ix_company_subscriptions__commercial_plan_version_id",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "uq_company_subscriptions__company_active",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_company_subscriptions__status_start_date",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "uq_company_subscriptions__company_scheduled",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "activated_at_utc",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "activated_by_user_public_id",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "commercial_plan_version_id",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "periodicity",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "plan_version_number",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "billable_since_utc",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "is_billable",
                table: "companies");

        }
    }
}
