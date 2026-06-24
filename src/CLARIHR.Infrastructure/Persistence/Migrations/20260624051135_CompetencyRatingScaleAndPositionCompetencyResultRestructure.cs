using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompetencyRatingScaleAndPositionCompetencyResultRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_personnel_file_position_competency_results__tenant_file_competency",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropColumn(
                name: "competency_code",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropColumn(
                name: "desired_behaviors",
                table: "personnel_file_position_competency_results");

            migrationBuilder.AlterColumn<DateTime>(
                name: "evaluation_date_utc",
                table: "personnel_file_position_competency_results",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "achieved_score",
                table: "personnel_file_position_competency_results",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "competency_catalog_item_id",
                table: "personnel_file_position_competency_results",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "competency_type_catalog_item_id",
                table: "personnel_file_position_competency_results",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "job_profile_competency_expectation_id",
                table: "personnel_file_position_competency_results",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "expected_value",
                table: "job_profile_competency_expectations",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "competency_rating_scales",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    scale_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    min_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    max_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    decimals = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_rating_scales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "competency_rating_scale_levels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    competency_rating_scale_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_rating_scale_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_competency_rating_scale_levels__scale",
                        column: x => x.competency_rating_scale_id,
                        principalTable: "competency_rating_scales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_position_competency_results__tenant_file_competency_date",
                table: "personnel_file_position_competency_results",
                columns: new[] { "tenant_id", "personnel_file_id", "competency_catalog_item_id", "evaluation_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_position_competency_results_competency_catal~",
                table: "personnel_file_position_competency_results",
                column: "competency_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_position_competency_results_competency_type_~",
                table: "personnel_file_position_competency_results",
                column: "competency_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_position_competency_results_job_profile_comp~",
                table: "personnel_file_position_competency_results",
                column: "job_profile_competency_expectation_id");

            migrationBuilder.CreateIndex(
                name: "ix_competency_rating_scale_levels__tenant_scale_sort",
                table: "competency_rating_scale_levels",
                columns: new[] { "tenant_id", "competency_rating_scale_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_competency_rating_scale_levels_competency_rating_scale_id",
                table: "competency_rating_scale_levels",
                column: "competency_rating_scale_id");

            migrationBuilder.CreateIndex(
                name: "uq_competency_rating_scale_levels__public_id",
                table: "competency_rating_scale_levels",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_competency_rating_scale_levels__tenant_scale_code",
                table: "competency_rating_scale_levels",
                columns: new[] { "tenant_id", "competency_rating_scale_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_competency_rating_scale_levels__tenant_scale_value",
                table: "competency_rating_scale_levels",
                columns: new[] { "tenant_id", "competency_rating_scale_id", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_competency_rating_scales__public_id",
                table: "competency_rating_scales",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_competency_rating_scales__tenant_active",
                table: "competency_rating_scales",
                column: "tenant_id",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "uq_competency_rating_scales__tenant_code",
                table: "competency_rating_scales",
                columns: new[] { "tenant_id", "normalized_code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_position_competency_results__competency_catalog_item",
                table: "personnel_file_position_competency_results",
                column: "competency_catalog_item_id",
                principalTable: "job_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_position_competency_results__competency_type_catalog_item",
                table: "personnel_file_position_competency_results",
                column: "competency_type_catalog_item_id",
                principalTable: "job_catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_position_competency_results__expectation",
                table: "personnel_file_position_competency_results",
                column: "job_profile_competency_expectation_id",
                principalTable: "job_profile_competency_expectations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_position_competency_results__competency_catalog_item",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_position_competency_results__competency_type_catalog_item",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_position_competency_results__expectation",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropTable(
                name: "competency_rating_scale_levels");

            migrationBuilder.DropTable(
                name: "competency_rating_scales");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_position_competency_results__tenant_file_competency_date",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_position_competency_results_competency_catal~",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_position_competency_results_competency_type_~",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_position_competency_results_job_profile_comp~",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropColumn(
                name: "competency_catalog_item_id",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropColumn(
                name: "competency_type_catalog_item_id",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropColumn(
                name: "job_profile_competency_expectation_id",
                table: "personnel_file_position_competency_results");

            migrationBuilder.DropColumn(
                name: "expected_value",
                table: "job_profile_competency_expectations");

            migrationBuilder.AlterColumn<DateTime>(
                name: "evaluation_date_utc",
                table: "personnel_file_position_competency_results",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "achieved_score",
                table: "personnel_file_position_competency_results",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "competency_code",
                table: "personnel_file_position_competency_results",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "desired_behaviors",
                table: "personnel_file_position_competency_results",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_position_competency_results__tenant_file_competency",
                table: "personnel_file_position_competency_results",
                columns: new[] { "tenant_id", "personnel_file_id", "competency_code" });
        }
    }
}
