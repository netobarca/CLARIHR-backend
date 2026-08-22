namespace CLARIHR.Domain.Payroll;

/// <summary>
/// Para qué sirve una nómina. No es un catálogo de país editable: cada valor cambia el COMPORTAMIENTO del
/// motor (qué población entra, qué conceptos se pagan), así que agregar uno es escribir código, no sembrar
/// una fila. Por eso vive acá y se valida contra esta lista cerrada.
/// </summary>
public static class PayrollPurposes
{
    /// <summary>La nómina de siempre: salario del periodo + los cinco pools + ley.</summary>
    public const string Ordinaria = "ORDINARIA";

    /// <summary>
    /// La nómina anual de aguinaldo (requerimiento 2026-08-12 §5/§6). Tres diferencias con la ordinaria:
    /// su población son TODOS los empleados activos —no solo los de un tipo de planilla—, NO paga salario
    /// del periodo ni consume ninguno de los cinco pools, y su única línea de ingreso es el aguinaldo, con
    /// su porción exenta de Renta separada.
    /// </summary>
    public const string Aguinaldo = "AGUINALDO";

    public static bool IsKnown(string? purposeCode) =>
        purposeCode is Ordinaria or Aguinaldo;

    /// <summary>
    /// Un propósito AUSENTE es ordinario. No es una cortesía: el contrato nació sin este campo y la enorme
    /// mayoría de los clientes —y de los tests— siguen sin enviarlo, así que tratar el hueco como error
    /// convertiría «crear una nómina» en una operación rota para todo el mundo. Además el default declarado
    /// en el record del request NO se aplica: el deserializador entrega null cuando la propiedad falta, de
    /// modo que el hueco tiene que resolverse acá y no en la firma.
    /// </summary>
    public static string Normalize(string? purposeCode) =>
        string.IsNullOrWhiteSpace(purposeCode) ? Ordinaria : purposeCode.Trim().ToUpperInvariant();
}
