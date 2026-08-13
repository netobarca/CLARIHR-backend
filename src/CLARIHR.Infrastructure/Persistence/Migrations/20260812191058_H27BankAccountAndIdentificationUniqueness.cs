using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H27BankAccountAndIdentificationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // H-27 — limpieza destructiva ANTES de los índices únicos: sin esto la migración falla en cualquier
            // base que ya cargue el desorden que el defecto permitía (en la corrida de pruebas quedaron 382
            // cuentas para 59 empleados, hasta 7 copias exactas, y las 382 marcadas como primaria). No hay
            // producción a esta fecha, así que se borra en vez de intentar adivinar cuál copia era la buena:
            // todas son idénticas por definición del duplicado.
            migrationBuilder.Sql(
                """
                -- 1) Duplicados de cuenta: se conserva la fila más antigua de cada (expediente, banco, número, moneda).
                DELETE FROM personnel_file_bank_accounts a
                USING personnel_file_bank_accounts b
                WHERE a.personnel_file_id = b.personnel_file_id
                  AND a.bank_catalog_item_id IS NOT DISTINCT FROM b.bank_catalog_item_id
                  AND a.normalized_account_number = b.normalized_account_number
                  AND a.currency_code = b.currency_code
                  AND a.id > b.id;

                -- 2) Varias primarias: se conserva la más antigua de cada expediente y se degradan las demás.
                UPDATE personnel_file_bank_accounts SET is_primary = false
                WHERE is_primary
                  AND id NOT IN (
                      SELECT min(id) FROM personnel_file_bank_accounts WHERE is_primary GROUP BY personnel_file_id);

                -- 3) El expediente que quedó con cuentas y ninguna primaria: la más antigua pasa a serlo. Si no,
                --    el consumidor de la conciliación elige con un FirstOrDefault() sin criterio.
                UPDATE personnel_file_bank_accounts SET is_primary = true
                WHERE id IN (
                    SELECT min(id) FROM personnel_file_bank_accounts
                    GROUP BY personnel_file_id
                    HAVING count(*) FILTER (WHERE is_primary) = 0);

                -- 4) Lo mismo para las identificaciones (sus duplicados ya los cerraba otro índice).
                UPDATE personnel_file_identifications SET is_primary = false
                WHERE is_primary
                  AND id NOT IN (
                      SELECT min(id) FROM personnel_file_identifications WHERE is_primary GROUP BY personnel_file_id);
                """);

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_identifications_personnel_file_id",
                table: "personnel_file_identifications");

            migrationBuilder.DropIndex(
                name: "IX_personnel_file_bank_accounts_personnel_file_id",
                table: "personnel_file_bank_accounts");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_identifications__file_primary",
                table: "personnel_file_identifications",
                column: "personnel_file_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_bank_accounts__file_bank_number_currency",
                table: "personnel_file_bank_accounts",
                columns: new[] { "personnel_file_id", "bank_catalog_item_id", "normalized_account_number", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_personnel_file_bank_accounts__file_primary",
                table: "personnel_file_bank_accounts",
                column: "personnel_file_id",
                unique: true,
                filter: "is_primary = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_personnel_file_identifications__file_primary",
                table: "personnel_file_identifications");

            migrationBuilder.DropIndex(
                name: "uq_personnel_file_bank_accounts__file_bank_number_currency",
                table: "personnel_file_bank_accounts");

            migrationBuilder.DropIndex(
                name: "uq_personnel_file_bank_accounts__file_primary",
                table: "personnel_file_bank_accounts");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_identifications_personnel_file_id",
                table: "personnel_file_identifications",
                column: "personnel_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_file_bank_accounts_personnel_file_id",
                table: "personnel_file_bank_accounts",
                column: "personnel_file_id");
        }
    }
}
