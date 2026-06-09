using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokenToCompanySubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing subscription rows with distinct random tokens (mirrors
            // AddConcurrencyTokenToPersonnelFileInterestsEntities). The app always sets the token on
            // insert/update via the domain initializer + RefreshConcurrencyToken(); the model/snapshot
            // carry no default, so this leftover column default is only a harmless backfill aid.
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "company_subscriptions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "company_subscriptions");
        }
    }
}
