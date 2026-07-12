using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndebtednessConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "max_indebtedness_percent",
                table: "company_preferences",
                type: "numeric(9,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "indebtedness_limits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recurring_deduction_type_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    max_percent = table.Column<decimal>(type: "numeric(11,8)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indebtedness_limits", x => x.id);
                    table.CheckConstraint("ck_indebtedness_limits__percent", "max_percent > 0 and max_percent <= 100");
                });

            migrationBuilder.CreateIndex(
                name: "uq_indebtedness_limits__public_id",
                table: "indebtedness_limits",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_indebtedness_limits__tenant_type_active",
                table: "indebtedness_limits",
                columns: new[] { "tenant_id", "recurring_deduction_type_code" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "indebtedness_limits");

            migrationBuilder.DropColumn(
                name: "max_indebtedness_percent",
                table: "company_preferences");
        }
    }
}
