using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkPersonnelFileEmployeeRelationsToPersonnelFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "related_personnel_file_id",
                table: "personnel_file_employee_relations",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH unique_employee_matches AS (
                    SELECT
                        tenant_id,
                        normalized_full_name,
                        MIN(id) AS personnel_file_id
                    FROM personnel_files
                    WHERE record_type = 'Employee'
                    GROUP BY tenant_id, normalized_full_name
                    HAVING COUNT(*) = 1
                )
                UPDATE personnel_file_employee_relations AS relation
                SET related_personnel_file_id = matched.personnel_file_id
                FROM unique_employee_matches AS matched
                WHERE relation.tenant_id = matched.tenant_id
                  AND UPPER(BTRIM(relation.related_employee_name)) = matched.normalized_full_name;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM personnel_file_employee_relations
                WHERE related_personnel_file_id IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "related_personnel_file_id",
                table: "personnel_file_employee_relations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "related_employee_name",
                table: "personnel_file_employee_relations");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_employee_relations__tenant_related_file",
                table: "personnel_file_employee_relations",
                columns: new[] { "tenant_id", "related_personnel_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_employee_relations_related_personnel_file_id",
                table: "personnel_file_employee_relations",
                column: "related_personnel_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_personnel_file_employee_relations__related_personnel_file",
                table: "personnel_file_employee_relations",
                column: "related_personnel_file_id",
                principalTable: "personnel_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_personnel_file_employee_relations__related_personnel_file",
                table: "personnel_file_employee_relations");

            migrationBuilder.DropIndex(
                name: "ix_personnel_file_employee_relations__tenant_related_file",
                table: "personnel_file_employee_relations");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_employee_relations_related_personnel_file_id",
                table: "personnel_file_employee_relations");

            migrationBuilder.DropColumn(
                name: "related_personnel_file_id",
                table: "personnel_file_employee_relations");

            migrationBuilder.AddColumn<string>(
                name: "related_employee_name",
                table: "personnel_file_employee_relations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
