using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class H28SettlementSeniorityStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se agrega NULLABLE, se rellena con `plaza_start_date` —la semántica exacta de antes del cambio, así
            // ningún finiquito ya valorado se mueve— y solo entonces se vuelve obligatoria. EF generaba un
            // `defaultValue` de `0001-01-01`: una antigüedad de dos mil años esperando a que exista una fila.
            migrationBuilder.Sql(
                """
                ALTER TABLE personnel_file_settlements ADD COLUMN seniority_start_date timestamptz NULL;
                UPDATE personnel_file_settlements SET seniority_start_date = plaza_start_date;
                ALTER TABLE personnel_file_settlements ALTER COLUMN seniority_start_date SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seniority_start_date",
                table: "personnel_file_settlements");
        }
    }
}
