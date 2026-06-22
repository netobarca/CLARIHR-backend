using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Unified, configurable compensation concept (income or deduction) of an employee, optionally scoped to
/// one of the employee's assigned positions (plaza). Replaces the former <c>PersonnelFileSalaryItem</c>:
/// a single model carries ingresos and egresos, fixed or percentage, with the statutory employer rate/cap
/// and the external-deduction context. The actual payroll calculation lives in a future module — here we
/// only store the configuration (see docs/business/analisis-plazas-ingresos-egresos.md, D-08).
/// </summary>
public sealed class PersonnelFileCompensationConcept : TenantEntity
{
    private PersonnelFileCompensationConcept()
    {
    }

    private PersonnelFileCompensationConcept(
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        bool isSystemSuggested,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        AssignedPositionPublicId = assignedPositionPublicId;
        Nature = nature;
        ConceptTypeCode = PersonnelFileNormalization.Clean(conceptTypeCode, nameof(conceptTypeCode));
        DeductionClass = deductionClass;
        CalculationType = calculationType;
        Value = value;
        CalculationBaseCode = PersonnelFileNormalization.CleanOptional(calculationBaseCode);
        EmployerRate = employerRate;
        ContributionCap = contributionCap;
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        PayPeriodCode = PersonnelFileNormalization.Clean(payPeriodCode, nameof(payPeriodCode));
        CounterpartyName = PersonnelFileNormalization.CleanOptional(counterpartyName);
        ExternalReference = PersonnelFileNormalization.CleanOptional(externalReference);
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsActive = isActive;
        IsSystemSuggested = isSystemSuggested;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // null = nivel empleado (aplica a la persona); con valor = nivel plaza (la EmploymentAssignment/plaza).
    public Guid? AssignedPositionPublicId { get; private set; }

    public CompensationNature Nature { get; private set; }

    public string ConceptTypeCode { get; private set; } = string.Empty;

    // Solo aplica a egresos; editable por instancia (hereda el default del tipo).
    public DeductionClass? DeductionClass { get; private set; }

    public CompensationCalculationType CalculationType { get; private set; }

    // Monto (si FIXED) o porcentaje (si PERCENTAGE). Siempre >= 0 (el signo lo da Nature).
    public decimal Value { get; private set; }

    // Requerido cuando CalculationType = PERCENTAGE (catálogo calculation-bases).
    public string? CalculationBaseCode { get; private set; }

    // Carga patronal (ISSS/AFP) — se guarda aunque no se descuente al empleado.
    public decimal? EmployerRate { get; private set; }

    public decimal? ContributionCap { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string PayPeriodCode { get; private set; } = string.Empty;

    public string? CounterpartyName { get; private set; }

    public string? ExternalReference { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    // true cuando el concepto fue propuesto automáticamente (ISSS/AFP al crear la plaza, D-20).
    public bool IsSystemSuggested { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileCompensationConcept Create(
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        bool isSystemSuggested,
        string? notes) =>
        new(
            assignedPositionPublicId,
            nature,
            conceptTypeCode,
            deductionClass,
            calculationType,
            value,
            calculationBaseCode,
            employerRate,
            contributionCap,
            currencyCode,
            payPeriodCode,
            counterpartyName,
            externalReference,
            startDate,
            endDate,
            isActive,
            isSystemSuggested,
            notes);

    /// <summary>Replaces the business fields. The active state and the system-suggested flag are preserved.</summary>
    public void Update(
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        AssignedPositionPublicId = assignedPositionPublicId;
        Nature = nature;
        ConceptTypeCode = PersonnelFileNormalization.Clean(conceptTypeCode, nameof(conceptTypeCode));
        DeductionClass = deductionClass;
        CalculationType = calculationType;
        Value = value;
        CalculationBaseCode = PersonnelFileNormalization.CleanOptional(calculationBaseCode);
        EmployerRate = employerRate;
        ContributionCap = contributionCap;
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        PayPeriodCode = PersonnelFileNormalization.Clean(payPeriodCode, nameof(payPeriodCode));
        CounterpartyName = PersonnelFileNormalization.CleanOptional(counterpartyName);
        ExternalReference = PersonnelFileNormalization.CleanOptional(externalReference);
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }
}
