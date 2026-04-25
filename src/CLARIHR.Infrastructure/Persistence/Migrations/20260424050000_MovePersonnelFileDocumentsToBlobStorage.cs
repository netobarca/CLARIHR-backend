using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260424050000_MovePersonnelFileDocumentsToBlobStorage")]
    public partial class MovePersonnelFileDocumentsToBlobStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM personnel_file_documents;");

            migrationBuilder.AddColumn<string>(
                name: "blob_name",
                table: "personnel_file_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "blob_url",
                table: "personnel_file_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropColumn(
                name: "file_data",
                table: "personnel_file_documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blob_name",
                table: "personnel_file_documents");

            migrationBuilder.DropColumn(
                name: "blob_url",
                table: "personnel_file_documents");

            migrationBuilder.AddColumn<byte[]>(
                name: "file_data",
                table: "personnel_file_documents",
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>());
        }
    }
}
