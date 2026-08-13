namespace CLARIHR.Domain.Common;

/// <summary>Naturaleza de un concepto de compensación: ingreso (suma) o egreso (descuento).</summary>
public enum CompensationNature
{
    Ingreso = 1,
    Egreso = 2,
}

/// <summary>Modo de cálculo de un concepto: monto fijo o porcentaje sobre una base.</summary>
public enum CompensationCalculationType
{
    Fixed = 1,
    Percentage = 2,
}

/// <summary>Clasificación de un egreso (editable por instancia): de ley, interno o externo.</summary>
public enum DeductionClass
{
    Ley = 1,
    Interno = 2,
    Externo = 3,
}

/// <summary>
/// H-29 — clasificación del INGRESO, simétrica a <see cref="DeductionClass"/>. Sus valores son las columnas del
/// reporte de planilla por empleado, no los conceptos: el objetivo es que un concepto nuevo que cree la empresa
/// (p. ej. <c>BONO_PRODUCTIVIDAD</c>) caiga en la columna correcta en vez de irse en silencio a «otros».
/// <c>NoDeducible</c> son los reintegros que están fuera del salario (viáticos, reembolsos), que además no deben
/// afectar las bases de ISSS/AFP/Renta.
/// </summary>
public enum IncomeClass
{
    Salario = 1,
    Bono = 2,
    Comision = 3,
    HorasExtra = 4,
    NoDeducible = 5,
    Aguinaldo = 6,
    Otro = 7,
}
