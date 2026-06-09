using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToIamRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing rows with distinct random tokens (a static default would give every
            // role the same token). The default is migration-only — the EF model maps the column with
            // .IsConcurrencyToken() and NO model default, so the app always supplies the token on insert
            // (IamRole seeds it in the constructor) and rotates it on every mutation.
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "iam_roles",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "iam_roles");
        }
    }
}
