using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExitInterviewForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exit_interview_forms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    retirement_reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active_for_reason = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exit_interview_forms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exit_interview_form_groups",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exit_interview_form_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exit_interview_form_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_exit_interview_form_groups__form",
                        column: x => x.exit_interview_form_id,
                        principalTable: "exit_interview_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exit_interview_form_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exit_interview_form_id = table.Column<long>(type: "bigint", nullable: false),
                    exit_interview_form_group_id = table.Column<long>(type: "bigint", nullable: true),
                    control_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    weight = table.Column<decimal>(type: "numeric(9,2)", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    max_value = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    scale_max = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exit_interview_form_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_exit_interview_form_fields__form",
                        column: x => x.exit_interview_form_id,
                        principalTable: "exit_interview_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exit_interview_form_fields__group",
                        column: x => x.exit_interview_form_group_id,
                        principalTable: "exit_interview_form_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "exit_interview_form_field_options",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exit_interview_form_field_id = table.Column<long>(type: "bigint", nullable: false),
                    option_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_option_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    score = table.Column<decimal>(type: "numeric(9,2)", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exit_interview_form_field_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_exit_interview_form_field_options__field",
                        column: x => x.exit_interview_form_field_id,
                        principalTable: "exit_interview_form_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_form_field_options__field_order",
                table: "exit_interview_form_field_options",
                columns: new[] { "tenant_id", "exit_interview_form_field_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_form_field_options__field_code",
                table: "exit_interview_form_field_options",
                columns: new[] { "exit_interview_form_field_id", "normalized_option_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_form_field_options__public_id",
                table: "exit_interview_form_field_options",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_form_fields__form_order",
                table: "exit_interview_form_fields",
                columns: new[] { "tenant_id", "exit_interview_form_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_exit_interview_form_fields_exit_interview_form_group_id",
                table: "exit_interview_form_fields",
                column: "exit_interview_form_group_id");

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_form_fields__form_key",
                table: "exit_interview_form_fields",
                columns: new[] { "exit_interview_form_id", "normalized_field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_form_fields__public_id",
                table: "exit_interview_form_fields",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_form_groups__form_order",
                table: "exit_interview_form_groups",
                columns: new[] { "tenant_id", "exit_interview_form_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_exit_interview_form_groups_exit_interview_form_id",
                table: "exit_interview_form_groups",
                column: "exit_interview_form_id");

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_form_groups__public_id",
                table: "exit_interview_form_groups",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exit_interview_forms__tenant_status",
                table: "exit_interview_forms",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_forms__public_id",
                table: "exit_interview_forms",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_forms__reason_active",
                table: "exit_interview_forms",
                columns: new[] { "tenant_id", "retirement_reason_code" },
                unique: true,
                filter: "is_active_for_reason AND status = 'Published' AND retirement_reason_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_exit_interview_forms__tenant_name",
                table: "exit_interview_forms",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exit_interview_form_field_options");

            migrationBuilder.DropTable(
                name: "exit_interview_form_fields");

            migrationBuilder.DropTable(
                name: "exit_interview_form_groups");

            migrationBuilder.DropTable(
                name: "exit_interview_forms");
        }
    }
}
