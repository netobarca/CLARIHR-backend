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
