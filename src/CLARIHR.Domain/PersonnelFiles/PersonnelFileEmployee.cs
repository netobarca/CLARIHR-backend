using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public sealed class PersonnelFileEmployeeProfile : TenantEntity
{
    /// <summary>
    /// Canonical "retired" employment status. Reserved to the retirement module: after D-01 only
    /// <see cref="ApplyRetirement"/> writes it (the legacy PUT rejects it).
    /// </summary>
    public const string RetiredEmploymentStatusCode = "RETIRADO";

    private PersonnelFileEmployeeProfile()
    {
    }

    private PersonnelFileEmployeeProfile(
        string employeeCode,
        string employmentStatusCode,
        DateTime hireDate,
        decimal? minimumMonthlyWage)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Update(employeeCode, employmentStatusCode, hireDate, minimumMonthlyWage);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string EmployeeCode { get; private set; } = string.Empty;

    public string NormalizedEmployeeCode { get; private set; } = string.Empty;

    // Employment status is now a validated catalog code (replaces the former IsEmploymentActive flag).
    public string EmploymentStatusCode { get; private set; } = string.Empty;

    // Company-level start date: the anchor for seniority (antigüedad), independent of plazas. A rehire
    // opens a new period by overwriting it (D-03). Contract data and structure (org/cost/work center)
    // now live per-plaza on the employment assignment.
    public DateTime HireDate { get; private set; }

    // "Baja" metadata (date + reason), set at retirement and cleared on rehire.
    public string? RetirementCategoryCode { get; private set; }

    public string? RetirementReasonCode { get; private set; }

    public string? RetirementNotes { get; private set; }

    public DateTime? RetirementDate { get; private set; }

    // Applicable minimum monthly wage (RF-011 of the settlement module, ratified §17.16: "el salario
    // mínimo debe estar en la ficha del empleado"). Reflects the employee's sector; the settlement
    // copies it as an auditable snapshot with override (a retired profile rejects the PUT, so the
    // override is the escape hatch there). Null = not configured yet.
    public decimal? MinimumMonthlyWage { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileEmployeeProfile Create(
        string employeeCode,
        string employmentStatusCode,
        DateTime hireDate,
        decimal? minimumMonthlyWage = null) =>
        new(employeeCode, employmentStatusCode, hireDate, minimumMonthlyWage);

    /// <summary>
    /// Updates the editable employment data. The retirement metadata is DELIBERATELY untouched (D-01 of the
    /// retirement module, ratified): only <see cref="ApplyRetirement"/> writes it and only
    /// <see cref="ClearRetirement"/> (reversal) or the rehire clear it.
    /// </summary>
    public void Update(
        string employeeCode,
        string employmentStatusCode,
        DateTime hireDate,
        decimal? minimumMonthlyWage = null)
    {
        if (minimumMonthlyWage is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumMonthlyWage), "The minimum monthly wage must be greater than zero.");
        }

        EmployeeCode = PersonnelFileNormalization.Clean(employeeCode, nameof(employeeCode));
        NormalizedEmployeeCode = PersonnelFileNormalization.NormalizeCode(employeeCode);
        EmploymentStatusCode = PersonnelFileNormalization.Clean(employmentStatusCode, nameof(employmentStatusCode));
        HireDate = PersonnelFileNormalization.NormalizeDate(hireDate);
        MinimumMonthlyWage = minimumMonthlyWage;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Consumes the baja on the profile (retirement-module execution, RF-006 — after D-01 the ONLY
    /// writer of retirement metadata): stamps the retirement fields and sets the RETIRADO status.
    /// </summary>
    public void ApplyRetirement(
        string retirementCategoryCode,
        string retirementReasonCode,
        string? retirementNotes,
        DateTime retirementDate)
    {
        var normalizedDate = PersonnelFileNormalization.NormalizeDate(retirementDate);
        if (normalizedDate < HireDate)
        {
            throw new InvalidOperationException("The retirement date cannot precede the hire date.");
        }

        RetirementCategoryCode = PersonnelFileNormalization.Clean(retirementCategoryCode, nameof(retirementCategoryCode)).ToUpperInvariant();
        RetirementReasonCode = PersonnelFileNormalization.Clean(retirementReasonCode, nameof(retirementReasonCode)).ToUpperInvariant();
        RetirementNotes = PersonnelFileNormalization.CleanOptional(retirementNotes);
        RetirementDate = normalizedDate;
        EmploymentStatusCode = RetiredEmploymentStatusCode;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reversal of an executed baja (RF-010/D-11): clears the retirement metadata and restores the PRIOR
    /// employment status captured in the execution snapshot (never assumes ACTIVO).
    /// </summary>
    public void ClearRetirement(string restoreEmploymentStatusCode)
    {
        RetirementCategoryCode = null;
        RetirementReasonCode = null;
        RetirementNotes = null;
        RetirementDate = null;
        EmploymentStatusCode = PersonnelFileNormalization.Clean(restoreEmploymentStatusCode, nameof(restoreEmploymentStatusCode));
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFileEmploymentAssignment : TenantEntity
{
    private PersonnelFileEmploymentAssignment()
    {
    }

    private PersonnelFileEmploymentAssignment(
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        bool isActive,
        string? notes,
        string? paymentMethodCode,
        Guid? paymentBankAccountPublicId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        AssignmentTypeCode = PersonnelFileNormalization.Clean(assignmentTypeCode, nameof(assignmentTypeCode));
        ContractTypeCode = PersonnelFileNormalization.CleanOptional(contractTypeCode);
        WorkdayCode = PersonnelFileNormalization.CleanOptional(workdayCode);
        PayrollTypeCode = PersonnelFileNormalization.CleanOptional(payrollTypeCode);
        PositionSlotPublicId = positionSlotPublicId;
        OrgUnitPublicId = orgUnitPublicId;
        WorkCenterPublicId = workCenterPublicId;
        CostCenterPublicId = costCenterPublicId;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsPrimary = isPrimary;
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        PaymentMethodCode = PersonnelFileNormalization.CleanOptional(paymentMethodCode);
        PaymentBankAccountPublicId = paymentBankAccountPublicId;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string AssignmentTypeCode { get; private set; } = string.Empty;

    // Per-plaza contract data: each assignment carries its own contract modality and work/payroll setup.
    // The assignment's StartDate/EndDate double as the contract vigencia (no separate contract date columns).
    public string? ContractTypeCode { get; private set; }

    public string? WorkdayCode { get; private set; }

    public string? PayrollTypeCode { get; private set; }

    // Forma de pago de la plaza: método (p. ej. transferencia/cheque/efectivo) + la cuenta bancaria del
    // empleado a usar (validada contra sus cuentas configuradas). Reemplaza la forma de pago a nivel empleado.
    public string? PaymentMethodCode { get; private set; }

    public Guid? PaymentBankAccountPublicId { get; private set; }

    public Guid? PositionSlotPublicId { get; private set; }

    public Guid? OrgUnitPublicId { get; private set; }

    public Guid? WorkCenterPublicId { get; private set; }

    public Guid? CostCenterPublicId { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileEmploymentAssignment Create(
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        bool isActive,
        string? notes,
        string? paymentMethodCode = null,
        Guid? paymentBankAccountPublicId = null) =>
        new(
            assignmentTypeCode,
            contractTypeCode,
            workdayCode,
            payrollTypeCode,
            positionSlotPublicId,
            orgUnitPublicId,
            workCenterPublicId,
            costCenterPublicId,
            startDate,
            endDate,
            isPrimary,
            isActive,
            notes,
            paymentMethodCode,
            paymentBankAccountPublicId);

    public void Update(
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        string? notes,
        string? paymentMethodCode = null,
        Guid? paymentBankAccountPublicId = null)
    {
        ConcurrencyToken = Guid.NewGuid();
        AssignmentTypeCode = PersonnelFileNormalization.Clean(assignmentTypeCode, nameof(assignmentTypeCode));
        ContractTypeCode = PersonnelFileNormalization.CleanOptional(contractTypeCode);
        WorkdayCode = PersonnelFileNormalization.CleanOptional(workdayCode);
        PayrollTypeCode = PersonnelFileNormalization.CleanOptional(payrollTypeCode);
        PositionSlotPublicId = positionSlotPublicId;
        OrgUnitPublicId = orgUnitPublicId;
        WorkCenterPublicId = workCenterPublicId;
        CostCenterPublicId = costCenterPublicId;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsPrimary = isPrimary;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        PaymentMethodCode = PersonnelFileNormalization.CleanOptional(paymentMethodCode);
        PaymentBankAccountPublicId = paymentBankAccountPublicId;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Closes an active assignment of a prior employment period: ends it and deactivates it (RF-004).</summary>
    public void Close(DateTime endDate)
    {
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reversal counterpart of <see cref="Close(DateTime)"/> (retirement module, RF-010/D-11): restores the
    /// EXACT pre-execution state — the end date the row had BEFORE the baja (null when the execution set it)
    /// — and reactivates the row. Never invents an end date.
    /// </summary>
    public void Reopen(DateTime? previousEndDate)
    {
        EndDate = PersonnelFileNormalization.NormalizeDate(previousEndDate);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFileContractHistory : TenantEntity
{
    private PersonnelFileContractHistory()
    {
    }

    private PersonnelFileContractHistory(
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        bool isActive,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        ContractTypeCode = PersonnelFileNormalization.Clean(contractTypeCode, nameof(contractTypeCode));
        ContractDate = PersonnelFileNormalization.NormalizeDate(contractDate);
        ContractEndDate = PersonnelFileNormalization.NormalizeDate(contractEndDate);
        PositionSlotPublicId = positionSlotPublicId;
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string ContractTypeCode { get; private set; } = string.Empty;

    public DateTime ContractDate { get; private set; }

    public DateTime? ContractEndDate { get; private set; }

    public Guid? PositionSlotPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileContractHistory Create(
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        bool isActive,
        string? notes) =>
        new(contractTypeCode, contractDate, contractEndDate, positionSlotPublicId, isActive, notes);

    public void Update(
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        ContractTypeCode = PersonnelFileNormalization.Clean(contractTypeCode, nameof(contractTypeCode));
        ContractDate = PersonnelFileNormalization.NormalizeDate(contractDate);
        ContractEndDate = PersonnelFileNormalization.NormalizeDate(contractEndDate);
        PositionSlotPublicId = positionSlotPublicId;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Closes the active contract of a prior employment period: sets its end date and deactivates it (RF-004).</summary>
    public void Close(DateTime contractEndDate)
    {
        ContractEndDate = PersonnelFileNormalization.NormalizeDate(contractEndDate);
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reversal counterpart of <see cref="Close(DateTime)"/> (retirement module, RF-010/D-11): restores the
    /// EXACT pre-execution state — the contract end date the row had BEFORE the baja (null when the execution
    /// set it) — and reactivates the row.
    /// </summary>
    public void Reopen(DateTime? previousContractEndDate)
    {
        ContractEndDate = PersonnelFileNormalization.NormalizeDate(previousContractEndDate);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFileAdditionalBenefit : TenantEntity
{
    private PersonnelFileAdditionalBenefit()
    {
    }

    private PersonnelFileAdditionalBenefit(
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        bool isActive,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        BenefitTypeCode = PersonnelFileNormalization.Clean(benefitTypeCode, nameof(benefitTypeCode));
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string BenefitTypeCode { get; private set; } = string.Empty;

    public DateTime? StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        BenefitTypeCode = PersonnelFileNormalization.Clean(benefitTypeCode, nameof(benefitTypeCode));
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileAdditionalBenefit Create(
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        bool isActive,
        string? notes) =>
        new(benefitTypeCode, startDate, endDate, isActive, notes);
}

public sealed class PersonnelFileAuthorizationSubstitution : TenantEntity
{
    private PersonnelFileAuthorizationSubstitution()
    {
    }

    private PersonnelFileAuthorizationSubstitution(
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        SubstitutionTypeCode = PersonnelFileNormalization.Clean(substitutionTypeCode, nameof(substitutionTypeCode));
        SubstitutePersonnelFilePublicId = substitutePersonnelFilePublicId;
        SubstitutePositionSlotPublicId = substitutePositionSlotPublicId;
        SubstitutePositionTitleSnapshot = PersonnelFileNormalization.CleanOptional(substitutePositionTitleSnapshot);
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string SubstitutionTypeCode { get; private set; } = string.Empty;

    public Guid SubstitutePersonnelFilePublicId { get; private set; }

    // The substitute's position is a reference to one of their ACTIVE position-slot assignments (D-02),
    // not free text; the title at designation time is snapshotted alongside it for history/UI (RF-003).
    public Guid SubstitutePositionSlotPublicId { get; private set; }

    public string? SubstitutePositionTitleSnapshot { get; private set; }

    public DateTime StartDate { get; private set; }

    // End date is mandatory (D-03): no open-ended substitutions.
    public DateTime EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileAuthorizationSubstitution Create(
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        string? notes) =>
        new(substitutionTypeCode, substitutePersonnelFilePublicId, substitutePositionSlotPublicId, substitutePositionTitleSnapshot, startDate, endDate, isActive, notes);

    public void Update(
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        SubstitutionTypeCode = PersonnelFileNormalization.Clean(substitutionTypeCode, nameof(substitutionTypeCode));
        SubstitutePersonnelFilePublicId = substitutePersonnelFilePublicId;
        SubstitutePositionSlotPublicId = substitutePositionSlotPublicId;
        SubstitutePositionTitleSnapshot = PersonnelFileNormalization.CleanOptional(substitutePositionTitleSnapshot);
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

public sealed class PersonnelFilePersonnelAction : TenantEntity
{
    private PersonnelFilePersonnelAction()
    {
    }

    private PersonnelFilePersonnelAction(
        string actionTypeCode,
        string actionStatusCode,
        DateTime actionDateUtc,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? description,
        string? reference,
        decimal? amount,
        string? currencyCode,
        bool isSystemGenerated)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        ActionTypeCode = PersonnelFileNormalization.Clean(actionTypeCode, nameof(actionTypeCode));
        ActionStatusCode = PersonnelFileNormalization.Clean(actionStatusCode, nameof(actionStatusCode));
        ActionDateUtc = PersonnelFileNormalization.NormalizeDate(actionDateUtc);
        EffectiveFromUtc = PersonnelFileNormalization.NormalizeDate(effectiveFromUtc);
        EffectiveToUtc = PersonnelFileNormalization.NormalizeDate(effectiveToUtc);
        Description = PersonnelFileNormalization.CleanOptional(description);
        Reference = PersonnelFileNormalization.CleanOptional(reference);
        Amount = amount;
        CurrencyCode = PersonnelFileNormalization.CleanOptional(currencyCode);
        IsSystemGenerated = isSystemGenerated;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string ActionTypeCode { get; private set; } = string.Empty;

    public string ActionStatusCode { get; private set; } = string.Empty;

    public DateTime ActionDateUtc { get; private set; }

    public DateTime? EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public string? Description { get; private set; }

    public string? Reference { get; private set; }

    public decimal? Amount { get; private set; }

    public string? CurrencyCode { get; private set; }

    public bool IsSystemGenerated { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFilePersonnelAction Create(
        string actionTypeCode,
        string actionStatusCode,
        DateTime actionDateUtc,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? description,
        string? reference,
        decimal? amount,
        string? currencyCode,
        bool isSystemGenerated) =>
        new(actionTypeCode, actionStatusCode, actionDateUtc, effectiveFromUtc, effectiveToUtc, description, reference, amount, currencyCode, isSystemGenerated);
}

public sealed class PersonnelFilePayrollTransaction : TenantEntity
{
    private PersonnelFilePayrollTransaction()
    {
    }

    private PersonnelFilePayrollTransaction(
        string transactionTypeCode,
        DateTime transactionDateUtc,
        string payrollPeriodCode,
        string? description,
        decimal amount,
        string currencyCode,
        bool isDebit,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        TransactionTypeCode = PersonnelFileNormalization.Clean(transactionTypeCode, nameof(transactionTypeCode));
        TransactionDateUtc = PersonnelFileNormalization.NormalizeDate(transactionDateUtc);
        PayrollPeriodCode = PersonnelFileNormalization.Clean(payrollPeriodCode, nameof(payrollPeriodCode));
        Description = PersonnelFileNormalization.CleanOptional(description);
        Amount = amount;
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        IsDebit = isDebit;
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string TransactionTypeCode { get; private set; } = string.Empty;

    public DateTime TransactionDateUtc { get; private set; }

    public string PayrollPeriodCode { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public bool IsDebit { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFilePayrollTransaction Create(
        string transactionTypeCode,
        DateTime transactionDateUtc,
        string payrollPeriodCode,
        string? description,
        decimal amount,
        string currencyCode,
        bool isDebit,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            transactionTypeCode,
            transactionDateUtc,
            payrollPeriodCode,
            description,
            amount,
            currencyCode,
            isDebit,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
}

public sealed class PersonnelFileAssetAccess : TenantEntity
{
    private PersonnelFileAssetAccess()
    {
    }

    private PersonnelFileAssetAccess(
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        bool isActive,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        AssetTypeCode = PersonnelFileNormalization.Clean(assetTypeCode, nameof(assetTypeCode));
        AssetOrAccessName = PersonnelFileNormalization.Clean(assetOrAccessName, nameof(assetOrAccessName));
        AccessLevelCode = PersonnelFileNormalization.CleanOptional(accessLevelCode);
        StartDateUtc = PersonnelFileNormalization.NormalizeDate(startDateUtc);
        EndDateUtc = PersonnelFileNormalization.NormalizeDate(endDateUtc);
        DeliveryDateUtc = PersonnelFileNormalization.NormalizeDate(deliveryDateUtc);
        DeliveryStatusCode = PersonnelFileNormalization.CleanOptional(deliveryStatusCode);
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string AssetTypeCode { get; private set; } = string.Empty;

    public string AssetOrAccessName { get; private set; } = string.Empty;

    public string? AccessLevelCode { get; private set; }

    public DateTime StartDateUtc { get; private set; }

    public DateTime? EndDateUtc { get; private set; }

    public DateTime? DeliveryDateUtc { get; private set; }

    public string? DeliveryStatusCode { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileAssetAccess Create(
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        bool isActive,
        string? notes) =>
        new(
            assetTypeCode,
            assetOrAccessName,
            accessLevelCode,
            startDateUtc,
            endDateUtc,
            deliveryDateUtc,
            deliveryStatusCode,
            isActive,
            notes);

    public void Update(
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        AssetTypeCode = PersonnelFileNormalization.Clean(assetTypeCode, nameof(assetTypeCode));
        AssetOrAccessName = PersonnelFileNormalization.Clean(assetOrAccessName, nameof(assetOrAccessName));
        AccessLevelCode = PersonnelFileNormalization.CleanOptional(accessLevelCode);
        StartDateUtc = PersonnelFileNormalization.NormalizeDate(startDateUtc);
        EndDateUtc = PersonnelFileNormalization.NormalizeDate(endDateUtc);
        DeliveryDateUtc = PersonnelFileNormalization.NormalizeDate(deliveryDateUtc);
        DeliveryStatusCode = PersonnelFileNormalization.CleanOptional(deliveryStatusCode);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFileInsurance : TenantEntity
{
    private readonly List<PersonnelFileInsuranceBeneficiary> _beneficiaries = [];

    private PersonnelFileInsurance()
    {
    }

    private PersonnelFileInsurance(
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        bool isActive,
        DateTime? startDateUtc,
        DateTime? endDateUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        InsuranceCode = PersonnelFileNormalization.Clean(insuranceCode, nameof(insuranceCode));
        EmployeeContribution = employeeContribution;
        EmployerContribution = employerContribution;
        RangeCode = PersonnelFileNormalization.CleanOptional(rangeCode);
        PolicyNumber = PersonnelFileNormalization.CleanOptional(policyNumber);
        InsuredAmount = insuredAmount;
        CurrencyCode = PersonnelFileNormalization.CleanOptional(currencyCode);
        IsActive = isActive;
        StartDateUtc = PersonnelFileNormalization.NormalizeDate(startDateUtc);
        EndDateUtc = PersonnelFileNormalization.NormalizeDate(endDateUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string InsuranceCode { get; private set; } = string.Empty;

    public decimal? EmployeeContribution { get; private set; }

    public decimal? EmployerContribution { get; private set; }

    public string? RangeCode { get; private set; }

    public string? PolicyNumber { get; private set; }

    public decimal? InsuredAmount { get; private set; }

    public string? CurrencyCode { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime? StartDateUtc { get; private set; }

    public DateTime? EndDateUtc { get; private set; }

    public IReadOnlyCollection<PersonnelFileInsuranceBeneficiary> Beneficiaries => _beneficiaries;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        DateTime? startDateUtc,
        DateTime? endDateUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        InsuranceCode = PersonnelFileNormalization.Clean(insuranceCode, nameof(insuranceCode));
        EmployeeContribution = employeeContribution;
        EmployerContribution = employerContribution;
        RangeCode = PersonnelFileNormalization.CleanOptional(rangeCode);
        PolicyNumber = PersonnelFileNormalization.CleanOptional(policyNumber);
        InsuredAmount = insuredAmount;
        CurrencyCode = PersonnelFileNormalization.CleanOptional(currencyCode);
        StartDateUtc = PersonnelFileNormalization.NormalizeDate(startDateUtc);
        EndDateUtc = PersonnelFileNormalization.NormalizeDate(endDateUtc);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileInsurance Create(
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        bool isActive,
        DateTime? startDateUtc,
        DateTime? endDateUtc) =>
        new(
            insuranceCode,
            employeeContribution,
            employerContribution,
            rangeCode,
            policyNumber,
            insuredAmount,
            currencyCode,
            isActive,
            startDateUtc,
            endDateUtc);
}

public sealed class PersonnelFileInsuranceBeneficiary : TenantEntity
{
    public const string TypePrimary = "PRINCIPAL";
    public const string TypeContingent = "CONTINGENTE";

    private PersonnelFileInsuranceBeneficiary()
    {
    }

    private PersonnelFileInsuranceBeneficiary(
        string fullName,
        string? documentNumber,
        string? documentTypeCode,
        DateTime? birthDate,
        string kinshipCode,
        decimal? allocationPercentage,
        string? beneficiaryType)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        FullName = PersonnelFileNormalization.Clean(fullName, nameof(fullName));
        DocumentNumber = PersonnelFileNormalization.CleanOptional(documentNumber);
        DocumentTypeCode = PersonnelFileNormalization.CleanOptional(documentTypeCode);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        KinshipCode = PersonnelFileNormalization.Clean(kinshipCode, nameof(kinshipCode));
        AllocationPercentage = allocationPercentage;
        BeneficiaryType = NormalizeBeneficiaryType(beneficiaryType);
        IsActive = true;
    }

    public long InsuranceId { get; private set; }

    public PersonnelFileInsurance Insurance { get; private set; } = null!;

    public string FullName { get; private set; } = string.Empty;

    public string? DocumentNumber { get; private set; }

    public string? DocumentTypeCode { get; private set; }

    public DateTime? BirthDate { get; private set; }

    public string KinshipCode { get; private set; } = string.Empty;

    public decimal? AllocationPercentage { get; private set; }

    public string? BeneficiaryType { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToInsurance(long insuranceId) => InsuranceId = insuranceId;

    public void Update(
        string fullName,
        string? documentNumber,
        string? documentTypeCode,
        DateTime? birthDate,
        string kinshipCode,
        decimal? allocationPercentage,
        string? beneficiaryType)
    {
        ConcurrencyToken = Guid.NewGuid();
        FullName = PersonnelFileNormalization.Clean(fullName, nameof(fullName));
        DocumentNumber = PersonnelFileNormalization.CleanOptional(documentNumber);
        DocumentTypeCode = PersonnelFileNormalization.CleanOptional(documentTypeCode);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        KinshipCode = PersonnelFileNormalization.Clean(kinshipCode, nameof(kinshipCode));
        AllocationPercentage = allocationPercentage;
        BeneficiaryType = NormalizeBeneficiaryType(beneficiaryType);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileInsuranceBeneficiary Create(
        string fullName,
        string? documentNumber,
        string? documentTypeCode,
        DateTime? birthDate,
        string kinshipCode,
        decimal? allocationPercentage,
        string? beneficiaryType) =>
        new(fullName, documentNumber, documentTypeCode, birthDate, kinshipCode, allocationPercentage, beneficiaryType);

    private static string? NormalizeBeneficiaryType(string? beneficiaryType) =>
        string.IsNullOrWhiteSpace(beneficiaryType) ? null : beneficiaryType.Trim().ToUpperInvariant();
}

public sealed class PersonnelFileMedicalClaim : TenantEntity
{
    private PersonnelFileMedicalClaim()
    {
    }

    private PersonnelFileMedicalClaim(
        Guid insurancePublicId,
        string? insuranceNameSnapshot,
        string? accountNumber,
        string claimantType,
        Guid? beneficiaryPublicId,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        string? notes,
        DateTime claimDateUtc,
        DateTime? resolutionDateUtc,
        string? claimStatusCode,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Apply(
            insurancePublicId,
            insuranceNameSnapshot,
            accountNumber,
            claimantType,
            beneficiaryPublicId,
            patientNameSnapshot,
            kinshipCodeSnapshot,
            claimTypeCode,
            diagnosis,
            claimAmount,
            currencyCode,
            paidAmount,
            notes,
            claimDateUtc,
            resolutionDateUtc,
            claimStatusCode,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public Guid InsurancePublicId { get; private set; }

    public string? InsuranceNameSnapshot { get; private set; }

    public string? AccountNumber { get; private set; }

    public string ClaimantType { get; private set; } = MedicalClaimClaimantTypes.Titular;

    public Guid? BeneficiaryPublicId { get; private set; }

    public string? PatientNameSnapshot { get; private set; }

    public string? KinshipCodeSnapshot { get; private set; }

    public string ClaimTypeCode { get; private set; } = string.Empty;

    public string? Diagnosis { get; private set; }

    public decimal? ClaimAmount { get; private set; }

    public string? CurrencyCode { get; private set; }

    public decimal? PaidAmount { get; private set; }

    public int? ResponseTimeDays { get; private set; }

    public string? Notes { get; private set; }

    public DateTime ClaimDateUtc { get; private set; }

    public DateTime? ResolutionDateUtc { get; private set; }

    public string? ClaimStatusCode { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        Guid insurancePublicId,
        string? insuranceNameSnapshot,
        string? accountNumber,
        string claimantType,
        Guid? beneficiaryPublicId,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        string? notes,
        DateTime claimDateUtc,
        DateTime? resolutionDateUtc,
        string? claimStatusCode,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        Apply(
            insurancePublicId,
            insuranceNameSnapshot,
            accountNumber,
            claimantType,
            beneficiaryPublicId,
            patientNameSnapshot,
            kinshipCodeSnapshot,
            claimTypeCode,
            diagnosis,
            claimAmount,
            currencyCode,
            paidAmount,
            notes,
            claimDateUtc,
            resolutionDateUtc,
            claimStatusCode,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Derives the response time in days from the claim and resolution dates (decision D-07).
    /// Returns null when there is no resolution date or it precedes the claim date.
    /// </summary>
    public static int? DeriveResponseTimeDays(DateTime claimDateUtc, DateTime? resolutionDateUtc) =>
        resolutionDateUtc is { } resolution && resolution.Date >= claimDateUtc.Date
            ? (int)(resolution.Date - claimDateUtc.Date).TotalDays
            : null;

    private void Apply(
        Guid insurancePublicId,
        string? insuranceNameSnapshot,
        string? accountNumber,
        string claimantType,
        Guid? beneficiaryPublicId,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        string? notes,
        DateTime claimDateUtc,
        DateTime? resolutionDateUtc,
        string? claimStatusCode,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        InsurancePublicId = insurancePublicId;
        InsuranceNameSnapshot = PersonnelFileNormalization.CleanOptional(insuranceNameSnapshot);
        AccountNumber = PersonnelFileNormalization.CleanOptional(accountNumber);
        ClaimantType = MedicalClaimClaimantTypes.Normalize(claimantType);
        BeneficiaryPublicId = ClaimantType == MedicalClaimClaimantTypes.Beneficiario ? beneficiaryPublicId : null;
        PatientNameSnapshot = PersonnelFileNormalization.CleanOptional(patientNameSnapshot);
        KinshipCodeSnapshot = NormalizeOptionalCode(kinshipCodeSnapshot);
        ClaimTypeCode = PersonnelFileNormalization.Clean(claimTypeCode, nameof(claimTypeCode));
        Diagnosis = PersonnelFileNormalization.CleanOptional(diagnosis);
        ClaimAmount = claimAmount;
        CurrencyCode = NormalizeOptionalCode(currencyCode);
        PaidAmount = paidAmount;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        ClaimDateUtc = PersonnelFileNormalization.NormalizeDate(claimDateUtc);
        ResolutionDateUtc = PersonnelFileNormalization.NormalizeDate(resolutionDateUtc);
        ResponseTimeDays = DeriveResponseTimeDays(ClaimDateUtc, ResolutionDateUtc);
        ClaimStatusCode = NormalizeOptionalCode(claimStatusCode);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    private static string? NormalizeOptionalCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    public static PersonnelFileMedicalClaim Create(
        Guid insurancePublicId,
        string? insuranceNameSnapshot,
        string? accountNumber,
        string claimantType,
        Guid? beneficiaryPublicId,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        string? notes,
        DateTime claimDateUtc,
        DateTime? resolutionDateUtc,
        string? claimStatusCode,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            insurancePublicId,
            insuranceNameSnapshot,
            accountNumber,
            claimantType,
            beneficiaryPublicId,
            patientNameSnapshot,
            kinshipCodeSnapshot,
            claimTypeCode,
            diagnosis,
            claimAmount,
            currencyCode,
            paidAmount,
            notes,
            claimDateUtc,
            resolutionDateUtc,
            claimStatusCode,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
}

/// <summary>
/// Constrained claimant-type codes for a medical claim (decision D-02). Modelled as a constrained
/// string set — like beneficiary type — instead of a country-scoped catalog, since the values are fixed.
/// </summary>
public static class MedicalClaimClaimantTypes
{
    public const string Titular = "TITULAR";
    public const string Beneficiario = "BENEFICIARIO";

    public static readonly IReadOnlyCollection<string> All = new[] { Titular, Beneficiario };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToUpperInvariant());

    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Titular : value.Trim().ToUpperInvariant();
}

/// <summary>
/// Supporting document attached to a medical claim (decision D-11 / RF-012). Mirrors
/// <see cref="PersonnelFileDocument"/> and reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.MedicalClaimDocument</c>).
/// </summary>
public sealed class MedicalClaimDocument : TenantEntity
{
    private MedicalClaimDocument()
    {
    }

    private MedicalClaimDocument(
        Guid publicId,
        long documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations)
    {
        if (documentTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentTypeCatalogItemId), "Document type catalog item id must be positive.");
        }

        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        PublicId = publicId;
        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long MedicalClaimId { get; private set; }

    public PersonnelFileMedicalClaim MedicalClaim { get; private set; } = null!;

    public long DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToMedicalClaim(long medicalClaimId) => MedicalClaimId = medicalClaimId;

    public static MedicalClaimDocument Create(
        Guid publicId,
        long documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes, observations);

    public void ReplaceFileReference(
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes)
    {
        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void UpdateMetadata(
        long documentTypeCatalogItemId,
        string? observations)
    {
        if (documentTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentTypeCatalogItemId), "Document type catalog item id must be positive.");
        }

        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}

/// <summary>
/// An off-payroll transaction ("transacción fuera de nómina"): a company expense incurred on behalf of an
/// employee that is NOT part of payroll (work tools, PPE, uniforms, promotional items, recognitions, gifts…).
/// Distinct sibling of <see cref="PersonnelFilePayrollTransaction"/> (the in-payroll immutable ledger): the
/// type comes from a managed catalog (D-02/D-03), the period is an explicit imputation Year + Month (D-05),
/// the amount may be negative for an adjustment that references the original transaction it corrects (D-04/D-12),
/// and it can be optionally linked to an <see cref="PersonnelFileAssetAccess"/> of the same employee (D-01).
/// Editable HR record (CRUD + soft delete, RN-10); internal to HR — no self-service (D-06).
/// </summary>
public sealed class PersonnelFileOffPayrollTransaction : TenantEntity
{
    private PersonnelFileOffPayrollTransaction()
    {
    }

    private PersonnelFileOffPayrollTransaction(
        string offPayrollTransactionTypeCode,
        string? transactionTypeNameSnapshot,
        DateTime transactionDateUtc,
        string currencyCode,
        decimal amount,
        int year,
        int month,
        string? comment,
        Guid? assetAccessPublicId,
        string? assetNameSnapshot,
        Guid? correctsTransactionPublicId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Apply(
            offPayrollTransactionTypeCode,
            transactionTypeNameSnapshot,
            transactionDateUtc,
            currencyCode,
            amount,
            year,
            month,
            comment,
            assetAccessPublicId,
            assetNameSnapshot,
            correctsTransactionPublicId);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string OffPayrollTransactionTypeCode { get; private set; } = string.Empty;

    public string? TransactionTypeNameSnapshot { get; private set; }

    public DateTime TransactionDateUtc { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public string? Comment { get; private set; }

    public Guid? AssetAccessPublicId { get; private set; }

    public string? AssetNameSnapshot { get; private set; }

    public Guid? CorrectsTransactionPublicId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Update(
        string offPayrollTransactionTypeCode,
        string? transactionTypeNameSnapshot,
        DateTime transactionDateUtc,
        string currencyCode,
        decimal amount,
        int year,
        int month,
        string? comment,
        Guid? assetAccessPublicId,
        string? assetNameSnapshot,
        Guid? correctsTransactionPublicId)
    {
        ConcurrencyToken = Guid.NewGuid();
        Apply(
            offPayrollTransactionTypeCode,
            transactionTypeNameSnapshot,
            transactionDateUtc,
            currencyCode,
            amount,
            year,
            month,
            comment,
            assetAccessPublicId,
            assetNameSnapshot,
            correctsTransactionPublicId);
    }

    public static PersonnelFileOffPayrollTransaction Create(
        string offPayrollTransactionTypeCode,
        string? transactionTypeNameSnapshot,
        DateTime transactionDateUtc,
        string currencyCode,
        decimal amount,
        int year,
        int month,
        string? comment,
        Guid? assetAccessPublicId,
        string? assetNameSnapshot,
        Guid? correctsTransactionPublicId) =>
        new(
            offPayrollTransactionTypeCode,
            transactionTypeNameSnapshot,
            transactionDateUtc,
            currencyCode,
            amount,
            year,
            month,
            comment,
            assetAccessPublicId,
            assetNameSnapshot,
            correctsTransactionPublicId);

    private void Apply(
        string offPayrollTransactionTypeCode,
        string? transactionTypeNameSnapshot,
        DateTime transactionDateUtc,
        string currencyCode,
        decimal amount,
        int year,
        int month,
        string? comment,
        Guid? assetAccessPublicId,
        string? assetNameSnapshot,
        Guid? correctsTransactionPublicId)
    {
        OffPayrollTransactionTypeCode = PersonnelFileNormalization.Clean(offPayrollTransactionTypeCode, nameof(offPayrollTransactionTypeCode)).ToUpperInvariant();
        TransactionTypeNameSnapshot = PersonnelFileNormalization.CleanOptional(transactionTypeNameSnapshot);
        TransactionDateUtc = PersonnelFileNormalization.NormalizeDate(transactionDateUtc);
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode)).ToUpperInvariant();
        Amount = amount;
        Year = year;
        Month = month;
        Comment = PersonnelFileNormalization.CleanOptional(comment);
        AssetAccessPublicId = assetAccessPublicId;
        AssetNameSnapshot = PersonnelFileNormalization.CleanOptional(assetNameSnapshot);
        CorrectsTransactionPublicId = correctsTransactionPublicId;
    }
}

/// <summary>
/// Supporting document ("comprobante de cualquier índole") attached to an off-payroll transaction (D-07).
/// Mirrors <see cref="MedicalClaimDocument"/> and reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.OffPayrollTransactionDocument</c>), but the
/// document-type classification is OPTIONAL here (D-07 — any kind of receipt), so the catalog FK is nullable.
/// </summary>
public sealed class OffPayrollTransactionDocument : TenantEntity
{
    private OffPayrollTransactionDocument()
    {
    }

    private OffPayrollTransactionDocument(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations)
    {
        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        PublicId = publicId;
        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long OffPayrollTransactionId { get; private set; }

    public PersonnelFileOffPayrollTransaction OffPayrollTransaction { get; private set; } = null!;

    public long? DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToOffPayrollTransaction(long offPayrollTransactionId) => OffPayrollTransactionId = offPayrollTransactionId;

    public static OffPayrollTransactionDocument Create(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes, observations);

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}

/// <summary>
/// Canonical status codes for an economic-aid request. The codes are validated against the country-scoped
/// <c>economic-aid-statuses</c> catalog (configurable / i18n), but the domain transition logic references these
/// constants. A future approval flow may ADD intermediate catalog states without changing the domain.
/// </summary>
public static class EconomicAidRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string EnRevision = "EN_REVISION";
    public const string PendienteDocumentacion = "PENDIENTE_DOCUMENTACION";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Desembolsada = "DESEMBOLSADA";
    public const string Anulada = "ANULADA";

    /// <summary>States from which a request may still be resolved or canceled.</summary>
    public static readonly IReadOnlyCollection<string> Pending = new[] { Solicitada, EnRevision, PendienteDocumentacion };

    /// <summary>States a manager may set through the resolution action.</summary>
    public static readonly IReadOnlyCollection<string> ResolutionTargets = new[] { EnRevision, PendienteDocumentacion, Aprobada, Rechazada };
}

/// <summary>
/// Employee-initiated economic-aid request ("ayuda económica" — emergency assistance the company grants and HR
/// validates). Self-service create/read by the employee; validation (resolution)/disbursement are HR-only.
/// Fase 1: non-refundable subsidy, single-step HR validation, informational disbursement. The status is a
/// country-scoped catalog code (forward-compatible with an approval flow); see <see cref="EconomicAidRequestStatuses"/>.
/// </summary>
public sealed class PersonnelFileEconomicAidRequest : TenantEntity
{
    private PersonnelFileEconomicAidRequest()
    {
    }

    private PersonnelFileEconomicAidRequest(
        string economicAidTypeCode,
        string? typeNameSnapshot,
        string description,
        decimal requestedAmount,
        string currencyCode,
        DateTime requestDateUtc,
        Guid requestedByUserId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        RequestStatusCode = EconomicAidRequestStatuses.Solicitada;
        RequestDateUtc = PersonnelFileNormalization.NormalizeDate(requestDateUtc);
        RequestedByUserId = requestedByUserId;
        ApplyRequestFields(economicAidTypeCode, typeNameSnapshot, description, requestedAmount, currencyCode);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string EconomicAidTypeCode { get; private set; } = string.Empty;

    public string? TypeNameSnapshot { get; private set; }

    public string RequestStatusCode { get; private set; } = EconomicAidRequestStatuses.Solicitada;

    public string Description { get; private set; } = string.Empty;

    public decimal RequestedAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public DateTime RequestDateUtc { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public decimal? ApprovedAmount { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTime? ResolutionDateUtc { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public int? ResponseTimeDays { get; private set; }

    public decimal? DisbursedAmount { get; private set; }

    public DateTime? DisbursementDateUtc { get; private set; }

    public string? PaymentMethodCode { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileEconomicAidRequest Create(
        string economicAidTypeCode,
        string? typeNameSnapshot,
        string description,
        decimal requestedAmount,
        string currencyCode,
        DateTime requestDateUtc,
        Guid requestedByUserId) =>
        new(economicAidTypeCode, typeNameSnapshot, description, requestedAmount, currencyCode, requestDateUtc, requestedByUserId);

    /// <summary>Edits the request's business fields (RR. HH.); does not change status or resolution.</summary>
    public void Update(
        string economicAidTypeCode,
        string? typeNameSnapshot,
        string description,
        decimal requestedAmount,
        string currencyCode)
    {
        ConcurrencyToken = Guid.NewGuid();
        ApplyRequestFields(economicAidTypeCode, typeNameSnapshot, description, requestedAmount, currencyCode);
    }

    /// <summary>
    /// HR validation (D-03). Transitions a pending request to a resolution target (EN_REVISION /
    /// PENDIENTE_DOCUMENTACION / APROBADA / RECHAZADA). Approving requires a positive amount (D-05). For a
    /// terminal decision (APROBADA/RECHAZADA) the resolver, date and derived response-time are recorded.
    /// </summary>
    public void Resolve(string targetStatusCode, decimal? approvedAmount, Guid decidedByUserId, DateTime decidedAtUtc, string? notes)
    {
        if (!EconomicAidRequestStatuses.Pending.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only a pending economic-aid request can be resolved.");
        }

        var normalizedTarget = PersonnelFileNormalization.Clean(targetStatusCode, nameof(targetStatusCode)).ToUpperInvariant();
        if (!EconomicAidRequestStatuses.ResolutionTargets.Contains(normalizedTarget))
        {
            throw new InvalidOperationException("The target status is not a valid resolution target.");
        }

        var isApproval = normalizedTarget == EconomicAidRequestStatuses.Aprobada;
        if (isApproval && approvedAmount is not > 0m)
        {
            throw new InvalidOperationException("Approved amount must be greater than zero when approving.");
        }

        var isTerminal = isApproval || normalizedTarget == EconomicAidRequestStatuses.Rechazada;

        RequestStatusCode = normalizedTarget;
        ResolutionNotes = PersonnelFileNormalization.CleanOptional(notes);

        if (isTerminal)
        {
            ApprovedAmount = isApproval ? approvedAmount : null;
            ResolvedByUserId = decidedByUserId;
            ResolutionDateUtc = PersonnelFileNormalization.NormalizeDate(decidedAtUtc);
            ResponseTimeDays = DeriveResponseTimeDays(RequestDateUtc, ResolutionDateUtc);
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Records the (informational) disbursement of an approved request (D-09).</summary>
    public void Disburse(decimal disbursedAmount, DateTime disbursementDateUtc, string? paymentMethodCode)
    {
        if (RequestStatusCode != EconomicAidRequestStatuses.Aprobada)
        {
            throw new InvalidOperationException("Only an approved economic-aid request can be disbursed.");
        }

        DisbursedAmount = disbursedAmount;
        DisbursementDateUtc = PersonnelFileNormalization.NormalizeDate(disbursementDateUtc);
        PaymentMethodCode = PersonnelFileNormalization.CleanOptional(paymentMethodCode)?.ToUpperInvariant();
        RequestStatusCode = EconomicAidRequestStatuses.Desembolsada;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Cancels a still-pending request (self-service for the owner, or HR) — D-11.</summary>
    public void Cancel()
    {
        if (!EconomicAidRequestStatuses.Pending.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only a pending economic-aid request can be canceled.");
        }

        RequestStatusCode = EconomicAidRequestStatuses.Anulada;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Derived response time in days = resolution − request (null when unresolved or incoherent).</summary>
    public static int? DeriveResponseTimeDays(DateTime requestDateUtc, DateTime? resolutionDateUtc) =>
        resolutionDateUtc is { } resolution && resolution.Date >= requestDateUtc.Date
            ? (int)(resolution.Date - requestDateUtc.Date).TotalDays
            : null;

    private void ApplyRequestFields(
        string economicAidTypeCode,
        string? typeNameSnapshot,
        string description,
        decimal requestedAmount,
        string currencyCode)
    {
        EconomicAidTypeCode = PersonnelFileNormalization.Clean(economicAidTypeCode, nameof(economicAidTypeCode)).ToUpperInvariant();
        TypeNameSnapshot = PersonnelFileNormalization.CleanOptional(typeNameSnapshot);
        Description = PersonnelFileNormalization.Clean(description, nameof(description));
        RequestedAmount = requestedAmount;
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode)).ToUpperInvariant();
    }
}

/// <summary>
/// Supporting document attached to an economic-aid request (D-06 — evidence of the emergency). Mirrors
/// <see cref="OffPayrollTransactionDocument"/> and reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.EconomicAidRequestDocument</c>); the
/// document-type classification is OPTIONAL (nullable FK).
/// </summary>
public sealed class EconomicAidRequestDocument : TenantEntity
{
    private EconomicAidRequestDocument()
    {
    }

    private EconomicAidRequestDocument(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations)
    {
        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        PublicId = publicId;
        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long EconomicAidRequestId { get; private set; }

    public PersonnelFileEconomicAidRequest EconomicAidRequest { get; private set; } = null!;

    public long? DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToEconomicAidRequest(long economicAidRequestId) => EconomicAidRequestId = economicAidRequestId;

    public static EconomicAidRequestDocument Create(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes, observations);

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFilePerformanceEvaluation : TenantEntity
{
    private PersonnelFilePerformanceEvaluation()
    {
    }

    private PersonnelFilePerformanceEvaluation(
        string evaluatorName,
        DateTime evaluationDateUtc,
        decimal? score,
        string? qualitativeScoreCode,
        string? comment,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        EvaluatorName = PersonnelFileNormalization.Clean(evaluatorName, nameof(evaluatorName));
        EvaluationDateUtc = PersonnelFileNormalization.NormalizeDate(evaluationDateUtc);
        Score = score;
        QualitativeScoreCode = PersonnelFileNormalization.CleanOptional(qualitativeScoreCode);
        Comment = PersonnelFileNormalization.CleanOptional(comment);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string EvaluatorName { get; private set; } = string.Empty;

    public DateTime EvaluationDateUtc { get; private set; }

    public decimal? Score { get; private set; }

    public string? QualitativeScoreCode { get; private set; }

    public string? Comment { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string evaluatorName,
        DateTime evaluationDateUtc,
        decimal? score,
        string? qualitativeScoreCode,
        string? comment,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        EvaluatorName = PersonnelFileNormalization.Clean(evaluatorName, nameof(evaluatorName));
        EvaluationDateUtc = PersonnelFileNormalization.NormalizeDate(evaluationDateUtc);
        Score = score;
        QualitativeScoreCode = PersonnelFileNormalization.CleanOptional(qualitativeScoreCode);
        Comment = PersonnelFileNormalization.CleanOptional(comment);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public static PersonnelFilePerformanceEvaluation Create(
        string evaluatorName,
        DateTime evaluationDateUtc,
        decimal? score,
        string? qualitativeScoreCode,
        string? comment,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            evaluatorName,
            evaluationDateUtc,
            score,
            qualitativeScoreCode,
            comment,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
}

public sealed class PersonnelFilePositionCompetencyResult : TenantEntity
{
    private PersonnelFilePositionCompetencyResult()
    {
    }

    private PersonnelFilePositionCompetencyResult(
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long? jobProfileCompetencyExpectationId,
        decimal? expectedScore,
        decimal achievedScore,
        DateTime evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        Apply(
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            jobProfileCompetencyExpectationId,
            expectedScore,
            achievedScore,
            evaluationDateUtc,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // The competency being evaluated, referenced from the competency framework (decision D-03/D-12): the
    // competency itself, its type (gestión/organizacional/técnica) and the matrix expectation it was scored
    // against. ExpectedScore is the snapshot of the matrix ExpectedValue at evaluation time, so historical
    // rows keep their gap even if the matrix later changes.
    public long CompetencyCatalogItemId { get; private set; }

    public long CompetencyTypeCatalogItemId { get; private set; }

    public long? JobProfileCompetencyExpectationId { get; private set; }

    public decimal? ExpectedScore { get; private set; }

    public decimal AchievedScore { get; private set; }

    public decimal? GapScore { get; private set; }

    public DateTime EvaluationDateUtc { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long? jobProfileCompetencyExpectationId,
        decimal? expectedScore,
        decimal achievedScore,
        DateTime evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        Apply(
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            jobProfileCompetencyExpectationId,
            expectedScore,
            achievedScore,
            evaluationDateUtc,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFilePositionCompetencyResult Create(
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long? jobProfileCompetencyExpectationId,
        decimal? expectedScore,
        decimal achievedScore,
        DateTime evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            jobProfileCompetencyExpectationId,
            expectedScore,
            achievedScore,
            evaluationDateUtc,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);

    private void Apply(
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long? jobProfileCompetencyExpectationId,
        decimal? expectedScore,
        decimal achievedScore,
        DateTime evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        if (competencyCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyCatalogItemId), "Competency catalog item id must be greater than zero.");
        }

        if (competencyTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyTypeCatalogItemId), "Competency type catalog item id must be greater than zero.");
        }

        CompetencyCatalogItemId = competencyCatalogItemId;
        CompetencyTypeCatalogItemId = competencyTypeCatalogItemId;
        JobProfileCompetencyExpectationId = jobProfileCompetencyExpectationId;
        ExpectedScore = expectedScore;
        AchievedScore = achievedScore;
        // Decision D-05: the gap is computed (expected − achieved), never supplied; null when no expected exists.
        GapScore = expectedScore.HasValue ? expectedScore.Value - achievedScore : null;
        EvaluationDateUtc = PersonnelFileNormalization.NormalizeDate(evaluationDateUtc);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }
}

public sealed class PersonnelFileSelectionContest : TenantEntity
{
    private PersonnelFileSelectionContest()
    {
    }

    private PersonnelFileSelectionContest(
        string contestCode,
        string contestName,
        DateTime contestDateUtc,
        string resultCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        ContestCode = PersonnelFileNormalization.Clean(contestCode, nameof(contestCode));
        ContestName = PersonnelFileNormalization.Clean(contestName, nameof(contestName));
        ContestDateUtc = PersonnelFileNormalization.NormalizeDate(contestDateUtc);
        ResultCode = PersonnelFileNormalization.Clean(resultCode, nameof(resultCode));
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string ContestCode { get; private set; } = string.Empty;

    public string ContestName { get; private set; } = string.Empty;

    public DateTime ContestDateUtc { get; private set; }

    public string ResultCode { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string contestCode,
        string contestName,
        DateTime contestDateUtc,
        string resultCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        ContestCode = PersonnelFileNormalization.Clean(contestCode, nameof(contestCode));
        ContestName = PersonnelFileNormalization.Clean(contestName, nameof(contestName));
        ContestDateUtc = PersonnelFileNormalization.NormalizeDate(contestDateUtc);
        ResultCode = PersonnelFileNormalization.Clean(resultCode, nameof(resultCode));
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public static PersonnelFileSelectionContest Create(
        string contestCode,
        string contestName,
        DateTime contestDateUtc,
        string resultCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(contestCode, contestName, contestDateUtc, resultCode, notes, sourceSystem, sourceReference, sourceSyncedUtc);
}

public sealed class PersonnelFileCurricularCompetency : TenantEntity
{
    private PersonnelFileCurricularCompetency()
    {
    }

    private PersonnelFileCurricularCompetency(
        string requirementTypeCode,
        string requirementName,
        string competencyDomain,
        decimal? experienceTimeValue,
        string? metricCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        RequirementTypeCode = PersonnelFileNormalization.Clean(requirementTypeCode, nameof(requirementTypeCode));
        RequirementName = PersonnelFileNormalization.Clean(requirementName, nameof(requirementName));
        CompetencyDomain = PersonnelFileNormalization.Clean(competencyDomain, nameof(competencyDomain));
        NormalizedRequirementName = PersonnelFileNormalization.NormalizeName(requirementName);
        ExperienceTimeValue = experienceTimeValue;
        MetricCode = PersonnelFileNormalization.CleanOptional(metricCode);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string RequirementTypeCode { get; private set; } = string.Empty;

    public string RequirementName { get; private set; } = string.Empty;

    public string CompetencyDomain { get; private set; } = string.Empty;

    /// <summary>Upper-cased requirement name, persisted to back the anti-duplicate unique index (D-05).</summary>
    public string NormalizedRequirementName { get; private set; } = string.Empty;

    public decimal? ExperienceTimeValue { get; private set; }

    public string? MetricCode { get; private set; }

    public string? Notes { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string requirementTypeCode,
        string requirementName,
        string competencyDomain,
        decimal? experienceTimeValue,
        string? metricCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        RequirementTypeCode = PersonnelFileNormalization.Clean(requirementTypeCode, nameof(requirementTypeCode));
        RequirementName = PersonnelFileNormalization.Clean(requirementName, nameof(requirementName));
        CompetencyDomain = PersonnelFileNormalization.Clean(competencyDomain, nameof(competencyDomain));
        NormalizedRequirementName = PersonnelFileNormalization.NormalizeName(requirementName);
        ExperienceTimeValue = experienceTimeValue;
        MetricCode = PersonnelFileNormalization.CleanOptional(metricCode);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public static PersonnelFileCurricularCompetency Create(
        string requirementTypeCode,
        string requirementName,
        string competencyDomain,
        decimal? experienceTimeValue,
        string? metricCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            requirementTypeCode,
            requirementName,
            competencyDomain,
            experienceTimeValue,
            metricCode,
            notes,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
}

/// <summary>
/// Canonical status codes for a definitive-retirement request ("retiro definitivo"). The codes are validated
/// against the country-scoped <c>retirement-request-statuses</c> catalog (visualization / i18n), but the domain
/// state machine references these constants (D-04/D-16).
/// </summary>
public static class RetirementRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string Autorizada = "AUTORIZADA";
    public const string Rechazada = "RECHAZADA";
    public const string Anulada = "ANULADA";
    public const string Ejecutada = "EJECUTADA";
    public const string Revertida = "REVERTIDA";

    /// <summary>Open states — at most one open request per employee (RN-001.2).</summary>
    public static readonly IReadOnlyCollection<string> Open = new[] { Solicitada, Autorizada };

    /// <summary>States the authorizer may set through the resolution action (RF-004).</summary>
    public static readonly IReadOnlyCollection<string> ResolutionTargets = new[] { Autorizada, Rechazada };
}

/// <summary>
/// Definitive-retirement request ("retiro definitivo") — the single door for the baja (D-01). Captured by HR
/// (no self-service in Fase 1, D-03) with a requester reference + name snapshot (D-02), authorized by a
/// dedicated grant (D-12/D-13), EXECUTED as a transactional orchestration that stamps the profile, closes
/// plazas/contracts, deactivates the login and captures the reversal snapshot (D-05/D-06/D-11), and optionally
/// REVERTED within 30 calendar days restoring exactly that snapshot (RN-012.4). The snapshot uses flat columns
/// here plus the <see cref="RetirementRequestClosedRecord"/> child rows (no jsonb on domain entities).
/// </summary>
public sealed class PersonnelFileRetirementRequest : TenantEntity
{
    private readonly List<RetirementRequestClosedRecord> _closedRecords = [];

    private PersonnelFileRetirementRequest()
    {
    }

    private PersonnelFileRetirementRequest(
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        string? notes,
        Guid requestedByUserId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        RequestStatusCode = RetirementRequestStatuses.Solicitada;
        RequestedByUserId = requestedByUserId;
        ApplyRequestFields(
            requesterFilePublicId,
            requesterNameSnapshot,
            requestDate,
            retirementDate,
            retirementCategoryCode,
            retirementCategoryNameSnapshot,
            retirementReasonCode,
            retirementReasonNameSnapshot,
            notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Requester ("solicitante", D-02): a personnel-file reference by PublicId (it may be the employee
    // themself — a resignation) plus a name snapshot; RequestedByUserId audits who typed it into the system.
    public Guid RequesterFilePublicId { get; private set; }

    public string RequesterNameSnapshot { get; private set; } = string.Empty;

    public DateTime RequestDate { get; private set; }

    public DateTime RetirementDate { get; private set; }

    public string RetirementCategoryCode { get; private set; } = string.Empty;

    public string? RetirementCategoryNameSnapshot { get; private set; }

    public string RetirementReasonCode { get; private set; } = string.Empty;

    public string? RetirementReasonNameSnapshot { get; private set; }

    public string? Notes { get; private set; }

    public string RequestStatusCode { get; private set; } = RetirementRequestStatuses.Solicitada;

    public Guid RequestedByUserId { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTime? ResolutionDateUtc { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public Guid? CanceledByUserId { get; private set; }

    public DateTime? CancellationDateUtc { get; private set; }

    public string? CancellationNotes { get; private set; }

    public Guid? ExecutedByUserId { get; private set; }

    // Exact execution timestamp — the 30-day reversal window (RN-012.4) anchors here, so it is NOT
    // normalized to date-only.
    public DateTime? ExecutionDateUtc { get; private set; }

    // Reversal snapshot (D-11), captured at execution: prior employment status, whether the linked login
    // was active (null = no linked login), and the prior rehire-block state.
    public string? PriorEmploymentStatusCode { get; private set; }

    public bool? PriorLoginWasActive { get; private set; }

    public bool? PriorRehireBlocked { get; private set; }

    public string? PriorRehireBlockReason { get; private set; }

    public Guid? RevertedByUserId { get; private set; }

    public DateTime? ReversalDateUtc { get; private set; }

    public string? ReversalReason { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<RetirementRequestClosedRecord> ClosedRecords => _closedRecords;

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileRetirementRequest Create(
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        string? notes,
        Guid requestedByUserId) =>
        new(
            requesterFilePublicId,
            requesterNameSnapshot,
            requestDate,
            retirementDate,
            retirementCategoryCode,
            retirementCategoryNameSnapshot,
            retirementReasonCode,
            retirementReasonNameSnapshot,
            notes,
            requestedByUserId);

    /// <summary>Edits the request's business fields — only while SOLICITADA (RN-003.1).</summary>
    public void Update(
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        string? notes)
    {
        if (RequestStatusCode != RetirementRequestStatuses.Solicitada)
        {
            throw new InvalidOperationException("Only a SOLICITADA retirement request can be edited.");
        }

        ConcurrencyToken = Guid.NewGuid();
        ApplyRequestFields(
            requesterFilePublicId,
            requesterNameSnapshot,
            requestDate,
            retirementDate,
            retirementCategoryCode,
            retirementCategoryNameSnapshot,
            retirementReasonCode,
            retirementReasonNameSnapshot,
            notes);
    }

    /// <summary>
    /// Authorizer resolution (RF-004): AUTORIZADA (note optional) or RECHAZADA (note mandatory — RN-004.3).
    /// Only from SOLICITADA; RECHAZADA is terminal, AUTORIZADA enables the interview (D-07) and execution.
    /// </summary>
    public void Resolve(string targetStatusCode, Guid decidedByUserId, DateTime decidedAtUtc, string? notes)
    {
        if (RequestStatusCode != RetirementRequestStatuses.Solicitada)
        {
            throw new InvalidOperationException("Only a SOLICITADA retirement request can be resolved.");
        }

        var normalizedTarget = PersonnelFileNormalization.Clean(targetStatusCode, nameof(targetStatusCode)).ToUpperInvariant();
        if (!RetirementRequestStatuses.ResolutionTargets.Contains(normalizedTarget))
        {
            throw new InvalidOperationException("The target status is not a valid resolution target.");
        }

        var normalizedNotes = PersonnelFileNormalization.CleanOptional(notes);
        if (normalizedTarget == RetirementRequestStatuses.Rechazada && string.IsNullOrWhiteSpace(normalizedNotes))
        {
            throw new InvalidOperationException("Rejecting a retirement request requires a note.");
        }

        RequestStatusCode = normalizedTarget;
        ResolvedByUserId = decidedByUserId;
        ResolutionDateUtc = PersonnelFileNormalization.NormalizeDate(decidedAtUtc);
        ResolutionNotes = normalizedNotes;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls an open request (RN-005): SOLICITADA by the manager, AUTORIZADA by the authorizer (the
    /// permission split by state lives in the handlers). An EJECUTADA is never annulled — it is reverted.
    /// </summary>
    public void Cancel(Guid canceledByUserId, DateTime canceledAtUtc, string? notes)
    {
        if (!RetirementRequestStatuses.Open.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only an open (SOLICITADA/AUTORIZADA) retirement request can be canceled.");
        }

        RequestStatusCode = RetirementRequestStatuses.Anulada;
        CanceledByUserId = canceledByUserId;
        CancellationDateUtc = PersonnelFileNormalization.NormalizeDate(canceledAtUtc);
        CancellationNotes = PersonnelFileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Marks the orchestrated execution (RF-006) and captures the reversal snapshot (D-11).</summary>
    public void MarkExecuted(
        Guid executedByUserId,
        DateTime executedAtUtc,
        string priorEmploymentStatusCode,
        bool? priorLoginWasActive,
        bool priorRehireBlocked,
        string? priorRehireBlockReason)
    {
        if (RequestStatusCode != RetirementRequestStatuses.Autorizada)
        {
            throw new InvalidOperationException("Only an AUTORIZADA retirement request can be executed.");
        }

        RequestStatusCode = RetirementRequestStatuses.Ejecutada;
        ExecutedByUserId = executedByUserId;
        ExecutionDateUtc = executedAtUtc;
        PriorEmploymentStatusCode = PersonnelFileNormalization.Clean(priorEmploymentStatusCode, nameof(priorEmploymentStatusCode));
        PriorLoginWasActive = priorLoginWasActive;
        PriorRehireBlocked = priorRehireBlocked;
        PriorRehireBlockReason = PersonnelFileNormalization.CleanOptional(priorRehireBlockReason);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Records one plaza/contract row closed by the execution, for exact reversal (D-11).</summary>
    public void AddClosedRecord(RetirementRequestClosedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _closedRecords.Add(record);
    }

    /// <summary>
    /// Marks the reversal (RF-010): reason is mandatory (RN-010.3); every historical field is preserved —
    /// the reversal changes the status, it never erases the record (RN-010.4).
    /// </summary>
    public void MarkReverted(Guid revertedByUserId, DateTime revertedAtUtc, string reason)
    {
        if (RequestStatusCode != RetirementRequestStatuses.Ejecutada)
        {
            throw new InvalidOperationException("Only an EJECUTADA retirement request can be reverted.");
        }

        RequestStatusCode = RetirementRequestStatuses.Revertida;
        RevertedByUserId = revertedByUserId;
        ReversalDateUtc = revertedAtUtc;
        ReversalReason = PersonnelFileNormalization.Clean(reason, nameof(reason));
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyRequestFields(
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        string? notes)
    {
        if (requesterFilePublicId == Guid.Empty)
        {
            throw new ArgumentException("The requester file reference is required.", nameof(requesterFilePublicId));
        }

        RequesterFilePublicId = requesterFilePublicId;
        RequesterNameSnapshot = PersonnelFileNormalization.Clean(requesterNameSnapshot, nameof(requesterNameSnapshot));
        RequestDate = PersonnelFileNormalization.NormalizeDate(requestDate);
        RetirementDate = PersonnelFileNormalization.NormalizeDate(retirementDate);
        RetirementCategoryCode = PersonnelFileNormalization.Clean(retirementCategoryCode, nameof(retirementCategoryCode)).ToUpperInvariant();
        RetirementCategoryNameSnapshot = PersonnelFileNormalization.CleanOptional(retirementCategoryNameSnapshot);
        RetirementReasonCode = PersonnelFileNormalization.Clean(retirementReasonCode, nameof(retirementReasonCode)).ToUpperInvariant();
        RetirementReasonNameSnapshot = PersonnelFileNormalization.CleanOptional(retirementReasonNameSnapshot);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }
}

/// <summary>Kinds of rows a retirement execution closes (and the reversal reopens) — D-11.</summary>
public static class RetirementClosedRecordKinds
{
    public const string Assignment = "ASSIGNMENT";
    public const string Contract = "CONTRACT";
}

/// <summary>
/// One plaza-assignment or contract row closed by a retirement execution, with the end date the row had
/// BEFORE closing (null when the execution set it — <c>Close</c> preserves an already-fixed end date by
/// only deactivating). The reversal reopens EXACTLY these rows via <c>Reopen(PreviousEndDate)</c> (D-11).
/// </summary>
public sealed class RetirementRequestClosedRecord : TenantEntity
{
    private RetirementRequestClosedRecord()
    {
    }

    private RetirementRequestClosedRecord(string entityKind, Guid entityPublicId, DateTime? previousEndDate)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        EntityKind = PersonnelFileNormalization.Clean(entityKind, nameof(entityKind)).ToUpperInvariant();
        EntityPublicId = entityPublicId;
        PreviousEndDate = PersonnelFileNormalization.NormalizeDate(previousEndDate);
    }

    public long RetirementRequestId { get; private set; }

    public PersonnelFileRetirementRequest RetirementRequest { get; private set; } = null!;

    public string EntityKind { get; private set; } = string.Empty;

    public Guid EntityPublicId { get; private set; }

    public DateTime? PreviousEndDate { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static RetirementRequestClosedRecord Create(string entityKind, Guid entityPublicId, DateTime? previousEndDate) =>
        new(entityKind, entityPublicId, previousEndDate);
}
