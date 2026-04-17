using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveLocaleToUserPreferencesAndAddCompanyPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_companies_public_id",
                table: "companies",
                column: "public_id");

            migrationBuilder.CreateTable(
                name: "company_preferences",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_preferences__companies",
                        column: x => x.tenant_id,
                        principalTable: "companies",
                        principalColumn: "public_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    locale = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_preferences__auth_users",
                        column: x => x.user_id,
                        principalTable: "auth_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_company_preferences__public_id",
                table: "company_preferences",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_preferences__tenant_id",
                table: "company_preferences",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_user_preferences__public_id",
                table: "user_preferences",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_user_preferences__user_id",
                table: "user_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO company_preferences (currency_code, time_zone, concurrency_token, public_id, created_utc, modified_utc, tenant_id)
                SELECT
                    'USD',
                    'UTC',
                    c.public_id,
                    c.public_id,
                    CURRENT_TIMESTAMP AT TIME ZONE 'UTC',
                    NULL,
                    c.public_id
                FROM companies c;
                """);

            migrationBuilder.Sql("""
                INSERT INTO user_preferences (user_id, locale, public_id, created_utc, modified_utc)
                SELECT
                    u.id,
                    COALESCE(
                        NULLIF(
                            LOWER(SPLIT_PART((
                                SELECT NULLIF(c.default_locale, '')
                                FROM user_companies uc
                                JOIN companies c ON c.id = uc.company_id
                                WHERE uc.user_id = u.id AND uc.is_primary = true
                                ORDER BY uc.id
                                LIMIT 1
                            ), '-', 1)),
                            ''),
                        NULLIF(
                            LOWER(SPLIT_PART((
                                SELECT NULLIF(c.default_locale, '')
                                FROM user_companies uc
                                JOIN companies c ON c.id = uc.company_id
                                WHERE uc.user_id = u.id
                                ORDER BY uc.id
                                LIMIT 1
                            ), '-', 1)),
                            ''),
                        'en'
                    ),
                    u.public_id,
                    COALESCE(u.created_utc, CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                    u.modified_utc
                FROM auth_users u;
                """);

            migrationBuilder.DropColumn(
                name: "default_locale",
                table: "companies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_locale",
                table: "companies",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en-US");

            migrationBuilder.Sql("""
                UPDATE companies c
                SET default_locale = CASE up.locale
                    WHEN 'es' THEN 'es-SV'
                    WHEN 'pt' THEN 'pt-BR'
                    WHEN 'it' THEN 'it-IT'
                    ELSE 'en-US'
                END
                FROM auth_users u
                JOIN user_preferences up ON up.user_id = u.id
                WHERE u.public_id = c.created_by_user_public_id;
                """);

            migrationBuilder.DropTable(
                name: "company_preferences");

            migrationBuilder.DropTable(
                name: "user_preferences");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_companies_public_id",
                table: "companies");
        }
    }
}
