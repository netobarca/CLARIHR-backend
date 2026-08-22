using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// B-04 — <c>is_primary</c> deja de ser anulable. El <c>null</c> era un tercer estado que ningún otro punto
    /// del sistema reconocía: el índice único parcial filtra por <c>is_primary = true</c> y <c>Inactivate()</c>
    /// escribe <c>false</c>, nunca <c>null</c>.
    /// <para>
    /// ⚠️ <b>El backfill de EF era `NULL → false` a secas, y eso no basta.</b> Habría dejado a las empresas cuyo
    /// único representante tenía <c>null</c> <b>sin ningún principal</b> — el estado exacto que este hallazgo
    /// existe para eliminar. La columna habría quedado conforme y el negocio violado.
    /// </para>
    /// <para>
    /// Por eso el paso 2: toda empresa con representantes activos termina con <b>exactamente uno</b> principal,
    /// promoviendo al activo más antiguo. Es el mismo criterio que aplica la promoción en runtime
    /// (<c>GetPromotionCandidateAsync</c>), para que datos migrados y datos nuevos no diverjan.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class LegalRepresentativePrimaryNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. El `null` desaparece: significa lo mismo que `false` — «no es el principal».
            migrationBuilder.Sql("""
                update legal_representatives
                   set is_primary = false
                 where is_primary is null;
                """);

            // 2. Toda empresa con representantes activos debe tener exactamente uno principal. Se promueve al
            //    activo más antiguo, desempatando por id para que la elección sea determinista.
            migrationBuilder.Sql("""
                with sin_principal as (
                    select distinct tenant_id
                      from legal_representatives
                     where is_active
                       and tenant_id not in (
                           select tenant_id
                             from legal_representatives
                            where is_active and is_primary
                       )
                ),
                sucesor as (
                    select distinct on (lr.tenant_id) lr.id
                      from legal_representatives lr
                      join sin_principal s on s.tenant_id = lr.tenant_id
                     where lr.is_active
                     order by lr.tenant_id, lr.created_utc, lr.id
                )
                update legal_representatives
                   set is_primary = true
                 where id in (select id from sucesor);
                """);

            // 3. Ya no queda ningún `null`: la columna puede exigirlo.
            migrationBuilder.AlterColumn<bool>(
                name: "is_primary",
                table: "legal_representatives",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se recupera la nulabilidad de la columna, no qué filas eran `null`: esa información se pierde a
            // propósito, porque era precisamente el estado que no debía existir. Sin producción, no hay nada
            // que preservar.
            migrationBuilder.AlterColumn<bool>(
                name: "is_primary",
                table: "legal_representatives",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: false,
                oldDefaultValue: false);
        }
    }
}
