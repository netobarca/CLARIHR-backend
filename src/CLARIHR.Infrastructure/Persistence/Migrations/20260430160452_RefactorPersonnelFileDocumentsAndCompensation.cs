using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPersonnelFileDocumentsAndCompensation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "personnel_file_payroll_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "personnel_file_medical_claims",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_education_study_type_catalog_items__active_sort",
                table: "education_study_type_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_study_type_catalog_items__code",
                table: "education_study_type_catalog_items",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_status_catalog_items__active_sort",
                table: "education_status_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_status_catalog_items__code",
                table: "education_status_catalog_items",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_shift_catalog_items__active_sort",
                table: "education_shift_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_shift_catalog_items__code",
                table: "education_shift_catalog_items",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_modality_catalog_items__active_sort",
                table: "education_modality_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_modality_catalog_items__code",
                table: "education_modality_catalog_items",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_career_catalog_items__active_sort",
                table: "education_career_catalog_items",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_education_career_catalog_items__code",
                table: "education_career_catalog_items",
                column: "normalized_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_education_study_type_catalog_items__active_sort",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_study_type_catalog_items__code",
                table: "education_study_type_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_status_catalog_items__active_sort",
                table: "education_status_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_status_catalog_items__code",
                table: "education_status_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_shift_catalog_items__active_sort",
                table: "education_shift_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_shift_catalog_items__code",
                table: "education_shift_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_modality_catalog_items__active_sort",
                table: "education_modality_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_modality_catalog_items__code",
                table: "education_modality_catalog_items");

            migrationBuilder.DropIndex(
                name: "ix_education_career_catalog_items__active_sort",
                table: "education_career_catalog_items");

            migrationBuilder.DropIndex(
                name: "uq_education_career_catalog_items__code",
                table: "education_career_catalog_items");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "personnel_file_payroll_transactions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "personnel_file_medical_claims");

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
        }
    }
}
