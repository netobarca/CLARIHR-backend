using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoldDiacriticsInNormalizedSearchColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


        // 00005 / B-01 (§2.10) — pliega los diacriticos de los valores YA guardados, para que casen con lo
        // que `SearchTextNormalization.Fold` produce a partir de ahora. Sin esto, las filas creadas antes
        // del cambio dejarian de encontrarse: la busqueda plegaria y lo almacenado no.
        //
        // Se recorren las columnas por catalogo en vez de listarlas: asi una tabla nueva con la misma
        // convencion queda cubierta sin tocar esta migracion.
        //
        // ⚠️ SOLO las cinco familias que escriben los helpers que cambiaron. Se excluye a proposito
        // `normalized_email` (se guarda en MINUSCULAS y su normalizador NO pliega: plegarlo aqui dejaria
        // el correo almacenado sin casar con el que calcula el login) y `normalized_code` (los codigos son
        // ASCII por su regex, asi que plegarlos no cambia nada).
        //
        // NO cambia la caja: cada columna conserva la suya. Solo se quitan las marcas diacriticas, que es
        // exactamente lo que hace `Fold`.
        migrationBuilder.Sql(@"
DO $$
DECLARE fila record;
BEGIN
    FOR fila IN
        SELECT c.table_name, c.column_name
        FROM information_schema.columns c
        WHERE c.table_schema = 'public'
          AND c.column_name IN ('normalized_name', 'normalized_full_name', 'normalized_description',
                                'normalized_requirement_name', 'normalized_title')
          AND c.data_type IN ('text', 'character varying', 'character')
    LOOP
        EXECUTE format(
            'UPDATE %I SET %I = translate(%I, %L, %L) WHERE %I ~ %L',
            fila.table_name, fila.column_name, fila.column_name,
            'ÁÀÄÂÃÉÈËÊÍÌÏÎÓÒÖÔÕÚÙÜÛÑÇáàäâãéèëêíìïîóòöôõúùüûñç',
            'AAAAAEEEEIIIIOOOOOUUUUNCaaaaaeeeeiiiiooooouuuunc',
            fila.column_name, '[^ -~]');
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {


        // 00005 / B-01 (§2.10) — sin vuelta atras: quitar una tilde es una operacion que PIERDE
        // informacion, y de «ESTACION» no se puede saber si venia de «Estación» o de «Estacion». El valor
        // se recalcula solo en la siguiente escritura de cada fila. Se deja explicito en vez de fingir
        // una reversion que no existe.
        }
    }
}
