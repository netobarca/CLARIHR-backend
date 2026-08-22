using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Compensation;

/// <summary>
/// La porción del aguinaldo EXENTA de Renta en un año, tal como la publica la ley (reforma de 2025: $1,500).
/// <para>
/// Es un monto <b>absoluto</b> y cambia por año —decisión del usuario 2026-08-12—, así que se guarda una fila
/// por año, exactamente como la tabla de Renta (<see cref="IncomeTaxWithholdingBracket"/>): tenant-scoped,
/// editable, sin sembrar. Deliberadamente NO es un múltiplo del salario mínimo: ese es el modelo del
/// finiquito (<c>settlement_concept_catalog_items.exemption_multiplier</c>), que responde a otra regla y que
/// esta pasada no toca por instrucción explícita.
/// </para>
/// <para>
/// Que no exista fila para un año NO es un error de configuración que deba bloquear la planilla: se grava
/// todo y la corrida emite la advertencia. Retener de más es visible y corregible; dejar de retener es una
/// contingencia fiscal silenciosa. Es la misma postura que el motor ya tiene con una tabla de Renta ausente.
/// </para>
/// </summary>
public sealed class AguinaldoExemption : TenantEntity
{
    private AguinaldoExemption()
    {
    }

    private AguinaldoExemption(int year, decimal exemptAmount, bool isActive)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Year = year;
        ExemptAmount = exemptAmount;
        IsActive = isActive;
    }

    /// <summary>Año calendario del aguinaldo al que aplica la exención.</summary>
    public int Year { get; private set; }

    /// <summary>Monto exento, absoluto y en la moneda de la empresa.</summary>
    public decimal ExemptAmount { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static AguinaldoExemption Create(int year, decimal exemptAmount, bool isActive)
    {
        if (year is < 2000 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be a plausible calendar year.");
        }

        if (exemptAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(exemptAmount), "The exempt amount cannot be negative.");
        }

        return new AguinaldoExemption(year, exemptAmount, isActive);
    }

    /// <summary>Corrige el monto de un año ya registrado — el año es la identidad y nunca cambia.</summary>
    public void UpdateAmount(decimal exemptAmount, bool isActive)
    {
        if (exemptAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(exemptAmount), "The exempt amount cannot be negative.");
        }

        ExemptAmount = exemptAmount;
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }
}
