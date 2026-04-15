using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateGeneralCatalogsFromPersonnelCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currency_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "duration_unit_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_duration_unit_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "language_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_language_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "language_level_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_language_level_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reference_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_type_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_type_catalog_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_currency_catalog_items__tenant_active_sort",
                table: "currency_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_currency_catalog_items__public_id",
                table: "currency_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_currency_catalog_items__tenant_code",
                table: "currency_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_duration_unit_catalog_items__tenant_active_sort",
                table: "duration_unit_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_duration_unit_catalog_items__public_id",
                table: "duration_unit_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_duration_unit_catalog_items__tenant_code",
                table: "duration_unit_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_catalog_items__tenant_active_sort",
                table: "language_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_language_catalog_items__public_id",
                table: "language_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_language_catalog_items__tenant_code",
                table: "language_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_level_catalog_items__tenant_active_sort",
                table: "language_level_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_language_level_catalog_items__public_id",
                table: "language_level_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_language_level_catalog_items__tenant_code",
                table: "language_level_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_type_catalog_items__tenant_active_sort",
                table: "reference_type_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_reference_type_catalog_items__public_id",
                table: "reference_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_reference_type_catalog_items__tenant_code",
                table: "reference_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_training_type_catalog_items__tenant_active_sort",
                table: "training_type_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_training_type_catalog_items__public_id",
                table: "training_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_training_type_catalog_items__tenant_code",
                table: "training_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO language_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumLanguage'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO language_level_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumLanguageLevel'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO training_type_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumTrainingType'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO duration_unit_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumDurationUnit'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO reference_type_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumReferenceType'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO currency_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'Currency'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.DropTable(
                name: "personnel_catalog_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personnel_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_catalog_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_catalog_items__tenant_category_active_sort",
                table: "personnel_catalog_items",
                columns: new[] { "tenant_id", "category", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_personnel_catalog_items__public_id",
                table: "personnel_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_catalog_items__tenant_category_code",
                table: "personnel_catalog_items",
                columns: new[] { "tenant_id", "category", "normalized_code" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO personnel_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    category,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    'CurriculumLanguage',
                    code,
                    normalized_code,
                    LEFT(name, 150),
                    LEFT(normalized_name, 150),
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM language_catalog_items
                ON CONFLICT (tenant_id, category, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO personnel_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    category,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    'CurriculumLanguageLevel',
                    code,
                    normalized_code,
                    LEFT(name, 150),
                    LEFT(normalized_name, 150),
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM language_level_catalog_items
                ON CONFLICT (tenant_id, category, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO personnel_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    category,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    'CurriculumTrainingType',
                    code,
                    normalized_code,
                    LEFT(name, 150),
                    LEFT(normalized_name, 150),
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM training_type_catalog_items
                ON CONFLICT (tenant_id, category, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO personnel_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    category,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    'CurriculumDurationUnit',
                    code,
                    normalized_code,
                    LEFT(name, 150),
                    LEFT(normalized_name, 150),
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM duration_unit_catalog_items
                ON CONFLICT (tenant_id, category, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO personnel_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    category,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    'CurriculumReferenceType',
                    code,
                    normalized_code,
                    LEFT(name, 150),
                    LEFT(normalized_name, 150),
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM reference_type_catalog_items
                ON CONFLICT (tenant_id, category, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO personnel_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    category,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    'Currency',
                    code,
                    normalized_code,
                    LEFT(name, 150),
                    LEFT(normalized_name, 150),
                    is_system,
                    is_active,
                    sort_order,
                    concurrency_token
                FROM currency_catalog_items
                ON CONFLICT (tenant_id, category, normalized_code) DO NOTHING;
                """);

            migrationBuilder.DropTable(
                name: "currency_catalog_items");

            migrationBuilder.DropTable(
                name: "duration_unit_catalog_items");

            migrationBuilder.DropTable(
                name: "language_catalog_items");

            migrationBuilder.DropTable(
                name: "language_level_catalog_items");

            migrationBuilder.DropTable(
                name: "reference_type_catalog_items");

            migrationBuilder.DropTable(
                name: "training_type_catalog_items");
        }
    }
}
