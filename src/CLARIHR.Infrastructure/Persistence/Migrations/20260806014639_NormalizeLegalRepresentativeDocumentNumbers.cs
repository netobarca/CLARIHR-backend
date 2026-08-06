using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations;

/// <summary>
/// Recomputes <c>normalized_document_number</c> after the domain stopped keeping separators in it.
///
/// The column backs the unique index on (tenant, document type, normalized number), i.e. it IS the
/// definition of "the same person". Rows written before this change still hold the punctuated value, so
/// without this backfill <c>01234567-8</c> (old row) and <c>012345678</c> (new row) would keep looking like
/// two different people — the exact duplicate this change exists to prevent.
///
/// If two rows in a tenant collapse onto the same document, the unique index makes this migration fail. That
/// is the intended outcome: it means the tenant already holds a real duplicate that a human has to merge,
/// and silently keeping one of them would be worse than stopping.
/// </summary>
public partial class NormalizeLegalRepresentativeDocumentNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE legal_representatives
            SET normalized_document_number = UPPER(REGEXP_REPLACE(document_number, '[^A-Za-z0-9]', '', 'g'))
            WHERE normalized_document_number
                  IS DISTINCT FROM UPPER(REGEXP_REPLACE(document_number, '[^A-Za-z0-9]', '', 'g'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restores the previous rule (trim + upper-case, separators kept).
        migrationBuilder.Sql(
            """
            UPDATE legal_representatives
            SET normalized_document_number = UPPER(TRIM(document_number))
            WHERE normalized_document_number IS DISTINCT FROM UPPER(TRIM(document_number));
            """);
    }
}
