using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public sealed class PersonnelFile : TenantEntity
{
    private readonly List<PersonnelFileIdentification> _identifications = [];
    private readonly List<PersonnelFileAddress> _addresses = [];
    private readonly List<PersonnelFileEmergencyContact> _emergencyContacts = [];
    private readonly List<PersonnelFileFamilyMember> _familyMembers = [];
    private readonly List<PersonnelFileHobby> _hobbies = [];
    private readonly List<PersonnelFileEmployeeRelation> _employeeRelations = [];
    private readonly List<PersonnelFileBankAccount> _bankAccounts = [];
    private readonly List<PersonnelFileAssociation> _associations = [];
    private readonly List<PersonnelFileEducation> _educations = [];
    private readonly List<PersonnelFileLanguage> _languages = [];
    private readonly List<PersonnelFileTraining> _trainings = [];
    private readonly List<PersonnelFilePreviousEmployment> _previousEmployments = [];
    private readonly List<PersonnelFileReference> _references = [];
    private readonly List<PersonnelFileDocument> _documents = [];
    private readonly List<PersonnelFileObservation> _observations = [];

    private PersonnelFile()
    {
    }

    private PersonnelFile(
        Guid publicId,
        PersonnelFileRecordType recordType,
        string firstName,
        string lastName,
        DateTime birthDate,
        string? maritalStatus,
        string? profession,
        string? nationality,
        string? personalEmail,
        string? institutionalEmail,
        string? personalPhone,
        string? institutionalPhone,
        string? birthCountry,
        string? birthDepartment,
        string? birthMunicipality,
        string? photoUrl,
        Guid? orgUnitPublicId,
        string? customDataJson)
    {
        PublicId = publicId;
        RecordType = recordType;
        SetName(firstName, lastName);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        MaritalStatus = PersonnelFileNormalization.CleanOptional(maritalStatus);
        Profession = PersonnelFileNormalization.CleanOptional(profession);
        Nationality = PersonnelFileNormalization.CleanOptional(nationality);
        PersonalEmail = PersonnelFileNormalization.CleanOptional(personalEmail);
        InstitutionalEmail = PersonnelFileNormalization.CleanOptional(institutionalEmail);
        PersonalPhone = PersonnelFileNormalization.CleanOptional(personalPhone);
        InstitutionalPhone = PersonnelFileNormalization.CleanOptional(institutionalPhone);
        BirthCountry = PersonnelFileNormalization.CleanOptional(birthCountry);
        BirthDepartment = PersonnelFileNormalization.CleanOptional(birthDepartment);
        BirthMunicipality = PersonnelFileNormalization.CleanOptional(birthMunicipality);
        PhotoUrl = PersonnelFileNormalization.CleanOptional(photoUrl);
        OrgUnitPublicId = orgUnitPublicId;
        CustomDataJson = PersonnelFileNormalization.CleanOptional(customDataJson);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public PersonnelFileRecordType RecordType { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string NormalizedFullName { get; private set; } = string.Empty;

    public DateTime BirthDate { get; private set; }

    public string? MaritalStatus { get; private set; }

    public string? Profession { get; private set; }

    public string? Nationality { get; private set; }

    public string? PersonalEmail { get; private set; }

    public string? InstitutionalEmail { get; private set; }

    public string? PersonalPhone { get; private set; }

    public string? InstitutionalPhone { get; private set; }

    public string? BirthCountry { get; private set; }

    public string? BirthDepartment { get; private set; }

    public string? BirthMunicipality { get; private set; }

    public string? PhotoUrl { get; private set; }

    public Guid? OrgUnitPublicId { get; private set; }

    public string? CustomDataJson { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileIdentification> Identifications => _identifications;

    public IReadOnlyCollection<PersonnelFileAddress> Addresses => _addresses;

    public IReadOnlyCollection<PersonnelFileEmergencyContact> EmergencyContacts => _emergencyContacts;

    public IReadOnlyCollection<PersonnelFileFamilyMember> FamilyMembers => _familyMembers;

    public IReadOnlyCollection<PersonnelFileHobby> Hobbies => _hobbies;

    public IReadOnlyCollection<PersonnelFileEmployeeRelation> EmployeeRelations => _employeeRelations;

    public IReadOnlyCollection<PersonnelFileBankAccount> BankAccounts => _bankAccounts;

    public IReadOnlyCollection<PersonnelFileAssociation> Associations => _associations;

    public IReadOnlyCollection<PersonnelFileEducation> Educations => _educations;

    public IReadOnlyCollection<PersonnelFileLanguage> Languages => _languages;

    public IReadOnlyCollection<PersonnelFileTraining> Trainings => _trainings;

    public IReadOnlyCollection<PersonnelFilePreviousEmployment> PreviousEmployments => _previousEmployments;

    public IReadOnlyCollection<PersonnelFileReference> References => _references;

    public IReadOnlyCollection<PersonnelFileDocument> Documents => _documents;

    public IReadOnlyCollection<PersonnelFileObservation> Observations => _observations;

    public static PersonnelFile Create(
        PersonnelFileRecordType recordType,
        string firstName,
        string lastName,
        DateTime birthDate,
        string? maritalStatus,
        string? profession,
        string? nationality,
        string? personalEmail,
        string? institutionalEmail,
        string? personalPhone,
        string? institutionalPhone,
        string? birthCountry,
        string? birthDepartment,
        string? birthMunicipality,
        string? photoUrl,
        Guid? orgUnitPublicId,
        string? customDataJson,
        IReadOnlyCollection<PersonnelFileIdentification>? identifications = null)
    {
        var file = new PersonnelFile(
            Guid.NewGuid(),
            recordType,
            firstName,
            lastName,
            birthDate,
            maritalStatus,
            profession,
            nationality,
            personalEmail,
            institutionalEmail,
            personalPhone,
            institutionalPhone,
            birthCountry,
            birthDepartment,
            birthMunicipality,
            photoUrl,
            orgUnitPublicId,
            customDataJson);

        if (identifications is not null)
        {
            file._identifications.AddRange(identifications);
        }

        return file;
    }

    public void UpdatePersonalInfo(
        PersonnelFileRecordType recordType,
        string firstName,
        string lastName,
        DateTime birthDate,
        string? maritalStatus,
        string? profession,
        string? nationality,
        string? personalEmail,
        string? institutionalEmail,
        string? personalPhone,
        string? institutionalPhone,
        string? birthCountry,
        string? birthDepartment,
        string? birthMunicipality,
        string? photoUrl,
        Guid? orgUnitPublicId,
        string? customDataJson)
    {
        RecordType = recordType;
        SetName(firstName, lastName);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        MaritalStatus = PersonnelFileNormalization.CleanOptional(maritalStatus);
        Profession = PersonnelFileNormalization.CleanOptional(profession);
        Nationality = PersonnelFileNormalization.CleanOptional(nationality);
        PersonalEmail = PersonnelFileNormalization.CleanOptional(personalEmail);
        InstitutionalEmail = PersonnelFileNormalization.CleanOptional(institutionalEmail);
        PersonalPhone = PersonnelFileNormalization.CleanOptional(personalPhone);
        InstitutionalPhone = PersonnelFileNormalization.CleanOptional(institutionalPhone);
        BirthCountry = PersonnelFileNormalization.CleanOptional(birthCountry);
        BirthDepartment = PersonnelFileNormalization.CleanOptional(birthDepartment);
        BirthMunicipality = PersonnelFileNormalization.CleanOptional(birthMunicipality);
        PhotoUrl = PersonnelFileNormalization.CleanOptional(photoUrl);
        OrgUnitPublicId = orgUnitPublicId;
        CustomDataJson = PersonnelFileNormalization.CleanOptional(customDataJson);
        RefreshConcurrencyToken();
    }

    public void ReplaceIdentifications(IEnumerable<PersonnelFileIdentification> items)
    {
        _identifications.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _identifications.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceAddresses(IEnumerable<PersonnelFileAddress> items)
    {
        _addresses.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _addresses.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceEmergencyContacts(IEnumerable<PersonnelFileEmergencyContact> items)
    {
        _emergencyContacts.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _emergencyContacts.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceFamilyMembers(IEnumerable<PersonnelFileFamilyMember> items)
    {
        _familyMembers.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _familyMembers.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceHobbies(IEnumerable<PersonnelFileHobby> items)
    {
        _hobbies.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _hobbies.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceEmployeeRelations(IEnumerable<PersonnelFileEmployeeRelation> items)
    {
        _employeeRelations.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _employeeRelations.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceBankAccounts(IEnumerable<PersonnelFileBankAccount> items)
    {
        _bankAccounts.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _bankAccounts.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceAssociations(IEnumerable<PersonnelFileAssociation> items)
    {
        _associations.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _associations.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceEducations(IEnumerable<PersonnelFileEducation> items)
    {
        _educations.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _educations.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceLanguages(IEnumerable<PersonnelFileLanguage> items)
    {
        _languages.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _languages.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceTrainings(IEnumerable<PersonnelFileTraining> items)
    {
        _trainings.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _trainings.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplacePreviousEmployments(IEnumerable<PersonnelFilePreviousEmployment> items)
    {
        _previousEmployments.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _previousEmployments.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void ReplaceReferences(IEnumerable<PersonnelFileReference> items)
    {
        _references.Clear();
        foreach (var item in items)
        {
            item.SetTenantId(TenantId);
            _references.Add(item);
        }

        RefreshConcurrencyToken();
    }

    public void AddDocument(PersonnelFileDocument document)
    {
        document.SetTenantId(TenantId);
        _documents.Add(document);
        RefreshConcurrencyToken();
    }

    public void AddObservation(PersonnelFileObservation observation)
    {
        observation.SetTenantId(TenantId);
        _observations.Add(observation);
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        RefreshConcurrencyToken();
    }

    private void SetName(string firstName, string lastName)
    {
        FirstName = PersonnelFileNormalization.Clean(firstName, nameof(firstName));
        LastName = PersonnelFileNormalization.Clean(lastName, nameof(lastName));
        FullName = $"{FirstName} {LastName}";
        NormalizedFullName = PersonnelFileNormalization.NormalizeName(FullName);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

public sealed class PersonnelFileIdentification : TenantEntity
{
    private PersonnelFileIdentification()
    {
    }

    private PersonnelFileIdentification(
        string identificationType,
        string identificationNumber,
        DateTime? issuedDate,
        DateTime? expiryDate,
        string? issuer,
        bool isPrimary)
    {
        if (issuedDate.HasValue && expiryDate.HasValue && expiryDate.Value.Date < issuedDate.Value.Date)
        {
            throw new InvalidOperationException("ExpiryDate cannot be earlier than IssuedDate.");
        }

        PublicId = Guid.NewGuid();
        IdentificationType = PersonnelFileNormalization.Clean(identificationType, nameof(identificationType));
        IdentificationNumber = PersonnelFileNormalization.Clean(identificationNumber, nameof(identificationNumber));
        NormalizedIdentificationNumber = PersonnelFileNormalization.NormalizeCode(identificationNumber);
        IssuedDate = PersonnelFileNormalization.NormalizeDate(issuedDate);
        ExpiryDate = PersonnelFileNormalization.NormalizeDate(expiryDate);
        Issuer = PersonnelFileNormalization.CleanOptional(issuer);
        IsPrimary = isPrimary;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string IdentificationType { get; private set; } = string.Empty;

    public string IdentificationNumber { get; private set; } = string.Empty;

    public string NormalizedIdentificationNumber { get; private set; } = string.Empty;

    public DateTime? IssuedDate { get; private set; }

    public DateTime? ExpiryDate { get; private set; }

    public string? Issuer { get; private set; }

    public bool IsPrimary { get; private set; }

    public static PersonnelFileIdentification Create(
        string identificationType,
        string identificationNumber,
        DateTime? issuedDate,
        DateTime? expiryDate,
        string? issuer,
        bool isPrimary) =>
        new(identificationType, identificationNumber, issuedDate, expiryDate, issuer, isPrimary);
}

public sealed class PersonnelFileAddress : TenantEntity
{
    private PersonnelFileAddress()
    {
    }

    private PersonnelFileAddress(
        string addressLine,
        string? country,
        string? department,
        string? municipality,
        string? postalCode,
        bool isCurrent)
    {
        PublicId = Guid.NewGuid();
        AddressLine = PersonnelFileNormalization.Clean(addressLine, nameof(addressLine));
        Country = PersonnelFileNormalization.CleanOptional(country);
        Department = PersonnelFileNormalization.CleanOptional(department);
        Municipality = PersonnelFileNormalization.CleanOptional(municipality);
        PostalCode = PersonnelFileNormalization.CleanOptional(postalCode);
        IsCurrent = isCurrent;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string AddressLine { get; private set; } = string.Empty;

    public string? Country { get; private set; }

    public string? Department { get; private set; }

    public string? Municipality { get; private set; }

    public string? PostalCode { get; private set; }

    public bool IsCurrent { get; private set; }

    public static PersonnelFileAddress Create(
        string addressLine,
        string? country,
        string? department,
        string? municipality,
        string? postalCode,
        bool isCurrent) =>
        new(addressLine, country, department, municipality, postalCode, isCurrent);
}

public sealed class PersonnelFileEmergencyContact : TenantEntity
{
    private PersonnelFileEmergencyContact()
    {
    }

    private PersonnelFileEmergencyContact(
        string name,
        string relationship,
        string phone,
        string? address,
        string? workplace)
    {
        PublicId = Guid.NewGuid();
        Name = PersonnelFileNormalization.Clean(name, nameof(name));
        Relationship = PersonnelFileNormalization.Clean(relationship, nameof(relationship));
        Phone = PersonnelFileNormalization.Clean(phone, nameof(phone));
        Address = PersonnelFileNormalization.CleanOptional(address);
        Workplace = PersonnelFileNormalization.CleanOptional(workplace);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string Relationship { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string? Workplace { get; private set; }

    public static PersonnelFileEmergencyContact Create(
        string name,
        string relationship,
        string phone,
        string? address,
        string? workplace) =>
        new(name, relationship, phone, address, workplace);
}

public sealed class PersonnelFileFamilyMember : TenantEntity
{
    private PersonnelFileFamilyMember()
    {
    }

    private PersonnelFileFamilyMember(
        string firstName,
        string lastName,
        string relationship,
        string? nationality,
        DateTime? birthDate,
        PersonnelFamilyMemberSex sex,
        string? maritalStatus,
        string? occupation,
        string? documentType,
        string? documentNumber,
        string? phone,
        bool isStudying,
        string? studyPlace,
        string? academicLevel,
        bool isBeneficiary,
        bool isWorking,
        string? workplace,
        string? jobTitle,
        string? workPhone,
        decimal? salary,
        bool isDeceased,
        DateTime? deceasedDate)
    {
        if (isStudying)
        {
            _ = PersonnelFileNormalization.Clean(studyPlace ?? string.Empty, nameof(studyPlace));
            _ = PersonnelFileNormalization.Clean(academicLevel ?? string.Empty, nameof(academicLevel));
        }

        if (isWorking)
        {
            _ = PersonnelFileNormalization.Clean(workplace ?? string.Empty, nameof(workplace));
            _ = PersonnelFileNormalization.Clean(jobTitle ?? string.Empty, nameof(jobTitle));
        }

        if (isDeceased && !deceasedDate.HasValue)
        {
            throw new InvalidOperationException("DeceasedDate is required when IsDeceased is true.");
        }

        PublicId = Guid.NewGuid();
        FirstName = PersonnelFileNormalization.Clean(firstName, nameof(firstName));
        LastName = PersonnelFileNormalization.Clean(lastName, nameof(lastName));
        FullName = $"{FirstName} {LastName}";
        Relationship = PersonnelFileNormalization.Clean(relationship, nameof(relationship));
        Nationality = PersonnelFileNormalization.CleanOptional(nationality);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        Sex = sex;
        MaritalStatus = PersonnelFileNormalization.CleanOptional(maritalStatus);
        Occupation = PersonnelFileNormalization.CleanOptional(occupation);
        DocumentType = PersonnelFileNormalization.CleanOptional(documentType);
        DocumentNumber = PersonnelFileNormalization.CleanOptional(documentNumber);
        Phone = PersonnelFileNormalization.CleanOptional(phone);
        IsStudying = isStudying;
        StudyPlace = PersonnelFileNormalization.CleanOptional(studyPlace);
        AcademicLevel = PersonnelFileNormalization.CleanOptional(academicLevel);
        IsBeneficiary = isBeneficiary;
        IsWorking = isWorking;
        Workplace = PersonnelFileNormalization.CleanOptional(workplace);
        JobTitle = PersonnelFileNormalization.CleanOptional(jobTitle);
        WorkPhone = PersonnelFileNormalization.CleanOptional(workPhone);
        Salary = salary;
        IsDeceased = isDeceased;
        DeceasedDate = PersonnelFileNormalization.NormalizeDate(deceasedDate);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string Relationship { get; private set; } = string.Empty;

    public string? Nationality { get; private set; }

    public DateTime? BirthDate { get; private set; }

    public PersonnelFamilyMemberSex Sex { get; private set; }

    public string? MaritalStatus { get; private set; }

    public string? Occupation { get; private set; }

    public string? DocumentType { get; private set; }

    public string? DocumentNumber { get; private set; }

    public string? Phone { get; private set; }

    public bool IsStudying { get; private set; }

    public string? StudyPlace { get; private set; }

    public string? AcademicLevel { get; private set; }

    public bool IsBeneficiary { get; private set; }

    public bool IsWorking { get; private set; }

    public string? Workplace { get; private set; }

    public string? JobTitle { get; private set; }

    public string? WorkPhone { get; private set; }

    public decimal? Salary { get; private set; }

    public bool IsDeceased { get; private set; }

    public DateTime? DeceasedDate { get; private set; }

    public static PersonnelFileFamilyMember Create(
        string firstName,
        string lastName,
        string relationship,
        string? nationality,
        DateTime? birthDate,
        PersonnelFamilyMemberSex sex,
        string? maritalStatus,
        string? occupation,
        string? documentType,
        string? documentNumber,
        string? phone,
        bool isStudying,
        string? studyPlace,
        string? academicLevel,
        bool isBeneficiary,
        bool isWorking,
        string? workplace,
        string? jobTitle,
        string? workPhone,
        decimal? salary,
        bool isDeceased,
        DateTime? deceasedDate) =>
        new(
            firstName,
            lastName,
            relationship,
            nationality,
            birthDate,
            sex,
            maritalStatus,
            occupation,
            documentType,
            documentNumber,
            phone,
            isStudying,
            studyPlace,
            academicLevel,
            isBeneficiary,
            isWorking,
            workplace,
            jobTitle,
            workPhone,
            salary,
            isDeceased,
            deceasedDate);
}

public sealed class PersonnelFileHobby : TenantEntity
{
    private PersonnelFileHobby()
    {
    }

    private PersonnelFileHobby(string hobbyName)
    {
        PublicId = Guid.NewGuid();
        HobbyName = PersonnelFileNormalization.Clean(hobbyName, nameof(hobbyName));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string HobbyName { get; private set; } = string.Empty;

    public static PersonnelFileHobby Create(string hobbyName) =>
        new(hobbyName);
}

public sealed class PersonnelFileEmployeeRelation : TenantEntity
{
    private PersonnelFileEmployeeRelation()
    {
    }

    private PersonnelFileEmployeeRelation(string relatedEmployeeName, string relationship)
    {
        PublicId = Guid.NewGuid();
        RelatedEmployeeName = PersonnelFileNormalization.Clean(relatedEmployeeName, nameof(relatedEmployeeName));
        Relationship = PersonnelFileNormalization.Clean(relationship, nameof(relationship));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string RelatedEmployeeName { get; private set; } = string.Empty;

    public string Relationship { get; private set; } = string.Empty;

    public static PersonnelFileEmployeeRelation Create(string relatedEmployeeName, string relationship) =>
        new(relatedEmployeeName, relationship);
}

public sealed class PersonnelFileBankAccount : TenantEntity
{
    private PersonnelFileBankAccount()
    {
    }

    private PersonnelFileBankAccount(
        string bankCode,
        string currencyCode,
        string accountNumber,
        string accountTypeCode,
        bool isPrimary)
    {
        PublicId = Guid.NewGuid();
        BankCode = PersonnelFileNormalization.Clean(bankCode, nameof(bankCode));
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        AccountNumber = PersonnelFileNormalization.Clean(accountNumber, nameof(accountNumber));
        NormalizedAccountNumber = PersonnelFileNormalization.NormalizeCode(accountNumber);
        AccountTypeCode = PersonnelFileNormalization.Clean(accountTypeCode, nameof(accountTypeCode));
        IsPrimary = isPrimary;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string BankCode { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public string AccountNumber { get; private set; } = string.Empty;

    public string NormalizedAccountNumber { get; private set; } = string.Empty;

    public string AccountTypeCode { get; private set; } = string.Empty;

    public bool IsPrimary { get; private set; }

    public static PersonnelFileBankAccount Create(
        string bankCode,
        string currencyCode,
        string accountNumber,
        string accountTypeCode,
        bool isPrimary) =>
        new(bankCode, currencyCode, accountNumber, accountTypeCode, isPrimary);
}

public sealed class PersonnelFileAssociation : TenantEntity
{
    private PersonnelFileAssociation()
    {
    }

    private PersonnelFileAssociation(
        string associationName,
        string? role,
        DateTime? joinedDate,
        DateTime? leftDate,
        decimal? payment)
    {
        if (joinedDate.HasValue && leftDate.HasValue && leftDate.Value.Date < joinedDate.Value.Date)
        {
            throw new InvalidOperationException("LeftDate cannot be earlier than JoinedDate.");
        }

        PublicId = Guid.NewGuid();
        AssociationName = PersonnelFileNormalization.Clean(associationName, nameof(associationName));
        Role = PersonnelFileNormalization.CleanOptional(role);
        JoinedDate = PersonnelFileNormalization.NormalizeDate(joinedDate);
        LeftDate = PersonnelFileNormalization.NormalizeDate(leftDate);
        Payment = payment;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string AssociationName { get; private set; } = string.Empty;

    public string? Role { get; private set; }

    public DateTime? JoinedDate { get; private set; }

    public DateTime? LeftDate { get; private set; }

    public decimal? Payment { get; private set; }

    public static PersonnelFileAssociation Create(
        string associationName,
        string? role,
        DateTime? joinedDate,
        DateTime? leftDate,
        decimal? payment) =>
        new(associationName, role, joinedDate, leftDate, payment);
}

public sealed class PersonnelFileEducation : TenantEntity
{
    private PersonnelFileEducation()
    {
    }

    private PersonnelFileEducation(
        string statusCode,
        string? degreeTitle,
        string studyTypeCode,
        string career,
        string institution,
        string countryCode,
        string? specialty,
        bool isCurrentlyStudying,
        DateTime startDate,
        DateTime? endDate,
        string? shiftCode,
        string? modalityCode,
        int? totalSubjects,
        int? approvedSubjects)
    {
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
        {
            throw new InvalidOperationException("EndDate cannot be earlier than StartDate.");
        }

        if (!isCurrentlyStudying && !endDate.HasValue)
        {
            throw new InvalidOperationException("EndDate is required when IsCurrentlyStudying is false.");
        }

        if (totalSubjects.HasValue && totalSubjects.Value < 0)
        {
            throw new InvalidOperationException("TotalSubjects cannot be negative.");
        }

        if (approvedSubjects.HasValue && approvedSubjects.Value < 0)
        {
            throw new InvalidOperationException("ApprovedSubjects cannot be negative.");
        }

        if (totalSubjects.HasValue && approvedSubjects.HasValue && approvedSubjects.Value > totalSubjects.Value)
        {
            throw new InvalidOperationException("ApprovedSubjects cannot be greater than TotalSubjects.");
        }

        PublicId = Guid.NewGuid();
        StatusCode = PersonnelFileNormalization.Clean(statusCode, nameof(statusCode));
        DegreeTitle = PersonnelFileNormalization.CleanOptional(degreeTitle);
        StudyTypeCode = PersonnelFileNormalization.Clean(studyTypeCode, nameof(studyTypeCode));
        Career = PersonnelFileNormalization.Clean(career, nameof(career));
        Institution = PersonnelFileNormalization.Clean(institution, nameof(institution));
        CountryCode = PersonnelFileNormalization.Clean(countryCode, nameof(countryCode));
        Specialty = PersonnelFileNormalization.CleanOptional(specialty);
        IsCurrentlyStudying = isCurrentlyStudying;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        ShiftCode = PersonnelFileNormalization.CleanOptional(shiftCode);
        ModalityCode = PersonnelFileNormalization.CleanOptional(modalityCode);
        TotalSubjects = totalSubjects;
        ApprovedSubjects = approvedSubjects;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string StatusCode { get; private set; } = string.Empty;

    public string? DegreeTitle { get; private set; }

    public string StudyTypeCode { get; private set; } = string.Empty;

    public string Career { get; private set; } = string.Empty;

    public string Institution { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string? Specialty { get; private set; }

    public bool IsCurrentlyStudying { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public string? ShiftCode { get; private set; }

    public string? ModalityCode { get; private set; }

    public int? TotalSubjects { get; private set; }

    public int? ApprovedSubjects { get; private set; }

    public static PersonnelFileEducation Create(
        string statusCode,
        string? degreeTitle,
        string studyTypeCode,
        string career,
        string institution,
        string countryCode,
        string? specialty,
        bool isCurrentlyStudying,
        DateTime startDate,
        DateTime? endDate,
        string? shiftCode,
        string? modalityCode,
        int? totalSubjects,
        int? approvedSubjects) =>
        new(
            statusCode,
            degreeTitle,
            studyTypeCode,
            career,
            institution,
            countryCode,
            specialty,
            isCurrentlyStudying,
            startDate,
            endDate,
            shiftCode,
            modalityCode,
            totalSubjects,
            approvedSubjects);
}

public sealed class PersonnelFileLanguage : TenantEntity
{
    private PersonnelFileLanguage()
    {
    }

    private PersonnelFileLanguage(
        string languageCode,
        string levelCode,
        bool speaks,
        bool writes,
        bool reads)
    {
        if (!speaks && !writes && !reads)
        {
            throw new InvalidOperationException("At least one language skill must be true.");
        }

        PublicId = Guid.NewGuid();
        LanguageCode = PersonnelFileNormalization.Clean(languageCode, nameof(languageCode));
        LevelCode = PersonnelFileNormalization.Clean(levelCode, nameof(levelCode));
        Speaks = speaks;
        Writes = writes;
        Reads = reads;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string LanguageCode { get; private set; } = string.Empty;

    public string LevelCode { get; private set; } = string.Empty;

    public bool Speaks { get; private set; }

    public bool Writes { get; private set; }

    public bool Reads { get; private set; }

    public static PersonnelFileLanguage Create(
        string languageCode,
        string levelCode,
        bool speaks,
        bool writes,
        bool reads) =>
        new(languageCode, levelCode, speaks, writes, reads);
}

public sealed class PersonnelFileTraining : TenantEntity
{
    private PersonnelFileTraining()
    {
    }

    private PersonnelFileTraining(
        string trainingName,
        string trainingTypeCode,
        string? description,
        string? topic,
        string? institution,
        string? instructors,
        decimal? score,
        DateTime startDate,
        DateTime? endDate,
        bool isInternal,
        bool isLocal,
        string countryCode,
        decimal durationValue,
        string durationUnitCode,
        decimal? costAmount,
        string? costCurrencyCode)
    {
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
        {
            throw new InvalidOperationException("EndDate cannot be earlier than StartDate.");
        }

        if (durationValue <= 0)
        {
            throw new InvalidOperationException("DurationValue must be greater than zero.");
        }

        if (costAmount.HasValue && costAmount.Value < 0)
        {
            throw new InvalidOperationException("CostAmount cannot be negative.");
        }

        if (costAmount.HasValue && string.IsNullOrWhiteSpace(costCurrencyCode))
        {
            throw new InvalidOperationException("CostCurrencyCode is required when CostAmount is provided.");
        }

        PublicId = Guid.NewGuid();
        TrainingName = PersonnelFileNormalization.Clean(trainingName, nameof(trainingName));
        TrainingTypeCode = PersonnelFileNormalization.Clean(trainingTypeCode, nameof(trainingTypeCode));
        Description = PersonnelFileNormalization.CleanOptional(description);
        Topic = PersonnelFileNormalization.CleanOptional(topic);
        Institution = PersonnelFileNormalization.CleanOptional(institution);
        Instructors = PersonnelFileNormalization.CleanOptional(instructors);
        Score = score;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        IsInternal = isInternal;
        IsLocal = isLocal;
        CountryCode = PersonnelFileNormalization.Clean(countryCode, nameof(countryCode));
        DurationValue = durationValue;
        DurationUnitCode = PersonnelFileNormalization.Clean(durationUnitCode, nameof(durationUnitCode));
        CostAmount = costAmount;
        CostCurrencyCode = PersonnelFileNormalization.CleanOptional(costCurrencyCode);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string TrainingName { get; private set; } = string.Empty;

    public string TrainingTypeCode { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Topic { get; private set; }

    public string? Institution { get; private set; }

    public string? Instructors { get; private set; }

    public decimal? Score { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public bool IsInternal { get; private set; }

    public bool IsLocal { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public decimal DurationValue { get; private set; }

    public string DurationUnitCode { get; private set; } = string.Empty;

    public decimal? CostAmount { get; private set; }

    public string? CostCurrencyCode { get; private set; }

    public static PersonnelFileTraining Create(
        string trainingName,
        string trainingTypeCode,
        string? description,
        string? topic,
        string? institution,
        string? instructors,
        decimal? score,
        DateTime startDate,
        DateTime? endDate,
        bool isInternal,
        bool isLocal,
        string countryCode,
        decimal durationValue,
        string durationUnitCode,
        decimal? costAmount,
        string? costCurrencyCode) =>
        new(
            trainingName,
            trainingTypeCode,
            description,
            topic,
            institution,
            instructors,
            score,
            startDate,
            endDate,
            isInternal,
            isLocal,
            countryCode,
            durationValue,
            durationUnitCode,
            costAmount,
            costCurrencyCode);
}

public sealed class PersonnelFilePreviousEmployment : TenantEntity
{
    private PersonnelFilePreviousEmployment()
    {
    }

    private PersonnelFilePreviousEmployment(
        string institution,
        string? place,
        string? lastPosition,
        string? managerName,
        DateTime entryDate,
        DateTime? retirementDate,
        string? companyPhone,
        string? exitReason,
        decimal? firstSalaryAmount,
        decimal? lastSalaryAmount,
        decimal? averageCommissionAmount,
        string currencyCode)
    {
        if (retirementDate.HasValue && retirementDate.Value.Date < entryDate.Date)
        {
            throw new InvalidOperationException("RetirementDate cannot be earlier than EntryDate.");
        }

        if (firstSalaryAmount.HasValue && firstSalaryAmount.Value < 0)
        {
            throw new InvalidOperationException("FirstSalaryAmount cannot be negative.");
        }

        if (lastSalaryAmount.HasValue && lastSalaryAmount.Value < 0)
        {
            throw new InvalidOperationException("LastSalaryAmount cannot be negative.");
        }

        if (averageCommissionAmount.HasValue && averageCommissionAmount.Value < 0)
        {
            throw new InvalidOperationException("AverageCommissionAmount cannot be negative.");
        }

        PublicId = Guid.NewGuid();
        Institution = PersonnelFileNormalization.Clean(institution, nameof(institution));
        Place = PersonnelFileNormalization.CleanOptional(place);
        LastPosition = PersonnelFileNormalization.CleanOptional(lastPosition);
        ManagerName = PersonnelFileNormalization.CleanOptional(managerName);
        EntryDate = PersonnelFileNormalization.NormalizeDate(entryDate);
        RetirementDate = PersonnelFileNormalization.NormalizeDate(retirementDate);
        CompanyPhone = PersonnelFileNormalization.CleanOptional(companyPhone);
        ExitReason = PersonnelFileNormalization.CleanOptional(exitReason);
        FirstSalaryAmount = firstSalaryAmount;
        LastSalaryAmount = lastSalaryAmount;
        AverageCommissionAmount = averageCommissionAmount;
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string Institution { get; private set; } = string.Empty;

    public string? Place { get; private set; }

    public string? LastPosition { get; private set; }

    public string? ManagerName { get; private set; }

    public DateTime EntryDate { get; private set; }

    public DateTime? RetirementDate { get; private set; }

    public string? CompanyPhone { get; private set; }

    public string? ExitReason { get; private set; }

    public decimal? FirstSalaryAmount { get; private set; }

    public decimal? LastSalaryAmount { get; private set; }

    public decimal? AverageCommissionAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public static PersonnelFilePreviousEmployment Create(
        string institution,
        string? place,
        string? lastPosition,
        string? managerName,
        DateTime entryDate,
        DateTime? retirementDate,
        string? companyPhone,
        string? exitReason,
        decimal? firstSalaryAmount,
        decimal? lastSalaryAmount,
        decimal? averageCommissionAmount,
        string currencyCode) =>
        new(
            institution,
            place,
            lastPosition,
            managerName,
            entryDate,
            retirementDate,
            companyPhone,
            exitReason,
            firstSalaryAmount,
            lastSalaryAmount,
            averageCommissionAmount,
            currencyCode);
}

public sealed class PersonnelFileReference : TenantEntity
{
    private PersonnelFileReference()
    {
    }

    private PersonnelFileReference(
        string personName,
        string? address,
        string phone,
        string referenceTypeCode,
        string? occupation,
        string? workplace,
        string? workPhone,
        decimal knownTimeYears)
    {
        if (knownTimeYears < 0)
        {
            throw new InvalidOperationException("KnownTimeYears cannot be negative.");
        }

        PublicId = Guid.NewGuid();
        PersonName = PersonnelFileNormalization.Clean(personName, nameof(personName));
        Address = PersonnelFileNormalization.CleanOptional(address);
        Phone = PersonnelFileNormalization.Clean(phone, nameof(phone));
        ReferenceTypeCode = PersonnelFileNormalization.Clean(referenceTypeCode, nameof(referenceTypeCode));
        Occupation = PersonnelFileNormalization.CleanOptional(occupation);
        Workplace = PersonnelFileNormalization.CleanOptional(workplace);
        WorkPhone = PersonnelFileNormalization.CleanOptional(workPhone);
        KnownTimeYears = knownTimeYears;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string PersonName { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string Phone { get; private set; } = string.Empty;

    public string ReferenceTypeCode { get; private set; } = string.Empty;

    public string? Occupation { get; private set; }

    public string? Workplace { get; private set; }

    public string? WorkPhone { get; private set; }

    public decimal KnownTimeYears { get; private set; }

    public static PersonnelFileReference Create(
        string personName,
        string? address,
        string phone,
        string referenceTypeCode,
        string? occupation,
        string? workplace,
        string? workPhone,
        decimal knownTimeYears) =>
        new(personName, address, phone, referenceTypeCode, occupation, workplace, workPhone, knownTimeYears);
}

public sealed class PersonnelFileDocument : TenantEntity
{
    private PersonnelFileDocument()
    {
    }

    private PersonnelFileDocument(
        string documentType,
        string? observations,
        DateTime? deliveryDate,
        DateTime? loanDate,
        DateTime? returnDate,
        string fileName,
        string contentType,
        int sizeBytes,
        string sha256,
        byte[] fileData)
    {
        if (loanDate.HasValue && returnDate.HasValue && returnDate.Value.Date < loanDate.Value.Date)
        {
            throw new InvalidOperationException("ReturnDate cannot be earlier than LoanDate.");
        }

        PublicId = Guid.NewGuid();
        DocumentType = PersonnelFileNormalization.Clean(documentType, nameof(documentType));
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        DeliveryDate = PersonnelFileNormalization.NormalizeDate(deliveryDate);
        LoanDate = PersonnelFileNormalization.NormalizeDate(loanDate);
        ReturnDate = PersonnelFileNormalization.NormalizeDate(returnDate);
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Sha256 = PersonnelFileNormalization.Clean(sha256, nameof(sha256));
        FileData = fileData;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string DocumentType { get; private set; } = string.Empty;

    public string? Observations { get; private set; }

    public DateTime? DeliveryDate { get; private set; }

    public DateTime? LoanDate { get; private set; }

    public DateTime? ReturnDate { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public byte[] FileData { get; private set; } = [];

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PersonnelFileDocument Create(
        string documentType,
        string? observations,
        DateTime? deliveryDate,
        DateTime? loanDate,
        DateTime? returnDate,
        string fileName,
        string contentType,
        int sizeBytes,
        string sha256,
        byte[] fileData) =>
        new(documentType, observations, deliveryDate, loanDate, returnDate, fileName, contentType, sizeBytes, sha256, fileData);

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFileCustomFieldDefinition : TenantEntity
{
    private PersonnelFileCustomFieldDefinition()
    {
    }

    private PersonnelFileCustomFieldDefinition(
        Guid publicId,
        string key,
        string label,
        PersonnelCustomFieldType fieldType,
        bool isRequired,
        bool isActive,
        string? optionsJson,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder cannot be negative.");
        }

        PublicId = publicId;
        Key = PersonnelFileNormalization.Clean(key, nameof(key));
        NormalizedKey = PersonnelFileNormalization.NormalizeCode(key);
        Label = PersonnelFileNormalization.Clean(label, nameof(label));
        FieldType = fieldType;
        IsRequired = isRequired;
        IsActive = isActive;
        OptionsJson = PersonnelFileNormalization.CleanOptional(optionsJson);
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Key { get; private set; } = string.Empty;

    public string NormalizedKey { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public PersonnelCustomFieldType FieldType { get; private set; }

    public bool IsRequired { get; private set; }

    public bool IsActive { get; private set; }

    public string? OptionsJson { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PersonnelFileCustomFieldDefinition Create(
        string key,
        string label,
        PersonnelCustomFieldType fieldType,
        bool isRequired,
        bool isActive,
        string? optionsJson,
        int sortOrder) =>
        new(Guid.NewGuid(), key, label, fieldType, isRequired, isActive, optionsJson, sortOrder);

    public void Update(
        string key,
        string label,
        PersonnelCustomFieldType fieldType,
        bool isRequired,
        bool isActive,
        string? optionsJson,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder cannot be negative.");
        }

        Key = PersonnelFileNormalization.Clean(key, nameof(key));
        NormalizedKey = PersonnelFileNormalization.NormalizeCode(key);
        Label = PersonnelFileNormalization.Clean(label, nameof(label));
        FieldType = fieldType;
        IsRequired = isRequired;
        IsActive = isActive;
        OptionsJson = PersonnelFileNormalization.CleanOptional(optionsJson);
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class PersonnelFileObservation : TenantEntity
{
    private PersonnelFileObservation()
    {
    }

    private PersonnelFileObservation(Guid authorUserPublicId, string note)
    {
        PublicId = Guid.NewGuid();
        AuthorUserPublicId = authorUserPublicId;
        Note = PersonnelFileNormalization.Clean(note, nameof(note));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public Guid AuthorUserPublicId { get; private set; }

    public string Note { get; private set; } = string.Empty;

    public static PersonnelFileObservation Create(Guid authorUserPublicId, string note) =>
        new(authorUserPublicId, note);
}

public sealed class PersonnelCatalogItem : TenantEntity
{
    private PersonnelCatalogItem()
    {
    }

    private PersonnelCatalogItem(
        Guid publicId,
        string category,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder cannot be negative.");
        }

        PublicId = publicId;
        Category = PersonnelFileNormalization.Clean(category, nameof(category));
        Code = PersonnelFileNormalization.NormalizeCode(code);
        NormalizedCode = Code;
        Name = PersonnelFileNormalization.Clean(name, nameof(name));
        NormalizedName = PersonnelFileNormalization.NormalizeName(name);
        IsSystem = isSystem;
        IsActive = isActive;
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Category { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PersonnelCatalogItem Create(
        string category,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), category, code, name, isSystem, isActive, sortOrder);
}
