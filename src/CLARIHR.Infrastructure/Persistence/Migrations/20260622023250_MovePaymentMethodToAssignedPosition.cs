using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovePaymentMethodToAssignedPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personnel_file_payment_methods");

            migrationBuilder.AddColumn<Guid>(
                name: "payment_bank_account_public_id",
                table: "personnel_file_employment_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method_code",
                table: "personnel_file_employment_assignments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_bank_account_public_id",
                table: "personnel_file_employment_assignments");

            migrationBuilder.DropColumn(
                name: "payment_method_code",
                table: "personnel_file_employment_assignments");

            migrationBuilder.CreateTable(
                name: "personnel_file_payment_methods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    bank_account_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    payment_method_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
        }
    }
}
