using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    auth_provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_type_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_centers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payroll_expense_account_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    employer_contribution_account_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provision_account_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cost_centers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "field_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    resource_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_resource_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    property_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_property_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_configurable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    data_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "field_permission_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: false),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_permission_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "functional_area_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_functional_area_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iam_permissions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    screen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_screen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    normalized_action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    normalized_field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    field_access = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iam_roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iam_users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_representative_document_type_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_representative_document_type_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_representative_position_title_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_representative_position_title_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_representative_representation_type_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_representative_representation_type_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_representatives",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    normalized_full_name = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    document_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_document_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    position_title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    representation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    authority_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    appointment_instrument = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    appointment_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_representatives", x => x.id);
                    table.CheckConstraint("ck_legal_representatives__effective_dates", "effective_to_utc is null or effective_to_utc >= effective_from_utc");
                });

            migrationBuilder.CreateTable(
                name: "location_groups",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_groups__parent",
                        column: x => x.parent_id,
                        principalTable: "location_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_hierarchy_configs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_multi_level = table.Column<bool>(type: "boolean", nullable: false),
                    default_group_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    default_group_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_hierarchy_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location_levels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    allows_work_centers = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "occupational_pyramid_levels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_occupational_pyramid_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_unit_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_unit_type_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personnel_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_custom_field_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    field_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_custom_field_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personnel_files",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    normalized_full_name = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    birth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marital_status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    profession = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    nationality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    personal_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    institutional_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    personal_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    institutional_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    birth_country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    birth_department = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    birth_municipality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    org_unit_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_data = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_entitlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    module_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_entitlements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "position_description_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_description_catalog_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rbac_permission_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_resource_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: false),
                    after_json = table.Column<string>(type: "jsonb", nullable: false),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rbac_permission_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rbac_resource_catalog",
                columns: table => new
                {
                    resource_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_resource_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rbac_resource_catalog", x => x.resource_key);
                });

            migrationBuilder.CreateTable(
                name: "role_field_permissions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_editable = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_masked = table.Column<bool>(type: "boolean", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_field_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_tabulator_change_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salary_tabulator_change_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_tabulator_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_class_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_salary_class_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    salary_scale_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_salary_scale_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    min_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    max_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salary_tabulator_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_center_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    requires_address = table.Column<bool>(type: "boolean", nullable: false),
                    requires_geo = table.Column<bool>(type: "boolean", nullable: false),
                    allows_biometric = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_center_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_refresh_tokens__auth_users",
                        column: x => x.user_id,
                        principalTable: "auth_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                    table.ForeignKey(
                        name: "fk_companies__company_type_catalog_item",
                        column: x => x.company_type_catalog_item_id,
                        principalTable: "company_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "iam_role_permission_assignments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_role_permission_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_iam_role_permission_assignments_iam_permissions_permission_~",
                        column: x => x.permission_id,
                        principalTable: "iam_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iam_role_permission_assignments_iam_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "iam_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iam_user_role_assignments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_user_role_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_iam_user_role_assignments_iam_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "iam_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iam_user_role_assignments_iam_users_user_id",
                        column: x => x.user_id,
                        principalTable: "iam_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "competency_conducts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    behavior_level_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    normalized_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_conducts", x => x.id);
                    table.ForeignKey(
                        name: "fk_competency_conducts__behavior_level_catalog_item",
                        column: x => x.behavior_level_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_competency_conducts__competency_catalog_item",
                        column: x => x.competency_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_competency_conducts__competency_type_catalog_item",
                        column: x => x.competency_type_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_units",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    org_unit_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    functional_area_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cost_center_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    manager_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_units__functional_area_catalog_item",
                        column: x => x.functional_area_catalog_item_id,
                        principalTable: "functional_area_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_units__org_unit_type_catalog_item",
                        column: x => x.org_unit_type_catalog_item_id,
                        principalTable: "org_unit_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_units__parent",
                        column: x => x.parent_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_additional_benefits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    benefit_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_additional_benefits", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_additional_benefits__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_addresses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    department = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    municipality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_addresses__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_assets_accesses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    asset_or_access_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    access_level_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_assets_accesses", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_assets_accesses__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_associations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    association_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    joined_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    left_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_associations", x => x.id);
                    table.CheckConstraint("ck_personnel_file_associations__joined_left", "left_date is null or joined_date is null or left_date >= joined_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_associations__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_authorization_substitutions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitution_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    substitute_personnel_file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_position_title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_authorization_substitutions", x => x.id);
                    table.CheckConstraint("ck_personnel_file_authorization_substitutions__dates", "end_date is null or end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_authorization_substitutions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_bank_accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    account_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_account_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    account_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_bank_accounts__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_contract_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    contract_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    contract_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position_slot_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_contract_histories", x => x.id);
                    table.CheckConstraint("ck_personnel_file_contract_histories__dates", "contract_end_date is null or contract_end_date >= contract_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_contract_histories__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_curricular_competencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    requirement_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    competency_domain = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    experience_time_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    metric_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_curricular_competencies", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_curricular_competencies__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    loan_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    return_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_documents", x => x.id);
                    table.CheckConstraint("ck_personnel_file_documents__loan_return", "return_date is null or loan_date is null or return_date >= loan_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_documents__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_educations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    degree_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    study_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    career = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_currently_studying = table.Column<bool>(type: "boolean", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shift_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    modality_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    total_subjects = table.Column<int>(type: "integer", nullable: true),
                    approved_subjects = table.Column<int>(type: "integer", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_educations", x => x.id);
                    table.CheckConstraint("ck_personnel_file_educations__active_end_date", "is_currently_studying = true or end_date is not null");
                    table.CheckConstraint("ck_personnel_file_educations__approved_subjects_non_negative", "approved_subjects is null or approved_subjects >= 0");
                    table.CheckConstraint("ck_personnel_file_educations__approved_subjects_range", "total_subjects is null or approved_subjects is null or approved_subjects <= total_subjects");
                    table.CheckConstraint("ck_personnel_file_educations__dates", "end_date is null or end_date >= start_date");
                    table.CheckConstraint("ck_personnel_file_educations__total_subjects_non_negative", "total_subjects is null or total_subjects >= 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_educations__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_emergency_contacts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    relationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_emergency_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_emergency_contacts__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_employee_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_employee_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    employment_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_employment_active = table.Column<bool>(type: "boolean", nullable: false),
                    contract_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    hire_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retirement_category_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    retirement_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    retirement_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    retirement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    workday_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    payroll_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    position_slot_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_profile_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    org_unit_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_center_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_center_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contract_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    contract_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    vacation_configuration_json = table.Column<string>(type: "jsonb", nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_employee_profiles", x => x.id);
                    table.CheckConstraint("ck_personnel_file_employee_profiles__contract_dates", "contract_end_date is null or contract_start_date is null or contract_end_date >= contract_start_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_employee_profiles__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_employee_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    related_employee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_employee_relations", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_employee_relations__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_employment_assignments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    position_slot_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    org_unit_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_center_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_center_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_employment_assignments", x => x.id);
                    table.CheckConstraint("ck_personnel_file_employment_assignments__dates", "end_date is null or end_date >= start_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_employment_assignments__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_family_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    relationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nationality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    birth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    marital_status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    occupation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    document_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    document_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_studying = table.Column<bool>(type: "boolean", nullable: false),
                    study_place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    academic_level = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    is_beneficiary = table.Column<bool>(type: "boolean", nullable: false),
                    is_working = table.Column<bool>(type: "boolean", nullable: false),
                    workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    job_title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    work_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_deceased = table.Column<bool>(type: "boolean", nullable: false),
                    deceased_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_family_members", x => x.id);
                    table.CheckConstraint("ck_personnel_file_family_members__deceased_date", "is_deceased = false or deceased_date is not null");
                    table.ForeignKey(
                        name: "fk_personnel_file_family_members__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_hobbies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hobby_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_hobbies", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_hobbies__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_identifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_identification_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    issued_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_identifications", x => x.id);
                    table.CheckConstraint("ck_personnel_file_identifications__issued_expiry", "expiry_date is null or issued_date is null or expiry_date >= issued_date");
                    table.ForeignKey(
                        name: "fk_personnel_file_identifications__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_insurances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    employee_contribution = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    employer_contribution = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    range_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    policy_number = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    insured_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_insurances", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_insurances__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_languages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    level_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    speaks = table.Column<bool>(type: "boolean", nullable: false),
                    writes = table.Column<bool>(type: "boolean", nullable: false),
                    reads = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_languages", x => x.id);
                    table.CheckConstraint("ck_personnel_file_languages__skills", "speaks = true or writes = true or reads = true");
                    table.ForeignKey(
                        name: "fk_personnel_file_languages__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_medical_claims",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_number = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    claim_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    diagnosis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    claim_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    response_time_days = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    claim_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_medical_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_medical_claims__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_observations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_observations__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_payment_methods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    bank_account_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_payment_methods", x => x.id);
                    table.CheckConstraint("ck_personnel_file_payment_methods__effective_dates", "effective_to_utc is null or effective_to_utc >= effective_from_utc");
                    table.ForeignKey(
                        name: "fk_personnel_file_payment_methods__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_payroll_transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    transaction_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payroll_period_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_debit = table.Column<bool>(type: "boolean", nullable: false),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_payroll_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_payroll_transactions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_performance_evaluations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evaluation_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    qualitative_score_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_performance_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_performance_evaluations__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_personnel_actions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    action_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    action_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_system_generated = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_personnel_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_personnel_actions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_position_competency_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    desired_behaviors = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expected_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    achieved_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    gap_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    evaluation_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_position_competency_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_position_competency_results__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_previous_employments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_position = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    manager_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    entry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retirement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    company_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    exit_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    first_salary_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    last_salary_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    average_commission_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_previous_employments", x => x.id);
                    table.CheckConstraint("ck_personnel_file_previous_employments__commission_non_negative", "average_commission_amount is null or average_commission_amount >= 0");
                    table.CheckConstraint("ck_personnel_file_previous_employments__dates", "retirement_date is null or retirement_date >= entry_date");
                    table.CheckConstraint("ck_personnel_file_previous_employments__first_salary_non_negat~", "first_salary_amount is null or first_salary_amount >= 0");
                    table.CheckConstraint("ck_personnel_file_previous_employments__last_salary_non_negati~", "last_salary_amount is null or last_salary_amount >= 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_previous_employments__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_references",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reference_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occupation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    work_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    known_time_years = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_references", x => x.id);
                    table.CheckConstraint("ck_personnel_file_references__known_time_non_negative", "known_time_years >= 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_references__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_salary_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    income_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    salary_rubric_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pay_period_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_salary_items", x => x.id);
                    table.CheckConstraint("ck_personnel_file_salary_items__amount_non_negative", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_salary_items__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_selection_contests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contest_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    contest_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contest_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    result_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_selection_contests", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_selection_contests__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_trainings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    training_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    instructors = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    is_local = table.Column<bool>(type: "boolean", nullable: false),
                    country_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    duration_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    duration_unit_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    cost_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    cost_currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_trainings", x => x.id);
                    table.CheckConstraint("ck_personnel_file_trainings__cost_currency", "cost_amount is null or cost_currency_code is not null");
                    table.CheckConstraint("ck_personnel_file_trainings__cost_non_negative", "cost_amount is null or cost_amount >= 0");
                    table.CheckConstraint("ck_personnel_file_trainings__dates", "end_date is null or end_date >= start_date");
                    table.CheckConstraint("ck_personnel_file_trainings__duration", "duration_value > 0");
                    table.ForeignKey(
                        name: "fk_personnel_file_trainings__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "position_category_classifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    position_function_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    position_contract_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    org_unit_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_category_classifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_position_category_classifications__org_unit_type_catalog_item",
                        column: x => x.org_unit_type_catalog_item_id,
                        principalTable: "org_unit_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_category_classifications__position_contract_catalog_item",
                        column: x => x.position_contract_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_category_classifications__position_function_catalog_item",
                        column: x => x.position_function_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "salary_tabulator_change_request_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    salary_tabulator_change_request_id = table.Column<long>(type: "bigint", nullable: false),
                    salary_class_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_salary_class_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    salary_scale_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_salary_scale_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    change_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    proposed_base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    current_min_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    proposed_min_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    current_max_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    proposed_max_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salary_tabulator_change_request_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_salary_tabulator_change_request_items__request",
                        column: x => x.salary_tabulator_change_request_id,
                        principalTable: "salary_tabulator_change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_centers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    work_center_type_id = table.Column<long>(type: "bigint", nullable: false),
                    location_group_id = table.Column<long>(type: "bigint", nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    geo_lat = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    geo_long = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_centers", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_centers__location_groups",
                        column: x => x.location_group_id,
                        principalTable: "location_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_centers__work_center_types",
                        column: x => x.work_center_type_id,
                        principalTable: "work_center_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_invitation_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expiration_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_invitation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_invitation_tokens__auth_users",
                        column: x => x.user_id,
                        principalTable: "auth_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_invitation_tokens__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_subscriptions__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_companies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_companies", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_companies__auth_users",
                        column: x => x.user_id,
                        principalTable: "auth_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_companies__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_companies__iam_roles",
                        column: x => x.role_id,
                        principalTable: "iam_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competency_conduct_behaviors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    competency_conduct_id = table.Column<long>(type: "bigint", nullable: false),
                    behavior_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_conduct_behaviors", x => x.id);
                    table.ForeignKey(
                        name: "fk_competency_conduct_behaviors__behavior_catalog_item",
                        column: x => x.behavior_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_competency_conduct_behaviors__conduct",
                        column: x => x.competency_conduct_id,
                        principalTable: "competency_conducts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_insurance_beneficiaries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    insurance_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    document_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    birth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    kinship_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_insurance_beneficiaries", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_insurance_beneficiaries__insurance",
                        column: x => x.insurance_id,
                        principalTable: "personnel_file_insurances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "position_categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    position_category_classification_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_position_categories__position_category_classification",
                        column: x => x.position_category_classification_id,
                        principalTable: "position_category_classifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    normalized_title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    reports_to_job_profile_id = table.Column<long>(type: "bigint", nullable: true),
                    position_category_id = table.Column<long>(type: "bigint", nullable: true),
                    strategic_objective_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_work_equipment_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    responsibility_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    decision_scope = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    assigned_resources = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    responsibilities = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    benefits_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    working_condition_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    market_salary_reference = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    valuation_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profiles__assigned_work_equipment_catalog_item",
                        column: x => x.assigned_work_equipment_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profiles__org_unit",
                        column: x => x.org_unit_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profiles__position_category",
                        column: x => x.position_category_id,
                        principalTable: "position_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profiles__reports_to",
                        column: x => x.reports_to_job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profiles__responsibility_catalog_item",
                        column: x => x.responsibility_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profiles__strategic_objective_catalog_item",
                        column: x => x.strategic_objective_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_benefits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_benefits", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_benefits__catalog_item",
                        column: x => x.catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_benefits__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_compensations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    salary_class_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    salary_class_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    min_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    max_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    work_schedule = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_compensations", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_compensations__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_profile_compensations__salary_class",
                        column: x => x.salary_class_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_competencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    expected_level = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_competencies", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_competencies__catalog_item",
                        column: x => x.catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_competencies__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_competency_expectations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    occupational_pyramid_level_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    behavior_level_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    expected_evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_competency_expectations", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectations__behavior_level_catalog_item",
                        column: x => x.behavior_level_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectations__competency_catalog_item",
                        column: x => x.competency_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectations__competency_type_catalog_item",
                        column: x => x.competency_type_catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectations__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectations__pyramid_level",
                        column: x => x.occupational_pyramid_level_id,
                        principalTable: "occupational_pyramid_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_dependent_positions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    dependent_job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_dependent_positions", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_dependent_positions__dependent_profile",
                        column: x => x.dependent_job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_dependent_positions__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_functions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    function_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    frequency_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_functions", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_functions__frequency_catalog_item",
                        column: x => x.frequency_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_functions__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    relation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    counterpart = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_relations", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_relations__catalog_item",
                        column: x => x.catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_relations__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_requirements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    requirement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requirement_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_requirements", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_requirements__catalog_item",
                        column: x => x.catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_requirements__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_profile_requirements__requirement_type_catalog_item",
                        column: x => x.requirement_type_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_trainings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_trainings", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_trainings__catalog_item",
                        column: x => x.catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_trainings__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_working_conditions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    work_condition_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_working_conditions", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_working_conditions__catalog_item",
                        column: x => x.catalog_item_id,
                        principalTable: "job_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_working_conditions__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_profile_working_conditions__work_condition_type_catalog_item",
                        column: x => x.work_condition_type_catalog_item_id,
                        principalTable: "position_description_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_slots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    work_center_id = table.Column<long>(type: "bigint", nullable: true),
                    cost_center_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    direct_dependency_position_slot_id = table.Column<long>(type: "bigint", nullable: true),
                    functional_dependency_position_slot_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_employees = table.Column<int>(type: "integer", nullable: false),
                    occupied_employees = table.Column<int>(type: "integer", nullable: false),
                    is_fixed_term = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_slots", x => x.id);
                    table.ForeignKey(
                        name: "fk_position_slots__direct_dependency",
                        column: x => x.direct_dependency_position_slot_id,
                        principalTable: "position_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_slots__functional_dependency",
                        column: x => x.functional_dependency_position_slot_id,
                        principalTable: "position_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_slots__job_profile",
                        column: x => x.job_profile_id,
                        principalTable: "job_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_slots__org_unit",
                        column: x => x.org_unit_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_slots__work_center",
                        column: x => x.work_center_id,
                        principalTable: "work_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_competency_expectation_conducts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_profile_competency_expectation_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_conduct_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_profile_competency_expectation_conducts", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectation_conducts__competency_conduct",
                        column: x => x.competency_conduct_id,
                        principalTable: "competency_conducts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_profile_competency_expectation_conducts__expectation",
                        column: x => x.job_profile_competency_expectation_id,
                        principalTable: "job_profile_competency_expectations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "field_catalog",
                columns: new[] { "id", "created_utc", "data_type", "display_name", "field_key", "is_configurable", "is_sensitive", "modified_utc", "normalized_field_key", "normalized_property_name", "normalized_resource_key", "property_name", "resource_key" },
                values: new object[,]
                {
                    { -2005L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "enum", "Status", "RBAC_USERS.STATUS", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS.STATUS", "STATUS", "RBAC_USERS", "Status", "RBAC_USERS" },
                    { -2004L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "lookup", "Role", "RBAC_USERS.ROLE", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS.ROLE", "ROLE", "RBAC_USERS", "Role", "RBAC_USERS" },
                    { -2003L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "string", "Last Name", "RBAC_USERS.LAST_NAME", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS.LAST_NAME", "LASTNAME", "RBAC_USERS", "LastName", "RBAC_USERS" },
                    { -2002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "string", "First Name", "RBAC_USERS.FIRST_NAME", true, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS.FIRST_NAME", "FIRSTNAME", "RBAC_USERS", "FirstName", "RBAC_USERS" },
                    { -2001L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "string", "Email", "RBAC_USERS.EMAIL", true, true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS.EMAIL", "EMAIL", "RBAC_USERS", "Email", "RBAC_USERS" },
                    { -2000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "guid", "Internal Id", "RBAC_USERS.ID", false, false, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS.ID", "ID", "RBAC_USERS", "Id", "RBAC_USERS" }
                });

            migrationBuilder.InsertData(
                table: "legal_representative_document_type_catalog",
                columns: new[] { "id", "code", "is_active", "name", "sort_order" },
                values: new object[,]
                {
                    { 1L, "NationalId", true, "National ID", 1 },
                    { 2L, "Passport", true, "Passport", 2 },
                    { 3L, "TaxId", true, "Tax ID", 3 },
                    { 4L, "Other", true, "Other", 4 }
                });

            migrationBuilder.InsertData(
                table: "legal_representative_position_title_catalog",
                columns: new[] { "id", "code", "is_active", "name", "sort_order" },
                values: new object[,]
                {
                    { 1L, "OWNER", true, "OWNER", 1 },
                    { 2L, "CEO", true, "CEO", 2 },
                    { 3L, "EXECUTIVE_MANAGEMENT", true, "Executive Management", 3 },
                    { 4L, "HUMAN_RESOURCES", true, "Human Resources", 4 },
                    { 5L, "FINANCE", true, "Finance", 5 },
                    { 6L, "ACCOUNTING", true, "Accounting", 6 },
                    { 7L, "OPERATIONS", true, "Operations", 7 },
                    { 8L, "PROCUREMENT", true, "Procurement", 8 },
                    { 9L, "SALES", true, "Sales", 9 },
                    { 10L, "MARKETING", true, "Marketing", 10 },
                    { 11L, "CUSTOMER_SERVICE", true, "Customer Service", 11 },
                    { 12L, "INFORMATION_TECHNOLOGY", true, "Information Technology", 12 },
                    { 13L, "SOFTWARE_DEVELOPMENT", true, "Software Development", 13 },
                    { 14L, "INFRASTRUCTURE_DEVOPS", true, "Infrastructure / DevOps", 14 },
                    { 15L, "DATA_ANALYTICS", true, "Data & Analytics", 15 },
                    { 16L, "LEGAL", true, "Legal", 16 },
                    { 17L, "ADMINISTRATION", true, "Administration", 17 },
                    { 18L, "LOGISTICS", true, "Logistics", 18 },
                    { 19L, "MAINTENANCE", true, "Maintenance", 19 },
                    { 20L, "SECURITY", true, "Security", 20 }
                });

            migrationBuilder.InsertData(
                table: "legal_representative_representation_type_catalog",
                columns: new[] { "id", "code", "is_active", "name", "sort_order" },
                values: new object[,]
                {
                    { 1L, "PrimaryLegalRepresentative", true, "Primary Legal Representative", 1 },
                    { 2L, "AlternateLegalRepresentative", true, "Alternate Legal Representative", 2 },
                    { 3L, "AttorneyInFact", true, "Attorney in Fact", 3 }
                });

            migrationBuilder.InsertData(
                table: "plan_entitlements",
                columns: new[] { "id", "created_utc", "is_enabled", "modified_utc", "module_key", "plan_code" },
                values: new object[,]
                {
                    { -1002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ORG_STRUCTURE_CATALOGS", "FREE" },
                    { -1001L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "USERS", "FREE" },
                    { -1000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC", "FREE" }
                });

            migrationBuilder.InsertData(
                table: "rbac_resource_catalog",
                columns: new[] { "resource_key", "created_utc", "display_name", "is_active", "modified_utc", "normalized_resource_key" },
                values: new object[,]
                {
                    { "AUDIT_LOGS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Audit Logs", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AUDIT_LOGS" },
                    { "RBAC_PERMISSIONS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Permissions", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_PERMISSIONS" },
                    { "RBAC_ROLES", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Roles", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_ROLES" },
                    { "RBAC_USERS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Users", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs__tenant_actor_created",
                table: "audit_logs",
                columns: new[] { "tenant_id", "actor_user_id", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs__tenant_created",
                table: "audit_logs",
                columns: new[] { "tenant_id", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs__tenant_entity",
                table: "audit_logs",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs__tenant_event_created",
                table: "audit_logs",
                columns: new[] { "tenant_id", "event_type", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_audit_logs__public_id",
                table: "audit_logs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_refresh_tokens__family_user",
                table: "auth_refresh_tokens",
                columns: new[] { "family_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_auth_refresh_tokens_user_id",
                table: "auth_refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_auth_refresh_tokens__token_hash",
                table: "auth_refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_auth_users__normalized_email",
                table: "auth_users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_auth_users__provider_link",
                table: "auth_users",
                columns: new[] { "auth_provider", "provider_user_id" },
                unique: true,
                filter: "provider_user_id is not null");

            migrationBuilder.CreateIndex(
                name: "uq_auth_users__public_id",
                table: "auth_users",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies__company_type_catalog_item",
                table: "companies",
                column: "company_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_companies__public_id",
                table: "companies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_companies__slug",
                table: "companies",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_invitation_tokens__user_company",
                table: "company_invitation_tokens",
                columns: new[] { "user_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_company_invitation_tokens_company_id",
                table: "company_invitation_tokens",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_invitation_tokens__token_hash",
                table: "company_invitation_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_active",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__owner_active",
                table: "company_type_catalog_items",
                columns: new[] { "owner_user_public_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__owner_name",
                table: "company_type_catalog_items",
                columns: new[] { "owner_user_public_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_company_type_catalog_items__owner_code",
                table: "company_type_catalog_items",
                columns: new[] { "owner_user_public_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_type_catalog_items__public_id",
                table: "company_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competency_conduct_behaviors__tenant_conduct_sort",
                table: "competency_conduct_behaviors",
                columns: new[] { "tenant_id", "competency_conduct_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_competency_conduct_behaviors_behavior_catalog_item_id",
                table: "competency_conduct_behaviors",
                column: "behavior_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_competency_conduct_behaviors_competency_conduct_id",
                table: "competency_conduct_behaviors",
                column: "competency_conduct_id");

            migrationBuilder.CreateIndex(
                name: "uq_competency_conduct_behaviors__tenant_conduct_behavior",
                table: "competency_conduct_behaviors",
                columns: new[] { "tenant_id", "competency_conduct_id", "behavior_catalog_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competency_conducts__tenant_active",
                table: "competency_conducts",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_competency_conducts__tenant_competency_type_level",
                table: "competency_conducts",
                columns: new[] { "tenant_id", "competency_catalog_item_id", "competency_type_catalog_item_id", "behavior_level_catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_competency_conducts_behavior_level_catalog_item_id",
                table: "competency_conducts",
                column: "behavior_level_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_competency_conducts_competency_catalog_item_id",
                table: "competency_conducts",
                column: "competency_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_competency_conducts_competency_type_catalog_item_id",
                table: "competency_conducts",
                column: "competency_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_competency_conducts__public_id",
                table: "competency_conducts",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_competency_conducts__tenant_competency_type_level_desc",
                table: "competency_conducts",
                columns: new[] { "tenant_id", "competency_catalog_item_id", "competency_type_catalog_item_id", "behavior_level_catalog_item_id", "normalized_description" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_centers__tenant_normalized_name",
                table: "cost_centers",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ix_cost_centers__tenant_type_active",
                table: "cost_centers",
                columns: new[] { "tenant_id", "type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_cost_centers__public_id",
                table: "cost_centers",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_cost_centers__tenant_code",
                table: "cost_centers",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_catalog__resource_configurable",
                table: "field_catalog",
                columns: new[] { "normalized_resource_key", "is_configurable" });

            migrationBuilder.CreateIndex(
                name: "uq_field_catalog__normalized_field_key",
                table: "field_catalog",
                column: "normalized_field_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_permission_audit_logs__tenant_field_changed_at",
                table: "field_permission_audit_logs",
                columns: new[] { "tenant_id", "normalized_field_key", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_field_permission_audit_logs__tenant_role_changed_at",
                table: "field_permission_audit_logs",
                columns: new[] { "tenant_id", "role_public_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_functional_area_catalog_items__tenant_active",
                table: "functional_area_catalog_items",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_functional_area_catalog_items__tenant_name",
                table: "functional_area_catalog_items",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_functional_area_catalog_items__public_id",
                table: "functional_area_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_functional_area_catalog_items__tenant_code",
                table: "functional_area_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_permissions__tenant_screen",
                table: "iam_permissions",
                columns: new[] { "tenant_id", "normalized_module", "normalized_screen" });

            migrationBuilder.CreateIndex(
                name: "uq_iam_permissions__tenant_code",
                table: "iam_permissions",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_role_permission_assignments_permission_id",
                table: "iam_role_permission_assignments",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_iam_role_permission_assignments_role_id",
                table: "iam_role_permission_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_iam_role_perm__tenant_role_perm",
                table: "iam_role_permission_assignments",
                columns: new[] { "tenant_id", "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_roles__tenant_name",
                table: "iam_roles",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "uq_iam_roles__tenant_name",
                table: "iam_roles",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_role_assignments_role_id",
                table: "iam_user_role_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_role_assignments_user_id",
                table: "iam_user_role_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_iam_user_role__tenant_user_role",
                table: "iam_user_role_assignments",
                columns: new[] { "tenant_id", "user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_users__tenant_name",
                table: "iam_users",
                columns: new[] { "tenant_id", "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "uq_iam_users__tenant_email",
                table: "iam_users",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_catalog_items__tenant_category_active",
                table: "job_catalog_items",
                columns: new[] { "tenant_id", "category", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_job_catalog_items__tenant_category_name",
                table: "job_catalog_items",
                columns: new[] { "tenant_id", "category", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_job_catalog_items__public_id",
                table: "job_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_catalog_items__tenant_category_code",
                table: "job_catalog_items",
                columns: new[] { "tenant_id", "category", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_benefits__tenant_profile_sort",
                table: "job_profile_benefits",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_benefits_catalog_item_id",
                table: "job_profile_benefits",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_benefits_job_profile_id",
                table: "job_profile_benefits",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_compensations__tenant_profile_primary",
                table: "job_profile_compensations",
                columns: new[] { "tenant_id", "job_profile_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_compensations_job_profile_id",
                table: "job_profile_compensations",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_compensations_salary_class_catalog_item_id",
                table: "job_profile_compensations",
                column: "salary_class_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_competencies__tenant_profile_sort",
                table: "job_profile_competencies",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competencies_catalog_item_id",
                table: "job_profile_competencies",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competencies_job_profile_id",
                table: "job_profile_competencies",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_competency_expectation_conducts__tenant_expectation_sort",
                table: "job_profile_competency_expectation_conducts",
                columns: new[] { "tenant_id", "job_profile_competency_expectation_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectation_conducts_competency_cond~",
                table: "job_profile_competency_expectation_conducts",
                column: "competency_conduct_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectation_conducts_job_profile_com~",
                table: "job_profile_competency_expectation_conducts",
                column: "job_profile_competency_expectation_id");

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_competency_expectation_conducts__tenant_expectation_conduct",
                table: "job_profile_competency_expectation_conducts",
                columns: new[] { "tenant_id", "job_profile_competency_expectation_id", "competency_conduct_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_competency_expectations__tenant_profile_sort",
                table: "job_profile_competency_expectations",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectations_behavior_level_catalog_~",
                table: "job_profile_competency_expectations",
                column: "behavior_level_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectations_competency_catalog_item~",
                table: "job_profile_competency_expectations",
                column: "competency_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectations_competency_type_catalog~",
                table: "job_profile_competency_expectations",
                column: "competency_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectations_job_profile_id",
                table: "job_profile_competency_expectations",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_competency_expectations_occupational_pyramid_le~",
                table: "job_profile_competency_expectations",
                column: "occupational_pyramid_level_id");

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_competency_expectations__public_id",
                table: "job_profile_competency_expectations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_competency_expectations__tenant_profile_competency_level",
                table: "job_profile_competency_expectations",
                columns: new[] { "tenant_id", "job_profile_id", "competency_catalog_item_id", "competency_type_catalog_item_id", "behavior_level_catalog_item_id", "occupational_pyramid_level_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_dependent_positions__tenant_profile_dep",
                table: "job_profile_dependent_positions",
                columns: new[] { "tenant_id", "job_profile_id", "dependent_job_profile_id" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_dependent_positions_dependent_job_profile_id",
                table: "job_profile_dependent_positions",
                column: "dependent_job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_dependent_positions_job_profile_id",
                table: "job_profile_dependent_positions",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_functions__tenant_profile_sort",
                table: "job_profile_functions",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_functions_frequency_catalog_item_id",
                table: "job_profile_functions",
                column: "frequency_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_functions_job_profile_id",
                table: "job_profile_functions",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_relations__tenant_profile_sort",
                table: "job_profile_relations",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_relations_catalog_item_id",
                table: "job_profile_relations",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_relations_job_profile_id",
                table: "job_profile_relations",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_requirements__tenant_profile_sort",
                table: "job_profile_requirements",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_requirements_catalog_item_id",
                table: "job_profile_requirements",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_requirements_job_profile_id",
                table: "job_profile_requirements",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_requirements_requirement_type_catalog_item_id",
                table: "job_profile_requirements",
                column: "requirement_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_trainings__tenant_profile_sort",
                table: "job_profile_trainings",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_trainings_catalog_item_id",
                table: "job_profile_trainings",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_trainings_job_profile_id",
                table: "job_profile_trainings",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profile_working_conditions__tenant_profile_sort",
                table: "job_profile_working_conditions",
                columns: new[] { "tenant_id", "job_profile_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_working_conditions_catalog_item_id",
                table: "job_profile_working_conditions",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_working_conditions_job_profile_id",
                table: "job_profile_working_conditions",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profile_working_conditions_work_condition_type_catalog_~",
                table: "job_profile_working_conditions",
                column: "work_condition_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_profiles__tenant_org_unit",
                table: "job_profiles",
                columns: new[] { "tenant_id", "org_unit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_job_profiles__tenant_position_category",
                table: "job_profiles",
                columns: new[] { "tenant_id", "position_category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_job_profiles__tenant_status",
                table: "job_profiles",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_profiles__tenant_title",
                table: "job_profiles",
                columns: new[] { "tenant_id", "normalized_title" });

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_assigned_work_equipment_catalog_item_id",
                table: "job_profiles",
                column: "assigned_work_equipment_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_org_unit_id",
                table: "job_profiles",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_position_category_id",
                table: "job_profiles",
                column: "position_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_reports_to_job_profile_id",
                table: "job_profiles",
                column: "reports_to_job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_responsibility_catalog_item_id",
                table: "job_profiles",
                column: "responsibility_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_strategic_objective_catalog_item_id",
                table: "job_profiles",
                column: "strategic_objective_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_job_profiles__public_id",
                table: "job_profiles",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profiles__tenant_code",
                table: "job_profiles",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_document_type_catalog__code",
                table: "legal_representative_document_type_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_position_title_catalog__code",
                table: "legal_representative_position_title_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_representation_type_catalog__code",
                table: "legal_representative_representation_type_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_active",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_effective_dates",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "effective_from_utc", "effective_to_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_normalized_name",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "normalized_full_name" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_primary",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_representation_active",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "representation_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_legal_representatives__public_id",
                table: "legal_representatives",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representatives__tenant_document_type_number",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "document_type", "normalized_document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_legal_representatives__tenant_primary_active",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "is_primary", "is_active" },
                unique: true,
                filter: "is_primary = true and is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_location_groups__tenant_level_active",
                table: "location_groups",
                columns: new[] { "tenant_id", "level_order", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_location_groups__tenant_name",
                table: "location_groups",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ix_location_groups__tenant_parent_active",
                table: "location_groups",
                columns: new[] { "tenant_id", "parent_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_location_groups__tenant_parent_name",
                table: "location_groups",
                columns: new[] { "tenant_id", "parent_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "IX_location_groups_parent_id",
                table: "location_groups",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_location_groups__public_id",
                table: "location_groups",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_location_groups__tenant_code",
                table: "location_groups",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_location_hierarchy_configs__public_id",
                table: "location_hierarchy_configs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_location_hierarchy_configs__tenant_id",
                table: "location_hierarchy_configs",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_location_levels__tenant_active_order",
                table: "location_levels",
                columns: new[] { "tenant_id", "is_active", "level_order" });

            migrationBuilder.CreateIndex(
                name: "uq_location_levels__public_id",
                table: "location_levels",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_location_levels__tenant_order",
                table: "location_levels",
                columns: new[] { "tenant_id", "level_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_occupational_pyramid_levels__tenant_active",
                table: "occupational_pyramid_levels",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_occupational_pyramid_levels__tenant_name",
                table: "occupational_pyramid_levels",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_occupational_pyramid_levels__public_id",
                table: "occupational_pyramid_levels",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_occupational_pyramid_levels__tenant_code",
                table: "occupational_pyramid_levels",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_occupational_pyramid_levels__tenant_level_order",
                table: "occupational_pyramid_levels",
                columns: new[] { "tenant_id", "level_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_type_catalog_items__tenant_active",
                table: "org_unit_type_catalog_items",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_type_catalog_items__tenant_name",
                table: "org_unit_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_org_unit_type_catalog_items__public_id",
                table: "org_unit_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_org_unit_type_catalog_items__tenant_code",
                table: "org_unit_type_catalog_items",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_units__tenant_active",
                table: "org_units",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units__tenant_functional_area_catalog_item",
                table: "org_units",
                columns: new[] { "tenant_id", "functional_area_catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units__tenant_name",
                table: "org_units",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units__tenant_org_unit_type_catalog_item",
                table: "org_units",
                columns: new[] { "tenant_id", "org_unit_type_catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units__tenant_parent",
                table: "org_units",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_org_units_functional_area_catalog_item_id",
                table: "org_units",
                column: "functional_area_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_units_org_unit_type_catalog_item_id",
                table: "org_units",
                column: "org_unit_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_units_parent_id",
                table: "org_units",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_org_units__public_id",
                table: "org_units",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_org_units__tenant_code",
                table: "org_units",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_additional_benefits__tenant_file_type_active",
                table: "personnel_file_additional_benefits",
                columns: new[] { "tenant_id", "personnel_file_id", "benefit_type_code", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_additional_benefits_personnel_file_id",
                table: "personnel_file_additional_benefits",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_additional_benefits__public_id",
                table: "personnel_file_additional_benefits",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_addresses__tenant_file",
                table: "personnel_file_addresses",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_addresses_personnel_file_id",
                table: "personnel_file_addresses",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_addresses__public_id",
                table: "personnel_file_addresses",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_assets_accesses__tenant_file_start_active",
                table: "personnel_file_assets_accesses",
                columns: new[] { "tenant_id", "personnel_file_id", "start_date_utc", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_assets_accesses_personnel_file_id",
                table: "personnel_file_assets_accesses",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_assets_accesses__public_id",
                table: "personnel_file_assets_accesses",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_associations__tenant_file",
                table: "personnel_file_associations",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_associations_personnel_file_id",
                table: "personnel_file_associations",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_associations__public_id",
                table: "personnel_file_associations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_authorization_substitutions__tenant_file_type_active",
                table: "personnel_file_authorization_substitutions",
                columns: new[] { "tenant_id", "personnel_file_id", "substitution_type_code", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_authorization_substitutions_personnel_file_id",
                table: "personnel_file_authorization_substitutions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_authorization_substitutions__public_id",
                table: "personnel_file_authorization_substitutions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_bank_accounts__tenant_file",
                table: "personnel_file_bank_accounts",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_bank_accounts_personnel_file_id",
                table: "personnel_file_bank_accounts",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_bank_accounts__public_id",
                table: "personnel_file_bank_accounts",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_contract_histories__tenant_file_contract_date",
                table: "personnel_file_contract_histories",
                columns: new[] { "tenant_id", "personnel_file_id", "contract_date" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_contract_histories_personnel_file_id",
                table: "personnel_file_contract_histories",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_contract_histories__public_id",
                table: "personnel_file_contract_histories",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_curricular_competencies__tenant_file_requirement_type",
                table: "personnel_file_curricular_competencies",
                columns: new[] { "tenant_id", "personnel_file_id", "requirement_type_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_curricular_competencies_personnel_file_id",
                table: "personnel_file_curricular_competencies",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_curricular_competencies__public_id",
                table: "personnel_file_curricular_competencies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_custom_field_definitions__tenant_active_sort",
                table: "personnel_file_custom_field_definitions",
                columns: new[] { "tenant_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_custom_field_definitions__public_id",
                table: "personnel_file_custom_field_definitions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_custom_field_definitions__tenant_key",
                table: "personnel_file_custom_field_definitions",
                columns: new[] { "tenant_id", "normalized_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_documents__tenant_file_active",
                table: "personnel_file_documents",
                columns: new[] { "tenant_id", "personnel_file_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_documents_personnel_file_id",
                table: "personnel_file_documents",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_documents__public_id",
                table: "personnel_file_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_educations__tenant_file",
                table: "personnel_file_educations",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_educations_personnel_file_id",
                table: "personnel_file_educations",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_educations__public_id",
                table: "personnel_file_educations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_emergency_contacts__tenant_file",
                table: "personnel_file_emergency_contacts",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_emergency_contacts_personnel_file_id",
                table: "personnel_file_emergency_contacts",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_emergency_contacts__public_id",
                table: "personnel_file_emergency_contacts",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_employee_profiles_personnel_file_id",
                table: "personnel_file_employee_profiles",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_employee_profiles__public_id",
                table: "personnel_file_employee_profiles",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_employee_profiles__tenant_employee_code",
                table: "personnel_file_employee_profiles",
                columns: new[] { "tenant_id", "normalized_employee_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_employee_profiles__tenant_file",
                table: "personnel_file_employee_profiles",
                columns: new[] { "tenant_id", "personnel_file_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_employee_relations__tenant_file",
                table: "personnel_file_employee_relations",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_employee_relations_personnel_file_id",
                table: "personnel_file_employee_relations",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_employee_relations__public_id",
                table: "personnel_file_employee_relations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_employment_assignments__tenant_file_active_primary",
                table: "personnel_file_employment_assignments",
                columns: new[] { "tenant_id", "personnel_file_id", "is_active", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_employment_assignments__tenant_file_start",
                table: "personnel_file_employment_assignments",
                columns: new[] { "tenant_id", "personnel_file_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_employment_assignments_personnel_file_id",
                table: "personnel_file_employment_assignments",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_employment_assignments__public_id",
                table: "personnel_file_employment_assignments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_family_members__tenant_file",
                table: "personnel_file_family_members",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_family_members_personnel_file_id",
                table: "personnel_file_family_members",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_family_members__public_id",
                table: "personnel_file_family_members",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_hobbies__tenant_file",
                table: "personnel_file_hobbies",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_hobbies_personnel_file_id",
                table: "personnel_file_hobbies",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_hobbies__public_id",
                table: "personnel_file_hobbies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_identifications__tenant_file",
                table: "personnel_file_identifications",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_identifications_personnel_file_id",
                table: "personnel_file_identifications",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_identifications__public_id",
                table: "personnel_file_identifications",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_identifications__tenant_type_number",
                table: "personnel_file_identifications",
                columns: new[] { "tenant_id", "identification_type", "normalized_identification_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_insurance_beneficiaries__tenant_insurance_active",
                table: "personnel_file_insurance_beneficiaries",
                columns: new[] { "tenant_id", "insurance_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_insurance_beneficiaries_insurance_id",
                table: "personnel_file_insurance_beneficiaries",
                column: "insurance_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_insurance_beneficiaries__public_id",
                table: "personnel_file_insurance_beneficiaries",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_insurances__tenant_file_active_code",
                table: "personnel_file_insurances",
                columns: new[] { "tenant_id", "personnel_file_id", "is_active", "insurance_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_insurances_personnel_file_id",
                table: "personnel_file_insurances",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_insurances__public_id",
                table: "personnel_file_insurances",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_languages__tenant_file",
                table: "personnel_file_languages",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_languages_personnel_file_id",
                table: "personnel_file_languages",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_languages__public_id",
                table: "personnel_file_languages",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_medical_claims__tenant_file_date_type",
                table: "personnel_file_medical_claims",
                columns: new[] { "tenant_id", "personnel_file_id", "claim_date_utc", "claim_type_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_medical_claims_personnel_file_id",
                table: "personnel_file_medical_claims",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_medical_claims__public_id",
                table: "personnel_file_medical_claims",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_observations__tenant_file_created",
                table: "personnel_file_observations",
                columns: new[] { "tenant_id", "personnel_file_id", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_observations_personnel_file_id",
                table: "personnel_file_observations",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_observations__public_id",
                table: "personnel_file_observations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_payment_methods__tenant_file_active_primary",
                table: "personnel_file_payment_methods",
                columns: new[] { "tenant_id", "personnel_file_id", "is_active", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_payment_methods_personnel_file_id",
                table: "personnel_file_payment_methods",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_payment_methods__public_id",
                table: "personnel_file_payment_methods",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_payroll_transactions__tenant_file_transaction_date",
                table: "personnel_file_payroll_transactions",
                columns: new[] { "tenant_id", "personnel_file_id", "transaction_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_payroll_transactions__tenant_file_type",
                table: "personnel_file_payroll_transactions",
                columns: new[] { "tenant_id", "personnel_file_id", "transaction_type_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_payroll_transactions_personnel_file_id",
                table: "personnel_file_payroll_transactions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_payroll_transactions__public_id",
                table: "personnel_file_payroll_transactions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_performance_evaluations__tenant_file_date",
                table: "personnel_file_performance_evaluations",
                columns: new[] { "tenant_id", "personnel_file_id", "evaluation_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_performance_evaluations_personnel_file_id",
                table: "personnel_file_performance_evaluations",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_performance_evaluations__public_id",
                table: "personnel_file_performance_evaluations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_personnel_actions__tenant_file_action_date",
                table: "personnel_file_personnel_actions",
                columns: new[] { "tenant_id", "personnel_file_id", "action_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_personnel_actions__tenant_file_type_status",
                table: "personnel_file_personnel_actions",
                columns: new[] { "tenant_id", "personnel_file_id", "action_type_code", "action_status_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_personnel_actions_personnel_file_id",
                table: "personnel_file_personnel_actions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_personnel_actions__public_id",
                table: "personnel_file_personnel_actions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_position_competency_results__tenant_file_competency",
                table: "personnel_file_position_competency_results",
                columns: new[] { "tenant_id", "personnel_file_id", "competency_code" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_position_competency_results_personnel_file_id",
                table: "personnel_file_position_competency_results",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_position_competency_results__public_id",
                table: "personnel_file_position_competency_results",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_previous_employments__tenant_file",
                table: "personnel_file_previous_employments",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_previous_employments_personnel_file_id",
                table: "personnel_file_previous_employments",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_previous_employments__public_id",
                table: "personnel_file_previous_employments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_references__tenant_file",
                table: "personnel_file_references",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_references_personnel_file_id",
                table: "personnel_file_references",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_references__public_id",
                table: "personnel_file_references",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_salary_items__tenant_file_start_active",
                table: "personnel_file_salary_items",
                columns: new[] { "tenant_id", "personnel_file_id", "start_date", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_salary_items_personnel_file_id",
                table: "personnel_file_salary_items",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_salary_items__public_id",
                table: "personnel_file_salary_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_selection_contests__tenant_file_date",
                table: "personnel_file_selection_contests",
                columns: new[] { "tenant_id", "personnel_file_id", "contest_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_selection_contests_personnel_file_id",
                table: "personnel_file_selection_contests",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_selection_contests__public_id",
                table: "personnel_file_selection_contests",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_trainings__tenant_file",
                table: "personnel_file_trainings",
                columns: new[] { "tenant_id", "personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_trainings_personnel_file_id",
                table: "personnel_file_trainings",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_trainings__public_id",
                table: "personnel_file_trainings",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_name",
                table: "personnel_files",
                columns: new[] { "tenant_id", "normalized_full_name" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_org_unit",
                table: "personnel_files",
                columns: new[] { "tenant_id", "org_unit_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_files__tenant_type_active",
                table: "personnel_files",
                columns: new[] { "tenant_id", "record_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_personnel_files__public_id",
                table: "personnel_files",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements__plan_module",
                table: "plan_entitlements",
                columns: new[] { "plan_code", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_categories__tenant_active",
                table: "position_categories",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_position_categories__tenant_classification",
                table: "position_categories",
                columns: new[] { "tenant_id", "position_category_classification_id" });

            migrationBuilder.CreateIndex(
                name: "ix_position_categories__tenant_name",
                table: "position_categories",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "IX_position_categories_position_category_classification_id",
                table: "position_categories",
                column: "position_category_classification_id");

            migrationBuilder.CreateIndex(
                name: "uq_position_categories__public_id",
                table: "position_categories",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_position_categories__tenant_code",
                table: "position_categories",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_category_classifications__tenant_active",
                table: "position_category_classifications",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_position_category_classifications__tenant_name",
                table: "position_category_classifications",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "IX_position_category_classifications_org_unit_type_catalog_ite~",
                table: "position_category_classifications",
                column: "org_unit_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_category_classifications_position_contract_catalog~",
                table: "position_category_classifications",
                column: "position_contract_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_category_classifications_position_function_catalog~",
                table: "position_category_classifications",
                column: "position_function_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_position_category_classifications__public_id",
                table: "position_category_classifications",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_position_category_classifications__tenant_axes",
                table: "position_category_classifications",
                columns: new[] { "tenant_id", "position_function_catalog_item_id", "position_contract_catalog_item_id", "org_unit_type_catalog_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_position_category_classifications__tenant_code",
                table: "position_category_classifications",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_description_catalog_items__tenant_type_active",
                table: "position_description_catalog_items",
                columns: new[] { "tenant_id", "catalog_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_position_description_catalog_items__tenant_type_name",
                table: "position_description_catalog_items",
                columns: new[] { "tenant_id", "catalog_type", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_position_description_catalog_items__public_id",
                table: "position_description_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_position_description_catalog_items__tenant_type_code",
                table: "position_description_catalog_items",
                columns: new[] { "tenant_id", "catalog_type", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_direct_dependency",
                table: "position_slots",
                columns: new[] { "tenant_id", "direct_dependency_position_slot_id" });

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_functional_dependency",
                table: "position_slots",
                columns: new[] { "tenant_id", "functional_dependency_position_slot_id" });

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_job_profile",
                table: "position_slots",
                columns: new[] { "tenant_id", "job_profile_id" });

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_org_unit",
                table: "position_slots",
                columns: new[] { "tenant_id", "org_unit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_status",
                table: "position_slots",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_position_slots__tenant_work_center",
                table: "position_slots",
                columns: new[] { "tenant_id", "work_center_id" });

            migrationBuilder.CreateIndex(
                name: "IX_position_slots_direct_dependency_position_slot_id",
                table: "position_slots",
                column: "direct_dependency_position_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_slots_functional_dependency_position_slot_id",
                table: "position_slots",
                column: "functional_dependency_position_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_slots_job_profile_id",
                table: "position_slots",
                column: "job_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_slots_org_unit_id",
                table: "position_slots",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_slots_work_center_id",
                table: "position_slots",
                column: "work_center_id");

            migrationBuilder.CreateIndex(
                name: "uq_position_slots__public_id",
                table: "position_slots",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_position_slots__tenant_code",
                table: "position_slots",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rbac_permission_audit_logs__tenant_resource_changed_at",
                table: "rbac_permission_audit_logs",
                columns: new[] { "tenant_id", "normalized_resource_key", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_rbac_permission_audit_logs__tenant_role_changed_at",
                table: "rbac_permission_audit_logs",
                columns: new[] { "tenant_id", "role_public_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_rbac_resource_catalog__normalized_resource_key",
                table: "rbac_resource_catalog",
                column: "normalized_resource_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_field_permissions__tenant_role",
                table: "role_field_permissions",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "uq_role_field_permissions__tenant_role_field",
                table: "role_field_permissions",
                columns: new[] { "tenant_id", "role_id", "normalized_field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_salary_tabulator_items__request",
                table: "salary_tabulator_change_request_items",
                column: "salary_tabulator_change_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_salary_tabulator_requests__tenant_request_number",
                table: "salary_tabulator_change_requests",
                columns: new[] { "tenant_id", "request_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_salary_tabulator_requests__tenant_status_created",
                table: "salary_tabulator_change_requests",
                columns: new[] { "tenant_id", "status", "created_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_salary_tabulator_change_requests__public_id",
                table: "salary_tabulator_change_requests",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_salary_tabulator_lines__tenant_class_scale_active",
                table: "salary_tabulator_lines",
                columns: new[] { "tenant_id", "normalized_salary_class_code", "normalized_salary_scale_code", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_salary_tabulator_lines__public_id",
                table: "salary_tabulator_lines",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_salary_tabulator_lines__tenant_class_scale_effective_from",
                table: "salary_tabulator_lines",
                columns: new[] { "tenant_id", "normalized_salary_class_code", "normalized_salary_scale_code", "effective_from_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_companies__company_status_role",
                table: "user_companies",
                columns: new[] { "company_id", "status", "role_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_companies_role_id",
                table: "user_companies",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_companies__primary_user",
                table: "user_companies",
                column: "user_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "uq_user_companies__user_company",
                table: "user_companies",
                columns: new[] { "user_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_center_types__tenant_active",
                table: "work_center_types",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_work_center_types__tenant_name",
                table: "work_center_types",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_work_center_types__public_id",
                table: "work_center_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_work_center_types__tenant_code",
                table: "work_center_types",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_centers__tenant_group_active",
                table: "work_centers",
                columns: new[] { "tenant_id", "location_group_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_work_centers__tenant_name",
                table: "work_centers",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ix_work_centers__tenant_type_active",
                table: "work_centers",
                columns: new[] { "tenant_id", "work_center_type_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_work_centers_location_group_id",
                table: "work_centers",
                column: "location_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_centers_work_center_type_id",
                table: "work_centers",
                column: "work_center_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_work_centers__public_id",
                table: "work_centers",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_work_centers__tenant_code",
                table: "work_centers",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "auth_refresh_tokens");

            migrationBuilder.DropTable(
                name: "company_invitation_tokens");

            migrationBuilder.DropTable(
                name: "company_subscriptions");

            migrationBuilder.DropTable(
                name: "competency_conduct_behaviors");

            migrationBuilder.DropTable(
                name: "cost_centers");

            migrationBuilder.DropTable(
                name: "field_catalog");

            migrationBuilder.DropTable(
                name: "field_permission_audit_logs");

            migrationBuilder.DropTable(
                name: "iam_role_permission_assignments");

            migrationBuilder.DropTable(
                name: "iam_user_role_assignments");

            migrationBuilder.DropTable(
                name: "job_profile_benefits");

            migrationBuilder.DropTable(
                name: "job_profile_compensations");

            migrationBuilder.DropTable(
                name: "job_profile_competencies");

            migrationBuilder.DropTable(
                name: "job_profile_competency_expectation_conducts");

            migrationBuilder.DropTable(
                name: "job_profile_dependent_positions");

            migrationBuilder.DropTable(
                name: "job_profile_functions");

            migrationBuilder.DropTable(
                name: "job_profile_relations");

            migrationBuilder.DropTable(
                name: "job_profile_requirements");

            migrationBuilder.DropTable(
                name: "job_profile_trainings");

            migrationBuilder.DropTable(
                name: "job_profile_working_conditions");

            migrationBuilder.DropTable(
                name: "legal_representative_document_type_catalog");

            migrationBuilder.DropTable(
                name: "legal_representative_position_title_catalog");

            migrationBuilder.DropTable(
                name: "legal_representative_representation_type_catalog");

            migrationBuilder.DropTable(
                name: "legal_representatives");

            migrationBuilder.DropTable(
                name: "location_hierarchy_configs");

            migrationBuilder.DropTable(
                name: "location_levels");

            migrationBuilder.DropTable(
                name: "personnel_catalog_items");

            migrationBuilder.DropTable(
                name: "personnel_file_additional_benefits");

            migrationBuilder.DropTable(
                name: "personnel_file_addresses");

            migrationBuilder.DropTable(
                name: "personnel_file_assets_accesses");

            migrationBuilder.DropTable(
                name: "personnel_file_associations");

            migrationBuilder.DropTable(
                name: "personnel_file_authorization_substitutions");

            migrationBuilder.DropTable(
                name: "personnel_file_bank_accounts");

            migrationBuilder.DropTable(
                name: "personnel_file_contract_histories");

            migrationBuilder.DropTable(
                name: "personnel_file_curricular_competencies");

            migrationBuilder.DropTable(
                name: "personnel_file_custom_field_definitions");

            migrationBuilder.DropTable(
                name: "personnel_file_documents");

            migrationBuilder.DropTable(
                name: "personnel_file_educations");

            migrationBuilder.DropTable(
                name: "personnel_file_emergency_contacts");

            migrationBuilder.DropTable(
                name: "personnel_file_employee_profiles");

            migrationBuilder.DropTable(
                name: "personnel_file_employee_relations");

            migrationBuilder.DropTable(
                name: "personnel_file_employment_assignments");

            migrationBuilder.DropTable(
                name: "personnel_file_family_members");

            migrationBuilder.DropTable(
                name: "personnel_file_hobbies");

            migrationBuilder.DropTable(
                name: "personnel_file_identifications");

            migrationBuilder.DropTable(
                name: "personnel_file_insurance_beneficiaries");

            migrationBuilder.DropTable(
                name: "personnel_file_languages");

            migrationBuilder.DropTable(
                name: "personnel_file_medical_claims");

            migrationBuilder.DropTable(
                name: "personnel_file_observations");

            migrationBuilder.DropTable(
                name: "personnel_file_payment_methods");

            migrationBuilder.DropTable(
                name: "personnel_file_payroll_transactions");

            migrationBuilder.DropTable(
                name: "personnel_file_performance_evaluations");

            migrationBuilder.DropTable(
                name: "personnel_file_personnel_actions");

            migrationBuilder.DropTable(
                name: "personnel_file_position_competency_results");

            migrationBuilder.DropTable(
                name: "personnel_file_previous_employments");

            migrationBuilder.DropTable(
                name: "personnel_file_references");

            migrationBuilder.DropTable(
                name: "personnel_file_salary_items");

            migrationBuilder.DropTable(
                name: "personnel_file_selection_contests");

            migrationBuilder.DropTable(
                name: "personnel_file_trainings");

            migrationBuilder.DropTable(
                name: "plan_entitlements");

            migrationBuilder.DropTable(
                name: "position_slots");

            migrationBuilder.DropTable(
                name: "rbac_permission_audit_logs");

            migrationBuilder.DropTable(
                name: "rbac_resource_catalog");

            migrationBuilder.DropTable(
                name: "role_field_permissions");

            migrationBuilder.DropTable(
                name: "salary_tabulator_change_request_items");

            migrationBuilder.DropTable(
                name: "salary_tabulator_lines");

            migrationBuilder.DropTable(
                name: "user_companies");

            migrationBuilder.DropTable(
                name: "iam_permissions");

            migrationBuilder.DropTable(
                name: "iam_users");

            migrationBuilder.DropTable(
                name: "competency_conducts");

            migrationBuilder.DropTable(
                name: "job_profile_competency_expectations");

            migrationBuilder.DropTable(
                name: "personnel_file_insurances");

            migrationBuilder.DropTable(
                name: "work_centers");

            migrationBuilder.DropTable(
                name: "salary_tabulator_change_requests");

            migrationBuilder.DropTable(
                name: "auth_users");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "iam_roles");

            migrationBuilder.DropTable(
                name: "job_catalog_items");

            migrationBuilder.DropTable(
                name: "job_profiles");

            migrationBuilder.DropTable(
                name: "occupational_pyramid_levels");

            migrationBuilder.DropTable(
                name: "personnel_files");

            migrationBuilder.DropTable(
                name: "location_groups");

            migrationBuilder.DropTable(
                name: "work_center_types");

            migrationBuilder.DropTable(
                name: "company_type_catalog_items");

            migrationBuilder.DropTable(
                name: "org_units");

            migrationBuilder.DropTable(
                name: "position_categories");

            migrationBuilder.DropTable(
                name: "functional_area_catalog_items");

            migrationBuilder.DropTable(
                name: "position_category_classifications");

            migrationBuilder.DropTable(
                name: "org_unit_type_catalog_items");

            migrationBuilder.DropTable(
                name: "position_description_catalog_items");
        }
    }
}
