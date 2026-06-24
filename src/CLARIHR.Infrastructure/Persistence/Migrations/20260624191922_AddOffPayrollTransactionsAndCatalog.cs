using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOffPayrollTransactionsAndCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "off_payroll_transaction_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_off_payroll_transaction_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_off_payroll_transaction_type_catalog_items_country_catalog_~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_file_off_payroll_transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    personnel_file_id = table.Column<long>(type: "bigint", nullable: false),
                    off_payroll_transaction_type_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    transaction_type_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    transaction_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    asset_access_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    corrects_transaction_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_file_off_payroll_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_file_off_payroll_transactions__personnel_file",
                        column: x => x.personnel_file_id,
                        principalTable: "personnel_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "off_payroll_transaction_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    off_payroll_transaction_id = table.Column<long>(type: "bigint", nullable: false),
                    document_type_catalog_item_id = table.Column<long>(type: "bigint", nullable: true),
                    file_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_off_payroll_transaction_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_off_payroll_transaction_documents__document_type",
                        column: x => x.document_type_catalog_item_id,
                        principalTable: "document_type_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_off_payroll_transaction_documents__transaction",
                        column: x => x.off_payroll_transaction_id,
                        principalTable: "personnel_file_off_payroll_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_off_payroll_transaction_documents__document_type",
                table: "off_payroll_transaction_documents",
                column: "document_type_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_off_payroll_transaction_documents__file_public_id",
                table: "off_payroll_transaction_documents",
                column: "file_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_off_payroll_transaction_documents__tenant_tx_active",
                table: "off_payroll_transaction_documents",
                columns: new[] { "tenant_id", "off_payroll_transaction_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_off_payroll_transaction_documents_off_payroll_transaction_id",
                table: "off_payroll_transaction_documents",
                column: "off_payroll_transaction_id");

            migrationBuilder.CreateIndex(
                name: "uq_off_payroll_transaction_documents__public_id",
                table: "off_payroll_transaction_documents",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_off_payroll_transaction_type_catalog_items__active_sort",
                table: "off_payroll_transaction_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_off_payroll_transaction_type_catalog_items__country_code",
                table: "off_payroll_transaction_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_off_payroll_transaction_type_catalog_items__public_id",
                table: "off_payroll_transaction_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_off_payroll_transactions__asset_access",
                table: "personnel_file_off_payroll_transactions",
                column: "asset_access_public_id");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_off_payroll_transactions__currency_code",
                table: "personnel_file_off_payroll_transactions",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_off_payroll_transactions__tenant_file_date",
                table: "personnel_file_off_payroll_transactions",
                columns: new[] { "tenant_id", "personnel_file_id", "transaction_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_personnel_file_off_payroll_transactions__tenant_file_period",
                table: "personnel_file_off_payroll_transactions",
                columns: new[] { "tenant_id", "personnel_file_id", "year", "month" });

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_off_payroll_transactions_personnel_file_id",
                table: "personnel_file_off_payroll_transactions",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_off_payroll_transactions__public_id",
                table: "personnel_file_off_payroll_transactions",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "off_payroll_transaction_documents");

            migrationBuilder.DropTable(
                name: "off_payroll_transaction_type_catalog_items");

            migrationBuilder.DropTable(
                name: "personnel_file_off_payroll_transactions");
        }
    }
}
