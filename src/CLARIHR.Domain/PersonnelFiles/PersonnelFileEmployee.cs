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
        bool isEmploymentActive,
        string contractTypeCode,
        DateTime hireDate,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        string? retirementNotes,
        DateTime? retirementDate,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime? contractStartDate,
        DateTime? contractEndDate,
        string? vacationConfigurationJson)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Update(
            employeeCode,
            employmentStatusCode,
            isEmploymentActive,
            contractTypeCode,
            hireDate,
            retirementCategoryCode,
            retirementReasonCode,
            retirementNotes,
            retirementDate,
            workdayCode,
            payrollTypeCode,
            orgUnitPublicId,
            workCenterPublicId,
            costCenterPublicId,
            contractStartDate,
            contractEndDate,
            vacationConfigurationJson);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string EmployeeCode { get; private set; } = string.Empty;

    public string NormalizedEmployeeCode { get; private set; } = string.Empty;

    public string EmploymentStatusCode { get; private set; } = string.Empty;

    public bool IsEmploymentActive { get; private set; }

    public string ContractTypeCode { get; private set; } = string.Empty;

    public DateTime HireDate { get; private set; }

    public string? RetirementCategoryCode { get; private set; }

    public string? RetirementReasonCode { get; private set; }

    public string? RetirementNotes { get; private set; }

    public DateTime? RetirementDate { get; private set; }

    public string? WorkdayCode { get; private set; }

    public string? PayrollTypeCode { get; private set; }

    public Guid? OrgUnitPublicId { get; private set; }

    public Guid? WorkCenterPublicId { get; private set; }

    public Guid? CostCenterPublicId { get; private set; }

    public DateTime? ContractStartDate { get; private set; }

    public DateTime? ContractEndDate { get; private set; }

    public string? VacationConfigurationJson { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileEmployeeProfile Create(
        string employeeCode,
        string employmentStatusCode,
        bool isEmploymentActive,
        string contractTypeCode,
        DateTime hireDate,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        string? retirementNotes,
        DateTime? retirementDate,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime? contractStartDate,
        DateTime? contractEndDate,
        string? vacationConfigurationJson) =>
        new(
            employeeCode,
            employmentStatusCode,
            isEmploymentActive,
            contractTypeCode,
            hireDate,
            retirementCategoryCode,
            retirementReasonCode,
            retirementNotes,
            retirementDate,
            workdayCode,
            payrollTypeCode,
            orgUnitPublicId,
            workCenterPublicId,
            costCenterPublicId,
            contractStartDate,
            contractEndDate,
            vacationConfigurationJson);

    public void Update(
        string employeeCode,
        string employmentStatusCode,
        bool isEmploymentActive,
        string contractTypeCode,
        DateTime hireDate,
        string? retirementCategoryCode,
        string? retirementReasonCode,
        string? retirementNotes,
        DateTime? retirementDate,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime? contractStartDate,
        DateTime? contractEndDate,
        string? vacationConfigurationJson)
    {
        EmployeeCode = PersonnelFileNormalization.Clean(employeeCode, nameof(employeeCode));
        NormalizedEmployeeCode = PersonnelFileNormalization.NormalizeCode(employeeCode);
        EmploymentStatusCode = PersonnelFileNormalization.Clean(employmentStatusCode, nameof(employmentStatusCode));
        IsEmploymentActive = isEmploymentActive;
        ContractTypeCode = PersonnelFileNormalization.Clean(contractTypeCode, nameof(contractTypeCode));
        HireDate = PersonnelFileNormalization.NormalizeDate(hireDate);
        RetirementCategoryCode = PersonnelFileNormalization.CleanOptional(retirementCategoryCode);
        RetirementReasonCode = PersonnelFileNormalization.CleanOptional(retirementReasonCode);
        RetirementNotes = PersonnelFileNormalization.CleanOptional(retirementNotes);
        RetirementDate = PersonnelFileNormalization.NormalizeDate(retirementDate);
        WorkdayCode = PersonnelFileNormalization.CleanOptional(workdayCode);
        PayrollTypeCode = PersonnelFileNormalization.CleanOptional(payrollTypeCode);
        OrgUnitPublicId = orgUnitPublicId;
        WorkCenterPublicId = workCenterPublicId;
        CostCenterPublicId = costCenterPublicId;
        ContractStartDate = PersonnelFileNormalization.NormalizeDate(contractStartDate);
        ContractEndDate = PersonnelFileNormalization.NormalizeDate(contractEndDate);
        VacationConfigurationJson = PersonnelFileNormalization.CleanOptional(vacationConfigurationJson);
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
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        bool isActive,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        AssignmentTypeCode = PersonnelFileNormalization.Clean(assignmentTypeCode, nameof(assignmentTypeCode));
        PositionSlotPublicId = positionSlotPublicId;
        OrgUnitPublicId = orgUnitPublicId;
        WorkCenterPublicId = workCenterPublicId;
        CostCenterPublicId = costCenterPublicId;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsPrimary = isPrimary;
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string AssignmentTypeCode { get; private set; } = string.Empty;

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
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        bool isActive,
        string? notes) =>
        new(
            assignmentTypeCode,
            positionSlotPublicId,
            orgUnitPublicId,
            workCenterPublicId,
            costCenterPublicId,
            startDate,
            endDate,
            isPrimary,
            isActive,
            notes);

    public void Update(
        string assignmentTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        AssignmentTypeCode = PersonnelFileNormalization.Clean(assignmentTypeCode, nameof(assignmentTypeCode));
        PositionSlotPublicId = positionSlotPublicId;
        OrgUnitPublicId = orgUnitPublicId;
        WorkCenterPublicId = workCenterPublicId;
        CostCenterPublicId = costCenterPublicId;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsPrimary = isPrimary;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
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
}

public sealed class PersonnelFileSalaryItem : TenantEntity
{
    private PersonnelFileSalaryItem()
    {
    }

    private PersonnelFileSalaryItem(
        string incomeTypeCode,
        string salaryRubricCode,
        string currencyCode,
        string payPeriodCode,
        decimal amount,
        DateTime startDate,
        DateTime? endDate,
        bool isActive)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IncomeTypeCode = PersonnelFileNormalization.Clean(incomeTypeCode, nameof(incomeTypeCode));
        SalaryRubricCode = PersonnelFileNormalization.Clean(salaryRubricCode, nameof(salaryRubricCode));
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        PayPeriodCode = PersonnelFileNormalization.Clean(payPeriodCode, nameof(payPeriodCode));
        Amount = amount;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsActive = isActive;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string IncomeTypeCode { get; private set; } = string.Empty;

    public string SalaryRubricCode { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public string PayPeriodCode { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string incomeTypeCode,
        string salaryRubricCode,
        string currencyCode,
        string payPeriodCode,
        decimal amount,
        DateTime startDate,
        DateTime? endDate)
    {
        ConcurrencyToken = Guid.NewGuid();
        IncomeTypeCode = PersonnelFileNormalization.Clean(incomeTypeCode, nameof(incomeTypeCode));
        SalaryRubricCode = PersonnelFileNormalization.Clean(salaryRubricCode, nameof(salaryRubricCode));
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        PayPeriodCode = PersonnelFileNormalization.Clean(payPeriodCode, nameof(payPeriodCode));
        Amount = amount;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileSalaryItem Create(
        string incomeTypeCode,
        string salaryRubricCode,
        string currencyCode,
        string payPeriodCode,
        decimal amount,
        DateTime startDate,
        DateTime? endDate,
        bool isActive) =>
        new(incomeTypeCode, salaryRubricCode, currencyCode, payPeriodCode, amount, startDate, endDate, isActive);
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

public sealed class PersonnelFilePaymentMethod : TenantEntity
{
    private PersonnelFilePaymentMethod()
    {
    }

    private PersonnelFilePaymentMethod(
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        bool isActive,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        PaymentMethodCode = PersonnelFileNormalization.Clean(paymentMethodCode, nameof(paymentMethodCode));
        BankAccountPublicId = bankAccountPublicId;
        IsPrimary = isPrimary;
        IsActive = isActive;
        EffectiveFromUtc = PersonnelFileNormalization.NormalizeDate(effectiveFromUtc);
        EffectiveToUtc = PersonnelFileNormalization.NormalizeDate(effectiveToUtc);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string PaymentMethodCode { get; private set; } = string.Empty;

    public Guid? BankAccountPublicId { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        PaymentMethodCode = PersonnelFileNormalization.Clean(paymentMethodCode, nameof(paymentMethodCode));
        BankAccountPublicId = bankAccountPublicId;
        IsPrimary = isPrimary;
        EffectiveFromUtc = PersonnelFileNormalization.NormalizeDate(effectiveFromUtc);
        EffectiveToUtc = PersonnelFileNormalization.NormalizeDate(effectiveToUtc);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFilePaymentMethod Create(
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        bool isActive,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes) =>
        new(paymentMethodCode, bankAccountPublicId, isPrimary, isActive, effectiveFromUtc, effectiveToUtc, notes);
}

public sealed class PersonnelFileAuthorizationSubstitution : TenantEntity
{
    private PersonnelFileAuthorizationSubstitution()
    {
    }

    private PersonnelFileAuthorizationSubstitution(
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        string? substitutePositionTitle,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        SubstitutionTypeCode = PersonnelFileNormalization.Clean(substitutionTypeCode, nameof(substitutionTypeCode));
        SubstitutePersonnelFilePublicId = substitutePersonnelFilePublicId;
        SubstitutePositionTitle = PersonnelFileNormalization.CleanOptional(substitutePositionTitle);
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsActive = isActive;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string SubstitutionTypeCode { get; private set; } = string.Empty;

    public Guid SubstitutePersonnelFilePublicId { get; private set; }

    public string? SubstitutePositionTitle { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileAuthorizationSubstitution Create(
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        string? substitutePositionTitle,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        string? notes) =>
        new(substitutionTypeCode, substitutePersonnelFilePublicId, substitutePositionTitle, startDate, endDate, isActive, notes);

    public void Update(
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        string? substitutePositionTitle,
        DateTime startDate,
        DateTime? endDate,
        string? notes)
    {
        ConcurrencyToken = Guid.NewGuid();
        SubstitutionTypeCode = PersonnelFileNormalization.Clean(substitutionTypeCode, nameof(substitutionTypeCode));
        SubstitutePersonnelFilePublicId = substitutePersonnelFilePublicId;
        SubstitutePositionTitle = PersonnelFileNormalization.CleanOptional(substitutePositionTitle);
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
    private PersonnelFileInsuranceBeneficiary()
    {
    }

    private PersonnelFileInsuranceBeneficiary(
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        FullName = PersonnelFileNormalization.Clean(fullName, nameof(fullName));
        DocumentNumber = PersonnelFileNormalization.CleanOptional(documentNumber);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        KinshipCode = PersonnelFileNormalization.Clean(kinshipCode, nameof(kinshipCode));
        IsActive = true;
    }

    public long InsuranceId { get; private set; }

    public PersonnelFileInsurance Insurance { get; private set; } = null!;

    public string FullName { get; private set; } = string.Empty;

    public string? DocumentNumber { get; private set; }

    public DateTime? BirthDate { get; private set; }

    public string KinshipCode { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToInsurance(long insuranceId) => InsuranceId = insuranceId;

    public void Update(
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode)
    {
        ConcurrencyToken = Guid.NewGuid();
        FullName = PersonnelFileNormalization.Clean(fullName, nameof(fullName));
        DocumentNumber = PersonnelFileNormalization.CleanOptional(documentNumber);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        KinshipCode = PersonnelFileNormalization.Clean(kinshipCode, nameof(kinshipCode));
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileInsuranceBeneficiary Create(
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode) =>
        new(fullName, documentNumber, birthDate, kinshipCode);
}

public sealed class PersonnelFileMedicalClaim : TenantEntity
{
    private PersonnelFileMedicalClaim()
    {
    }

    private PersonnelFileMedicalClaim(
        Guid? insurancePublicId,
        string? accountNumber,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        int? responseTimeDays,
        string? notes,
        DateTime claimDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        InsurancePublicId = insurancePublicId;
        AccountNumber = PersonnelFileNormalization.CleanOptional(accountNumber);
        ClaimTypeCode = PersonnelFileNormalization.Clean(claimTypeCode, nameof(claimTypeCode));
        Diagnosis = PersonnelFileNormalization.CleanOptional(diagnosis);
        ClaimAmount = claimAmount;
        CurrencyCode = PersonnelFileNormalization.CleanOptional(currencyCode);
        PaidAmount = paidAmount;
        ResponseTimeDays = responseTimeDays;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        ClaimDateUtc = PersonnelFileNormalization.NormalizeDate(claimDateUtc);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public Guid? InsurancePublicId { get; private set; }

    public string? AccountNumber { get; private set; }

    public string ClaimTypeCode { get; private set; } = string.Empty;

    public string? Diagnosis { get; private set; }

    public decimal? ClaimAmount { get; private set; }

    public string? CurrencyCode { get; private set; }

    public decimal? PaidAmount { get; private set; }

    public int? ResponseTimeDays { get; private set; }

    public string? Notes { get; private set; }

    public DateTime ClaimDateUtc { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        Guid? insurancePublicId,
        string? accountNumber,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        int? responseTimeDays,
        string? notes,
        DateTime claimDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        InsurancePublicId = insurancePublicId;
        AccountNumber = PersonnelFileNormalization.CleanOptional(accountNumber);
        ClaimTypeCode = PersonnelFileNormalization.Clean(claimTypeCode, nameof(claimTypeCode));
        Diagnosis = PersonnelFileNormalization.CleanOptional(diagnosis);
        ClaimAmount = claimAmount;
        CurrencyCode = PersonnelFileNormalization.CleanOptional(currencyCode);
        PaidAmount = paidAmount;
        ResponseTimeDays = responseTimeDays;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        ClaimDateUtc = PersonnelFileNormalization.NormalizeDate(claimDateUtc);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileMedicalClaim Create(
        Guid? insurancePublicId,
        string? accountNumber,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        int? responseTimeDays,
        string? notes,
        DateTime claimDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            insurancePublicId,
            accountNumber,
            claimTypeCode,
            diagnosis,
            claimAmount,
            currencyCode,
            paidAmount,
            responseTimeDays,
            notes,
            claimDateUtc,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
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
        string competencyCode,
        string? desiredBehaviors,
        decimal? expectedScore,
        decimal? achievedScore,
        decimal? gapScore,
        DateTime? evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        CompetencyCode = PersonnelFileNormalization.Clean(competencyCode, nameof(competencyCode));
        DesiredBehaviors = PersonnelFileNormalization.CleanOptional(desiredBehaviors);
        ExpectedScore = expectedScore;
        AchievedScore = achievedScore;
        GapScore = gapScore;
        EvaluationDateUtc = PersonnelFileNormalization.NormalizeDate(evaluationDateUtc);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string CompetencyCode { get; private set; } = string.Empty;

    public string? DesiredBehaviors { get; private set; }

    public decimal? ExpectedScore { get; private set; }

    public decimal? AchievedScore { get; private set; }

    public decimal? GapScore { get; private set; }

    public DateTime? EvaluationDateUtc { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? SourceSyncedUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void Update(
        string competencyCode,
        string? desiredBehaviors,
        decimal? expectedScore,
        decimal? achievedScore,
        decimal? gapScore,
        DateTime? evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc)
    {
        ConcurrencyToken = Guid.NewGuid();
        CompetencyCode = PersonnelFileNormalization.Clean(competencyCode, nameof(competencyCode));
        DesiredBehaviors = PersonnelFileNormalization.CleanOptional(desiredBehaviors);
        ExpectedScore = expectedScore;
        AchievedScore = achievedScore;
        GapScore = gapScore;
        EvaluationDateUtc = PersonnelFileNormalization.NormalizeDate(evaluationDateUtc);
        SourceSystem = PersonnelFileNormalization.CleanOptional(sourceSystem);
        SourceReference = PersonnelFileNormalization.CleanOptional(sourceReference);
        SourceSyncedUtc = PersonnelFileNormalization.NormalizeDate(sourceSyncedUtc);
    }

    public static PersonnelFilePositionCompetencyResult Create(
        string competencyCode,
        string? desiredBehaviors,
        decimal? expectedScore,
        decimal? achievedScore,
        decimal? gapScore,
        DateTime? evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc) =>
        new(
            competencyCode,
            desiredBehaviors,
            expectedScore,
            achievedScore,
            gapScore,
            evaluationDateUtc,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
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
