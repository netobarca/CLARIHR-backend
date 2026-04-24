using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSocialLinksAndPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expiration_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_password_reset_tokens__auth_users",
                        column: x => x.user_id,
                        principalTable: "auth_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_social_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_preference_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_social_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_social_links__user_preferences",
                        column: x => x.user_preference_id,
                        principalTable: "user_preferences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auth_password_reset_tokens__user_id",
                table: "auth_password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_auth_password_reset_tokens__public_id",
                table: "auth_password_reset_tokens",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_auth_password_reset_tokens__token_hash",
                table: "auth_password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_user_social_links__preference_provider",
                table: "user_social_links",
                columns: new[] { "user_preference_id", "provider_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_user_social_links__public_id",
                table: "user_social_links",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_password_reset_tokens");

            migrationBuilder.DropTable(
                name: "user_social_links");
        }
    }
}
