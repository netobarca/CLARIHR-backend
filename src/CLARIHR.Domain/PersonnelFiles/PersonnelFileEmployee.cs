using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public sealed class PersonnelFileEmployeeProfile : TenantEntity
{
    private PersonnelFileEmployeeProfile()
    {
    }

    private PersonnelFileEmployeeProfile(
        string employeeCode,
        string employmentStatusCode,
        DateTime hireDate,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        string? retirementNotes,
        DateTime? retirementDate)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Update(
            employeeCode,
            employmentStatusCode,
            hireDate,
            retirementCategoryCode,
            retirementReasonCode,
            retirementNotes,
            retirementDate);
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

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileEmployeeProfile Create(
        string employeeCode,
        string employmentStatusCode,
        DateTime hireDate,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        string? retirementNotes,
        DateTime? retirementDate) =>
        new(
            employeeCode,
            employmentStatusCode,
            hireDate,
            retirementCategoryCode,
            retirementReasonCode,
            retirementNotes,
            retirementDate);

    public void Update(
        string employeeCode,
        string employmentStatusCode,
        DateTime hireDate,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        string? retirementNotes,
        DateTime? retirementDate)
    {
        EmployeeCode = PersonnelFileNormalization.Clean(employeeCode, nameof(employeeCode));
        NormalizedEmployeeCode = PersonnelFileNormalization.NormalizeCode(employeeCode);
        EmploymentStatusCode = PersonnelFileNormalization.Clean(employmentStatusCode, nameof(employmentStatusCode));
        HireDate = PersonnelFileNormalization.NormalizeDate(hireDate);
        RetirementCategoryCode = PersonnelFileNormalization.CleanOptional(retirementCategoryCode);
        RetirementReasonCode = PersonnelFileNormalization.CleanOptional(retirementReasonCode);
        RetirementNotes = PersonnelFileNormalization.CleanOptional(retirementNotes);
        RetirementDate = PersonnelFileNormalization.NormalizeDate(retirementDate);
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
