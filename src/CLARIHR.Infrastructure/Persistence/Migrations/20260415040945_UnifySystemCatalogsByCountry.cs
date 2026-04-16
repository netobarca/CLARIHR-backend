using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnifySystemCatalogsByCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_training_type_catalog_items__tenant_active_sort",
                table: "training_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_training_type_catalog_items__tenant_code",
                table: "training_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_reference_type_catalog_items__tenant_active_sort",
                table: "reference_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_reference_type_catalog_items__tenant_code",
                table: "reference_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_language_level_catalog_items__tenant_active_sort",
                table: "language_level_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_language_level_catalog_items__tenant_code",
                table: "language_level_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_language_catalog_items__tenant_active_sort",
                table: "language_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_language_catalog_items__tenant_code",
                table: "language_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_study_type_catalog_items__tenant_active_sort",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_study_type_catalog_items__tenant_code",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_status_catalog_items__tenant_active_sort",
                table: "education_status_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_status_catalog_items__tenant_code",
                table: "education_status_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_shift_catalog_items__tenant_active_sort",
                table: "education_shift_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_shift_catalog_items__tenant_code",
                table: "education_shift_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_modality_catalog_items__tenant_active_sort",
                table: "education_modality_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_modality_catalog_items__tenant_code",
                table: "education_modality_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_career_catalog_items__tenant_active_sort",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_career_catalog_items__tenant_code",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_duration_unit_catalog_items__tenant_active_sort",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_duration_unit_catalog_items__tenant_code",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_currency_catalog_items__tenant_active_sort",
                table: "currency_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_currency_catalog_items__tenant_code",
                table: "currency_catalog_items");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "training_type_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "training_type_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "reference_type_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "reference_type_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "language_level_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "language_level_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "language_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "language_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "education_study_type_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "education_study_type_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "education_status_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "education_status_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "education_shift_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "education_shift_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "education_modality_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "education_modality_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "education_career_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "education_career_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "duration_unit_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "duration_unit_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "currency_catalog_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "currency_catalog_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "department_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_department_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_department_catalog_items_country_catalog_country_catalog_it~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identification_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identification_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_identification_type_catalog_items_country_catalog_country_c~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kinship_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kinship_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_kinship_catalog_items_country_catalog_country_catalog_item_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "marital_status_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marital_status_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_marital_status_catalog_items_country_catalog_country_catalo~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profession_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profession_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_profession_catalog_items_country_catalog_country_catalog_it~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "municipality_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    department_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipality_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_municipality_catalog_items_country_catalog_country_catalog_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_municipality_catalog_items__department",
                        column: x => x.department_catalog_item_id,
                        principalTable: "department_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            BackfillCountryScopedCatalogTable(migrationBuilder, "language_catalog_items");
            BackfillCountryScopedCatalogTable(migrationBuilder, "language_level_catalog_items");
            BackfillCountryScopedCatalogTable(migrationBuilder, "training_type_catalog_items");
            BackfillCountryScopedCatalogTable(migrationBuilder, "duration_unit_catalog_items");
            BackfillCountryScopedCatalogTable(migrationBuilder, "reference_type_catalog_items");
            BackfillCountryScopedCatalogTable(migrationBuilder, "currency_catalog_items");

            BackfillCountryScopedEducationCatalogTable(
                migrationBuilder,
                "education_status_catalog_items",
                "personnel_file_educations",
                "education_status_catalog_item_id");
            BackfillCountryScopedEducationCatalogTable(
                migrationBuilder,
                "education_study_type_catalog_items",
                "personnel_file_educations",
                "education_study_type_catalog_item_id");
            BackfillCountryScopedEducationCatalogTable(
                migrationBuilder,
                "education_career_catalog_items",
                "personnel_file_educations",
                "education_career_catalog_item_id");
            BackfillCountryScopedEducationCatalogTable(
                migrationBuilder,
                "education_shift_catalog_items",
                "personnel_file_educations",
                "education_shift_catalog_item_id");
            BackfillCountryScopedEducationCatalogTable(
                migrationBuilder,
                "education_modality_catalog_items",
                "personnel_file_educations",
                "education_modality_catalog_item_id");

            CopyFlatReferenceCatalogItems(migrationBuilder, "IdentificationType", "identification_type_catalog_items");
            CopyFlatReferenceCatalogItems(migrationBuilder, "Profession", "profession_catalog_items");
            CopyFlatReferenceCatalogItems(migrationBuilder, "MaritalStatus", "marital_status_catalog_items");
            CopyFlatReferenceCatalogItems(migrationBuilder, "Kinship", "kinship_catalog_items");
            CopyDepartmentCatalogItems(migrationBuilder);
            CopyMunicipalityCatalogItems(migrationBuilder);
            MigrateLegalRepresentativeDocumentTypes(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "training_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "training_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "reference_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "reference_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "language_level_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "language_level_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "language_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "language_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "education_status_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "education_shift_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "education_modality_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "currency_catalog_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "currency_catalog_items");

            migrationBuilder.DropTable(
                name: "legal_representative_document_type_catalog");

            migrationBuilder.DropTable(
                name: "personnel_reference_catalog_items");

            migrationBuilder.CreateIndex(
                name: "ix_training_type_catalog_items__country_active_sort",
                table: "training_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_training_type_catalog_items__country_code",
                table: "training_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_type_catalog_items__country_active_sort",
                table: "reference_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_reference_type_catalog_items__country_code",
                table: "reference_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_level_catalog_items__country_active_sort",
                table: "language_level_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_language_level_catalog_items__country_code",
                table: "language_level_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_catalog_items__country_active_sort",
                table: "language_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_language_catalog_items__country_code",
                table: "language_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_study_type_catalog_items__country_active_sort",
                table: "education_study_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_study_type_catalog_items__country_code",
                table: "education_study_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_status_catalog_items__country_active_sort",
                table: "education_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_status_catalog_items__country_code",
                table: "education_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_shift_catalog_items__country_active_sort",
                table: "education_shift_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_shift_catalog_items__country_code",
                table: "education_shift_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_modality_catalog_items__country_active_sort",
                table: "education_modality_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_modality_catalog_items__country_code",
                table: "education_modality_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_career_catalog_items__country_active_sort",
                table: "education_career_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__country_code",
                table: "education_career_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_duration_unit_catalog_items__country_active_sort",
                table: "duration_unit_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_duration_unit_catalog_items__country_code",
                table: "duration_unit_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currency_catalog_items__country_active_sort",
                table: "currency_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_currency_catalog_items__country_code",
                table: "currency_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_department_catalog_items__country_active_sort",
                table: "department_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_department_catalog_items__country_code",
                table: "department_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_department_catalog_items__public_id",
                table: "department_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identification_type_catalog_items__country_active_sort",
                table: "identification_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_identification_type_catalog_items__country_code",
                table: "identification_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_identification_type_catalog_items__public_id",
                table: "identification_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kinship_catalog_items__country_active_sort",
                table: "kinship_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_kinship_catalog_items__country_code",
                table: "kinship_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_kinship_catalog_items__public_id",
                table: "kinship_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marital_status_catalog_items__country_active_sort",
                table: "marital_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_marital_status_catalog_items__country_code",
                table: "marital_status_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_marital_status_catalog_items__public_id",
                table: "marital_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipality_catalog_items__country_active_sort",
                table: "municipality_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_municipality_catalog_items__department_active_sort",
                table: "municipality_catalog_items",
                columns: new[] { "department_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_municipality_catalog_items__country_code",
                table: "municipality_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_municipality_catalog_items__public_id",
                table: "municipality_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profession_catalog_items__country_active_sort",
                table: "profession_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_profession_catalog_items__country_code",
                table: "profession_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_profession_catalog_items__public_id",
                table: "profession_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_currency_catalog_items_country_catalog_country_catalog_item~",
                table: "currency_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_duration_unit_catalog_items_country_catalog_country_catalog~",
                table: "duration_unit_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_education_career_catalog_items_country_catalog_country_cata~",
                table: "education_career_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_education_modality_catalog_items_country_catalog_country_ca~",
                table: "education_modality_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_education_shift_catalog_items_country_catalog_country_catal~",
                table: "education_shift_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_education_status_catalog_items_country_catalog_country_cata~",
                table: "education_status_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_education_study_type_catalog_items_country_catalog_country_~",
                table: "education_study_type_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_language_catalog_items_country_catalog_country_catalog_item~",
                table: "language_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_language_level_catalog_items_country_catalog_country_catalo~",
                table: "language_level_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reference_type_catalog_items_country_catalog_country_catalo~",
                table: "reference_type_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_training_type_catalog_items_country_catalog_country_catalog~",
                table: "training_type_catalog_items",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        private static void BackfillCountryScopedCatalogTable(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE {tableName} item
                SET country_catalog_item_id = company.country_catalog_item_id,
                    country_code = company.country_code
                FROM companies company
                WHERE company.public_id = item.tenant_id;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM {tableName}
                        WHERE country_catalog_item_id = 0
                           OR country_code = '') THEN
                        RAISE EXCEPTION 'Country backfill failed for table {tableName}.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT country_catalog_item_id, normalized_code
                            FROM {tableName}
                            GROUP BY country_catalog_item_id, normalized_code
                            HAVING COUNT(DISTINCT normalized_name) > 1
                        ) conflicts) THEN
                        RAISE EXCEPTION 'Conflicting country-scoped codes detected in table {tableName}.';
                    END IF;
                END $$;

                WITH duplicates AS (
                    SELECT MIN(id) AS keep_id,
                           UNNEST(ARRAY_REMOVE(ARRAY_AGG(id ORDER BY id), MIN(id))) AS duplicate_id
                    FROM {tableName}
                    GROUP BY country_catalog_item_id, normalized_code
                    HAVING COUNT(*) > 1
                )
                DELETE FROM {tableName} target
                USING duplicates
                WHERE target.id = duplicates.duplicate_id;
                """);
        }

        private static void BackfillCountryScopedEducationCatalogTable(
            MigrationBuilder migrationBuilder,
            string tableName,
            string dependentTableName,
            string dependentColumnName)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE {tableName} item
                SET country_catalog_item_id = company.country_catalog_item_id,
                    country_code = company.country_code
                FROM companies company
                WHERE company.public_id = item.tenant_id;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM {tableName}
                        WHERE country_catalog_item_id = 0
                           OR country_code = '') THEN
                        RAISE EXCEPTION 'Country backfill failed for table {tableName}.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT country_catalog_item_id, normalized_code
                            FROM {tableName}
                            GROUP BY country_catalog_item_id, normalized_code
                            HAVING COUNT(DISTINCT normalized_name) > 1
                        ) conflicts) THEN
                        RAISE EXCEPTION 'Conflicting country-scoped codes detected in table {tableName}.';
                    END IF;
                END $$;

                WITH duplicates AS (
                    SELECT MIN(id) AS keep_id,
                           UNNEST(ARRAY_REMOVE(ARRAY_AGG(id ORDER BY id), MIN(id))) AS duplicate_id
                    FROM {tableName}
                    GROUP BY country_catalog_item_id, normalized_code
                    HAVING COUNT(*) > 1
                )
                UPDATE {dependentTableName} dependent
                SET {dependentColumnName} = duplicates.keep_id
                FROM duplicates
                WHERE dependent.{dependentColumnName} = duplicates.duplicate_id;

                WITH duplicates AS (
                    SELECT MIN(id) AS keep_id,
                           UNNEST(ARRAY_REMOVE(ARRAY_AGG(id ORDER BY id), MIN(id))) AS duplicate_id
                    FROM {tableName}
                    GROUP BY country_catalog_item_id, normalized_code
                    HAVING COUNT(*) > 1
                )
                DELETE FROM {tableName} target
                USING duplicates
                WHERE target.id = duplicates.duplicate_id;
                """);
        }

        private static void CopyFlatReferenceCatalogItems(MigrationBuilder migrationBuilder, string category, string targetTableName)
        {
            migrationBuilder.Sql(
                $"""
                INSERT INTO {targetTableName} (
                    public_id,
                    created_utc,
                    modified_utc,
                    country_catalog_item_id,
                    country_code,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT DISTINCT ON (country.id, item.normalized_code)
                    item.public_id,
                    item.created_utc,
                    item.modified_utc,
                    country.id,
                    item.country_code,
                    item.code,
                    item.normalized_code,
                    item.name,
                    item.normalized_name,
                    item.is_active,
                    item.sort_order,
                    item.public_id
                FROM personnel_reference_catalog_items item
                JOIN country_catalog country
                  ON country.normalized_code = UPPER(item.country_code)
                WHERE item.category = '{category}'
                ORDER BY country.id, item.normalized_code, item.id;
                """);
        }

        private static void CopyDepartmentCatalogItems(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO department_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    country_catalog_item_id,
                    country_code,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT DISTINCT ON (country.id, item.normalized_code)
                    item.public_id,
                    item.created_utc,
                    item.modified_utc,
                    country.id,
                    item.country_code,
                    item.code,
                    item.normalized_code,
                    item.name,
                    item.normalized_name,
                    item.is_active,
                    item.sort_order,
                    item.public_id
                FROM personnel_reference_catalog_items item
                JOIN country_catalog country
                  ON country.normalized_code = UPPER(item.country_code)
                WHERE item.category = 'Department'
                ORDER BY country.id, item.normalized_code, item.id;
                """);
        }

        private static void CopyMunicipalityCatalogItems(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM personnel_reference_catalog_items item
                        WHERE item.category = 'Municipality'
                          AND item.parent_id IS NULL) THEN
                        RAISE EXCEPTION 'Municipality rows without department parent were found in personnel_reference_catalog_items.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM personnel_reference_catalog_items item
                        LEFT JOIN personnel_reference_catalog_items parent
                          ON parent.id = item.parent_id
                         AND parent.category = 'Department'
                         AND UPPER(parent.country_code) = UPPER(item.country_code)
                        LEFT JOIN country_catalog country
                          ON country.normalized_code = UPPER(item.country_code)
                        LEFT JOIN department_catalog_items department
                          ON department.country_catalog_item_id = country.id
                         AND department.normalized_code = parent.normalized_code
                        WHERE item.category = 'Municipality'
                          AND (parent.id IS NULL OR country.id IS NULL OR department.id IS NULL)) THEN
                        RAISE EXCEPTION 'Municipality rows with invalid department relation were found in personnel_reference_catalog_items.';
                    END IF;
                END $$;

                INSERT INTO municipality_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    country_catalog_item_id,
                    country_code,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_active,
                    sort_order,
                    concurrency_token,
                    department_catalog_item_id)
                SELECT DISTINCT ON (country.id, item.normalized_code)
                    item.public_id,
                    item.created_utc,
                    item.modified_utc,
                    country.id,
                    item.country_code,
                    item.code,
                    item.normalized_code,
                    item.name,
                    item.normalized_name,
                    item.is_active,
                    item.sort_order,
                    item.public_id,
                    department.id
                FROM personnel_reference_catalog_items item
                JOIN personnel_reference_catalog_items parent
                  ON parent.id = item.parent_id
                JOIN country_catalog country
                  ON country.normalized_code = UPPER(item.country_code)
                JOIN department_catalog_items department
                  ON department.country_catalog_item_id = country.id
                 AND department.normalized_code = parent.normalized_code
                WHERE item.category = 'Municipality'
                ORDER BY country.id, item.normalized_code, item.id;
                """);
        }

        private static void MigrateLegalRepresentativeDocumentTypes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE legal_representatives legal_representative
                SET document_type = CASE UPPER(legal_representative.document_type)
                    WHEN 'NATIONALID' THEN CASE WHEN company.country_code = 'SV' THEN 'DUI' ELSE 'NATIONAL_ID' END
                    WHEN 'TAXID' THEN CASE WHEN company.country_code = 'SV' THEN 'NIT' ELSE 'TAX_ID' END
                    WHEN 'PASSPORT' THEN 'PASSPORT'
                    WHEN 'OTHER' THEN 'OTHER'
                    ELSE UPPER(legal_representative.document_type)
                END
                FROM companies company
                WHERE company.public_id = legal_representative.tenant_id;

                INSERT INTO identification_type_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    country_catalog_item_id,
                    country_code,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT DISTINCT
                    (
                        SUBSTRING(MD5(company.country_code || ':identification:' || mapped.code), 1, 8) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification:' || mapped.code), 9, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification:' || mapped.code), 13, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification:' || mapped.code), 17, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification:' || mapped.code), 21, 12)
                    )::uuid,
                    NOW(),
                    NOW(),
                    company.country_catalog_item_id,
                    company.country_code,
                    mapped.code,
                    mapped.code,
                    mapped.name,
                    UPPER(mapped.name),
                    TRUE,
                    mapped.sort_order,
                    (
                        SUBSTRING(MD5(company.country_code || ':identification-token:' || mapped.code), 1, 8) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification-token:' || mapped.code), 9, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification-token:' || mapped.code), 13, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification-token:' || mapped.code), 17, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':identification-token:' || mapped.code), 21, 12)
                    )::uuid
                FROM companies company
                CROSS JOIN LATERAL (
                    SELECT
                        CASE
                            WHEN company.country_code = 'SV' AND UPPER(item.code) = 'NATIONALID' THEN 'DUI'
                            WHEN company.country_code = 'SV' AND UPPER(item.code) = 'TAXID' THEN 'NIT'
                            ELSE UPPER(item.code)
                        END AS code,
                        CASE
                            WHEN company.country_code = 'SV' AND UPPER(item.code) = 'NATIONALID' THEN 'DUI'
                            WHEN company.country_code = 'SV' AND UPPER(item.code) = 'TAXID' THEN 'NIT'
                            WHEN UPPER(item.code) = 'OTHER' THEN 'Other'
                            ELSE item.name
                        END AS name,
                        item.sort_order
                    FROM legal_representative_document_type_catalog item
                ) mapped
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM identification_type_catalog_items existing
                    WHERE existing.country_catalog_item_id = company.country_catalog_item_id
                      AND existing.normalized_code = mapped.code);

                INSERT INTO identification_type_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    country_catalog_item_id,
                    country_code,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    is_active,
                    sort_order,
                    concurrency_token)
                SELECT DISTINCT
                    (
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification:' || UPPER(legal_representative.document_type)), 1, 8) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification:' || UPPER(legal_representative.document_type)), 9, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification:' || UPPER(legal_representative.document_type)), 13, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification:' || UPPER(legal_representative.document_type)), 17, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification:' || UPPER(legal_representative.document_type)), 21, 12)
                    )::uuid,
                    NOW(),
                    NOW(),
                    company.country_catalog_item_id,
                    company.country_code,
                    UPPER(legal_representative.document_type),
                    UPPER(legal_representative.document_type),
                    CASE UPPER(legal_representative.document_type)
                        WHEN 'DUI' THEN 'DUI'
                        WHEN 'NIT' THEN 'NIT'
                        WHEN 'PASSPORT' THEN 'Passport'
                        WHEN 'OTHER' THEN 'Other'
                        WHEN 'NATIONAL_ID' THEN 'National ID'
                        WHEN 'TAX_ID' THEN 'Tax ID'
                        ELSE UPPER(legal_representative.document_type)
                    END,
                    UPPER(CASE UPPER(legal_representative.document_type)
                        WHEN 'DUI' THEN 'DUI'
                        WHEN 'NIT' THEN 'NIT'
                        WHEN 'PASSPORT' THEN 'Passport'
                        WHEN 'OTHER' THEN 'Other'
                        WHEN 'NATIONAL_ID' THEN 'National ID'
                        WHEN 'TAX_ID' THEN 'Tax ID'
                        ELSE UPPER(legal_representative.document_type)
                    END),
                    TRUE,
                    1000,
                    (
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification-token:' || UPPER(legal_representative.document_type)), 1, 8) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification-token:' || UPPER(legal_representative.document_type)), 9, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification-token:' || UPPER(legal_representative.document_type)), 13, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification-token:' || UPPER(legal_representative.document_type)), 17, 4) || '-' ||
                        SUBSTRING(MD5(company.country_code || ':legal-representative-identification-token:' || UPPER(legal_representative.document_type)), 21, 12)
                    )::uuid
                FROM legal_representatives legal_representative
                JOIN companies company
                  ON company.public_id = legal_representative.tenant_id
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM identification_type_catalog_items existing
                    WHERE existing.country_catalog_item_id = company.country_catalog_item_id
                      AND existing.normalized_code = UPPER(legal_representative.document_type));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_currency_catalog_items_country_catalog_country_catalog_item~",
                table: "currency_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_duration_unit_catalog_items_country_catalog_country_catalog~",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_education_career_catalog_items_country_catalog_country_cata~",
                table: "education_career_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_education_modality_catalog_items_country_catalog_country_ca~",
                table: "education_modality_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_education_shift_catalog_items_country_catalog_country_catal~",
                table: "education_shift_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_education_status_catalog_items_country_catalog_country_cata~",
                table: "education_status_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_education_study_type_catalog_items_country_catalog_country_~",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_language_catalog_items_country_catalog_country_catalog_item~",
                table: "language_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_language_level_catalog_items_country_catalog_country_catalo~",
                table: "language_level_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_reference_type_catalog_items_country_catalog_country_catalo~",
                table: "reference_type_catalog_items");

            migrationBuilder.DropForeignKey(
                name: "FK_training_type_catalog_items_country_catalog_country_catalog~",
                table: "training_type_catalog_items");

            migrationBuilder.DropTable(
                name: "identification_type_catalog_items");

            migrationBuilder.DropTable(
                name: "kinship_catalog_items");

            migrationBuilder.DropTable(
                name: "marital_status_catalog_items");

            migrationBuilder.DropTable(
                name: "municipality_catalog_items");

            migrationBuilder.DropTable(
                name: "profession_catalog_items");

            migrationBuilder.DropTable(
                name: "department_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_training_type_catalog_items__country_active_sort",
                table: "training_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_training_type_catalog_items__country_code",
                table: "training_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_reference_type_catalog_items__country_active_sort",
                table: "reference_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_reference_type_catalog_items__country_code",
                table: "reference_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_language_level_catalog_items__country_active_sort",
                table: "language_level_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_language_level_catalog_items__country_code",
                table: "language_level_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_language_catalog_items__country_active_sort",
                table: "language_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_language_catalog_items__country_code",
                table: "language_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_study_type_catalog_items__country_active_sort",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_study_type_catalog_items__country_code",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_status_catalog_items__country_active_sort",
                table: "education_status_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_status_catalog_items__country_code",
                table: "education_status_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_shift_catalog_items__country_active_sort",
                table: "education_shift_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_shift_catalog_items__country_code",
                table: "education_shift_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_modality_catalog_items__country_active_sort",
                table: "education_modality_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_modality_catalog_items__country_code",
                table: "education_modality_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_career_catalog_items__country_active_sort",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_career_catalog_items__country_code",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_duration_unit_catalog_items__country_active_sort",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_duration_unit_catalog_items__country_code",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_currency_catalog_items__country_active_sort",
                table: "currency_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_currency_catalog_items__country_code",
                table: "currency_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "training_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "training_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "reference_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "reference_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "language_level_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "language_level_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "language_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "language_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "education_status_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "education_status_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "education_shift_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "education_shift_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "education_modality_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "education_modality_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "duration_unit_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "currency_catalog_items");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "currency_catalog_items");

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "training_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "training_type_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "reference_type_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "reference_type_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "language_level_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "language_level_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "language_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "language_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "education_study_type_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "education_status_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "education_shift_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "education_modality_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "education_career_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "duration_unit_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "duration_unit_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "currency_catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "currency_catalog_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "legal_representative_document_type_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_representative_document_type_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personnel_reference_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_reference_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_reference_catalog_items__parent",
                        column: x => x.parent_id,
                        principalTable: "personnel_reference_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "legal_representative_document_type_catalog",
                columns: new[] { "id", "code", "is_active", "name", "normalized_code", "public_id", "sort_order" },
                values: new object[,]
                {
                    { 1L, "NATIONALID", true, "National ID", "NATIONALID", new Guid("fedf12e0-eaf7-5b63-23f2-3dbd66dfca00"), 1 },
                    { 2L, "PASSPORT", true, "Passport", "PASSPORT", new Guid("c3b0ed20-3b2e-e0a3-4a80-4b2677409133"), 2 },
                    { 3L, "TAXID", true, "Tax ID", "TAXID", new Guid("b3f3e7aa-8d30-154d-94eb-eab837c8a1db"), 3 },
                    { 4L, "OTHER", true, "Other", "OTHER", new Guid("8c070836-b6b1-5393-ed1c-648de59d3653"), 4 }
                });

            migrationBuilder.InsertData(
                table: "personnel_reference_catalog_items",
                columns: new[] { "id", "category", "code", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "parent_id", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9612L, "Kinship", "OTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", null, new Guid("f488c015-109f-d1fc-5d91-2270ae305efe"), 100 },
                    { -9611L, "Kinship", "TIO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tio/a", "TIO_A", "TIO/A", null, new Guid("a5cad122-7a87-e36c-7b6e-24530e947161"), 90 },
                    { -9610L, "Kinship", "NIETO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nieto/a", "NIETO_A", "NIETO/A", null, new Guid("3b3b848c-c640-6aac-9f92-a143bd8f0afe"), 80 },
                    { -9609L, "Kinship", "ABUELO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Abuelo/a", "ABUELO_A", "ABUELO/A", null, new Guid("88b3c1c4-44de-f9fc-c681-449829ae8b7e"), 70 },
                    { -9608L, "Kinship", "HERMANO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hermano/a", "HERMANO_A", "HERMANO/A", null, new Guid("a2a08702-b2de-293f-ee57-b0c3b01a45a9"), 60 },
                    { -9607L, "Kinship", "HIJO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hijo/a", "HIJO_A", "HIJO/A", null, new Guid("742bce7c-992a-f4ed-a454-d36cf9dbe362"), 50 },
                    { -9606L, "Kinship", "MADRE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Madre", "MADRE", "MADRE", null, new Guid("a6777218-fbfa-ec8a-32e7-6ff044a6245d"), 40 },
                    { -9605L, "Kinship", "PADRE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Padre", "PADRE", "PADRE", null, new Guid("acb3b1cb-4384-9a9a-46d6-e81ee37d289a"), 30 },
                    { -9604L, "Kinship", "PAREJA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pareja", "PAREJA", "PAREJA", null, new Guid("61b6bf8d-674a-dd8e-1765-1664dc818329"), 20 },
                    { -9603L, "Kinship", "CONYUGE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Conyuge", "CONYUGE", "CONYUGE", null, new Guid("e40d1b19-815a-eccc-7e79-f0334f41f832"), 10 },
                    { -9558L, "Department", "LA_UNION", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Union", "LA_UNION", "LA UNION", null, new Guid("d7b593da-5576-7607-0a5c-2f6f83647650"), 140 },
                    { -9557L, "Department", "MORAZAN", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Morazan", "MORAZAN", "MORAZAN", null, new Guid("26bef224-a0cf-d000-0da6-f9a4dd414dad"), 130 },
                    { -9556L, "Department", "SAN_MIGUEL", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Miguel", "SAN_MIGUEL", "SAN MIGUEL", null, new Guid("875f7799-39da-ae3e-4e00-1f14b19d4502"), 120 },
                    { -9555L, "Department", "USULUTAN", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Usulutan", "USULUTAN", "USULUTAN", null, new Guid("ee60ece2-c729-1491-8eeb-1ce18c25630a"), 110 },
                    { -9554L, "Department", "SAN_VICENTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Vicente", "SAN_VICENTE", "SAN VICENTE", null, new Guid("0fc50ec4-63e3-7530-bafb-57faa3b7f6e6"), 100 },
                    { -9553L, "Department", "CABANAS", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cabanas", "CABANAS", "CABANAS", null, new Guid("6ea44b4e-40d5-af33-0e11-f96be6a26c7d"), 90 },
                    { -9552L, "Department", "LA_PAZ", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Paz", "LA_PAZ", "LA PAZ", null, new Guid("b30fbd9e-4f6c-558f-6bcd-89ac10eb8f03"), 80 },
                    { -9551L, "Department", "CUSCATLAN", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuscatlan", "CUSCATLAN", "CUSCATLAN", null, new Guid("70a87347-c4d0-4b7a-3ee9-8e8b296c7c15"), 70 },
                    { -9550L, "Department", "SAN_SALVADOR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Salvador", "SAN_SALVADOR", "SAN SALVADOR", null, new Guid("469719a6-f18d-9130-4f8f-d5ff4930d901"), 60 },
                    { -9549L, "Department", "LA_LIBERTAD", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad", "LA_LIBERTAD", "LA LIBERTAD", null, new Guid("a571ab57-603a-27bb-c1e1-251be5644ff7"), 50 },
                    { -9548L, "Department", "CHALATENANGO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chalatenango", "CHALATENANGO", "CHALATENANGO", null, new Guid("96b4270c-8cf1-6d8e-ebbe-188483c6ecbd"), 40 },
                    { -9547L, "Department", "SONSONATE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sonsonate", "SONSONATE", "SONSONATE", null, new Guid("a75ff0b3-75ef-23c0-9ab8-972d41a1e721"), 30 },
                    { -9546L, "Department", "SANTA_ANA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Santa Ana", "SANTA_ANA", "SANTA ANA", null, new Guid("3c9a4157-236b-e697-efa5-77f3d866b63a"), 20 },
                    { -9545L, "Department", "AHUACHAPAN", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ahuachapan", "AHUACHAPAN", "AHUACHAPAN", null, new Guid("826f83fe-d9fa-8099-d797-f7e41d76ba21"), 10 },
                    { -9544L, "Profession", "VENDEDOR_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vendedor/a", "VENDEDOR_A", "VENDEDOR/A", null, new Guid("40d6d96e-785b-8061-52ee-b2556803dcd2"), 350 },
                    { -9543L, "Profession", "TECNICO_A_DE_SOPORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tecnico/a de soporte", "TECNICO_A_DE_SOPORTE", "TECNICO/A DE SOPORTE", null, new Guid("31e4df57-4fda-eaa0-5c24-94f27021dfd1"), 340 },
                    { -9542L, "Profession", "TECNICO_A_DE_MANTENIMIENTO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tecnico/a de mantenimiento", "TECNICO_A_DE_MANTENIMIENTO", "TECNICO/A DE MANTENIMIENTO", null, new Guid("61544548-2caf-b073-f2a1-11c7c7b29262"), 330 },
                    { -9541L, "Profession", "SUPERVISOR_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Supervisor/a", "SUPERVISOR_A", "SUPERVISOR/A", null, new Guid("6caf9ea7-4698-47f0-4e94-fd0b83a24c79"), 320 },
                    { -9540L, "Profession", "SOLDADOR_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Soldador/a", "SOLDADOR_A", "SOLDADOR/A", null, new Guid("d80fb0e6-4e76-74f2-41ff-e6cb60bb1a18"), 310 },
                    { -9539L, "Profession", "RECEPCIONISTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Recepcionista", "RECEPCIONISTA", "RECEPCIONISTA", null, new Guid("a34a87d3-739d-84d0-e201-9f00d1fb46c9"), 300 },
                    { -9538L, "Profession", "PSICOLOGO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Psicologo/a", "PSICOLOGO_A", "PSICOLOGO/A", null, new Guid("ff18f8df-8cd0-09ef-48ca-a109e186250b"), 290 },
                    { -9537L, "Profession", "PERIODISTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Periodista", "PERIODISTA", "PERIODISTA", null, new Guid("b924e735-7dcf-5db8-0ce7-31cf25002b95"), 280 },
                    { -9536L, "Profession", "OPERARIO_A_DE_PRODUCCION", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Operario/a de produccion", "OPERARIO_A_DE_PRODUCCION", "OPERARIO/A DE PRODUCCION", null, new Guid("9433d359-de6c-122a-2e42-b123d0a42e8b"), 270 },
                    { -9535L, "Profession", "ODONTOLOGO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Odontologo/a", "ODONTOLOGO_A", "ODONTOLOGO/A", null, new Guid("38ba8d2d-85d9-0bc7-1ed3-f6dfc3897c8f"), 260 },
                    { -9534L, "Profession", "MOTORISTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Motorista", "MOTORISTA", "MOTORISTA", null, new Guid("db459aa8-81ac-6c88-46f0-ab20719d088d"), 250 },
                    { -9533L, "Profession", "MERCADERISTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mercaderista", "MERCADERISTA", "MERCADERISTA", null, new Guid("7a8523f0-8d59-5b96-d693-58e7208f5b73"), 240 },
                    { -9532L, "Profession", "MEDICO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Medico/a", "MEDICO_A", "MEDICO/A", null, new Guid("8b3b629e-f17e-2d79-d4d0-bb561a1836ea"), 230 },
                    { -9531L, "Profession", "JEFE_A_DE_OPERACIONES", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jefe/a de operaciones", "JEFE_A_DE_OPERACIONES", "JEFE/A DE OPERACIONES", null, new Guid("cba81ecb-204c-b122-49d3-34c0c71361ac"), 220 },
                    { -9530L, "Profession", "INGENIERO_A_EN_SISTEMAS", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingeniero/a en sistemas", "INGENIERO_A_EN_SISTEMAS", "INGENIERO/A EN SISTEMAS", null, new Guid("67328e98-d14b-4219-4967-cdfc1a5662b0"), 210 },
                    { -9529L, "Profession", "INGENIERO_A_INDUSTRIAL", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingeniero/a industrial", "INGENIERO_A_INDUSTRIAL", "INGENIERO/A INDUSTRIAL", null, new Guid("05beafa8-2e8a-7644-dc77-9fdff49f4ac6"), 200 },
                    { -9528L, "Profession", "INGENIERO_A_CIVIL", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingeniero/a civil", "INGENIERO_A_CIVIL", "INGENIERO/A CIVIL", null, new Guid("559bc6e5-0579-f940-4e8b-eee269423044"), 190 },
                    { -9527L, "Profession", "INGENIERO_A_AGRONOMO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ingeniero/a agronomo/a", "INGENIERO_A_AGRONOMO_A", "INGENIERO/A AGRONOMO/A", null, new Guid("f75cf926-bb10-071d-2b4e-ee49bae11506"), 180 },
                    { -9526L, "Profession", "ESPECIALISTA_DE_RECURSOS_HUMANOS", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Especialista de recursos humanos", "ESPECIALISTA_DE_RECURSOS_HUMANOS", "ESPECIALISTA DE RECURSOS HUMANOS", null, new Guid("3d941f12-96cf-2a3a-b25e-09e1b50ddb78"), 170 },
                    { -9525L, "Profession", "ENFERMERO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Enfermero/a", "ENFERMERO_A", "ENFERMERO/A", null, new Guid("36ec3f29-06d9-c2ce-a0d4-0452c19c6499"), 160 },
                    { -9524L, "Profession", "ELECTRICISTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Electricista", "ELECTRICISTA", "ELECTRICISTA", null, new Guid("93a617a9-5e5c-b810-39b6-75a2909e60df"), 150 },
                    { -9523L, "Profession", "ECONOMISTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Economista", "ECONOMISTA", "ECONOMISTA", null, new Guid("93a6a950-62c3-0af7-4d59-6e5a2b772263"), 140 },
                    { -9522L, "Profession", "DOCENTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Docente", "DOCENTE", "DOCENTE", null, new Guid("6fe62200-4432-c5f2-6750-444805409ff7"), 130 },
                    { -9521L, "Profession", "DISENADOR_A_GRAFICO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Disenador/a grafico/a", "DISENADOR_A_GRAFICO_A", "DISENADOR/A GRAFICO/A", null, new Guid("cba8af92-8ae9-f437-9f04-2784b8bd561b"), 120 },
                    { -9520L, "Profession", "CONTADOR_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Contador/a", "CONTADOR_A", "CONTADOR/A", null, new Guid("e8ec096c-5682-566f-ccd1-51e129a0c580"), 110 },
                    { -9519L, "Profession", "COMERCIANTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Comerciante", "COMERCIANTE", "COMERCIANTE", null, new Guid("b8c64522-84ee-9d4f-2700-54734539e055"), 100 },
                    { -9518L, "Profession", "CAJERO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cajero/a", "CAJERO_A", "CAJERO/A", null, new Guid("99e979b6-7b08-0e70-4c87-41748c1d4803"), 90 },
                    { -9517L, "Profession", "BODEGUERO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bodeguero/a", "BODEGUERO_A", "BODEGUERO/A", null, new Guid("4fd00fbb-d70e-1090-5d66-1bf6c6067e96"), 80 },
                    { -9516L, "Profession", "AUXILIAR_CONTABLE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Auxiliar contable", "AUXILIAR_CONTABLE", "AUXILIAR CONTABLE", null, new Guid("f6c97782-b2fe-5fd1-4896-321f3271737a"), 70 },
                    { -9515L, "Profession", "AUDITOR_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Auditor/a", "AUDITOR_A", "AUDITOR/A", null, new Guid("42fbda90-675b-ca6c-07b6-d6beadf04747"), 60 },
                    { -9514L, "Profession", "ASISTENTE_ADMINISTRATIVO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asistente administrativo/a", "ASISTENTE_ADMINISTRATIVO_A", "ASISTENTE ADMINISTRATIVO/A", null, new Guid("0578afeb-413d-8811-d838-d9c3ba3064d0"), 50 },
                    { -9513L, "Profession", "ARQUITECTO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Arquitecto/a", "ARQUITECTO_A", "ARQUITECTO/A", null, new Guid("1ce54041-8282-9091-3321-3a9050e7862b"), 40 },
                    { -9512L, "Profession", "ANALISTA_DE_DATOS", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Analista de datos", "ANALISTA_DE_DATOS", "ANALISTA DE DATOS", null, new Guid("0bc1744f-00af-9079-de3d-fa4395c8cbec"), 30 },
                    { -9511L, "Profession", "ADMINISTRADOR_A_DE_EMPRESAS", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Administrador/a de empresas", "ADMINISTRADOR_A_DE_EMPRESAS", "ADMINISTRADOR/A DE EMPRESAS", null, new Guid("f8e5fdb2-e52d-684e-7538-4eb5593f06e3"), 20 },
                    { -9510L, "Profession", "ABOGADO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Abogado/a", "ABOGADO_A", "ABOGADO/A", null, new Guid("9c1895dd-b700-9c96-6f85-5847595f65f8"), 10 },
                    { -9509L, "IdentificationType", "RESIDENT_CARD", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Carne de residente", "RESIDENT_CARD", "CARNE DE RESIDENTE", null, new Guid("7d399d0b-9f72-44d1-f7a5-070b96bd17be"), 40 },
                    { -9508L, "IdentificationType", "PASSPORT", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pasaporte", "PASSPORT", "PASAPORTE", null, new Guid("05e1ebd9-7378-ea5e-6afa-ea2bd713f0f4"), 30 },
                    { -9507L, "IdentificationType", "NIT", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "NIT", "NIT", "NIT", null, new Guid("c24010b9-7d34-3194-6cac-4bccf3b0d24c"), 20 },
                    { -9506L, "IdentificationType", "DUI", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "DUI", "DUI", "DUI", null, new Guid("3d4d6c2f-d567-ec68-7afd-bd8f04ece139"), 10 },
                    { -9505L, "MaritalStatus", "SEPARADO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Separado/a", "SEPARADO_A", "SEPARADO/A", null, new Guid("553b23af-346a-3d41-56b9-10531c1204d6"), 60 },
                    { -9504L, "MaritalStatus", "VIUDO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Viudo/a", "VIUDO_A", "VIUDO/A", null, new Guid("f77da6b7-5d7d-92dc-a5e9-4135a14779ac"), 50 },
                    { -9503L, "MaritalStatus", "DIVORCIADO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Divorciado/a", "DIVORCIADO_A", "DIVORCIADO/A", null, new Guid("2409243e-7520-d5ef-b4ee-eafd7c5d9747"), 40 },
                    { -9502L, "MaritalStatus", "UNION_NO_MATRIMONIAL", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Union no matrimonial", "UNION_NO_MATRIMONIAL", "UNION NO MATRIMONIAL", null, new Guid("7f50d72f-91c0-f1f5-fa9a-05c99e31f1bb"), 30 },
                    { -9501L, "MaritalStatus", "CASADO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Casado/a", "CASADO_A", "CASADO/A", null, new Guid("770efe49-7531-d13a-ea8d-b5a339318045"), 20 },
                    { -9500L, "MaritalStatus", "SOLTERO_A", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Soltero/a", "SOLTERO_A", "SOLTERO/A", null, new Guid("1786bfa7-2ca7-0643-8b26-9a0983421eac"), 10 },
                    { -9602L, "Municipality", "LA_UNION_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Union Sur", "LA_UNION_SUR", "LA UNION SUR", -9558L, new Guid("afbd8039-2edb-5ff7-26a3-d62fdf9e66dc"), 440 },
                    { -9601L, "Municipality", "LA_UNION_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Union Norte", "LA_UNION_NORTE", "LA UNION NORTE", -9558L, new Guid("fe0b1dda-ade1-e230-ee89-fcf88ecb508c"), 430 },
                    { -9600L, "Municipality", "MORAZAN_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Morazan Sur", "MORAZAN_SUR", "MORAZAN SUR", -9557L, new Guid("f374ea70-a6d9-4153-f6bf-aa8cb3139d85"), 420 },
                    { -9599L, "Municipality", "MORAZAN_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Morazan Norte", "MORAZAN_NORTE", "MORAZAN NORTE", -9557L, new Guid("dd429a5b-7c70-6fd9-ac08-645afc424ac8"), 410 },
                    { -9598L, "Municipality", "SAN_MIGUEL_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Miguel Oeste", "SAN_MIGUEL_OESTE", "SAN MIGUEL OESTE", -9556L, new Guid("56c60522-63e7-1481-bdbe-81cc44416f7d"), 400 },
                    { -9597L, "Municipality", "SAN_MIGUEL_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Miguel Norte", "SAN_MIGUEL_NORTE", "SAN MIGUEL NORTE", -9556L, new Guid("6d53dc05-15c4-e632-1d83-da15d12782b6"), 390 },
                    { -9596L, "Municipality", "SAN_MIGUEL_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Miguel Centro", "SAN_MIGUEL_CENTRO", "SAN MIGUEL CENTRO", -9556L, new Guid("65572591-c834-c010-e652-5b141933677d"), 380 },
                    { -9595L, "Municipality", "USULUTAN_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Usulutan Oeste", "USULUTAN_OESTE", "USULUTAN OESTE", -9555L, new Guid("af6e6ba9-6412-8641-1f5f-6c41f58af49d"), 370 },
                    { -9594L, "Municipality", "USULUTAN_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Usulutan Norte", "USULUTAN_NORTE", "USULUTAN NORTE", -9555L, new Guid("2b9fae9b-b0b9-c0d3-4c72-092c21fbb0e8"), 360 },
                    { -9593L, "Municipality", "USULUTAN_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Usulutan Este", "USULUTAN_ESTE", "USULUTAN ESTE", -9555L, new Guid("f37dd9f1-132c-309e-b867-879ccddddfe5"), 350 },
                    { -9592L, "Municipality", "SAN_VICENTE_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Vicente Sur", "SAN_VICENTE_SUR", "SAN VICENTE SUR", -9554L, new Guid("82433873-97a1-63e4-a561-c8ec0fbfd8be"), 340 },
                    { -9591L, "Municipality", "SAN_VICENTE_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Vicente Norte", "SAN_VICENTE_NORTE", "SAN VICENTE NORTE", -9554L, new Guid("f0bc6e25-e812-c633-0655-e940a10379d7"), 330 },
                    { -9590L, "Municipality", "CABANAS_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cabanas Oeste", "CABANAS_OESTE", "CABANAS OESTE", -9553L, new Guid("64c10baa-c354-fdef-2531-87350b4c20e6"), 320 },
                    { -9589L, "Municipality", "CABANAS_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cabanas Este", "CABANAS_ESTE", "CABANAS ESTE", -9553L, new Guid("34928e6b-b58b-7668-2ae0-75c497d36135"), 310 },
                    { -9588L, "Municipality", "LA_PAZ_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Paz Oeste", "LA_PAZ_OESTE", "LA PAZ OESTE", -9552L, new Guid("5ea16ce2-3e7b-185c-d051-00252130b70d"), 300 },
                    { -9587L, "Municipality", "LA_PAZ_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Paz Este", "LA_PAZ_ESTE", "LA PAZ ESTE", -9552L, new Guid("254fa9e4-050e-20af-35da-46123574b3a4"), 290 },
                    { -9586L, "Municipality", "LA_PAZ_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Paz Centro", "LA_PAZ_CENTRO", "LA PAZ CENTRO", -9552L, new Guid("55968795-3f06-e37f-c742-ca4cbd0c3161"), 280 },
                    { -9585L, "Municipality", "CUSCATLAN_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuscatlan Sur", "CUSCATLAN_SUR", "CUSCATLAN SUR", -9551L, new Guid("12172ffa-fbab-ec24-dab5-db7ea5017724"), 270 },
                    { -9584L, "Municipality", "CUSCATLAN_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuscatlan Norte", "CUSCATLAN_NORTE", "CUSCATLAN NORTE", -9551L, new Guid("935a0a70-2f52-2e82-fa7b-1352b1329cee"), 260 },
                    { -9583L, "Municipality", "SAN_SALVADOR_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Salvador Sur", "SAN_SALVADOR_SUR", "SAN SALVADOR SUR", -9550L, new Guid("4f6ea9f2-b97e-d956-37f9-db1d196bb2bc"), 250 },
                    { -9582L, "Municipality", "SAN_SALVADOR_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Salvador Oeste", "SAN_SALVADOR_OESTE", "SAN SALVADOR OESTE", -9550L, new Guid("aa9585e6-00cb-d4d7-2179-42ea67731f68"), 240 },
                    { -9581L, "Municipality", "SAN_SALVADOR_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Salvador Norte", "SAN_SALVADOR_NORTE", "SAN SALVADOR NORTE", -9550L, new Guid("d9077bc3-2bd8-2a6d-9bb2-d51ae4389073"), 230 },
                    { -9580L, "Municipality", "SAN_SALVADOR_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Salvador Este", "SAN_SALVADOR_ESTE", "SAN SALVADOR ESTE", -9550L, new Guid("fc5949f4-fd33-84b5-632e-9071f11516f5"), 220 },
                    { -9579L, "Municipality", "SAN_SALVADOR_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Salvador Centro", "SAN_SALVADOR_CENTRO", "SAN SALVADOR CENTRO", -9550L, new Guid("22ff05df-546a-3b6e-9bc5-3cc187c58bcf"), 210 },
                    { -9578L, "Municipality", "LA_LIBERTAD_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad Oeste", "LA_LIBERTAD_OESTE", "LA LIBERTAD OESTE", -9549L, new Guid("21596f6b-c061-9b3e-7022-a6af0072a2f6"), 200 },
                    { -9577L, "Municipality", "LA_LIBERTAD_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad Sur", "LA_LIBERTAD_SUR", "LA LIBERTAD SUR", -9549L, new Guid("377187d3-439a-26e6-8f6a-9f780a92d9a8"), 190 },
                    { -9576L, "Municipality", "LA_LIBERTAD_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad Norte", "LA_LIBERTAD_NORTE", "LA LIBERTAD NORTE", -9549L, new Guid("47b9b87e-43cc-fe85-a9ab-a89c91b04492"), 180 },
                    { -9575L, "Municipality", "LA_LIBERTAD_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad Este", "LA_LIBERTAD_ESTE", "LA LIBERTAD ESTE", -9549L, new Guid("86109d7a-d637-8e6f-5586-69a1f29ac3b1"), 170 },
                    { -9574L, "Municipality", "LA_LIBERTAD_COSTA", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad Costa", "LA_LIBERTAD_COSTA", "LA LIBERTAD COSTA", -9549L, new Guid("f88ed89a-7b1b-cc8f-93d8-868786567d3b"), 160 },
                    { -9573L, "Municipality", "LA_LIBERTAD_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "La Libertad Centro", "LA_LIBERTAD_CENTRO", "LA LIBERTAD CENTRO", -9549L, new Guid("0bd5b137-5fd3-2887-9c19-fadeb9d4a8ad"), 150 },
                    { -9572L, "Municipality", "CHALATENANGO_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chalatenango Sur", "CHALATENANGO_SUR", "CHALATENANGO SUR", -9548L, new Guid("91295538-d3c6-3645-cfe5-710f958dece9"), 140 },
                    { -9571L, "Municipality", "CHALATENANGO_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chalatenango Norte", "CHALATENANGO_NORTE", "CHALATENANGO NORTE", -9548L, new Guid("3dcb46ae-8fe2-66e8-ccde-19036a1afd46"), 130 },
                    { -9570L, "Municipality", "CHALATENANGO_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chalatenango Centro", "CHALATENANGO_CENTRO", "CHALATENANGO CENTRO", -9548L, new Guid("297defd0-137d-2b16-f424-ce1625f75975"), 120 },
                    { -9569L, "Municipality", "SONSONATE_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sonsonate Oeste", "SONSONATE_OESTE", "SONSONATE OESTE", -9547L, new Guid("4035134f-f0a2-c914-1de8-494161a86665"), 110 },
                    { -9568L, "Municipality", "SONSONATE_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sonsonate Norte", "SONSONATE_NORTE", "SONSONATE NORTE", -9547L, new Guid("1f325e3b-5118-bb0d-3283-e7cde310d59a"), 100 },
                    { -9567L, "Municipality", "SONSONATE_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sonsonate Este", "SONSONATE_ESTE", "SONSONATE ESTE", -9547L, new Guid("10462cda-cb6e-79e0-df9d-90216d15ff3f"), 90 },
                    { -9566L, "Municipality", "SONSONATE_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sonsonate Centro", "SONSONATE_CENTRO", "SONSONATE CENTRO", -9547L, new Guid("fa3396e0-4cc7-c845-038c-38ab7990496e"), 80 },
                    { -9565L, "Municipality", "SANTA_ANA_OESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Santa Ana Oeste", "SANTA_ANA_OESTE", "SANTA ANA OESTE", -9546L, new Guid("fc7b8586-5f6d-c6c6-09ef-51c105c5df12"), 70 },
                    { -9564L, "Municipality", "SANTA_ANA_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Santa Ana Norte", "SANTA_ANA_NORTE", "SANTA ANA NORTE", -9546L, new Guid("646012c2-b7fc-e605-99fb-1a6b1f684ca9"), 60 },
                    { -9563L, "Municipality", "SANTA_ANA_ESTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Santa Ana Este", "SANTA_ANA_ESTE", "SANTA ANA ESTE", -9546L, new Guid("ac6d4692-a2c6-4aa4-5dff-f8666d58cf49"), 50 },
                    { -9562L, "Municipality", "SANTA_ANA_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Santa Ana Centro", "SANTA_ANA_CENTRO", "SANTA ANA CENTRO", -9546L, new Guid("474ca046-1414-ba2d-287b-8d71b2789950"), 40 },
                    { -9561L, "Municipality", "AHUACHAPAN_SUR", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ahuachapan Sur", "AHUACHAPAN_SUR", "AHUACHAPAN SUR", -9545L, new Guid("7886e88f-35d2-9060-2da9-1f974c98abb0"), 30 },
                    { -9560L, "Municipality", "AHUACHAPAN_NORTE", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ahuachapan Norte", "AHUACHAPAN_NORTE", "AHUACHAPAN NORTE", -9545L, new Guid("0c70ff03-4303-7aff-8ad0-b5a6a133cafe"), 20 },
                    { -9559L, "Municipality", "AHUACHAPAN_CENTRO", "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ahuachapan Centro", "AHUACHAPAN_CENTRO", "AHUACHAPAN CENTRO", -9545L, new Guid("5098d63f-ff11-7438-ef22-ff4cc73faaad"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_training_type_catalog_items__tenant_active_sort",
                table: "training_type_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_training_type_catalog_items__tenant_code",
                table: "training_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_type_catalog_items__tenant_active_sort",
                table: "reference_type_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_reference_type_catalog_items__tenant_code",
                table: "reference_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_level_catalog_items__tenant_active_sort",
                table: "language_level_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_language_level_catalog_items__tenant_code",
                table: "language_level_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_catalog_items__tenant_active_sort",
                table: "language_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_language_catalog_items__tenant_code",
                table: "language_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_study_type_catalog_items__tenant_active_sort",
                table: "education_study_type_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_study_type_catalog_items__tenant_code",
                table: "education_study_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_status_catalog_items__tenant_active_sort",
                table: "education_status_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_status_catalog_items__tenant_code",
                table: "education_status_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_shift_catalog_items__tenant_active_sort",
                table: "education_shift_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_shift_catalog_items__tenant_code",
                table: "education_shift_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_modality_catalog_items__tenant_active_sort",
                table: "education_modality_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_modality_catalog_items__tenant_code",
                table: "education_modality_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_career_catalog_items__tenant_active_sort",
                table: "education_career_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__tenant_code",
                table: "education_career_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_duration_unit_catalog_items__tenant_active_sort",
                table: "duration_unit_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_duration_unit_catalog_items__tenant_code",
                table: "duration_unit_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currency_catalog_items__tenant_active_sort",
                table: "currency_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_currency_catalog_items__tenant_code",
                table: "currency_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_document_type_catalog__normalized_code",
                table: "legal_representative_document_type_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_document_type_catalog__public_id",
                table: "legal_representative_document_type_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_reference_catalog_items__country_category_parent_active_sort",
                table: "personnel_reference_catalog_items",
                columns: new[] { "country_code", "category", "parent_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_reference_catalog_items_parent_id",
                table: "personnel_reference_catalog_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_reference_catalog_items__country_category_code",
                table: "personnel_reference_catalog_items",
                columns: new[] { "country_code", "category", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_reference_catalog_items__public_id",
                table: "personnel_reference_catalog_items",
                column: "public_id",
                unique: true);
        }
    }
}
