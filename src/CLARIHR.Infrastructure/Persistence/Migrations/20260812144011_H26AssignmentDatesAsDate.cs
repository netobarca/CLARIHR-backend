using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// H-26/H-28 — la asignación de plaza guarda DÍAS, no instantes: `start_date`/`end_date` pasan de
    /// `timestamptz` a `date`.
    /// <para>
    /// El `USING` es explícito a propósito. Un `timestamptz::date` a secas se resuelve con el `TimeZone` de la
    /// SESIÓN, así que la misma migración corrida desde una conexión en `America/El_Salvador` (-06:00) convertiría
    /// `2026-12-01 00:00+00` en **`2026-11-30`**: un día menos, silencioso, en la fecha de la que cuelga la
    /// antigüedad de vacaciones. Anclar el `AT TIME ZONE 'UTC'` deja la conversión igual en cualquier cliente.
    /// </para>
    /// </summary>
    public partial class H26AssignmentDatesAsDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                ALTER TABLE personnel_file_employment_assignments
                    ALTER COLUMN start_date TYPE date USING (start_date AT TIME ZONE 'UTC')::date,
                    ALTER COLUMN end_date   TYPE date USING (end_date   AT TIME ZONE 'UTC')::date;
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            // La vuelta reconstruye el instante como la medianoche UTC del día, que es exactamente lo que la
            // convención anterior guardaba.
            migrationBuilder.Sql(
                """
                ALTER TABLE personnel_file_employment_assignments
                    ALTER COLUMN start_date TYPE timestamptz USING (start_date::timestamp AT TIME ZONE 'UTC'),
                    ALTER COLUMN end_date   TYPE timestamptz USING (end_date::timestamp   AT TIME ZONE 'UTC');
                """);
    }
}
