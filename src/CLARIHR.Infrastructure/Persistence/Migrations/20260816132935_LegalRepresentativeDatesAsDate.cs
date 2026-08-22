using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// B-02 — las tres fechas del representante legal pasan de `timestamptz` a `date`. Responden a «¿qué día?»,
    /// no a «¿en qué momento?», y guardarlas como instante obligaba a cada consumidor a recordar la convención
    /// «medianoche UTC» — de donde salía el corrimiento de F-03.
    /// <para>
    /// ⚠️ <b>Escrita a mano, y por dos razones.</b>
    /// </para>
    /// <para>
    /// 1. EF escaffoldeó <c>DropColumn</c> + <c>AddColumn</c>, que no convierte los datos: los <b>borra</b>. Cada
    /// representante existente habría perdido su fecha de nombramiento y habría quedado con
    /// <c>effective_from = 0001-01-01</c>. Aquí se hace <c>ALTER COLUMN … TYPE date</c>, que conserva el valor.
    /// </para>
    /// <para>
    /// 2. El cast desnudo <c>::date</c> se resuelve con el <c>TimeZone</c> de la <b>SESIÓN</b>, no con UTC.
    /// Medido contra <c>clarihr_dev</c>: con <c>TimeZone='America/El_Salvador'</c>,
    /// <c>('2026-12-01 00:00:00+00'::timestamptz)::date</c> devuelve <b><c>2026-11-30</c></b>. Sin
    /// <c>AT TIME ZONE 'UTC'</c>, la migración que existe para eliminar el corrimiento de un día lo habría
    /// causado ella misma en cada fila.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class LegalRepresentativeDatesAsDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El índice y el CHECK referencian los nombres viejos: se caen antes de tocar las columnas.
            migrationBuilder.DropIndex(
                name: "ix_legal_representatives__tenant_effective_dates",
                table: "legal_representatives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_legal_representatives__effective_dates",
                table: "legal_representatives");

            // El sufijo `Utc` era para instantes; en un campo de día induce justo el error que se corrige.
            migrationBuilder.RenameColumn(
                name: "appointment_date_utc",
                table: "legal_representatives",
                newName: "appointment_date");

            migrationBuilder.RenameColumn(
                name: "effective_from_utc",
                table: "legal_representatives",
                newName: "effective_from");

            migrationBuilder.RenameColumn(
                name: "effective_to_utc",
                table: "legal_representatives",
                newName: "effective_to");

            // `AT TIME ZONE 'UTC'` ancla la conversión. Sin él, el día retrocede en cualquier sesión al oeste
            // de Greenwich. Ver el resumen de la clase.
            migrationBuilder.Sql("""
                alter table legal_representatives
                    alter column appointment_date type date using (appointment_date at time zone 'UTC')::date,
                    alter column effective_from  type date using (effective_from  at time zone 'UTC')::date,
                    alter column effective_to    type date using (effective_to    at time zone 'UTC')::date;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_effective_dates",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "effective_from", "effective_to" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_representatives__effective_dates",
                table: "legal_representatives",
                sql: "effective_to is null or effective_to >= effective_from");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_legal_representatives__tenant_effective_dates",
                table: "legal_representatives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_legal_representatives__effective_dates",
                table: "legal_representatives");

            // La vuelta tiene la trampa simétrica: `date::timestamptz` interpreta la medianoche en la zona de
            // la sesión. `col::timestamp at time zone 'UTC'` la fija en UTC, que es donde estaba.
            migrationBuilder.Sql("""
                alter table legal_representatives
                    alter column appointment_date type timestamp with time zone
                        using (appointment_date::timestamp at time zone 'UTC'),
                    alter column effective_from  type timestamp with time zone
                        using (effective_from::timestamp  at time zone 'UTC'),
                    alter column effective_to    type timestamp with time zone
                        using (effective_to::timestamp    at time zone 'UTC');
                """);

            migrationBuilder.RenameColumn(
                name: "appointment_date",
                table: "legal_representatives",
                newName: "appointment_date_utc");

            migrationBuilder.RenameColumn(
                name: "effective_from",
                table: "legal_representatives",
                newName: "effective_from_utc");

            migrationBuilder.RenameColumn(
                name: "effective_to",
                table: "legal_representatives",
                newName: "effective_to_utc");

            migrationBuilder.CreateIndex(
                name: "ix_legal_representatives__tenant_effective_dates",
                table: "legal_representatives",
                columns: new[] { "tenant_id", "effective_from_utc", "effective_to_utc" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_legal_representatives__effective_dates",
                table: "legal_representatives",
                sql: "effective_to_utc is null or effective_to_utc >= effective_from_utc");
        }
    }
}
