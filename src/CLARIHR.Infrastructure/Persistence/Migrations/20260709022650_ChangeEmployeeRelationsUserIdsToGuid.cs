using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmployeeRelationsUserIdsToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The recognition / disciplinary-action user references (registered_by / decided_by / annulled_by)
            // were originally created as text (M2). The public contract exposes user references as Guid
            // (`Guid XxxId` → `xxxPublicId`), so they become uuid end-to-end. PostgreSQL has no implicit
            // (assignment) cast from text to uuid, so the type change needs an explicit `USING …::uuid`;
            // EF/Npgsql's AlterColumn does not emit one. There is no productive data, so the cast is safe.
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_recognitions " +
                "ALTER COLUMN registered_by_user_id TYPE uuid USING registered_by_user_id::uuid;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_recognitions " +
                "ALTER COLUMN decided_by_user_id TYPE uuid USING decided_by_user_id::uuid;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_recognitions " +
                "ALTER COLUMN annulled_by_user_id TYPE uuid USING annulled_by_user_id::uuid;");

            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_disciplinary_actions " +
                "ALTER COLUMN registered_by_user_id TYPE uuid USING registered_by_user_id::uuid;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_disciplinary_actions " +
                "ALTER COLUMN decided_by_user_id TYPE uuid USING decided_by_user_id::uuid;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_disciplinary_actions " +
                "ALTER COLUMN annulled_by_user_id TYPE uuid USING annulled_by_user_id::uuid;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_recognitions " +
                "ALTER COLUMN registered_by_user_id TYPE character varying(100) USING registered_by_user_id::text;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_recognitions " +
                "ALTER COLUMN decided_by_user_id TYPE character varying(100) USING decided_by_user_id::text;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_recognitions " +
                "ALTER COLUMN annulled_by_user_id TYPE character varying(100) USING annulled_by_user_id::text;");

            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_disciplinary_actions " +
                "ALTER COLUMN registered_by_user_id TYPE character varying(100) USING registered_by_user_id::text;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_disciplinary_actions " +
                "ALTER COLUMN decided_by_user_id TYPE character varying(100) USING decided_by_user_id::text;");
            migrationBuilder.Sql(
                "ALTER TABLE personnel_file_disciplinary_actions " +
                "ALTER COLUMN annulled_by_user_id TYPE character varying(100) USING annulled_by_user_id::text;");
        }
    }
}
