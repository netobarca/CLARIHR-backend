using CLARIHR.Domain.Common;
using CLARIHR.Domain.Banks;
using CLARIHR.Domain.EducationCatalogs;

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
        Guid? photoFilePublicId,
        Guid? orgUnitPublicId,
        Guid? assignedPositionSlotPublicId)
    {
        PublicId = publicId;
        RecordType = recordType;
        LifecycleStatus = PersonnelFileLifecycleStatus.Draft;
        SetName(firstName, lastName);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        MaritalStatus = NormalizeOptionalCode(maritalStatus);
        Profession = NormalizeOptionalCode(profession);
        Nationality = PersonnelFileNormalization.CleanOptional(nationality);
        PersonalEmail = PersonnelFileNormalization.CleanOptional(personalEmail);
        InstitutionalEmail = PersonnelFileNormalization.CleanOptional(institutionalEmail);
        PersonalPhone = PersonnelFileNormalization.CleanOptional(personalPhone);
        InstitutionalPhone = PersonnelFileNormalization.CleanOptional(institutionalPhone);
        BirthCountry = NormalizeOptionalCode(birthCountry);
        BirthDepartment = NormalizeOptionalCode(birthDepartment);
        BirthMunicipality = NormalizeOptionalCode(birthMunicipality);
        PhotoFilePublicId = photoFilePublicId;
        OrgUnitPublicId = orgUnitPublicId;
        AssignedPositionSlotPublicId = assignedPositionSlotPublicId;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public PersonnelFileRecordType RecordType { get; private set; }

    public PersonnelFileLifecycleStatus LifecycleStatus { get; private set; }

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

    public Guid? PhotoFilePublicId { get; private set; }

    public Guid? OrgUnitPublicId { get; private set; }

    public Guid? AssignedPositionSlotPublicId { get; private set; }

    public Guid? LinkedUserPublicId { get; private set; }



    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public bool IsCompletedEmployee =>
        RecordType == PersonnelFileRecordType.Employee &&
        LifecycleStatus == PersonnelFileLifecycleStatus.Completed;

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
        Guid? photoFilePublicId,
        Guid? orgUnitPublicId,
        Guid? assignedPositionSlotPublicId,
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
            photoFilePublicId,
            orgUnitPublicId,
            assignedPositionSlotPublicId);

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
        Guid? photoFilePublicId,
        Guid? orgUnitPublicId,
        Guid? assignedPositionSlotPublicId)
    {
        var normalizedInstitutionalEmail = PersonnelFileNormalization.CleanOptional(institutionalEmail);
        if (LifecycleStatus == PersonnelFileLifecycleStatus.Completed)
        {
            if (!string.Equals(InstitutionalEmail, normalizedInstitutionalEmail, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("InstitutionalEmail cannot be changed after personnel file completion.");
            }

            if (AssignedPositionSlotPublicId != assignedPositionSlotPublicId)
            {
                throw new InvalidOperationException("AssignedPositionSlotPublicId cannot be changed after personnel file completion.");
            }
        }

        RecordType = recordType;
        SetName(firstName, lastName);
        BirthDate = PersonnelFileNormalization.NormalizeDate(birthDate);
        MaritalStatus = NormalizeOptionalCode(maritalStatus);
        Profession = NormalizeOptionalCode(profession);
        Nationality = PersonnelFileNormalization.CleanOptional(nationality);
        PersonalEmail = PersonnelFileNormalization.CleanOptional(personalEmail);
        InstitutionalEmail = normalizedInstitutionalEmail;
        PersonalPhone = PersonnelFileNormalization.CleanOptional(personalPhone);
        InstitutionalPhone = PersonnelFileNormalization.CleanOptional(institutionalPhone);
        BirthCountry = NormalizeOptionalCode(birthCountry);
        BirthDepartment = NormalizeOptionalCode(birthDepartment);
        BirthMunicipality = NormalizeOptionalCode(birthMunicipality);
        PhotoFilePublicId = photoFilePublicId;
        OrgUnitPublicId = orgUnitPublicId;
        AssignedPositionSlotPublicId = assignedPositionSlotPublicId;
        RefreshConcurrencyToken();
    }

    public void Complete(Guid linkedUserPublicId)
    {
        if (linkedUserPublicId == Guid.Empty)
        {
            throw new ArgumentException("Linked user public id is required.", nameof(linkedUserPublicId));
        }

        LifecycleStatus = PersonnelFileLifecycleStatus.Completed;
        LinkedUserPublicId = linkedUserPublicId;
        RefreshConcurrencyToken();
    }

    public void CompleteWithoutLinkedUser()
    {
        LifecycleStatus = PersonnelFileLifecycleStatus.Completed;
        LinkedUserPublicId = null;
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

    public void AddIdentification(PersonnelFileIdentification item)
    {
        item.SetTenantId(TenantId);
        _identifications.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateIdentification(
        Guid identificationPublicId,
        string identificationType,
        string identificationNumber,
        DateTime? issuedDate,
        DateTime? expiryDate,
        string? issuer,
        bool isPrimary)
    {
        var identification = _identifications.FirstOrDefault(i => i.PublicId == identificationPublicId)
            ?? throw new InvalidOperationException($"Identification with public id {identificationPublicId} not found.");

        identification.Update(
            identificationType,
            identificationNumber,
            issuedDate,
            expiryDate,
            issuer,
            isPrimary);

        RefreshConcurrencyToken();
    }

    public void RemoveIdentification(Guid identificationPublicId)
    {
        var identification = _identifications.FirstOrDefault(i => i.PublicId == identificationPublicId)
            ?? throw new InvalidOperationException($"Identification with public id {identificationPublicId} not found.");

        _identifications.Remove(identification);
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

    public void AddAddress(PersonnelFileAddress item)
    {
        item.SetTenantId(TenantId);
        _addresses.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateAddress(
        Guid addressPublicId,
        string addressLine,
        string? country,
        string? department,
        string? municipality,
        string? postalCode,
        bool isCurrent)
    {
        var address = _addresses.FirstOrDefault(i => i.PublicId == addressPublicId)
            ?? throw new InvalidOperationException($"Address with public id {addressPublicId} not found.");

        address.Update(addressLine, country, department, municipality, postalCode, isCurrent);
        RefreshConcurrencyToken();
    }

    public void RemoveAddress(Guid addressPublicId)
    {
        var address = _addresses.FirstOrDefault(i => i.PublicId == addressPublicId)
            ?? throw new InvalidOperationException($"Address with public id {addressPublicId} not found.");

        _addresses.Remove(address);
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

    public void AddEmergencyContact(PersonnelFileEmergencyContact item)
    {
        item.SetTenantId(TenantId);
        _emergencyContacts.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateEmergencyContact(
        Guid emergencyContactPublicId,
        string name,
        string relationship,
        string phone,
        string? address,
        string? workplace)
    {
        var emergencyContact = _emergencyContacts.FirstOrDefault(i => i.PublicId == emergencyContactPublicId)
            ?? throw new InvalidOperationException($"Emergency contact with public id {emergencyContactPublicId} not found.");

        emergencyContact.Update(name, relationship, phone, address, workplace);
        RefreshConcurrencyToken();
    }

    public void RemoveEmergencyContact(Guid emergencyContactPublicId)
    {
        var emergencyContact = _emergencyContacts.FirstOrDefault(i => i.PublicId == emergencyContactPublicId)
            ?? throw new InvalidOperationException($"Emergency contact with public id {emergencyContactPublicId} not found.");

        _emergencyContacts.Remove(emergencyContact);
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

    public void AddFamilyMember(PersonnelFileFamilyMember item)
    {
        item.SetTenantId(TenantId);
        _familyMembers.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateFamilyMember(
        Guid familyMemberPublicId,
        string firstName,
        string lastName,
        string kinshipCode,
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
        var familyMember = _familyMembers.FirstOrDefault(i => i.PublicId == familyMemberPublicId)
            ?? throw new InvalidOperationException($"Family member with public id {familyMemberPublicId} not found.");

        familyMember.Update(
            firstName,
            lastName,
            kinshipCode,
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

        RefreshConcurrencyToken();
    }

    public void RemoveFamilyMember(Guid familyMemberPublicId)
    {
        var familyMember = _familyMembers.FirstOrDefault(i => i.PublicId == familyMemberPublicId)
            ?? throw new InvalidOperationException($"Family member with public id {familyMemberPublicId} not found.");

        _familyMembers.Remove(familyMember);
        RefreshConcurrencyToken();
    }

    public void AddHobby(PersonnelFileHobby item)
    {
        item.SetTenantId(TenantId);
        _hobbies.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateHobby(Guid hobbyPublicId, string hobbyName)
    {
        var hobby = _hobbies.FirstOrDefault(i => i.PublicId == hobbyPublicId)
            ?? throw new InvalidOperationException($"Hobby with public id {hobbyPublicId} not found.");

        hobby.Update(hobbyName);
        RefreshConcurrencyToken();
    }

    public void RemoveHobby(Guid hobbyPublicId)
    {
        var hobby = _hobbies.FirstOrDefault(i => i.PublicId == hobbyPublicId)
            ?? throw new InvalidOperationException($"Hobby with public id {hobbyPublicId} not found.");

        _hobbies.Remove(hobby);
        RefreshConcurrencyToken();
    }

    public void AddEmployeeRelation(PersonnelFileEmployeeRelation item)
    {
        item.SetTenantId(TenantId);
        _employeeRelations.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateEmployeeRelation(
        Guid relationPublicId,
        long relatedPersonnelFileId,
        string relationship)
    {
        var relation = _employeeRelations.FirstOrDefault(i => i.PublicId == relationPublicId)
            ?? throw new InvalidOperationException($"Employee relation with public id {relationPublicId} not found.");

        relation.Update(relatedPersonnelFileId, relationship);
        RefreshConcurrencyToken();
    }

    public void RemoveEmployeeRelation(Guid relationPublicId)
    {
        var relation = _employeeRelations.FirstOrDefault(i => i.PublicId == relationPublicId)
            ?? throw new InvalidOperationException($"Employee relation with public id {relationPublicId} not found.");

        _employeeRelations.Remove(relation);
        RefreshConcurrencyToken();
    }

    public void AddBankAccount(PersonnelFileBankAccount item)
    {
        item.SetTenantId(TenantId);
        _bankAccounts.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateBankAccount(
        Guid bankAccountPublicId,
        long? bankCatalogItemId,
        string bankCode,
        string currencyCode,
        string accountNumber,
        string accountTypeCode,
        bool isPrimary)
    {
        var bankAccount = _bankAccounts.FirstOrDefault(i => i.PublicId == bankAccountPublicId)
            ?? throw new InvalidOperationException($"Bank account with public id {bankAccountPublicId} not found.");

        bankAccount.Update(bankCatalogItemId, bankCode, currencyCode, accountNumber, accountTypeCode, isPrimary);
        RefreshConcurrencyToken();
    }

    public void RemoveBankAccount(Guid bankAccountPublicId)
    {
        var bankAccount = _bankAccounts.FirstOrDefault(i => i.PublicId == bankAccountPublicId)
            ?? throw new InvalidOperationException($"Bank account with public id {bankAccountPublicId} not found.");

        _bankAccounts.Remove(bankAccount);
        RefreshConcurrencyToken();
    }

    public void AddAssociation(PersonnelFileAssociation item)
    {
        item.SetTenantId(TenantId);
        _associations.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateAssociation(
        Guid associationPublicId,
        string associationName,
        string? role,
        DateTime? joinedDate,
        DateTime? leftDate,
        decimal? payment)
    {
        var association = _associations.FirstOrDefault(i => i.PublicId == associationPublicId)
            ?? throw new InvalidOperationException($"Association with public id {associationPublicId} not found.");

        association.Update(associationName, role, joinedDate, leftDate, payment);
        RefreshConcurrencyToken();
    }

    public void RemoveAssociation(Guid associationPublicId)
    {
        var association = _associations.FirstOrDefault(i => i.PublicId == associationPublicId)
            ?? throw new InvalidOperationException($"Association with public id {associationPublicId} not found.");

        _associations.Remove(association);
        RefreshConcurrencyToken();
    }

    public void AddEducation(PersonnelFileEducation item)
    {
        item.SetTenantId(TenantId);
        _educations.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateEducation(
        Guid educationPublicId,
        long educationStatusCatalogItemId,
        string? degreeTitle,
        long educationStudyTypeCatalogItemId,
        long educationCareerCatalogItemId,
        string institution,
        string countryCode,
        string? specialty,
        bool isCurrentlyStudying,
        DateTime startDate,
        DateTime? endDate,
        long? educationShiftCatalogItemId,
        long? educationModalityCatalogItemId,
        int? totalSubjects,
        int? approvedSubjects)
    {
        var education = _educations.FirstOrDefault(i => i.PublicId == educationPublicId)
            ?? throw new InvalidOperationException($"Education with public id {educationPublicId} not found.");

        education.Update(
            educationStatusCatalogItemId,
            degreeTitle,
            educationStudyTypeCatalogItemId,
            educationCareerCatalogItemId,
            institution,
            countryCode,
            specialty,
            isCurrentlyStudying,
            startDate,
            endDate,
            educationShiftCatalogItemId,
            educationModalityCatalogItemId,
            totalSubjects,
            approvedSubjects);

        RefreshConcurrencyToken();
    }

    public void RemoveEducation(Guid educationPublicId)
    {
        var education = _educations.FirstOrDefault(i => i.PublicId == educationPublicId)
            ?? throw new InvalidOperationException($"Education with public id {educationPublicId} not found.");

        _educations.Remove(education);
        RefreshConcurrencyToken();
    }

    public void AddLanguage(PersonnelFileLanguage item)
    {
        item.SetTenantId(TenantId);
        _languages.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateLanguage(
        Guid languagePublicId,
        string languageCode,
        string levelCode,
        bool speaks,
        bool writes,
        bool reads)
    {
        var language = _languages.FirstOrDefault(i => i.PublicId == languagePublicId)
            ?? throw new InvalidOperationException($"Language with public id {languagePublicId} not found.");

        language.Update(languageCode, levelCode, speaks, writes, reads);
        RefreshConcurrencyToken();
    }

    public void RemoveLanguage(Guid languagePublicId)
    {
        var language = _languages.FirstOrDefault(i => i.PublicId == languagePublicId)
            ?? throw new InvalidOperationException($"Language with public id {languagePublicId} not found.");

        _languages.Remove(language);
        RefreshConcurrencyToken();
    }

    public void AddTraining(PersonnelFileTraining training)
    {
        training.SetTenantId(TenantId);
        _trainings.Add(training);
        RefreshConcurrencyToken();
    }

    public void UpdateTraining(
        Guid trainingPublicId,
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
        var training = _trainings.FirstOrDefault(i => i.PublicId == trainingPublicId)
            ?? throw new InvalidOperationException($"Training with public id {trainingPublicId} not found.");

        training.Update(
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

        RefreshConcurrencyToken();
    }

    public void RemoveTraining(Guid trainingPublicId)
    {
        var training = _trainings.FirstOrDefault(i => i.PublicId == trainingPublicId)
            ?? throw new InvalidOperationException($"Training with public id {trainingPublicId} not found.");

        _trainings.Remove(training);
        RefreshConcurrencyToken();
    }

    public void AddPreviousEmployment(PersonnelFilePreviousEmployment item)
    {
        item.SetTenantId(TenantId);
        _previousEmployments.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdatePreviousEmployment(
        Guid previousEmploymentPublicId,
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
        var item = _previousEmployments.FirstOrDefault(i => i.PublicId == previousEmploymentPublicId)
            ?? throw new InvalidOperationException($"PreviousEmployment with public id {previousEmploymentPublicId} not found.");

        item.Update(institution, place, lastPosition, managerName, entryDate, retirementDate,
            companyPhone, exitReason, firstSalaryAmount, lastSalaryAmount, averageCommissionAmount, currencyCode);
        RefreshConcurrencyToken();
    }

    public void RemovePreviousEmployment(Guid previousEmploymentPublicId)
    {
        var item = _previousEmployments.FirstOrDefault(i => i.PublicId == previousEmploymentPublicId)
            ?? throw new InvalidOperationException($"PreviousEmployment with public id {previousEmploymentPublicId} not found.");

        _previousEmployments.Remove(item);
        RefreshConcurrencyToken();
    }

    public void AddReference(PersonnelFileReference item)
    {
        item.SetTenantId(TenantId);
        _references.Add(item);
        RefreshConcurrencyToken();
    }

    public void UpdateReference(
        Guid referencePublicId,
        string personName,
        string? address,
        string phone,
        string referenceTypeCode,
        string? occupation,
        string? workplace,
        string? workPhone,
        decimal knownTimeYears)
    {
        var reference = _references.FirstOrDefault(i => i.PublicId == referencePublicId)
            ?? throw new InvalidOperationException($"Reference with public id {referencePublicId} not found.");

        reference.Update(personName, address, phone, referenceTypeCode, occupation, workplace, workPhone, knownTimeYears);
        RefreshConcurrencyToken();
    }

    public void RemoveReference(Guid referencePublicId)
    {
        var reference = _references.FirstOrDefault(i => i.PublicId == referencePublicId)
            ?? throw new InvalidOperationException($"Reference with public id {referencePublicId} not found.");

        _references.Remove(reference);
        RefreshConcurrencyToken();
    }

    public void AddDocument(PersonnelFileDocument document)
    {
        document.SetTenantId(TenantId);
        _documents.Add(document);
        RefreshConcurrencyToken();
    }

    public void MarkDocumentsUpdated() => RefreshConcurrencyToken();

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

    private static string? NormalizeOptionalCode(string? value)
    {
        var cleaned = PersonnelFileNormalization.CleanOptional(value);
        return cleaned is null ? null : PersonnelFileNormalization.NormalizeCode(cleaned);
    }
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
        IdentificationType = PersonnelFileNormalization.NormalizeCode(identificationType);
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

    internal void Update(
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

        IdentificationType = PersonnelFileNormalization.NormalizeCode(identificationType);
        IdentificationNumber = PersonnelFileNormalization.Clean(identificationNumber, nameof(identificationNumber));
        NormalizedIdentificationNumber = PersonnelFileNormalization.NormalizeCode(identificationNumber);
        IssuedDate = PersonnelFileNormalization.NormalizeDate(issuedDate);
        ExpiryDate = PersonnelFileNormalization.NormalizeDate(expiryDate);
        Issuer = PersonnelFileNormalization.CleanOptional(issuer);
        IsPrimary = isPrimary;
    }
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

    internal void Update(
        string addressLine,
        string? country,
        string? department,
        string? municipality,
        string? postalCode,
        bool isCurrent)
    {
        AddressLine = PersonnelFileNormalization.Clean(addressLine, nameof(addressLine));
        Country = PersonnelFileNormalization.CleanOptional(country);
        Department = PersonnelFileNormalization.CleanOptional(department);
        Municipality = PersonnelFileNormalization.CleanOptional(municipality);
        PostalCode = PersonnelFileNormalization.CleanOptional(postalCode);
        IsCurrent = isCurrent;
    }
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

    internal void Update(
        string name,
        string relationship,
        string phone,
        string? address,
        string? workplace)
    {
        Name = PersonnelFileNormalization.Clean(name, nameof(name));
        Relationship = PersonnelFileNormalization.Clean(relationship, nameof(relationship));
        Phone = PersonnelFileNormalization.Clean(phone, nameof(phone));
        Address = PersonnelFileNormalization.CleanOptional(address);
        Workplace = PersonnelFileNormalization.CleanOptional(workplace);
    }
}

public sealed class PersonnelFileFamilyMember : TenantEntity
{
    private PersonnelFileFamilyMember()
    {
    }

    private PersonnelFileFamilyMember(
        string firstName,
        string lastName,
        string kinshipCode,
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
        KinshipCode = PersonnelFileNormalization.Clean(kinshipCode, nameof(kinshipCode));
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

    public string KinshipCode { get; private set; } = string.Empty;

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
        string kinshipCode,
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
            kinshipCode,
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

    internal void Update(
        string firstName,
        string lastName,
        string kinshipCode,
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

        FirstName = PersonnelFileNormalization.Clean(firstName, nameof(firstName));
        LastName = PersonnelFileNormalization.Clean(lastName, nameof(lastName));
        FullName = $"{FirstName} {LastName}";
        KinshipCode = PersonnelFileNormalization.Clean(kinshipCode, nameof(kinshipCode));
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

    internal void Update(string hobbyName)
    {
        HobbyName = PersonnelFileNormalization.Clean(hobbyName, nameof(hobbyName));
    }
}

public sealed class PersonnelFileEmployeeRelation : TenantEntity
{
    private PersonnelFileEmployeeRelation()
    {
    }

    private PersonnelFileEmployeeRelation(long relatedPersonnelFileId, string relationship)
    {
        if (relatedPersonnelFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relatedPersonnelFileId));
        }

        PublicId = Guid.NewGuid();
        RelatedPersonnelFileId = relatedPersonnelFileId;
        Relationship = PersonnelFileNormalization.Clean(relationship, nameof(relationship));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public long RelatedPersonnelFileId { get; private set; }

    public PersonnelFile RelatedPersonnelFile { get; private set; } = null!;

    public string Relationship { get; private set; } = string.Empty;

    public static PersonnelFileEmployeeRelation Create(long relatedPersonnelFileId, string relationship) =>
        new(relatedPersonnelFileId, relationship);

    internal void Update(long relatedPersonnelFileId, string relationship)
    {
        if (relatedPersonnelFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relatedPersonnelFileId));
        }

        RelatedPersonnelFileId = relatedPersonnelFileId;
        Relationship = PersonnelFileNormalization.Clean(relationship, nameof(relationship));
    }
}

public sealed class PersonnelFileBankAccount : TenantEntity
{
    private PersonnelFileBankAccount()
    {
    }

    private PersonnelFileBankAccount(
        long? bankCatalogItemId,
        string bankCode,
        string currencyCode,
        string accountNumber,
        string accountTypeCode,
        bool isPrimary)
    {
        PublicId = Guid.NewGuid();
        BankCatalogItemId = bankCatalogItemId;
        BankCode = PersonnelFileNormalization.Clean(bankCode, nameof(bankCode));
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        AccountNumber = PersonnelFileNormalization.Clean(accountNumber, nameof(accountNumber));
        NormalizedAccountNumber = PersonnelFileNormalization.NormalizeCode(accountNumber);
        AccountTypeCode = PersonnelFileNormalization.Clean(accountTypeCode, nameof(accountTypeCode));
        IsPrimary = isPrimary;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public long? BankCatalogItemId { get; private set; }

    public BankCatalogItem? BankCatalogItem { get; private set; }

    public string BankCode { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public string AccountNumber { get; private set; } = string.Empty;

    public string NormalizedAccountNumber { get; private set; } = string.Empty;

    public string AccountTypeCode { get; private set; } = string.Empty;

    public bool IsPrimary { get; private set; }

    public static PersonnelFileBankAccount Create(
        long? bankCatalogItemId,
        string bankCode,
        string currencyCode,
        string accountNumber,
        string accountTypeCode,
        bool isPrimary) =>
        new(bankCatalogItemId, bankCode, currencyCode, accountNumber, accountTypeCode, isPrimary);

    internal void Update(
        long? bankCatalogItemId,
        string bankCode,
        string currencyCode,
        string accountNumber,
        string accountTypeCode,
        bool isPrimary)
    {
        BankCatalogItemId = bankCatalogItemId;
        BankCode = PersonnelFileNormalization.Clean(bankCode, nameof(bankCode));
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode));
        AccountNumber = PersonnelFileNormalization.Clean(accountNumber, nameof(accountNumber));
        NormalizedAccountNumber = PersonnelFileNormalization.NormalizeCode(accountNumber);
        AccountTypeCode = PersonnelFileNormalization.Clean(accountTypeCode, nameof(accountTypeCode));
        IsPrimary = isPrimary;
    }
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

    internal void Update(
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

        AssociationName = PersonnelFileNormalization.Clean(associationName, nameof(associationName));
        Role = PersonnelFileNormalization.CleanOptional(role);
        JoinedDate = PersonnelFileNormalization.NormalizeDate(joinedDate);
        LeftDate = PersonnelFileNormalization.NormalizeDate(leftDate);
        Payment = payment;
    }
}

public sealed class PersonnelFileEducation : TenantEntity
{
    private PersonnelFileEducation()
    {
    }

    private PersonnelFileEducation(
        long educationStatusCatalogItemId,
        string? degreeTitle,
        long educationStudyTypeCatalogItemId,
        long educationCareerCatalogItemId,
        string institution,
        string countryCode,
        string? specialty,
        bool isCurrentlyStudying,
        DateTime startDate,
        DateTime? endDate,
        long? educationShiftCatalogItemId,
        long? educationModalityCatalogItemId,
        int? totalSubjects,
        int? approvedSubjects)
    {
        if (educationStatusCatalogItemId <= 0)
        {
            throw new InvalidOperationException("EducationStatusCatalogItemId must be greater than zero.");
        }

        if (educationStudyTypeCatalogItemId <= 0)
        {
            throw new InvalidOperationException("EducationStudyTypeCatalogItemId must be greater than zero.");
        }

        if (educationCareerCatalogItemId <= 0)
        {
            throw new InvalidOperationException("EducationCareerCatalogItemId must be greater than zero.");
        }

        if (educationShiftCatalogItemId.HasValue && educationShiftCatalogItemId.Value <= 0)
        {
            throw new InvalidOperationException("EducationShiftCatalogItemId must be greater than zero.");
        }

        if (educationModalityCatalogItemId.HasValue && educationModalityCatalogItemId.Value <= 0)
        {
            throw new InvalidOperationException("EducationModalityCatalogItemId must be greater than zero.");
        }

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
        EducationStatusCatalogItemId = educationStatusCatalogItemId;
        DegreeTitle = PersonnelFileNormalization.CleanOptional(degreeTitle);
        EducationStudyTypeCatalogItemId = educationStudyTypeCatalogItemId;
        EducationCareerCatalogItemId = educationCareerCatalogItemId;
        Institution = PersonnelFileNormalization.Clean(institution, nameof(institution));
        CountryCode = PersonnelFileNormalization.Clean(countryCode, nameof(countryCode));
        Specialty = PersonnelFileNormalization.CleanOptional(specialty);
        IsCurrentlyStudying = isCurrentlyStudying;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        EducationShiftCatalogItemId = educationShiftCatalogItemId;
        EducationModalityCatalogItemId = educationModalityCatalogItemId;
        TotalSubjects = totalSubjects;
        ApprovedSubjects = approvedSubjects;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public long EducationStatusCatalogItemId { get; private set; }

    public EducationStatusCatalogItem EducationStatusCatalogItem { get; private set; } = null!;

    public string? DegreeTitle { get; private set; }

    public long EducationStudyTypeCatalogItemId { get; private set; }

    public EducationStudyTypeCatalogItem EducationStudyTypeCatalogItem { get; private set; } = null!;

    public long EducationCareerCatalogItemId { get; private set; }

    public EducationCareerCatalogItem EducationCareerCatalogItem { get; private set; } = null!;

    public string Institution { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string? Specialty { get; private set; }

    public bool IsCurrentlyStudying { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    public long? EducationShiftCatalogItemId { get; private set; }

    public EducationShiftCatalogItem? EducationShiftCatalogItem { get; private set; }

    public long? EducationModalityCatalogItemId { get; private set; }

    public EducationModalityCatalogItem? EducationModalityCatalogItem { get; private set; }

    public int? TotalSubjects { get; private set; }

    public int? ApprovedSubjects { get; private set; }

    public static PersonnelFileEducation Create(
        long educationStatusCatalogItemId,
        string? degreeTitle,
        long educationStudyTypeCatalogItemId,
        long educationCareerCatalogItemId,
        string institution,
        string countryCode,
        string? specialty,
        bool isCurrentlyStudying,
        DateTime startDate,
        DateTime? endDate,
        long? educationShiftCatalogItemId,
        long? educationModalityCatalogItemId,
        int? totalSubjects,
        int? approvedSubjects) =>
        new(
            educationStatusCatalogItemId,
            degreeTitle,
            educationStudyTypeCatalogItemId,
            educationCareerCatalogItemId,
            institution,
            countryCode,
            specialty,
            isCurrentlyStudying,
            startDate,
            endDate,
            educationShiftCatalogItemId,
            educationModalityCatalogItemId,
            totalSubjects,
            approvedSubjects);

    internal void Update(
        long educationStatusCatalogItemId,
        string? degreeTitle,
        long educationStudyTypeCatalogItemId,
        long educationCareerCatalogItemId,
        string institution,
        string countryCode,
        string? specialty,
        bool isCurrentlyStudying,
        DateTime startDate,
        DateTime? endDate,
        long? educationShiftCatalogItemId,
        long? educationModalityCatalogItemId,
        int? totalSubjects,
        int? approvedSubjects)
    {
        if (educationStatusCatalogItemId <= 0)
        {
            throw new InvalidOperationException("EducationStatusCatalogItemId must be greater than zero.");
        }

        if (educationStudyTypeCatalogItemId <= 0)
        {
            throw new InvalidOperationException("EducationStudyTypeCatalogItemId must be greater than zero.");
        }

        if (educationCareerCatalogItemId <= 0)
        {
            throw new InvalidOperationException("EducationCareerCatalogItemId must be greater than zero.");
        }

        if (educationShiftCatalogItemId.HasValue && educationShiftCatalogItemId.Value <= 0)
        {
            throw new InvalidOperationException("EducationShiftCatalogItemId must be greater than zero.");
        }

        if (educationModalityCatalogItemId.HasValue && educationModalityCatalogItemId.Value <= 0)
        {
            throw new InvalidOperationException("EducationModalityCatalogItemId must be greater than zero.");
        }

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

        EducationStatusCatalogItemId = educationStatusCatalogItemId;
        DegreeTitle = PersonnelFileNormalization.CleanOptional(degreeTitle);
        EducationStudyTypeCatalogItemId = educationStudyTypeCatalogItemId;
        EducationCareerCatalogItemId = educationCareerCatalogItemId;
        Institution = PersonnelFileNormalization.Clean(institution, nameof(institution));
        CountryCode = PersonnelFileNormalization.Clean(countryCode, nameof(countryCode));
        Specialty = PersonnelFileNormalization.CleanOptional(specialty);
        IsCurrentlyStudying = isCurrentlyStudying;
        StartDate = PersonnelFileNormalization.NormalizeDate(startDate);
        EndDate = PersonnelFileNormalization.NormalizeDate(endDate);
        EducationShiftCatalogItemId = educationShiftCatalogItemId;
        EducationModalityCatalogItemId = educationModalityCatalogItemId;
        TotalSubjects = totalSubjects;
        ApprovedSubjects = approvedSubjects;
    }
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

    internal void Update(
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

        LanguageCode = PersonnelFileNormalization.Clean(languageCode, nameof(languageCode));
        LevelCode = PersonnelFileNormalization.Clean(levelCode, nameof(levelCode));
        Speaks = speaks;
        Writes = writes;
        Reads = reads;
    }
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

    internal void Update(
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

    internal void Update(
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

    public void Update(
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

        PersonName = PersonnelFileNormalization.Clean(personName, nameof(personName));
        Address = PersonnelFileNormalization.CleanOptional(address);
        Phone = PersonnelFileNormalization.Clean(phone, nameof(phone));
        ReferenceTypeCode = PersonnelFileNormalization.Clean(referenceTypeCode, nameof(referenceTypeCode));
        Occupation = PersonnelFileNormalization.CleanOptional(occupation);
        Workplace = PersonnelFileNormalization.CleanOptional(workplace);
        WorkPhone = PersonnelFileNormalization.CleanOptional(workPhone);
        KnownTimeYears = knownTimeYears;
    }
}

public sealed class PersonnelFileDocument : TenantEntity
{
    private PersonnelFileDocument()
    {
    }

    private PersonnelFileDocument(
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

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public long DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    /// <summary>
    /// Legacy string field kept for data migration. Not set in new Create/Update flows.
    /// </summary>
    public string DocumentType { get; private set; } = string.Empty;

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PersonnelFileDocument Create(
        Guid publicId,
        long documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes,
            observations);

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
