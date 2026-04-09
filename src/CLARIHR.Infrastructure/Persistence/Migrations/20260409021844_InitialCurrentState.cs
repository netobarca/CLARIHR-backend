using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCurrentState : Migration
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_addons",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    billing_model = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    measurement_unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    minimum_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_addons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    base_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_per_active_employee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_system_plan = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_centers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cost_centers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "country_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "functional_area_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    linked_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "internal_catalog_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    last_used_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_internal_catalog_values", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    normalized_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    is_primary = table.Column<bool>(type: "boolean", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    is_multi_level = table.Column<bool>(type: "boolean", nullable: false),
                    default_group_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    default_group_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    allows_work_centers = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    field_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_files", x => x.id);
                });

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
                name: "position_description_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_description_catalog_items", x => x.id);
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    requires_address = table.Column<bool>(type: "boolean", nullable: false),
                    requires_geo = table.Column<bool>(type: "boolean", nullable: false),
                    allows_biometric = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    client_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "commercial_addon_entitlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    commercial_addon_id = table.Column<long>(type: "bigint", nullable: false),
                    addon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    capability_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    module_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_addon_entitlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_commercial_addon_entitlements__commercial_addons",
                        column: x => x.commercial_addon_id,
                        principalTable: "commercial_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plan_limits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    limit_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_limit_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commercial_plan_limits", x => x.id);
                    table.ForeignKey(
                        name: "fk_commercial_plan_limits__commercial_plans",
                        column: x => x.commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "plan_entitlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    capability_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    module_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_entitlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_entitlements__commercial_plans",
                        column: x => x.commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_type_catalog_items_country_catalog_country_catalog_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    competency_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    behavior_level_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    normalized_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    benefit_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    address_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    department = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    municipality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    asset_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    asset_or_access_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    access_level_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    association_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    joined_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    left_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    substitution_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    substitute_personnel_file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_position_title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    bank_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    account_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_account_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    account_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    contract_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    contract_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    contract_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position_slot_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    requirement_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    requirement_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    competency_domain = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    experience_time_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    metric_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    relationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    related_employee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    hobby_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    identification_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_identification_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    issued_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    language_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    level_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    speaks = table.Column<bool>(type: "boolean", nullable: false),
                    writes = table.Column<bool>(type: "boolean", nullable: false),
                    reads = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    author_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    payment_method_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    bank_account_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    evaluator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evaluation_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    qualitative_score_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    competency_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    desired_behaviors = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expected_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    achieved_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    gap_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    evaluation_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    person_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reference_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occupation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    workplace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    work_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    known_time_years = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    income_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    salary_rubric_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pay_period_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    contest_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    contest_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contest_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    result_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_system = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_synced_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "companies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false),
                    billable_since_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.ForeignKey(
                        name: "fk_companies__country_catalog_item",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    document_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    birth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    kinship_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    position_category_classification_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    commercial_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    commercial_plan_version_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    plan_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    plan_version_number = table.Column<int>(type: "integer", nullable: false),
                    base_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_per_active_employee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activated_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status_changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_status_reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    current_status_observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    current_status_origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_subscriptions__commercial_plan_versions",
                        column: x => x.commercial_plan_version_id,
                        principalTable: "commercial_plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_subscriptions__commercial_plans",
                        column: x => x.commercial_plan_id,
                        principalTable: "commercial_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "job_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    normalized_title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "company_commercial_addon_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    commercial_addon_id = table.Column<long>(type: "bigint", nullable: false),
                    addon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    addon_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    addon_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    billing_model = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    measurement_unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    minimum_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resulting_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    quantity_basis = table.Column<int>(type: "integer", nullable: false),
                    estimated_next_charge_impact = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_subscription_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_commercial_addon_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_commercial_addon_changes__commercial_addons",
                        column: x => x.commercial_addon_id,
                        principalTable: "commercial_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_commercial_addon_changes__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_commercial_addon_changes__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_commercial_addons",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    commercial_addon_id = table.Column<long>(type: "bigint", nullable: false),
                    addon_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    addon_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    addon_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    billing_model = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    measurement_unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: true),
                    minimum_monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    periodicity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status_effective_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_commercial_addons", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_commercial_addons__commercial_addons",
                        column: x => x.commercial_addon_id,
                        principalTable: "commercial_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_commercial_addons__companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_commercial_addons__company_subscriptions",
                        column: x => x.company_subscription_id,
                        principalTable: "company_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_subscription_plan_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    company_subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    current_commercial_plan_id = table.Column<long>(type: "bigint", nullable: true),
                    current_commercial_plan_version_id = table.Column<long>(type: "bigint", nullable: true),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    occupational_pyramid_level_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    competency_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    behavior_level_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    expected_evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    job_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    work_center_id = table.Column<long>(type: "bigint", nullable: true),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                table: "commercial_plans",
                columns: new[] { "id", "base_monthly_fee", "code", "concurrency_token", "created_utc", "description", "is_system_plan", "modified_utc", "name", "normalized_code", "normalized_name", "price_per_active_employee", "public_id", "status" },
                values: new object[,]
                {
                    { -3002L, 0m, "MASTER", new Guid("00000000-0000-0000-0000-000000000904"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Internal master commercial plan reserved for CLARI operators.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Master", "MASTER", "MASTER", 0m, new Guid("00000000-0000-0000-0000-000000000903"), "Active" },
                    { -3000L, 0m, "FREE", new Guid("00000000-0000-0000-0000-000000000902"), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Public baseline commercial plan used during standard provisioning.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Free", "FREE", "FREE", 0m, new Guid("00000000-0000-0000-0000-000000000901"), "Active" }
                });

            migrationBuilder.InsertData(
                table: "country_catalog",
                columns: new[] { "id", "code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -7247L, "ZW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Zimbabwe", "ZW", new Guid("022c9d49-fcb8-4e3a-e77b-63845c7493d3"), 248 },
                    { -7246L, "ZM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Zambia", "ZM", new Guid("b83fd5fe-f638-5bc6-d39d-e85c4d9f5336"), 247 },
                    { -7245L, "YE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Yemen", "YE", new Guid("37c69f70-1413-caa5-6d37-460bbd6f8e03"), 246 },
                    { -7244L, "EH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Western Sahara", "EH", new Guid("6527fe89-8f31-1113-eb30-88832cb100b6"), 245 },
                    { -7243L, "WF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Wallis & Futuna", "WF", new Guid("f2bb61f4-1763-11f8-f2d5-4b7853bd0c13"), 244 },
                    { -7242L, "VN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vietnam", "VN", new Guid("c7e8ac52-1656-0887-6005-ba817afd45c0"), 243 },
                    { -7241L, "VE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Venezuela", "VE", new Guid("f1a11cd9-75d5-582a-ba81-67a3a27d188d"), 242 },
                    { -7240L, "VA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vatican City", "VA", new Guid("b3469661-a32c-ad29-0d51-36c65c360eed"), 241 },
                    { -7239L, "VU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vanuatu", "VU", new Guid("03a49951-64c7-ad80-7f83-fb8cbf3bb2b6"), 240 },
                    { -7238L, "UZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uzbekistan", "UZ", new Guid("59a3aab8-cfe8-1512-7693-d3014675d2b1"), 239 },
                    { -7237L, "UY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uruguay", "UY", new Guid("d39e353b-22fe-53b6-451d-0d08f552396b"), 238 },
                    { -7236L, "US", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "United States", "US", new Guid("af56fbff-ee04-82cf-8bac-c4072f558afc"), 237 },
                    { -7235L, "GB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "United Kingdom", "GB", new Guid("60ad796b-ee34-9052-b4e3-305bf67ebc97"), 236 },
                    { -7234L, "AE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "United Arab Emirates", "AE", new Guid("58fad332-8a6f-633d-b3bc-81f82aa8d4e1"), 235 },
                    { -7233L, "UA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ukraine", "UA", new Guid("44a23ba6-07cf-6259-258d-77f6cc0cdcf1"), 234 },
                    { -7232L, "UG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uganda", "UG", new Guid("315a1a30-5760-8bf2-c697-3421292ab9cb"), 233 },
                    { -7231L, "VI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "U.S. Virgin Islands", "VI", new Guid("c819a34f-8b92-500f-d725-70aaa1218d0b"), 232 },
                    { -7230L, "UM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "U.S. Outlying Islands", "UM", new Guid("f91f2e86-0c9d-ebb6-c00f-4f1c41c3ff29"), 231 },
                    { -7229L, "TV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tuvalu", "TV", new Guid("eb04a361-fa1d-d08a-14eb-7856d11da3a7"), 230 },
                    { -7228L, "TC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Turks & Caicos Islands", "TC", new Guid("84e11c89-fe22-559c-29e3-ced7efc93bd9"), 229 },
                    { -7227L, "TM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Turkmenistan", "TM", new Guid("82dfb95f-d3bc-564f-1258-b479f19e0772"), 228 },
                    { -7226L, "TR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Turkiye", "TR", new Guid("44eba2fc-da98-7657-9953-85fbc1a69168"), 227 },
                    { -7225L, "TN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tunisia", "TN", new Guid("8a2ebcfd-b09a-9f5a-2a00-0da7b856b5c2"), 226 },
                    { -7224L, "TT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Trinidad & Tobago", "TT", new Guid("3807515e-0bf1-7b32-d0ca-0beb0127348b"), 225 },
                    { -7223L, "TO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tonga", "TO", new Guid("5f814efe-d5ed-71ab-4d6f-32b6b0b6d645"), 224 },
                    { -7222L, "TK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tokelau", "TK", new Guid("5429464e-7436-bcba-03d6-fcaabe5108ad"), 223 },
                    { -7221L, "TG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Togo", "TG", new Guid("dbf7c52c-bf8e-fd7a-951b-4e906255f54b"), 222 },
                    { -7220L, "TL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Timor-Leste", "TL", new Guid("27a8c162-d2af-66dc-0ea3-c3fbc6a89187"), 221 },
                    { -7219L, "TH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Thailand", "TH", new Guid("b8be86e5-b4d3-dc8e-8c31-cb553adfa499"), 220 },
                    { -7218L, "TZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tanzania", "TZ", new Guid("9511fb4c-ee5d-65a6-13df-2a02d1a6649b"), 219 },
                    { -7217L, "TJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tajikistan", "TJ", new Guid("37a7fe4d-fa27-8e2c-3429-b2157279b699"), 218 },
                    { -7216L, "TW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Taiwan", "TW", new Guid("79dfd176-73e3-2b08-2fb8-a2e02b62898b"), 217 },
                    { -7215L, "SY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Syria", "SY", new Guid("05d72557-0d98-8dbf-fde3-e1bd1a41117d"), 216 },
                    { -7214L, "CH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Switzerland", "CH", new Guid("cd6ea7d1-e323-5c1d-7bd1-1e36c2a83c60"), 215 },
                    { -7213L, "SE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sweden", "SE", new Guid("7d62edcf-6448-a72a-e878-23cfb34bee3a"), 214 },
                    { -7212L, "SJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Svalbard & Jan Mayen", "SJ", new Guid("27f5b818-de4d-5b86-3d05-b9f530054e1c"), 213 },
                    { -7211L, "SR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Suriname", "SR", new Guid("5ce65ae1-1bf7-158e-b20c-8622066d68fe"), 212 },
                    { -7210L, "SD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sudan", "SD", new Guid("a38ce262-768d-c3c5-c029-6131e9163a97"), 211 },
                    { -7209L, "VC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Vincent & Grenadines", "VC", new Guid("76b315a7-cd4b-66cf-08b4-1648c291900a"), 210 },
                    { -7208L, "PM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Pierre & Miquelon", "PM", new Guid("e3403429-4e79-e340-5fbc-590142d9284c"), 209 },
                    { -7207L, "MF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Martin", "MF", new Guid("0b3f1a2c-23a7-e957-429a-d0409529bb13"), 208 },
                    { -7206L, "LC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Lucia", "LC", new Guid("5bd0479d-ff63-fe57-805f-9ef8ea577ddb"), 207 },
                    { -7205L, "KN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Kitts & Nevis", "KN", new Guid("9faf016b-a4aa-b36d-4556-4cb24251c784"), 206 },
                    { -7204L, "SH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Helena", "SH", new Guid("375e5361-4184-a18f-cf3a-970f0888f8b4"), 205 },
                    { -7203L, "BL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Barthelemy", "BL", new Guid("c3aeff1f-4632-9a79-44be-708bc95ce680"), 204 },
                    { -7202L, "LK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sri Lanka", "LK", new Guid("b12e7890-ba5a-292c-dcd3-f6d5ba1da61e"), 203 },
                    { -7201L, "ES", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Spain", "ES", new Guid("ef095456-0955-daf2-ce8a-aa4ad018840f"), 202 },
                    { -7200L, "SS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "South Sudan", "SS", new Guid("b6667f6d-617f-5189-cf79-ae6d03930a90"), 201 },
                    { -7199L, "KR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "South Korea", "KR", new Guid("8a518bd6-13c0-f125-f256-61ef2885ad84"), 200 },
                    { -7198L, "ZA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "South Africa", "ZA", new Guid("6f58e54a-97e2-a364-2cde-dd8b83cb2dfb"), 199 },
                    { -7197L, "SO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Somalia", "SO", new Guid("080c5e94-2fd7-4d58-232e-f094d70ca74f"), 198 },
                    { -7196L, "SB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solomon Islands", "SB", new Guid("1584e0cd-9086-7921-2b8f-271a402ec833"), 197 },
                    { -7195L, "SI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Slovenia", "SI", new Guid("7ebf0064-a93b-a100-fc7a-b4cd5d75ff72"), 196 },
                    { -7194L, "SK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Slovakia", "SK", new Guid("b77e4277-bd79-5e35-f2e0-960fc2252e65"), 195 },
                    { -7193L, "SX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sint Maarten", "SX", new Guid("495b8c39-ddbc-f112-e40c-b5cdd1e0f142"), 194 },
                    { -7192L, "SG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Singapore", "SG", new Guid("5dec3eb7-7e7e-813e-e5ad-4e74cc56fee7"), 193 },
                    { -7191L, "SL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sierra Leone", "SL", new Guid("71f0413b-892c-a93a-f5a2-31ddb66f5405"), 192 },
                    { -7190L, "SC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Seychelles", "SC", new Guid("b9ba3d79-d394-b7f1-4cf2-af65d36eedad"), 191 },
                    { -7189L, "RS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Serbia", "RS", new Guid("62d59937-9d31-715c-f823-e99ffc70634e"), 190 },
                    { -7188L, "SN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Senegal", "SN", new Guid("dd86a979-ac0e-059d-be9f-9b7e7c51ce18"), 189 },
                    { -7187L, "SA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Saudi Arabia", "SA", new Guid("ea2d42a1-3c84-2f3f-bac4-e409394fd5ea"), 188 },
                    { -7186L, "ST", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sao Tome & Principe", "ST", new Guid("8ff16f3f-1a83-5660-1fcc-9ab82168e5d4"), 187 },
                    { -7185L, "SM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Marino", "SM", new Guid("c892de95-5332-b228-1c09-4fbadd235334"), 186 },
                    { -7184L, "WS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Samoa", "WS", new Guid("664d3de2-eb5d-cfa1-68e1-2825bdf0beab"), 185 },
                    { -7183L, "RW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rwanda", "RW", new Guid("2ef40af2-0082-b7b8-c749-0534f7f7d21f"), 184 },
                    { -7182L, "RU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Russia", "RU", new Guid("db6ee5d5-db93-2c34-87f6-c083ffc821c4"), 183 },
                    { -7181L, "RO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Romania", "RO", new Guid("6a2d39db-3784-2336-43aa-47e93e0edc7d"), 182 },
                    { -7180L, "RE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reunion", "RE", new Guid("7e3d71f7-0521-58b2-91bf-c5923eaf6358"), 181 },
                    { -7179L, "QA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Qatar", "QA", new Guid("d7b9938d-eb8f-4f70-c367-1d3cabe6cdc7"), 180 },
                    { -7178L, "PR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Puerto Rico", "PR", new Guid("d5a35c59-2a2f-981a-2ee2-aef2c2b9d9d2"), 179 },
                    { -7177L, "PT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Portugal", "PT", new Guid("9560f2d6-b29a-c698-e257-3cfa77d42d7f"), 178 },
                    { -7176L, "PL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Poland", "PL", new Guid("32fd250c-88fb-2bad-9dd8-82e11578c30c"), 177 },
                    { -7175L, "PN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pitcairn Islands", "PN", new Guid("37626e54-6b16-b71e-6b76-6f05e450fd69"), 176 },
                    { -7174L, "PH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Philippines", "PH", new Guid("91bc4173-a392-bc81-e2e7-cd8e7d137ff7"), 175 },
                    { -7173L, "PE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Peru", "PE", new Guid("2f47b218-6dd5-1069-5a91-63e9f8485f55"), 174 },
                    { -7172L, "PY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Paraguay", "PY", new Guid("ef68c7af-643a-bc73-06fc-780aaf840297"), 173 },
                    { -7171L, "PG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Papua New Guinea", "PG", new Guid("f1c3da66-dce0-4540-3f9e-5331926f375a"), 172 },
                    { -7170L, "PA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Panama", "PA", new Guid("0d67b596-1155-c434-c68c-1c614ee5f6ce"), 171 },
                    { -7169L, "PS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Palestinian Territories", "PS", new Guid("abeb336b-8fe8-4241-30d3-be1debf4676e"), 170 },
                    { -7168L, "PW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Palau", "PW", new Guid("6a451257-2888-8cf3-4183-e262679403c5"), 169 },
                    { -7167L, "PK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pakistan", "PK", new Guid("a9d97769-b011-af8d-49e6-13c41edd7090"), 168 },
                    { -7166L, "OM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Oman", "OM", new Guid("96ee6337-ae78-241d-0eb2-4e25329917f5"), 167 },
                    { -7165L, "NO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Norway", "NO", new Guid("67c0cc58-f3fd-c23a-60cd-413a4706a803"), 166 },
                    { -7164L, "MP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Northern Mariana Islands", "MP", new Guid("eb7b0de3-b138-2a94-68db-04e7cef3b66a"), 165 },
                    { -7163L, "MK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "North Macedonia", "MK", new Guid("617fff95-9090-1613-e9d8-01c5b8741031"), 164 },
                    { -7162L, "KP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "North Korea", "KP", new Guid("f3f8aed8-0a5a-6fd1-361d-fc363b5f141a"), 163 },
                    { -7161L, "NF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Norfolk Island", "NF", new Guid("b6304adc-e062-7446-d90d-12f035ba46f8"), 162 },
                    { -7160L, "NU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Niue", "NU", new Guid("10b0c5aa-66ae-62cc-27a1-5527c65c0417"), 161 },
                    { -7159L, "NG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nigeria", "NG", new Guid("1f030f27-2949-5f10-dfab-780caa48f617"), 160 },
                    { -7158L, "NE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Niger", "NE", new Guid("9a6b6c51-3850-a3b9-eb76-09e8ab129706"), 159 },
                    { -7157L, "NI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nicaragua", "NI", new Guid("6f59daa9-637e-5a46-c17c-73736b3506f1"), 158 },
                    { -7156L, "NZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "New Zealand", "NZ", new Guid("ab1a3a65-a5d1-fad5-0643-031c7bcafd9f"), 157 },
                    { -7155L, "NC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "New Caledonia", "NC", new Guid("d5e61089-97fb-4ec5-f098-0bd4597b3ce1"), 156 },
                    { -7154L, "NL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Netherlands", "NL", new Guid("d83b98f6-c017-70f7-4b68-68b8418f43bc"), 155 },
                    { -7153L, "NP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nepal", "NP", new Guid("c0679401-a6bd-4101-152d-c6ef469133e4"), 154 },
                    { -7152L, "NR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nauru", "NR", new Guid("99c05900-e5dc-70fa-0ef7-6ceaed568089"), 153 },
                    { -7151L, "NA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Namibia", "NA", new Guid("858df66c-b361-2819-8802-3ab74b37b9cc"), 152 },
                    { -7150L, "MM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Myanmar (Burma)", "MM", new Guid("f7967348-9746-04a9-76f9-ba88f226b2b6"), 151 },
                    { -7149L, "MZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mozambique", "MZ", new Guid("6f3daffd-b673-675b-7a30-f64901f40b24"), 150 },
                    { -7148L, "MA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Morocco", "MA", new Guid("e80f188a-4cea-1bb7-cb55-4f824f98ed10"), 149 },
                    { -7147L, "MS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Montserrat", "MS", new Guid("7740e417-92eb-ae25-e8cf-01225d30535d"), 148 },
                    { -7146L, "ME", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Montenegro", "ME", new Guid("870c7c16-9bd4-85f5-941a-26e0a9708c52"), 147 },
                    { -7145L, "MN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mongolia", "MN", new Guid("be4f8568-44b9-226b-bd4b-d8d647e7f4fe"), 146 },
                    { -7144L, "MC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Monaco", "MC", new Guid("4d2c4419-1e4a-1eed-eaf3-a3e16e7892a4"), 145 },
                    { -7143L, "MD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Moldova", "MD", new Guid("b00996f2-57e4-5bf2-65a4-731fa11c3fe9"), 144 },
                    { -7142L, "FM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Micronesia", "FM", new Guid("78600692-c16e-5dfa-bea9-af1dbd8f8d27"), 143 },
                    { -7141L, "MX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mexico", "MX", new Guid("5be4674e-3070-8f4a-e419-581492b3f677"), 142 },
                    { -7140L, "YT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mayotte", "YT", new Guid("4b156b57-a367-cd0a-21ef-a19f858c2c67"), 141 },
                    { -7139L, "MU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mauritius", "MU", new Guid("2d6eff06-886f-6070-021d-2b50c03f4242"), 140 },
                    { -7138L, "MR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mauritania", "MR", new Guid("db02be6d-1850-b1da-3072-c766048f55fc"), 139 },
                    { -7137L, "MQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Martinique", "MQ", new Guid("d865861d-7907-fc33-d0b0-ef84fbf80187"), 138 },
                    { -7136L, "MH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Marshall Islands", "MH", new Guid("de1be22e-699e-4c44-ef74-914cfbd2365b"), 137 },
                    { -7135L, "MT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Malta", "MT", new Guid("d4f78625-a77f-39f5-fa31-74775eeda196"), 136 },
                    { -7134L, "ML", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mali", "ML", new Guid("314cda6e-2a8e-cb02-d0d1-6a1963c97617"), 135 },
                    { -7133L, "MV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Maldives", "MV", new Guid("b9c63305-7f84-9ca5-0dda-05ef8f5eea29"), 134 },
                    { -7132L, "MY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Malaysia", "MY", new Guid("2e2d4851-fd73-d29c-28c5-e5ff75e5119b"), 133 },
                    { -7131L, "MW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Malawi", "MW", new Guid("37e43d4a-47cb-9628-ebc5-557e730c4580"), 132 },
                    { -7130L, "MG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Madagascar", "MG", new Guid("9c4a6c26-c836-e0d3-141f-23246f49281b"), 131 },
                    { -7129L, "MO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Macao", "MO", new Guid("41cdcd74-127c-041e-b48f-7dc738909f8a"), 130 },
                    { -7128L, "LU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Luxembourg", "LU", new Guid("d0c3f7f5-8f97-7100-1f73-80b8bd2cbb1d"), 129 },
                    { -7127L, "LT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lithuania", "LT", new Guid("7f7047aa-965e-aee0-ff82-47c1e635d69c"), 128 },
                    { -7126L, "LI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Liechtenstein", "LI", new Guid("f7d157bb-424a-21e3-a58b-8d0941732f60"), 127 },
                    { -7125L, "LY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Libya", "LY", new Guid("98c7ea84-d918-b010-f00c-9569b2d68515"), 126 },
                    { -7124L, "LR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Liberia", "LR", new Guid("510bc644-31fd-39c3-9393-71e2a4263320"), 125 },
                    { -7123L, "LS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lesotho", "LS", new Guid("d90091c6-3389-33b1-2a14-a6e73f158dd4"), 124 },
                    { -7122L, "LB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lebanon", "LB", new Guid("39b70154-5d39-80e3-ea46-b517f24d620e"), 123 },
                    { -7121L, "LV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Latvia", "LV", new Guid("4ae5e72a-18a5-eb7f-4490-354410c6229a"), 122 },
                    { -7120L, "LA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Laos", "LA", new Guid("6d8a5d5a-328c-4e19-23e4-9e18eb9e39eb"), 121 },
                    { -7119L, "KG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kyrgyzstan", "KG", new Guid("8814f8a6-af1d-f779-6396-f3035203e058"), 120 },
                    { -7118L, "KW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kuwait", "KW", new Guid("aa329426-23b8-e80b-5e00-f0ff9efc51cc"), 119 },
                    { -7117L, "XK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kosovo", "XK", new Guid("6a15bb42-194a-7045-b828-0973a9c9a358"), 118 },
                    { -7116L, "KI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kiribati", "KI", new Guid("232dcc72-ed0f-efab-9bb8-c169b9a10540"), 117 },
                    { -7115L, "KE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kenya", "KE", new Guid("064c0873-f8d4-83c5-4be1-3da695efb6d3"), 116 },
                    { -7114L, "KZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kazakhstan", "KZ", new Guid("83caf43f-ae35-9ccb-b7ad-8c06a7e98bab"), 115 },
                    { -7113L, "JO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jordan", "JO", new Guid("abd3ffe4-e97a-e7d7-727c-b728cdc12fa3"), 114 },
                    { -7112L, "JE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jersey", "JE", new Guid("471ca971-ef85-a712-563f-d6562f130ebf"), 113 },
                    { -7111L, "JP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Japan", "JP", new Guid("a51e6d29-d04b-be74-ee98-018d6a619164"), 112 },
                    { -7110L, "JM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jamaica", "JM", new Guid("39d2a465-4d54-68f4-171c-2edcc2e84345"), 111 },
                    { -7109L, "IT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Italy", "IT", new Guid("d3dcf854-860f-a98c-7f6b-5a95a300af8e"), 110 },
                    { -7108L, "IL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Israel", "IL", new Guid("a9d7725f-49d6-3d42-b7a3-a9d61c6215cb"), 109 },
                    { -7107L, "IM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Isle of Man", "IM", new Guid("f3d4b455-29a7-bbb6-9a77-0cd7b309d3c5"), 108 },
                    { -7106L, "IE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ireland", "IE", new Guid("900083c4-34da-c680-c91b-dd2b8bb74ae0"), 107 },
                    { -7105L, "IQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Iraq", "IQ", new Guid("9a9dbaa9-f5e1-7de6-eb1a-2c3d06eb79cf"), 106 },
                    { -7104L, "IR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Iran", "IR", new Guid("d69b89cf-8a4a-d5e1-ab57-6cab8c832ebb"), 105 },
                    { -7103L, "ID", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Indonesia", "ID", new Guid("dc0bb2af-b11e-3022-007f-4f4c53f9608c"), 104 },
                    { -7102L, "IN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "India", "IN", new Guid("ed3b489a-f4ba-6067-66c6-9185c7bf7c4e"), 103 },
                    { -7101L, "IS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Iceland", "IS", new Guid("c018cb0d-b504-f979-c651-1733f820a7ee"), 102 },
                    { -7100L, "HU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hungary", "HU", new Guid("4d0bf9bc-7cd7-e174-656f-daa19d029d4d"), 101 },
                    { -7099L, "HK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hong Kong", "HK", new Guid("1dbb46b3-cc6e-e0df-ddc4-1d4b728665e0"), 100 },
                    { -7098L, "HN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Honduras", "HN", new Guid("a2a1eb51-efaf-0566-b881-a073235f7101"), 99 },
                    { -7097L, "HT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Haiti", "HT", new Guid("eb8a0344-81a3-76d2-8060-4f4b0d40adbd"), 98 },
                    { -7096L, "GY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guyana", "GY", new Guid("29495857-ec7e-8bf5-45c4-a48b43fa25f3"), 97 },
                    { -7095L, "GW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guinea-Bissau", "GW", new Guid("ca14c038-8b91-f16c-9249-b46a1d7fe9da"), 96 },
                    { -7094L, "GN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guinea", "GN", new Guid("776fa913-186f-a9d2-7c35-859151496aac"), 95 },
                    { -7093L, "GG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guernsey", "GG", new Guid("1c947fc9-e355-9b7c-3579-d1895d96d7f3"), 94 },
                    { -7092L, "GT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guatemala", "GT", new Guid("4482fc22-baef-a84d-1d72-83c42322b3b1"), 93 },
                    { -7091L, "GU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guam", "GU", new Guid("af3c2cff-a1b0-ae02-2833-663e2f348c5d"), 92 },
                    { -7090L, "GP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guadeloupe", "GP", new Guid("c09d83b1-3c03-63af-1247-94f7835c9990"), 91 },
                    { -7089L, "GD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Grenada", "GD", new Guid("b28b1ac7-8e6f-cbae-1199-a2bc71e250fc"), 90 },
                    { -7088L, "GL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Greenland", "GL", new Guid("c79d7f83-9ed5-e0e4-df81-ca6ebbb723c4"), 89 },
                    { -7087L, "GR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Greece", "GR", new Guid("32428479-fc1a-9a33-59f8-d7c7ebcc5f05"), 88 },
                    { -7086L, "GI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gibraltar", "GI", new Guid("9cab0e17-9897-0491-ccf8-481a165e416c"), 87 },
                    { -7085L, "GH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ghana", "GH", new Guid("ecd3da8b-41a2-3105-938b-886bae0d3dc6"), 86 },
                    { -7084L, "DE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Germany", "DE", new Guid("ea3af316-0ea3-c13d-04a7-507b31eb4d0b"), 85 },
                    { -7083L, "GE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Georgia", "GE", new Guid("3ffdddc6-a2e1-4305-f6f8-e21e3dedbbb4"), 84 },
                    { -7082L, "GM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gambia", "GM", new Guid("fc03ffc2-8a54-e808-b8f0-3af6717142d3"), 83 },
                    { -7081L, "GA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gabon", "GA", new Guid("6ec32a47-29bf-1fd9-f7c6-bd803b1a816d"), 82 },
                    { -7080L, "PF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "French Polynesia", "PF", new Guid("4171cae2-7b91-d4eb-3541-a9909278b5b9"), 81 },
                    { -7079L, "GF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "French Guiana", "GF", new Guid("56119d01-3df0-cff8-c26a-fe5d9fd2ef73"), 80 },
                    { -7078L, "FR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "France", "FR", new Guid("91563e58-8854-b169-81a1-d2a260a0dbc3"), 79 },
                    { -7077L, "FI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Finland", "FI", new Guid("26118f54-8d67-3b1e-8dfe-8b5d93c5893a"), 78 },
                    { -7076L, "FJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fiji", "FJ", new Guid("99960e4d-f1d1-9cfe-6cbd-5de518832d28"), 77 },
                    { -7075L, "FO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Faroe Islands", "FO", new Guid("af8b3ba6-8ce1-52e1-2636-090779e038c6"), 76 },
                    { -7074L, "FK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Falkland Islands", "FK", new Guid("e4c795f1-e00c-a2fc-185a-cd159cdbedce"), 75 },
                    { -7073L, "ET", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ethiopia", "ET", new Guid("5f070476-4120-7691-1819-fb5048ecbff3"), 74 },
                    { -7072L, "SZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Eswatini", "SZ", new Guid("db65e4da-5f02-2a69-fd70-a685711f7fa4"), 73 },
                    { -7071L, "EE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Estonia", "EE", new Guid("cc5ee3f4-2ec3-f96a-11f3-aad1ed2c41c1"), 72 },
                    { -7070L, "ER", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Eritrea", "ER", new Guid("2376e024-f591-3403-a3e5-23af09b3b119"), 71 },
                    { -7069L, "GQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Equatorial Guinea", "GQ", new Guid("a33009f6-12f2-9c31-79ed-9a541f31db0f"), 70 },
                    { -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "El Salvador", "SV", new Guid("4ba8c8cd-d7b0-361f-ef2a-f63e44b04c7b"), 69 },
                    { -7067L, "EG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Egypt", "EG", new Guid("e27a22db-d2f7-f9a6-dd44-2f14ed840ec6"), 68 },
                    { -7066L, "EC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ecuador", "EC", new Guid("ca935f5d-0e9c-fe6c-1030-09af1bc09174"), 67 },
                    { -7065L, "DO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dominican Republic", "DO", new Guid("3edd9881-c754-74bc-7a08-6c5e47ca96c4"), 66 },
                    { -7064L, "DM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dominica", "DM", new Guid("65fe830a-bb79-cf28-21b8-0936b4cd7056"), 65 },
                    { -7063L, "DJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Djibouti", "DJ", new Guid("2652a191-e94f-e20e-6bd5-cb81d3e1457f"), 64 },
                    { -7062L, "DG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Diego Garcia", "DG", new Guid("9e08cef7-41a2-411d-70a1-53dbf5b9b4d3"), 63 },
                    { -7061L, "DK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Denmark", "DK", new Guid("f3c744aa-049a-8935-8251-4a14d2a47ed0"), 62 },
                    { -7060L, "CZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Czechia", "CZ", new Guid("a28e8926-b5ef-0da3-319b-83a0f6b9e63b"), 61 },
                    { -7059L, "CY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cyprus", "CY", new Guid("4b7207f5-3f84-773c-d9f5-8dbd0be1805b"), 60 },
                    { -7058L, "CW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Curacao", "CW", new Guid("2a9b1919-4608-5b6b-6b06-b808c3108d33"), 59 },
                    { -7057L, "CU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuba", "CU", new Guid("82767b58-6830-93fe-4aba-9b54796eae55"), 58 },
                    { -7056L, "HR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Croatia", "HR", new Guid("f284c98e-ab4a-ce97-60db-e651034cdf00"), 57 },
                    { -7055L, "CI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cote d'Ivoire", "CI", new Guid("b6b3b6b8-e327-cbb5-10f7-25854e86d052"), 56 },
                    { -7054L, "CR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Costa Rica", "CR", new Guid("708cbe5a-67a4-2b1d-b321-7789fc21e493"), 55 },
                    { -7053L, "CK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cook Islands", "CK", new Guid("f960e81e-2888-aef3-ac43-8ee1dd79abd7"), 54 },
                    { -7052L, "CD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Congo - Kinshasa", "CD", new Guid("8f95006a-4b4c-e0b8-95de-151c421d5018"), 53 },
                    { -7051L, "CG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Congo - Brazzaville", "CG", new Guid("67159e77-f91f-eaf9-f588-84011dc45d10"), 52 },
                    { -7050L, "KM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Comoros", "KM", new Guid("450ec4f9-9473-02f3-ea84-33f326c401c7"), 51 },
                    { -7049L, "CO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Colombia", "CO", new Guid("faed71bb-e976-55ae-67ae-a30e2a0341b7"), 50 },
                    { -7048L, "CC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cocos (Keeling) Islands", "CC", new Guid("58d3c78b-4c04-9032-94f0-1acc161291a0"), 49 },
                    { -7047L, "CX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Christmas Island", "CX", new Guid("d8160a33-93f3-6337-8d40-43c8b166ddd4"), 48 },
                    { -7046L, "CN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "China mainland", "CN", new Guid("b0a78491-9f7d-9f17-9013-7ba2f818cc08"), 47 },
                    { -7045L, "CL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chile", "CL", new Guid("a9c17e33-89cf-8aa9-d450-d34688cdb0ff"), 46 },
                    { -7044L, "IO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chagos Archipelago", "IO", new Guid("16c8e523-4be0-c8f1-a247-1dcd200944ec"), 45 },
                    { -7043L, "TD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chad", "TD", new Guid("12e2d434-ff12-9251-28df-04cd07d98821"), 44 },
                    { -7042L, "EA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ceuta & Melilla", "EA", new Guid("e672f9d7-c555-001d-7ef2-fbe1711523ba"), 43 },
                    { -7041L, "CF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Central African Republic", "CF", new Guid("8c66b86c-4cad-ca0b-c1c7-86d1cbffaa87"), 42 },
                    { -7040L, "KY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cayman Islands", "KY", new Guid("9330af41-eb0f-73bb-97b6-a7b8474d2557"), 41 },
                    { -7039L, "BQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Caribbean Netherlands", "BQ", new Guid("893fa715-3ae9-9d5c-9582-7d5e6394ebc0"), 40 },
                    { -7038L, "CV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cape Verde", "CV", new Guid("772ddeff-bb5e-3228-7e90-dfdded3df25d"), 39 },
                    { -7037L, "IC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Canary Islands", "IC", new Guid("10461941-f9c5-f547-4f84-62c79b31b59f"), 38 },
                    { -7036L, "CA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Canada", "CA", new Guid("3d3f0f1a-571b-8ebb-f2e1-82a6f8cdfb67"), 37 },
                    { -7035L, "CM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cameroon", "CM", new Guid("a0842837-2e57-cee4-80cf-3736a2043f21"), 36 },
                    { -7034L, "KH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cambodia", "KH", new Guid("abf92bf5-df28-6a14-a547-5fecdaaf45bd"), 35 },
                    { -7033L, "BI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Burundi", "BI", new Guid("f821e3b8-fbc5-5491-a1e4-28168bfab8b8"), 34 },
                    { -7032L, "BF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Burkina Faso", "BF", new Guid("1bb71f5e-8652-878f-ef0f-e124fbf5d659"), 33 },
                    { -7031L, "BG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bulgaria", "BG", new Guid("94fbfc68-3efd-47fc-4cb3-f9dcdac24d31"), 32 },
                    { -7030L, "BN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Brunei", "BN", new Guid("4ff347de-75b9-e25b-53ef-67a4a58af95c"), 31 },
                    { -7029L, "VG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "British Virgin Islands", "VG", new Guid("6b808b22-3ae6-bf1e-edc3-590693b37f75"), 30 },
                    { -7028L, "BR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Brazil", "BR", new Guid("3639c8c5-5018-eb39-2dd3-b9ad5d287790"), 29 },
                    { -7027L, "BW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Botswana", "BW", new Guid("8b691019-abd0-a23d-8679-65dcb0a98b65"), 28 },
                    { -7026L, "BA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bosnia & Herzegovina", "BA", new Guid("e111f756-9145-291e-360e-d06e4d7fb429"), 27 },
                    { -7025L, "BO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bolivia", "BO", new Guid("8943ebd2-bc23-1b64-c031-3f28398efb6d"), 26 },
                    { -7024L, "BT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bhutan", "BT", new Guid("d2219e2c-3d83-d6e2-3189-11b56b7abb04"), 25 },
                    { -7023L, "BM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bermuda", "BM", new Guid("15a98680-9859-c663-0f1b-d659e9d54dd0"), 24 },
                    { -7022L, "BJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Benin", "BJ", new Guid("85664618-e24e-bfdf-5ca3-665da6822633"), 23 },
                    { -7021L, "BZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Belize", "BZ", new Guid("57a7f3d9-bf2f-28f4-103f-6909846ba75d"), 22 },
                    { -7020L, "BE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Belgium", "BE", new Guid("ff505ce8-e127-82c5-0f90-721f627c70f2"), 21 },
                    { -7019L, "BY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Belarus", "BY", new Guid("1249c7c9-ae9a-4a65-5cfb-821e3fb45bba"), 20 },
                    { -7018L, "BB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Barbados", "BB", new Guid("f6b312b1-a7d1-2cf4-ff53-8726cff0cf64"), 19 },
                    { -7017L, "BD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bangladesh", "BD", new Guid("8dbad947-a9b8-c3bf-b755-5c52dd6cadfa"), 18 },
                    { -7016L, "BH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bahrain", "BH", new Guid("917e476f-7803-5631-ec88-7e0715073b29"), 17 },
                    { -7015L, "BS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bahamas", "BS", new Guid("ebd638b9-d7ed-fcb8-2c32-170e09da380d"), 16 },
                    { -7014L, "AZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Azerbaijan", "AZ", new Guid("b480c305-4f92-cbef-41db-96d902b4cb97"), 15 },
                    { -7013L, "AT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Austria", "AT", new Guid("1a0e768f-ac25-728e-dfce-e5fdb1a4f311"), 14 },
                    { -7012L, "AU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Australia", "AU", new Guid("93f3885e-c4f5-f8ab-ede0-12b60a9e9cfc"), 13 },
                    { -7011L, "AW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aruba", "AW", new Guid("3affeadc-f404-2a8b-c578-3f21f8609415"), 12 },
                    { -7010L, "AM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Armenia", "AM", new Guid("eb440fc9-a8b6-f042-758d-ab67c84ac3ec"), 11 },
                    { -7009L, "AR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Argentina", "AR", new Guid("3b97a9ae-6d22-54f2-fb78-646e120af0b8"), 10 },
                    { -7008L, "AG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Antigua & Barbuda", "AG", new Guid("c4fb9808-a455-9e96-a219-ae6535783137"), 9 },
                    { -7007L, "AI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anguilla", "AI", new Guid("5879810a-56c5-cbd3-c649-0cc715c4709b"), 8 },
                    { -7006L, "AO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Angola", "AO", new Guid("fab95c4c-a5db-e7ff-e4c9-5c7e21af0b06"), 7 },
                    { -7005L, "AD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Andorra", "AD", new Guid("82ee27ea-1466-0637-1784-67408c139f54"), 6 },
                    { -7004L, "AS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "American Samoa", "AS", new Guid("6c86b297-d55d-62fc-d4d6-3f767625cef8"), 5 },
                    { -7003L, "DZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Algeria", "DZ", new Guid("c9513617-54cf-0086-222b-e5110d253ff5"), 4 },
                    { -7002L, "AL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Albania", "AL", new Guid("7131e6e0-1257-eba1-832d-7d6b9b715ed5"), 3 },
                    { -7001L, "AX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aland Islands", "AX", new Guid("d0a9cf43-51b0-4caa-c7ae-cc80188aebfe"), 2 },
                    { -7000L, "AF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Afghanistan", "AF", new Guid("2930bd1e-9301-5047-e2b2-306f2de35755"), 1 }
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
                table: "legal_representative_position_title_catalog",
                columns: new[] { "id", "code", "is_active", "name", "normalized_code", "public_id", "sort_order" },
                values: new object[,]
                {
                    { 1L, "OWNER", true, "OWNER", "OWNER", new Guid("0ef310ac-b7ff-c89a-aec7-9c2c03678981"), 1 },
                    { 2L, "CEO", true, "CEO", "CEO", new Guid("84ba6abe-edc9-f364-7141-a54f67419938"), 2 },
                    { 3L, "EXECUTIVE_MANAGEMENT", true, "Executive Management", "EXECUTIVE_MANAGEMENT", new Guid("f83e1ed4-e95a-a367-0204-ab3c6f0f6cce"), 3 },
                    { 4L, "HUMAN_RESOURCES", true, "Human Resources", "HUMAN_RESOURCES", new Guid("b0fccf41-39a5-7933-7678-69cdbcfbf154"), 4 },
                    { 5L, "FINANCE", true, "Finance", "FINANCE", new Guid("91fb9fb2-07aa-fff8-ed96-326fe9edebec"), 5 },
                    { 6L, "ACCOUNTING", true, "Accounting", "ACCOUNTING", new Guid("8b2df04d-d57c-1767-7971-841b114df552"), 6 },
                    { 7L, "OPERATIONS", true, "Operations", "OPERATIONS", new Guid("6aa22f66-30bc-76d6-c0e8-bd5d2db06ca4"), 7 },
                    { 8L, "PROCUREMENT", true, "Procurement", "PROCUREMENT", new Guid("c21b0284-f48b-9c94-9bf2-43ab39f2ef91"), 8 },
                    { 9L, "SALES", true, "Sales", "SALES", new Guid("beeaa4e2-8044-e91c-a4d8-859c8cf8eaab"), 9 },
                    { 10L, "MARKETING", true, "Marketing", "MARKETING", new Guid("cd8738ec-d87f-57a3-8780-2f8b1f99329a"), 10 },
                    { 11L, "CUSTOMER_SERVICE", true, "Customer Service", "CUSTOMER_SERVICE", new Guid("1ad62c2f-b983-6ef1-ebca-414eb1d39d39"), 11 },
                    { 12L, "INFORMATION_TECHNOLOGY", true, "Information Technology", "INFORMATION_TECHNOLOGY", new Guid("b1fad938-a892-43f7-aff4-425b4fe28594"), 12 },
                    { 13L, "SOFTWARE_DEVELOPMENT", true, "Software Development", "SOFTWARE_DEVELOPMENT", new Guid("a63b76cf-822f-f56a-ea0b-e84bc3ba050e"), 13 },
                    { 14L, "INFRASTRUCTURE_DEVOPS", true, "Infrastructure / DevOps", "INFRASTRUCTURE_DEVOPS", new Guid("d4bbe59d-4864-d1f2-3e5e-99c6202050c7"), 14 },
                    { 15L, "DATA_ANALYTICS", true, "Data & Analytics", "DATA_ANALYTICS", new Guid("d47b9164-cdb9-43c8-0643-64b8474f176e"), 15 },
                    { 16L, "LEGAL", true, "Legal", "LEGAL", new Guid("49cd1563-df08-7f86-5e8b-990b38d6cbb4"), 16 },
                    { 17L, "ADMINISTRATION", true, "Administration", "ADMINISTRATION", new Guid("7560e5ba-619e-8b09-0fc6-98728793bf9f"), 17 },
                    { 18L, "LOGISTICS", true, "Logistics", "LOGISTICS", new Guid("e5648d21-1f5a-db49-7979-a69836b809a2"), 18 },
                    { 19L, "MAINTENANCE", true, "Maintenance", "MAINTENANCE", new Guid("d7281db7-26ca-1868-691b-24a49d576690"), 19 },
                    { 20L, "SECURITY", true, "Security", "SECURITY", new Guid("23e24f56-f387-32f9-4a27-f48ad594f919"), 20 }
                });

            migrationBuilder.InsertData(
                table: "legal_representative_representation_type_catalog",
                columns: new[] { "id", "code", "is_active", "name", "normalized_code", "public_id", "sort_order" },
                values: new object[,]
                {
                    { 1L, "PRIMARYLEGALREPRESENTATIVE", true, "Primary Legal Representative", "PRIMARYLEGALREPRESENTATIVE", new Guid("f116b8b0-fbec-26f9-e0d6-bef155566cb7"), 1 },
                    { 2L, "ALTERNATELEGALREPRESENTATIVE", true, "Alternate Legal Representative", "ALTERNATELEGALREPRESENTATIVE", new Guid("6d1b562e-7aa2-3cfd-1d9e-03cf11122280"), 2 },
                    { 3L, "ATTORNEYINFACT", true, "Attorney in Fact", "ATTORNEYINFACT", new Guid("a5debeb2-9e02-76b8-4a0c-02c1d3e4881f"), 3 }
                });

            migrationBuilder.InsertData(
                table: "commercial_plan_versions",
                columns: new[] { "id", "base_monthly_fee", "commercial_plan_id", "created_utc", "currency_code", "effective_from_utc", "effective_to_utc", "modified_utc", "price_per_active_employee", "public_id", "version_number" },
                values: new object[,]
                {
                    { -3003L, 0m, -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "USD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), 0m, new Guid("552d115b-6eb1-044d-900a-6d1e339b96aa"), 1 },
                    { -3001L, 0m, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "USD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), 0m, new Guid("cf0c879c-d6c7-3d5d-d1cf-903ef0f66cfb"), 1 }
                });

            migrationBuilder.InsertData(
                table: "company_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "created_utc", "description", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -8304L, "NONPROFIT", new Guid("11d194d5-88b0-f5db-2da7-f9e72c7adb74"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Corporation organized for charitable, educational or public benefit purposes.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nonprofit Corporation", "NONPROFIT", "NONPROFIT CORPORATION", new Guid("350cd327-f33f-af8b-e729-383190f047b1"), 50 },
                    { -8303L, "LLP", new Guid("bad183d7-1cf7-e216-8220-b00e198d670b"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Partnership structure with liability protections for partners.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Limited Liability Partnership", "LLP", "LIMITED LIABILITY PARTNERSHIP", new Guid("0de7547f-7824-3f09-6821-7fdfcc22477b"), 40 },
                    { -8302L, "S_CORP", new Guid("4bdefd1e-0249-a89c-023f-b956c863cdb9"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Corporation with pass-through taxation under eligible IRS election.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "S Corporation", "S_CORP", "S CORPORATION", new Guid("0e5fc91d-4c0e-7a38-d572-8516a155f03a"), 30 },
                    { -8301L, "C_CORP", new Guid("da04091d-e96d-caf0-b76e-73c725a497c9"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Corporation taxed separately from its owners.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "C Corporation", "C_CORP", "C CORPORATION", new Guid("62b93007-f025-adc5-72f4-69b799c2203c"), 20 },
                    { -8300L, "LLC", new Guid("03e6f071-4107-7745-f627-1a0f90c33c65"), -7236L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Business entity with limited liability and flexible tax treatment.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Limited Liability Company", "LLC", "LIMITED LIABILITY COMPANY", new Guid("513c0578-fbc7-efab-e7ff-20b5d01ce120"), 10 },
                    { -8204L, "AC", new Guid("c59684b9-4cf3-e0b9-84dc-38ae1f338a16"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Persona moral de naturaleza civil sin fines preponderantemente mercantiles.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asociacion Civil", "AC", "ASOCIACION CIVIL", new Guid("9f23ad01-a794-6393-8bc9-1bd6559027f8"), 50 },
                    { -8203L, "BRANCH_OFFICE", new Guid("dfa6068a-714b-9622-4fb8-8ebd241cc116"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Establecimiento mexicano dependiente de una sociedad matriz.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sucursal", "BRANCH_OFFICE", "SUCURSAL", new Guid("f6c55351-b07a-b645-8b99-ba58cdc405c8"), 40 },
                    { -8202L, "SAS", new Guid("1ff2a0b0-b0ed-fc81-38aa-8b5a040b5a5e"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil simplificada constituida por accionistas.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad por Acciones Simplificada", "SAS", "SOCIEDAD POR ACCIONES SIMPLIFICADA", new Guid("21361a52-175f-7753-0ae3-003a84fd8d76"), 30 },
                    { -8201L, "S_DE_RL_DE_CV", new Guid("53591402-f147-48e3-4505-d023a0d0dda2"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil mexicana de partes sociales con responsabilidad limitada.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad de Responsabilidad Limitada de Capital Variable", "S_DE_RL_DE_CV", "SOCIEDAD DE RESPONSABILIDAD LIMITADA DE CAPITAL VARIABLE", new Guid("f5a185f4-a5ae-b48e-2940-b57eb0822911"), 20 },
                    { -8200L, "SA_DE_CV", new Guid("865c1575-f03a-c4d4-47a8-a532978fccfc"), -7141L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil mexicana con capital representado en acciones.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad Anonima de Capital Variable", "SA_DE_CV", "SOCIEDAD ANONIMA DE CAPITAL VARIABLE", new Guid("8f6b5423-1e9d-8eeb-0578-4c3597d2b3a5"), 10 },
                    { -8104L, "ASSOCIATION", new Guid("db0f00d6-3720-c452-56c9-af138d276891"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entidad asociativa sin fines de lucro reconocida legalmente.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asociacion", "ASSOCIATION", "ASOCIACION", new Guid("a36cd3b2-0ebe-a377-7d58-470a6ec059ae"), 50 },
                    { -8103L, "COOPERATIVE", new Guid("b61c2e4a-d3ce-c341-c7bb-170f378c5828"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Entidad asociativa organizada bajo el regimen cooperativo.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cooperativa", "COOPERATIVE", "COOPERATIVA", new Guid("ea00afce-0662-cf80-bc97-9da08d4aed5c"), 40 },
                    { -8102L, "INDIVIDUAL_ENTERPRISE", new Guid("34430f95-ab20-0377-0154-824491183943"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Operacion empresarial inscrita a nombre de una sola persona.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Empresa Individual", "INDIVIDUAL_ENTERPRISE", "EMPRESA INDIVIDUAL", new Guid("ddbd3d51-421d-d96a-bc34-f39d8f3d4159"), 30 },
                    { -8101L, "S_DE_RL", new Guid("92cb6ee1-f3d5-0022-336a-d23f73465588"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil de cuotas con responsabilidad limitada al aporte de los socios.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad de Responsabilidad Limitada", "S_DE_RL", "SOCIEDAD DE RESPONSABILIDAD LIMITADA", new Guid("9698c622-97f0-f976-7a59-ede072ffae46"), 20 },
                    { -8100L, "SA_DE_CV", new Guid("4f1c9778-dcce-91b8-9019-726b201a6014"), -7068L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad mercantil con capital representado en acciones y posibilidad de variacion de capital.", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sociedad Anonima de Capital Variable", "SA_DE_CV", "SOCIEDAD ANONIMA DE CAPITAL VARIABLE", new Guid("44fce38b-0973-102e-4294-c4d4668201f4"), 10 }
                });

            migrationBuilder.InsertData(
                table: "plan_entitlements",
                columns: new[] { "id", "capability_code", "commercial_plan_id", "created_utc", "is_enabled", "modified_utc", "module_key", "plan_code", "public_id" },
                values: new object[,]
                {
                    { -2012L, "PERSONNEL_FILE_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "PERSONNEL_FILES", "MASTER", new Guid("15d2e7bb-a6d3-d838-b287-fad1894f09bc") },
                    { -2011L, "LOCATION_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "LOCATIONS", "MASTER", new Guid("45ff0c5a-7232-a848-0880-88c97fae88ba") },
                    { -2010L, "ORG_UNIT_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ORG_UNITS", "MASTER", new Guid("f000855e-5ae9-04e3-af1e-9cfdaff37166") },
                    { -2009L, "COMPETENCY_FRAMEWORK_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "COMPETENCY_FRAMEWORK", "MASTER", new Guid("c8835d4a-43e8-4183-c80f-bd4e9feec5b2") },
                    { -2008L, "LEGAL_REPRESENTATIVE_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "LEGAL_REPRESENTATIVES", "MASTER", new Guid("bb22c032-1146-d619-7e53-a484a391bbe8") },
                    { -2007L, "COST_CENTER_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "COST_CENTERS", "MASTER", new Guid("44f2ca8b-acf6-9bd9-7793-87a398c54074") },
                    { -2006L, "SALARY_TABULATOR_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "SALARY_TABULATOR", "MASTER", new Guid("df8a53e6-e05a-a85e-af5f-90aef47de2ef") },
                    { -2005L, "POSITION_SLOT_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "POSITION_SLOTS", "MASTER", new Guid("c6f5cfac-5cc0-0f88-0d69-7bc97b9f5d38") },
                    { -2004L, "JOB_PROFILE_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "JOB_PROFILES", "MASTER", new Guid("9ec4d948-fd2c-4f33-f824-99732549df99") },
                    { -2003L, "POSITION_DESCRIPTION_CATALOG_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "POSITION_DESCRIPTION_CATALOGS", "MASTER", new Guid("eec34f8c-156c-acde-5728-f78cfb6d9633") },
                    { -2002L, "ORG_STRUCTURE_CATALOG_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ORG_STRUCTURE_CATALOGS", "MASTER", new Guid("82297e8a-996e-db0f-a8fe-fe4d26364162") },
                    { -2001L, "USER_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "USERS", "MASTER", new Guid("6042e2fd-0d1d-c1d2-6597-3b1e988d6d27") },
                    { -2000L, "RBAC_ADMINISTRATION", -3002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC", "MASTER", new Guid("8d59b8a4-2562-f046-9255-768e0d7bf4e1") },
                    { -1012L, "PERSONNEL_FILE_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "PERSONNEL_FILES", "FREE", new Guid("8e126af7-5d0b-c25d-cb58-ef07e2cab19d") },
                    { -1011L, "LOCATION_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "LOCATIONS", "FREE", new Guid("78e49355-1212-0692-663c-a70f1102e3f2") },
                    { -1010L, "ORG_UNIT_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ORG_UNITS", "FREE", new Guid("bbf1bc58-712b-e451-6b68-581ba2a6ec21") },
                    { -1009L, "COMPETENCY_FRAMEWORK_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "COMPETENCY_FRAMEWORK", "FREE", new Guid("7eafe939-b1c7-d816-ecd2-f526567ea98a") },
                    { -1008L, "LEGAL_REPRESENTATIVE_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "LEGAL_REPRESENTATIVES", "FREE", new Guid("4b8b66bd-7d2d-b8ad-22a3-d9494e3f89c8") },
                    { -1007L, "COST_CENTER_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "COST_CENTERS", "FREE", new Guid("1eeb2560-cace-5e46-b011-abd3d9f1dada") },
                    { -1006L, "SALARY_TABULATOR_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "SALARY_TABULATOR", "FREE", new Guid("fd24c2f1-15a3-05a8-9114-a7a803e8fbb1") },
                    { -1005L, "POSITION_SLOT_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "POSITION_SLOTS", "FREE", new Guid("e1625c56-41e3-ccbc-c99e-aef37dd894f5") },
                    { -1004L, "JOB_PROFILE_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "JOB_PROFILES", "FREE", new Guid("27cc2bc1-05fa-4ddd-cb03-d0af8081134b") },
                    { -1003L, "POSITION_DESCRIPTION_CATALOG_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "POSITION_DESCRIPTION_CATALOGS", "FREE", new Guid("b18eff5b-d656-75d9-8f0b-07fc0e31786a") },
                    { -1002L, "ORG_STRUCTURE_CATALOG_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ORG_STRUCTURE_CATALOGS", "FREE", new Guid("f9ee6521-5825-7481-8913-ef3347b50090") },
                    { -1001L, "USER_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "USERS", "FREE", new Guid("0fb82b8d-88f9-c60d-32ff-cff37eded5bf") },
                    { -1000L, "RBAC_ADMINISTRATION", -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC", "FREE", new Guid("a61c47db-3437-7c9e-2e59-4c7f4307db4e") }
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
                name: "ix_auth_refresh_tokens__user_client_revoked",
                table: "auth_refresh_tokens",
                columns: new[] { "user_id", "client_type", "revoked_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_auth_refresh_tokens__public_id",
                table: "auth_refresh_tokens",
                column: "public_id",
                unique: true);

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
                name: "uq_commercial_addon_entitlements__addon_capability",
                table: "commercial_addon_entitlements",
                columns: new[] { "commercial_addon_id", "capability_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_addon_entitlements__addon_module",
                table: "commercial_addon_entitlements",
                columns: new[] { "commercial_addon_id", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_addon_entitlements__public_id",
                table: "commercial_addon_entitlements",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commercial_addons__billing_model",
                table: "commercial_addons",
                column: "billing_model");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_addons__normalized_name",
                table: "commercial_addons",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_addons__status",
                table: "commercial_addons",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_addons__type",
                table: "commercial_addons",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "uq_commercial_addons__normalized_code",
                table: "commercial_addons",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_addons__public_id",
                table: "commercial_addons",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plan_limits__plan_limit_code",
                table: "commercial_plan_limits",
                columns: new[] { "commercial_plan_id", "normalized_limit_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plan_limits__public_id",
                table: "commercial_plan_limits",
                column: "public_id",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_commercial_plans__normalized_name",
                table: "commercial_plans",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_plans__status",
                table: "commercial_plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plans__normalized_code",
                table: "commercial_plans",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plans__public_id",
                table: "commercial_plans",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies__company_type_catalog_item",
                table: "companies",
                column: "company_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_companies__country_catalog_item",
                table: "companies",
                column: "country_catalog_item_id");

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
                name: "ix_company_commercial_addon_changes__company_requested",
                table: "company_commercial_addon_changes",
                columns: new[] { "company_id", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_commercial_addon_changes__status_effective_date",
                table: "company_commercial_addon_changes",
                columns: new[] { "status", "effective_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addon_changes_commercial_addon_id",
                table: "company_commercial_addon_changes",
                column: "commercial_addon_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addon_changes_company_subscription_id",
                table: "company_commercial_addon_changes",
                column: "company_subscription_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addon_changes__company_addon_scheduled",
                table: "company_commercial_addon_changes",
                columns: new[] { "company_id", "commercial_addon_id", "status" },
                unique: true,
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addon_changes__public_id",
                table: "company_commercial_addon_changes",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_commercial_addons__company_status",
                table: "company_commercial_addons",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addons_commercial_addon_id",
                table: "company_commercial_addons",
                column: "commercial_addon_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_commercial_addons_company_subscription_id",
                table: "company_commercial_addons",
                column: "company_subscription_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addons__company_addon",
                table: "company_commercial_addons",
                columns: new[] { "company_id", "commercial_addon_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_commercial_addons__public_id",
                table: "company_commercial_addons",
                column: "public_id",
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
                name: "uq_company_invitation_tokens__public_id",
                table: "company_invitation_tokens",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_invitation_tokens__token_hash",
                table: "company_invitation_tokens",
                column: "token_hash",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_company_subscription_status_transitions__subscription_changed",
                table: "company_subscription_status_transitions",
                columns: new[] { "company_subscription_id", "changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_company_subscription_status_transitions__public_id",
                table: "company_subscription_status_transitions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__commercial_plan_id",
                table: "company_subscriptions",
                column: "commercial_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__commercial_plan_version_id",
                table: "company_subscriptions",
                column: "commercial_plan_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__company_status_changed",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status_changed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_subscriptions__status_start_date",
                table: "company_subscriptions",
                columns: new[] { "status", "start_date_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_live",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status IN ('Draft', 'Trial', 'Active', 'Suspended')");

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__company_scheduled",
                table: "company_subscriptions",
                columns: new[] { "company_id", "status" },
                unique: true,
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__public_id",
                table: "company_subscriptions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__country_active",
                table: "company_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_company_type_catalog_items__country_name",
                table: "company_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "uq_company_type_catalog_items__country_code",
                table: "company_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
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
                name: "uq_competency_conduct_behaviors__public_id",
                table: "competency_conduct_behaviors",
                column: "public_id",
                unique: true);

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
                name: "ix_country_catalog__name",
                table: "country_catalog",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "uq_country_catalog__normalized_code",
                table: "country_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_country_catalog__public_id",
                table: "country_catalog",
                column: "public_id",
                unique: true);

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
                name: "uq_iam_permissions__public_id",
                table: "iam_permissions",
                column: "public_id",
                unique: true);

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
                name: "uq_iam_role_permission_assignments__public_id",
                table: "iam_role_permission_assignments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_roles__tenant_name",
                table: "iam_roles",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "uq_iam_roles__public_id",
                table: "iam_roles",
                column: "public_id",
                unique: true);

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
                name: "uq_iam_user_role_assignments__public_id",
                table: "iam_user_role_assignments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_users__tenant_name",
                table: "iam_users",
                columns: new[] { "tenant_id", "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "uq_iam_users__public_id",
                table: "iam_users",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_users__tenant_email",
                table: "iam_users",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_users__tenant_linked_user_public_id",
                table: "iam_users",
                columns: new[] { "tenant_id", "linked_user_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_internal_catalog_values__catalog_key_active",
                table: "internal_catalog_values",
                columns: new[] { "catalog_key", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_internal_catalog_values__catalog_key_normalized_value",
                table: "internal_catalog_values",
                columns: new[] { "catalog_key", "normalized_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_internal_catalog_values__public_id",
                table: "internal_catalog_values",
                column: "public_id",
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
                name: "uq_job_profile_benefits__public_id",
                table: "job_profile_benefits",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_compensations__public_id",
                table: "job_profile_compensations",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_competencies__public_id",
                table: "job_profile_competencies",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_competency_expectation_conducts__public_id",
                table: "job_profile_competency_expectation_conducts",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_dependent_positions__public_id",
                table: "job_profile_dependent_positions",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_functions__public_id",
                table: "job_profile_functions",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_relations__public_id",
                table: "job_profile_relations",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_requirements__public_id",
                table: "job_profile_requirements",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_trainings__public_id",
                table: "job_profile_trainings",
                column: "public_id",
                unique: true);

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
                name: "uq_job_profile_working_conditions__public_id",
                table: "job_profile_working_conditions",
                column: "public_id",
                unique: true);

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
                name: "uq_legal_representative_position_title_catalog__normalized_code",
                table: "legal_representative_position_title_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_position_title_catalog__public_id",
                table: "legal_representative_position_title_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_representation_type_catalog__normalized_code",
                table: "legal_representative_representation_type_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_representation_type_catalog__public_id",
                table: "legal_representative_representation_type_catalog",
                column: "public_id",
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
                name: "ix_org_units__tenant_cost_center_code",
                table: "org_units",
                columns: new[] { "tenant_id", "cost_center_code" });

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
                name: "uq_plan_entitlements__plan_capability",
                table: "plan_entitlements",
                columns: new[] { "commercial_plan_id", "capability_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements__plan_module",
                table: "plan_entitlements",
                columns: new[] { "commercial_plan_id", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements__public_id",
                table: "plan_entitlements",
                column: "public_id",
                unique: true);

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
                name: "ix_role_field_permissions__tenant_role",
                table: "role_field_permissions",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "uq_role_field_permissions__public_id",
                table: "role_field_permissions",
                column: "public_id",
                unique: true);

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
                name: "uq_salary_tabulator_change_request_items__public_id",
                table: "salary_tabulator_change_request_items",
                column: "public_id",
                unique: true);

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
                name: "uq_user_companies__public_id",
                table: "user_companies",
                column: "public_id",
                unique: true);

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
                name: "commercial_addon_entitlements");

            migrationBuilder.DropTable(
                name: "commercial_plan_limits");

            migrationBuilder.DropTable(
                name: "company_commercial_addon_changes");

            migrationBuilder.DropTable(
                name: "company_commercial_addons");

            migrationBuilder.DropTable(
                name: "company_invitation_tokens");

            migrationBuilder.DropTable(
                name: "company_subscription_plan_changes");

            migrationBuilder.DropTable(
                name: "company_subscription_status_change_requests");

            migrationBuilder.DropTable(
                name: "company_subscription_status_transitions");

            migrationBuilder.DropTable(
                name: "competency_conduct_behaviors");

            migrationBuilder.DropTable(
                name: "cost_centers");

            migrationBuilder.DropTable(
                name: "iam_role_permission_assignments");

            migrationBuilder.DropTable(
                name: "iam_user_role_assignments");

            migrationBuilder.DropTable(
                name: "internal_catalog_values");

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
                name: "platform_audit_logs");

            migrationBuilder.DropTable(
                name: "platform_operators");

            migrationBuilder.DropTable(
                name: "position_slots");

            migrationBuilder.DropTable(
                name: "role_field_permissions");

            migrationBuilder.DropTable(
                name: "salary_tabulator_change_request_items");

            migrationBuilder.DropTable(
                name: "salary_tabulator_lines");

            migrationBuilder.DropTable(
                name: "user_companies");

            migrationBuilder.DropTable(
                name: "commercial_addons");

            migrationBuilder.DropTable(
                name: "company_subscriptions");

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
                name: "iam_roles");

            migrationBuilder.DropTable(
                name: "commercial_plan_versions");

            migrationBuilder.DropTable(
                name: "companies");

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
                name: "commercial_plans");

            migrationBuilder.DropTable(
                name: "company_type_catalog_items");

            migrationBuilder.DropTable(
                name: "org_units");

            migrationBuilder.DropTable(
                name: "position_categories");

            migrationBuilder.DropTable(
                name: "country_catalog");

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
