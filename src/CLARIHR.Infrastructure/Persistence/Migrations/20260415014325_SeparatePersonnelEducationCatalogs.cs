using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePersonnelEducationCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "education_career_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "education_modality_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "education_shift_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "education_status_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "education_study_type_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "education_career_catalog_items",
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
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education_career_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "education_modality_catalog_items",
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
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education_modality_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "education_shift_catalog_items",
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
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education_shift_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "education_status_catalog_items",
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
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education_status_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "education_study_type_catalog_items",
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
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education_study_type_catalog_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_educations_education_career_catalog_item_id",
                table: "personnel_file_educations",
                column: "education_career_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_educations_education_modality_catalog_item_id",
                table: "personnel_file_educations",
                column: "education_modality_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_educations_education_shift_catalog_item_id",
                table: "personnel_file_educations",
                column: "education_shift_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_educations_education_status_catalog_item_id",
                table: "personnel_file_educations",
                column: "education_status_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_educations_education_study_type_catalog_item~",
                table: "personnel_file_educations",
                column: "education_study_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_education_career_catalog_items__tenant_active_sort",
                table: "education_career_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__public_id",
                table: "education_career_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__tenant_code",
                table: "education_career_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_modality_catalog_items__tenant_active_sort",
                table: "education_modality_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_modality_catalog_items__public_id",
                table: "education_modality_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_education_modality_catalog_items__tenant_code",
                table: "education_modality_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_shift_catalog_items__tenant_active_sort",
                table: "education_shift_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_shift_catalog_items__public_id",
                table: "education_shift_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_education_shift_catalog_items__tenant_code",
                table: "education_shift_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_status_catalog_items__tenant_active_sort",
                table: "education_status_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_status_catalog_items__public_id",
                table: "education_status_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_education_status_catalog_items__tenant_code",
                table: "education_status_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_study_type_catalog_items__tenant_active_sort",
                table: "education_study_type_catalog_items",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_study_type_catalog_items__public_id",
                table: "education_study_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_education_study_type_catalog_items__tenant_code",
                table: "education_study_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO education_status_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
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
                    sort_order,
                    is_active,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumEducationStatus'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO education_study_type_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
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
                    sort_order,
                    is_active,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumStudyType'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO education_shift_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
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
                    sort_order,
                    is_active,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumShift'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO education_modality_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
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
                    sort_order,
                    is_active,
                    concurrency_token
                FROM personnel_catalog_items
                WHERE category = 'CurriculumModality'
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                WITH distinct_careers AS (
                    SELECT DISTINCT
                        pfe.tenant_id,
                        LEFT(BTRIM(pfe.career), 200) AS career_name,
                        UPPER(LEFT(BTRIM(pfe.career), 200)) AS normalized_name
                    FROM personnel_file_educations pfe
                    WHERE pfe.career IS NOT NULL
                      AND BTRIM(pfe.career) <> ''
                ),
                ranked AS (
                    SELECT
                        dc.tenant_id,
                        dc.career_name,
                        dc.normalized_name,
                        ROW_NUMBER() OVER (
                            PARTITION BY dc.tenant_id
                            ORDER BY dc.normalized_name, dc.career_name) AS rn
                    FROM distinct_careers dc
                ),
                prepared AS (
                    SELECT
                        r.tenant_id,
                        r.career_name,
                        r.normalized_name,
                        LEFT('CAREER_' || LPAD(r.rn::text, 4, '0'), 80) AS code,
                        UPPER(LEFT('CAREER_' || LPAD(r.rn::text, 4, '0'), 80)) AS normalized_code,
                        r.rn AS sort_order,
                        md5('education-career:' || r.tenant_id::text || ':' || r.normalized_name) AS hash
                    FROM ranked r
                )
                INSERT INTO education_career_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
                    concurrency_token)
                SELECT
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'a' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid,
                    CURRENT_TIMESTAMP,
                    NULL,
                    p.tenant_id,
                    p.code,
                    p.normalized_code,
                    p.career_name,
                    p.normalized_name,
                    p.sort_order,
                    TRUE,
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'b' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid
                FROM prepared p
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                WITH missing AS (
                    SELECT DISTINCT
                        pfe.tenant_id,
                        LEFT(BTRIM(pfe.status_code), 80) AS code,
                        UPPER(LEFT(BTRIM(pfe.status_code), 80)) AS normalized_code,
                        LEFT(BTRIM(pfe.status_code), 200) AS name,
                        UPPER(LEFT(BTRIM(pfe.status_code), 200)) AS normalized_name
                    FROM personnel_file_educations pfe
                    LEFT JOIN education_status_catalog_items esc
                        ON esc.tenant_id = pfe.tenant_id
                       AND esc.normalized_code = UPPER(BTRIM(pfe.status_code))
                    WHERE pfe.status_code IS NOT NULL
                      AND BTRIM(pfe.status_code) <> ''
                      AND esc.id IS NULL
                ),
                prepared AS (
                    SELECT
                        m.*,
                        md5('education-status:' || m.tenant_id::text || ':' || m.normalized_code) AS hash
                    FROM missing m
                )
                INSERT INTO education_status_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
                    concurrency_token)
                SELECT
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'a' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid,
                    CURRENT_TIMESTAMP,
                    NULL,
                    p.tenant_id,
                    p.code,
                    p.normalized_code,
                    p.name,
                    p.normalized_name,
                    9999,
                    FALSE,
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'b' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid
                FROM prepared p
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                WITH missing AS (
                    SELECT DISTINCT
                        pfe.tenant_id,
                        LEFT(BTRIM(pfe.study_type_code), 80) AS code,
                        UPPER(LEFT(BTRIM(pfe.study_type_code), 80)) AS normalized_code,
                        LEFT(BTRIM(pfe.study_type_code), 200) AS name,
                        UPPER(LEFT(BTRIM(pfe.study_type_code), 200)) AS normalized_name
                    FROM personnel_file_educations pfe
                    LEFT JOIN education_study_type_catalog_items est
                        ON est.tenant_id = pfe.tenant_id
                       AND est.normalized_code = UPPER(BTRIM(pfe.study_type_code))
                    WHERE pfe.study_type_code IS NOT NULL
                      AND BTRIM(pfe.study_type_code) <> ''
                      AND est.id IS NULL
                ),
                prepared AS (
                    SELECT
                        m.*,
                        md5('education-study-type:' || m.tenant_id::text || ':' || m.normalized_code) AS hash
                    FROM missing m
                )
                INSERT INTO education_study_type_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
                    concurrency_token)
                SELECT
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'a' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid,
                    CURRENT_TIMESTAMP,
                    NULL,
                    p.tenant_id,
                    p.code,
                    p.normalized_code,
                    p.name,
                    p.normalized_name,
                    9999,
                    FALSE,
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'b' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid
                FROM prepared p
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                WITH missing AS (
                    SELECT DISTINCT
                        pfe.tenant_id,
                        LEFT(BTRIM(pfe.shift_code), 80) AS code,
                        UPPER(LEFT(BTRIM(pfe.shift_code), 80)) AS normalized_code,
                        LEFT(BTRIM(pfe.shift_code), 200) AS name,
                        UPPER(LEFT(BTRIM(pfe.shift_code), 200)) AS normalized_name
                    FROM personnel_file_educations pfe
                    LEFT JOIN education_shift_catalog_items esh
                        ON esh.tenant_id = pfe.tenant_id
                       AND esh.normalized_code = UPPER(BTRIM(pfe.shift_code))
                    WHERE pfe.shift_code IS NOT NULL
                      AND BTRIM(pfe.shift_code) <> ''
                      AND esh.id IS NULL
                ),
                prepared AS (
                    SELECT
                        m.*,
                        md5('education-shift:' || m.tenant_id::text || ':' || m.normalized_code) AS hash
                    FROM missing m
                )
                INSERT INTO education_shift_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
                    concurrency_token)
                SELECT
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'a' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid,
                    CURRENT_TIMESTAMP,
                    NULL,
                    p.tenant_id,
                    p.code,
                    p.normalized_code,
                    p.name,
                    p.normalized_name,
                    9999,
                    FALSE,
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'b' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid
                FROM prepared p
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                WITH missing AS (
                    SELECT DISTINCT
                        pfe.tenant_id,
                        LEFT(BTRIM(pfe.modality_code), 80) AS code,
                        UPPER(LEFT(BTRIM(pfe.modality_code), 80)) AS normalized_code,
                        LEFT(BTRIM(pfe.modality_code), 200) AS name,
                        UPPER(LEFT(BTRIM(pfe.modality_code), 200)) AS normalized_name
                    FROM personnel_file_educations pfe
                    LEFT JOIN education_modality_catalog_items emo
                        ON emo.tenant_id = pfe.tenant_id
                       AND emo.normalized_code = UPPER(BTRIM(pfe.modality_code))
                    WHERE pfe.modality_code IS NOT NULL
                      AND BTRIM(pfe.modality_code) <> ''
                      AND emo.id IS NULL
                ),
                prepared AS (
                    SELECT
                        m.*,
                        md5('education-modality:' || m.tenant_id::text || ':' || m.normalized_code) AS hash
                    FROM missing m
                )
                INSERT INTO education_modality_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
                    concurrency_token)
                SELECT
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'a' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid,
                    CURRENT_TIMESTAMP,
                    NULL,
                    p.tenant_id,
                    p.code,
                    p.normalized_code,
                    p.name,
                    p.normalized_name,
                    9999,
                    FALSE,
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'b' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid
                FROM prepared p
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                UPDATE personnel_file_educations pfe
                SET education_status_catalog_item_id = esc.id
                FROM education_status_catalog_items esc
                WHERE esc.tenant_id = pfe.tenant_id
                  AND esc.normalized_code = UPPER(BTRIM(pfe.status_code));
                """);

            migrationBuilder.Sql(
                """
                UPDATE personnel_file_educations pfe
                SET education_study_type_catalog_item_id = est.id
                FROM education_study_type_catalog_items est
                WHERE est.tenant_id = pfe.tenant_id
                  AND est.normalized_code = UPPER(BTRIM(pfe.study_type_code));
                """);

            migrationBuilder.Sql(
                """
                UPDATE personnel_file_educations pfe
                SET education_shift_catalog_item_id = esh.id
                FROM education_shift_catalog_items esh
                WHERE pfe.shift_code IS NOT NULL
                  AND BTRIM(pfe.shift_code) <> ''
                  AND esh.tenant_id = pfe.tenant_id
                  AND esh.normalized_code = UPPER(BTRIM(pfe.shift_code));
                """);

            migrationBuilder.Sql(
                """
                UPDATE personnel_file_educations pfe
                SET education_modality_catalog_item_id = emo.id
                FROM education_modality_catalog_items emo
                WHERE pfe.modality_code IS NOT NULL
                  AND BTRIM(pfe.modality_code) <> ''
                  AND emo.tenant_id = pfe.tenant_id
                  AND emo.normalized_code = UPPER(BTRIM(pfe.modality_code));
                """);

            migrationBuilder.Sql(
                """
                UPDATE personnel_file_educations pfe
                SET education_career_catalog_item_id = ec.id
                FROM education_career_catalog_items ec
                WHERE ec.tenant_id = pfe.tenant_id
                  AND ec.normalized_name = UPPER(LEFT(BTRIM(pfe.career), 200));
                """);

            migrationBuilder.Sql(
                """
                WITH tenants AS (
                    SELECT DISTINCT pfe.tenant_id
                    FROM personnel_file_educations pfe
                    WHERE pfe.education_career_catalog_item_id IS NULL
                ),
                prepared AS (
                    SELECT
                        t.tenant_id,
                        md5('education-career-fallback:' || t.tenant_id::text) AS hash
                    FROM tenants t
                )
                INSERT INTO education_career_catalog_items (
                    public_id,
                    created_utc,
                    modified_utc,
                    tenant_id,
                    code,
                    normalized_code,
                    name,
                    normalized_name,
                    sort_order,
                    is_active,
                    concurrency_token)
                SELECT
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'a' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid,
                    CURRENT_TIMESTAMP,
                    NULL,
                    p.tenant_id,
                    'LEGACY_UNKNOWN',
                    'LEGACY_UNKNOWN',
                    'Legacy Unknown',
                    'LEGACY UNKNOWN',
                    999999,
                    FALSE,
                    (
                        substr(p.hash, 1, 8) || '-' ||
                        substr(p.hash, 9, 4) || '-' ||
                        '4' || substr(p.hash, 14, 3) || '-' ||
                        'b' || substr(p.hash, 18, 3) || '-' ||
                        substr(p.hash, 21, 12)
                    )::uuid
                FROM prepared p
                ON CONFLICT (tenant_id, normalized_code) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                UPDATE personnel_file_educations pfe
                SET education_career_catalog_item_id = ec.id
                FROM education_career_catalog_items ec
                WHERE pfe.education_career_catalog_item_id IS NULL
                  AND ec.tenant_id = pfe.tenant_id
                  AND ec.normalized_code = 'LEGACY_UNKNOWN';
                """);

            migrationBuilder.AlterColumn<long>(
                name: "education_status_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "education_study_type_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "education_career_catalog_item_id",
                table: "personnel_file_educations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_educations__education_career_catalog_item",
                table: "personnel_file_educations",
                column: "education_career_catalog_item_id",
                principalTable: "education_career_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_educations__education_modality_catalog_item",
                table: "personnel_file_educations",
                column: "education_modality_catalog_item_id",
                principalTable: "education_modality_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_educations__education_shift_catalog_item",
                table: "personnel_file_educations",
                column: "education_shift_catalog_item_id",
                principalTable: "education_shift_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_educations__education_status_catalog_item",
                table: "personnel_file_educations",
                column: "education_status_catalog_item_id",
                principalTable: "education_status_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_educations__education_study_type_catalog_item",
                table: "personnel_file_educations",
                column: "education_study_type_catalog_item_id",
                principalTable: "education_study_type_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "career",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "modality_code",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "shift_code",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "status_code",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "study_type_code",
                table: "personnel_file_educations");

            migrationBuilder.Sql(
                """
                DELETE FROM personnel_catalog_items
                WHERE category IN (
                    'CurriculumEducationStatus',
                    'CurriculumStudyType',
                    'CurriculumShift',
                    'CurriculumModality');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_educations__education_career_catalog_item",
                table: "personnel_file_educations");

            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_educations__education_modality_catalog_item",
                table: "personnel_file_educations");

            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_educations__education_shift_catalog_item",
                table: "personnel_file_educations");

            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_educations__education_status_catalog_item",
                table: "personnel_file_educations");

            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_educations__education_study_type_catalog_item",
                table: "personnel_file_educations");

            migrationBuilder.DropTable(
                name: "education_career_catalog_items");

            migrationBuilder.DropTable(
                name: "education_modality_catalog_items");

            migrationBuilder.DropTable(
                name: "education_shift_catalog_items");

            migrationBuilder.DropTable(
                name: "education_status_catalog_items");

            migrationBuilder.DropTable(
                name: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_educations_education_career_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_educations_education_modality_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_educations_education_shift_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_educations_education_status_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_educations_education_study_type_catalog_item~",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "education_career_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "education_modality_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "education_shift_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "education_status_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.DropColumn(
                name: "education_study_type_catalog_item_id",
                table: "personnel_file_educations");

            migrationBuilder.AddColumn<string>(
                name: "career",
                table: "personnel_file_educations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "modality_code",
                table: "personnel_file_educations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shift_code",
                table: "personnel_file_educations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_code",
                table: "personnel_file_educations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "study_type_code",
                table: "personnel_file_educations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");
        }
    }
}
