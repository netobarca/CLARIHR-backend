using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Banks;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Banks;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileProfileItemCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task AddAddress_WhenRequestIsValid_ShouldPersistAndReturnCreatedItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateAddAddressHandler(repository);

        var result = await handler.Handle(
            new AddPersonnelFileAddressCommand(
                personnelFile.PublicId,
                new AddressInput("Colonia Escalon", null, "SV", "SAN_SALVADOR", "SAN_SALVADOR_CENTRO", "1101", true)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Colonia Escalon", result.Value.AddressLine);
        Assert.Single(personnelFile.Addresses);
        Assert.Equal(2, repository.GetAddressesCalls);
    }

    [Fact]
    public async Task DeleteEmergencyContact_WhenItemExists_ShouldRemoveItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var emergencyContact = PersonnelFileEmergencyContact.Create("Maria", "Madre", "+50370000001", null, null);
        personnelFile.AddEmergencyContact(emergencyContact);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateDeleteEmergencyContactHandler(repository);

        var result = await handler.Handle(
            new DeletePersonnelFileEmergencyContactCommand(
                personnelFile.PublicId,
                emergencyContact.PublicId,
                emergencyContact.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(personnelFile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Empty(personnelFile.EmergencyContacts);
        Assert.Equal(2, repository.GetEmergencyContactsCalls);
    }

    [Fact]
    public async Task AddBankAccount_WhenRequestIsValid_ShouldPersistAndReturnCreatedItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var bankPublicId = Guid.NewGuid();
        var bankCatalogRepository = new TestBankCatalogRepository(bankPublicId, 1, "AGRI");
        var handler = CreateAddBankAccountHandler(repository, bankCatalogRepository);

        var result = await handler.Handle(
            new AddPersonnelFileBankAccountCommand(
                personnelFile.PublicId,
                new BankAccountInput(bankPublicId, "USD", "0001-1234-5678", "SAVINGS", true)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("0001-1234-5678", result.Value.AccountNumber);
        Assert.Single(personnelFile.BankAccounts);
        Assert.Equal(2, repository.GetBankAccountsCalls);
    }

    [Fact]
    public async Task DeleteBankAccount_WhenItemExists_ShouldRemoveItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var bankAccount = PersonnelFileBankAccount.Create(null, "AGRI", "USD", "0001-1234-5678", "SAVINGS", isPrimary: true);
        personnelFile.AddBankAccount(bankAccount);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateDeleteBankAccountHandler(repository);

        var result = await handler.Handle(
            new DeletePersonnelFileBankAccountCommand(
                personnelFile.PublicId,
                bankAccount.PublicId,
                bankAccount.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(personnelFile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Empty(personnelFile.BankAccounts);
        Assert.Equal(2, repository.GetBankAccountsCalls);
    }

    [Fact]
    public async Task AddBankAccount_WhenBankNotFound_ShouldReturnValidationError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var bankCatalogRepository = new TestBankCatalogRepository(null, 0, null);
        var handler = CreateAddBankAccountHandler(repository, bankCatalogRepository);

        var result = await handler.Handle(
            new AddPersonnelFileBankAccountCommand(
                personnelFile.PublicId,
                new BankAccountInput(Guid.NewGuid(), "USD", "0001-1234-5678", "SAVINGS", true)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task UpdateFamilyMember_WhenKinshipCodeIsInvalid_ShouldReturnValidationError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var familyMember = PersonnelFileFamilyMember.Create(
            "Luis",
            "Lopez",
            "HERMANO_A",
            null,
            null,
            PersonnelFamilyMemberSex.Male,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            null,
            null,
            false,
            null);
        personnelFile.AddFamilyMember(familyMember);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateUpdateFamilyMemberHandler(repository);

        var result = await handler.Handle(
            new UpdatePersonnelFileFamilyMemberCommand(
                personnelFile.PublicId,
                familyMember.PublicId,
                new FamilyMemberInput(
                    "Luis",
                    "Lopez",
                    "INVALID_KINSHIP",
                    null,
                    null,
                    PersonnelFamilyMemberSex.Male,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    null,
                    null,
                    false,
                    null),
                familyMember.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.Error.ValidationErrors);
        Assert.Contains("kinshipCode", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task AddAssociation_WhenRequestIsValid_ShouldPersistAndReturnCreatedItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateAddAssociationHandler(repository);

        var result = await handler.Handle(
            new AddPersonnelFileAssociationCommand(
                personnelFile.PublicId,
                new AssociationInput("COLEGIO_PROF", "Colegio de Abogados", "Miembro", new DateTime(2020, 1, 1), null, 50.00m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Colegio de Abogados", result.Value.AssociationName);
        Assert.Equal("Miembro", result.Value.Role);
        Assert.Equal(50.00m, result.Value.Payment);
        Assert.Single(personnelFile.Associations);
        Assert.Equal(2, repository.GetAssociationsCalls);
    }

    [Fact]
    public async Task DeleteAssociation_WhenItemExists_ShouldRemoveItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var association = PersonnelFileAssociation.Create("COLEGIO_PROF", "Colegio de Abogados", "Miembro", new DateTime(2020, 1, 1), null, 50.00m);
        personnelFile.AddAssociation(association);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateDeleteAssociationHandler(repository);

        var result = await handler.Handle(
            new DeletePersonnelFileAssociationCommand(
                personnelFile.PublicId,
                association.PublicId,
                association.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(personnelFile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Empty(personnelFile.Associations);
        Assert.Equal(2, repository.GetAssociationsCalls);
    }

    [Fact]
    public async Task AddLanguage_WhenRequestIsValid_ShouldPersistAndReturnCreatedItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateAddLanguageHandler(repository);

        var result = await handler.Handle(
            new AddPersonnelFileLanguageCommand(
                personnelFile.PublicId,
                new LanguageInput("ENGLISH", "ADVANCED", true, true, true)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ENGLISH", result.Value.LanguageCode);
        Assert.Equal("ADVANCED", result.Value.LevelCode);
        Assert.Single(personnelFile.Languages);
        Assert.Equal(2, repository.GetLanguagesCalls);
    }

    [Fact]
    public async Task DeleteLanguage_WhenItemExists_ShouldRemoveItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var language = PersonnelFileLanguage.Create("ENGLISH", "ADVANCED", true, true, true);
        personnelFile.AddLanguage(language);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateDeleteLanguageHandler(repository);

        var result = await handler.Handle(
            new DeletePersonnelFileLanguageCommand(
                personnelFile.PublicId,
                language.PublicId,
                language.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(personnelFile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Empty(personnelFile.Languages);
        Assert.Equal(2, repository.GetLanguagesCalls);
    }

    [Fact]
    public async Task AddReference_WhenRequestIsValid_ShouldPersistAndReturnCreatedItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateAddReferenceHandler(repository);

        var result = await handler.Handle(
            new AddPersonnelFileReferenceCommand(
                personnelFile.PublicId,
                new ReferenceInput("Juan Perez", null, "+50370001234", "PROFESSIONAL", "Gerente", "Empresa SA", null, 3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Juan Perez", result.Value.PersonName);
        Assert.Equal("PROFESSIONAL", result.Value.ReferenceTypeCode);
        Assert.Single(personnelFile.References);
        Assert.Equal(2, repository.GetReferencesCalls);
    }

    [Fact]
    public async Task AddReference_WhenReferenceTypeCodeIsInvalid_ShouldReturnValidationError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository([personnelFile], rejectReferenceTypes: true);
        var handler = CreateAddReferenceHandler(repository);

        var result = await handler.Handle(
            new AddPersonnelFileReferenceCommand(
                personnelFile.PublicId,
                new ReferenceInput("Juan Perez", null, "+50370001234", "INVALID_TYPE", null, null, null, 3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task UpdateReference_WhenRequestIsValid_ShouldReturnUpdatedItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var reference = PersonnelFileReference.Create("Juan Perez", null, "+50370001234", "PROFESSIONAL", null, null, null, 3);
        personnelFile.AddReference(reference);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateUpdateReferenceHandler(repository);

        var result = await handler.Handle(
            new UpdatePersonnelFileReferenceCommand(
                personnelFile.PublicId,
                reference.PublicId,
                new ReferenceInput("Juan Carlos Perez", "Col. Escalon", "+50370001234", "PROFESSIONAL", "Director", "Corp SA", null, 5),
                reference.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Juan Carlos Perez", result.Value.PersonName);
        Assert.Equal(5m, result.Value.KnownTimeYears);
        Assert.Equal(2, repository.GetReferencesCalls);
    }

    [Fact]
    public async Task UpdateReference_WhenItemNotFound_ShouldReturnNotFoundError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateUpdateReferenceHandler(repository);

        var result = await handler.Handle(
            new UpdatePersonnelFileReferenceCommand(
                personnelFile.PublicId,
                Guid.NewGuid(),
                new ReferenceInput("Juan Perez", null, "+50370001234", "PROFESSIONAL", null, null, null, 3),
                personnelFile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task DeleteReference_WhenItemExists_ShouldRemoveItem()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var reference = PersonnelFileReference.Create("Juan Perez", null, "+50370001234", "PROFESSIONAL", null, null, null, 3);
        personnelFile.AddReference(reference);
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateDeleteReferenceHandler(repository);

        var result = await handler.Handle(
            new DeletePersonnelFileReferenceCommand(
                personnelFile.PublicId,
                reference.PublicId,
                reference.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(personnelFile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Empty(personnelFile.References);
        Assert.Equal(2, repository.GetReferencesCalls);
    }

    [Fact]
    public async Task DeleteReference_WhenItemNotFound_ShouldReturnNotFoundError()
    {
        var personnelFile = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Lopez");
        var repository = new TestPersonnelFileRepository(personnelFile);
        var handler = CreateDeleteReferenceHandler(repository);

        var result = await handler.Handle(
            new DeletePersonnelFileReferenceCommand(
                personnelFile.PublicId,
                Guid.NewGuid(),
                personnelFile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }


    private static AddPersonnelFileReferenceCommandHandler CreateAddReferenceHandler(TestPersonnelFileRepository repository)
    {
        return new AddPersonnelFileReferenceCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static UpdatePersonnelFileReferenceCommandHandler CreateUpdateReferenceHandler(TestPersonnelFileRepository repository)
    {
        return new UpdatePersonnelFileReferenceCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static DeletePersonnelFileReferenceCommandHandler CreateDeleteReferenceHandler(TestPersonnelFileRepository repository)
    {
        return new DeletePersonnelFileReferenceCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static AddPersonnelFileAddressCommandHandler CreateAddAddressHandler(TestPersonnelFileRepository repository)
    {
        return new AddPersonnelFileAddressCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static DeletePersonnelFileEmergencyContactCommandHandler CreateDeleteEmergencyContactHandler(TestPersonnelFileRepository repository)
    {
        return new DeletePersonnelFileEmergencyContactCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static AddPersonnelFileBankAccountCommandHandler CreateAddBankAccountHandler(
        TestPersonnelFileRepository repository,
        TestBankCatalogRepository bankCatalogRepository)
    {
        return new AddPersonnelFileBankAccountCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            bankCatalogRepository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static DeletePersonnelFileBankAccountCommandHandler CreateDeleteBankAccountHandler(TestPersonnelFileRepository repository)
    {
        return new DeletePersonnelFileBankAccountCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static UpdatePersonnelFileFamilyMemberCommandHandler CreateUpdateFamilyMemberHandler(TestPersonnelFileRepository repository)
    {
        return new UpdatePersonnelFileFamilyMemberCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static AddPersonnelFileAssociationCommandHandler CreateAddAssociationHandler(TestPersonnelFileRepository repository)
    {
        return new AddPersonnelFileAssociationCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static DeletePersonnelFileAssociationCommandHandler CreateDeleteAssociationHandler(TestPersonnelFileRepository repository)
    {
        return new DeletePersonnelFileAssociationCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static AddPersonnelFileLanguageCommandHandler CreateAddLanguageHandler(TestPersonnelFileRepository repository)
    {
        return new AddPersonnelFileLanguageCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static DeletePersonnelFileLanguageCommandHandler CreateDeleteLanguageHandler(TestPersonnelFileRepository repository)
    {
        return new DeletePersonnelFileLanguageCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new TestUnitOfWork());
    }

    private static PersonnelFile CreatePersonnelFile(PersonnelFileRecordType recordType, string firstName, string lastName)
    {
        var file = PersonnelFile.Create(
            recordType,
            firstName,
            lastName,
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(TenantId);
        SetEntityId(file, Random.Shared.NextInt64(1, long.MaxValue));
        return file;
    }

    private static void SetEntityId(PersonnelFile file, long value)
    {
        var property = typeof(PersonnelFile).BaseType?.BaseType?.GetProperty("Id");
        property?.SetValue(file, value);
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AllowPersonnelFileAuthorizationService : IPersonnelFileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<bool> HasRehireAuthorizationAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TENANT_MISMATCH", "Tenant mismatch.", ErrorType.Forbidden);
    }

    private sealed class TestPersonnelFileRepository(params PersonnelFile[] files) : IPersonnelFileRepository
    {
        public TestPersonnelFileRepository(PersonnelFile[] files, bool rejectReferenceTypes) : this(files)
        {
            _rejectCatalogCodes = rejectReferenceTypes;
        }

        private readonly bool _rejectCatalogCodes;
        private readonly Dictionary<Guid, PersonnelFile> _files = files.ToDictionary(file => file.PublicId);

        public int GetAddressesCalls { get; private set; }
        public int GetEmergencyContactsCalls { get; private set; }
        public int GetBankAccountsCalls { get; private set; }
        public int GetAssociationsCalls { get; private set; }
        public int GetLanguagesCalls { get; private set; }
        public int GetReferencesCalls { get; private set; }

        public void Add(PersonnelFile personnelFile) => throw new NotSupportedException();

        public Task<int> CountActiveEmployeesAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            Task.FromResult(_files.GetValueOrDefault(personnelFileId));

        public Task<PersonnelFile?> GetForAccessCheckAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
            GetByIdAsync(personnelFileId, cancellationToken);

        public Task<PersonnelFile?> GetForProfileSectionUpdateAsync(Guid personnelFileId, PersonnelFileTrackedSection section, CancellationToken cancellationToken) =>
            GetByIdAsync(personnelFileId, cancellationToken);

        public Task<PersonnelFile?> GetByLinkedUserIdAsync(Guid tenantId, Guid linkedUserPublicId, CancellationToken cancellationToken) =>
            Task.FromResult<PersonnelFile?>(null);

        public Task<bool> ExistsOutsideTenantAsync(Guid personnelFileId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> IdentificationExistsAsync(Guid tenantId, string identificationType, string normalizedIdentificationNumber, long? excludingPersonnelFileId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<PagedResponse<PersonnelFileListItemResponse>> SearchAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileShellResponse?> GetShellByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileIdentificationResponse?> GetIdentificationAsync(Guid personnelFileId, Guid identificationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileAddressResponse?> GetAddressAsync(Guid personnelFileId, Guid addressPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileEmergencyContactResponse?> GetEmergencyContactAsync(Guid personnelFileId, Guid emergencyContactPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileFamilyMemberResponse?> GetFamilyMemberAsync(Guid personnelFileId, Guid familyMemberPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetAddressesCalls++;
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileAddressResponse>>(Array.Empty<PersonnelFileAddressResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileAddressResponse>>(
                file.Addresses.Select(item => new PersonnelFileAddressResponse(
                    item.PublicId,
                    item.AddressLine,
                    item.AddressTypeCode,
                    item.Country,
                    item.Department,
                    item.Municipality,
                    item.PostalCode,
                    item.IsCurrent,
                    item.ConcurrencyToken)).ToArray());
        }

        public Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetEmergencyContactsCalls++;
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>(Array.Empty<PersonnelFileEmergencyContactResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>(
                file.EmergencyContacts.Select(item => new PersonnelFileEmergencyContactResponse(
                    item.PublicId,
                    item.Name,
                    item.Relationship,
                    item.Phone,
                    item.Address,
                    item.Workplace,
                    item.ConcurrencyToken)).ToArray());
        }

        public Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(Array.Empty<PersonnelFileFamilyMemberResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(
                file.FamilyMembers.Select(item => new PersonnelFileFamilyMemberResponse(
                    item.PublicId,
                    item.FirstName,
                    item.LastName,
                    item.FullName,
                    item.KinshipCode,
                    item.Nationality,
                    item.BirthDate,
                    item.Sex,
                    item.MaritalStatus,
                    item.Occupation,
                    item.DocumentType,
                    item.DocumentNumber,
                    item.Phone,
                    item.IsStudying,
                    item.StudyPlace,
                    item.AcademicLevel,
                    item.IsBeneficiary,
                    item.IsWorking,
                    item.Workplace,
                    item.JobTitle,
                    item.WorkPhone,
                    item.Salary,
                    item.IsDeceased,
                    item.DeceasedDate,
                    item.ConcurrencyToken)).ToArray());
        }

        public Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileHobbyResponse?> GetHobbyAsync(Guid personnelFileId, Guid hobbyPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileEmployeeRelationResponse?> GetEmployeeRelationAsync(Guid personnelFileId, Guid employeeRelationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileBankAccountResponse>> GetBankAccountsAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetBankAccountsCalls++;
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileBankAccountResponse>>(Array.Empty<PersonnelFileBankAccountResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileBankAccountResponse>>(
                file.BankAccounts.Select(item => new PersonnelFileBankAccountResponse(
                    item.PublicId,
                    null,
                    item.BankCode,
                    null,
                    null,
                    null,
                    null,
                    item.CurrencyCode,
                    item.AccountNumber,
                    item.AccountTypeCode,
                    item.IsPrimary,
                    item.ConcurrencyToken)).ToArray());
        }
        public Task<PersonnelFileBankAccountResponse?> GetBankAccountAsync(Guid personnelFileId, Guid bankAccountPublicId, CancellationToken cancellationToken)
        {
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<PersonnelFileBankAccountResponse?>(null);
            }

            var item = file.BankAccounts.FirstOrDefault(bankAccount => bankAccount.PublicId == bankAccountPublicId);
            return Task.FromResult(item is null
                ? null
                : new PersonnelFileBankAccountResponse(
                    item.PublicId,
                    null,
                    item.BankCode,
                    null,
                    null,
                    null,
                    null,
                    item.CurrencyCode,
                    item.AccountNumber,
                    item.AccountTypeCode,
                    item.IsPrimary,
                    item.ConcurrencyToken));
        }
        public Task<IReadOnlyCollection<PersonnelFileAssociationResponse>> GetAssociationsAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetAssociationsCalls++;
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileAssociationResponse>>(Array.Empty<PersonnelFileAssociationResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileAssociationResponse>>(
                file.Associations.Select(item => new PersonnelFileAssociationResponse(
                    item.PublicId,
                    item.AssociationCode,
                    item.AssociationName,
                    item.Role,
                    item.JoinedDate,
                    item.LeftDate,
                    item.Payment,
                    item.ConcurrencyToken)).ToArray());
        }

        public Task<PersonnelFileAssociationResponse?> GetAssociationAsync(Guid personnelFileId, Guid associationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileEducationResponse>> GetEducationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileEducationResponse?> GetEducationAsync(Guid personnelFileId, Guid educationPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonnelFileLanguageResponse?> GetLanguageAsync(Guid personnelFileId, Guid languagePublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileLanguageResponse>> GetLanguagesAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetLanguagesCalls++;
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileLanguageResponse>>(Array.Empty<PersonnelFileLanguageResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileLanguageResponse>>(
                file.Languages.Select(item => new PersonnelFileLanguageResponse(
                    item.PublicId,
                    item.LanguageCode,
                    item.LevelCode,
                    item.Speaks,
                    item.Writes,
                    item.Reads,
                    item.ConcurrencyToken)).ToArray());
        }
        public Task<IReadOnlyCollection<PersonnelFileTrainingResponse>> GetTrainingsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileTrainingResponse?> GetTrainingAsync(Guid personnelFileId, Guid trainingPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFilePreviousEmploymentResponse?> GetPreviousEmploymentAsync(Guid personnelFileId, Guid previousEmploymentPublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileReferenceResponse?> GetReferenceAsync(Guid personnelFileId, Guid referencePublicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileReferenceResponse>> GetReferencesAsync(Guid personnelFileId, CancellationToken cancellationToken)
        {
            GetReferencesCalls++;
            if (!_files.TryGetValue(personnelFileId, out var file))
            {
                return Task.FromResult<IReadOnlyCollection<PersonnelFileReferenceResponse>>(Array.Empty<PersonnelFileReferenceResponse>());
            }

            return Task.FromResult<IReadOnlyCollection<PersonnelFileReferenceResponse>>(
                file.References.Select(item => new PersonnelFileReferenceResponse(
                    item.PublicId,
                    item.PersonName,
                    item.Address,
                    item.Phone,
                    item.ReferenceTypeCode,
                    item.Occupation,
                    item.Workplace,
                    item.WorkPhone,
                    item.KnownTimeYears,
                    item.ConcurrencyToken)).ToArray());
        }
        public Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> GetDocumentsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDocumentMetadataResponse?> GetDocumentMetadataByIdAsync(Guid personnelFileId, Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileObservationResponse>> GetObservationsAsync(Guid personnelFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(Guid personnelFileId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
        public Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(string? countryCode, string category, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(string countryCode, string category, string? parentCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult<string?>("SV");
        public Task<bool> CatalogCodeIsActiveAsync(Guid companyId, string category, string code, CancellationToken cancellationToken) => Task.FromResult(!_rejectCatalogCodes);
        public Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReferenceCatalogCodeIsActiveAsync(string countryCode, string category, string code, CancellationToken cancellationToken) =>
            Task.FromResult(category != PersonnelReferenceCatalogCategories.Kinship || string.Equals(code, "HERMANO_A", StringComparison.OrdinalIgnoreCase));
        public Task<bool> ReferenceMunicipalityBelongsToDepartmentAsync(string countryCode, string departmentCode, string municipalityCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ReferenceInsuranceRangeBelongsToTypeAsync(string countryCode, string insuranceTypeCode, string insuranceRangeCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? maritalStatus, string? nationality, string? profession, DateTime? createdFromUtc, DateTime? createdToUtc, string? search, string? sortBy, PersonnelFileSortDirection sortDirection, int? maxRows, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(Guid tenantId, IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters, IReadOnlyCollection<string> groupBy, IReadOnlyCollection<PersonnelFileDynamicSortInput> sort, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(Guid tenantId, bool? isActive, PersonnelFileRecordType? recordType, Guid? orgUnitId, int? minAge, int? maxAge, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(Guid tenantId, Guid assignedPositionSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestBankCatalogRepository : IBankCatalogRepository
    {
        private readonly Guid? _matchPublicId;
        private readonly long _internalId;
        private readonly string? _code;

        public TestBankCatalogRepository(Guid? matchPublicId, long internalId, string? code)
        {
            _matchPublicId = matchPublicId;
            _internalId = internalId;
            _code = code;
        }

        public void Add(BankCatalogItem item) => throw new NotSupportedException();

        public Task<BankCatalogItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExistsByCodeAsync(long countryCatalogItemId, string normalizedCode, long? excludingId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResponse<BankCatalogItemResponse>> SearchAsync(long countryCatalogItemId, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BankCatalogItemResponse?> GetResponseByIdAsync(Guid publicId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResponse<CompanyBankCatalogItemResponse>> SearchActiveByCompanyAsync(Guid companyId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BankCatalogLookup?> GetActiveLookupByCountryAsync(string countryCode, Guid publicId, CancellationToken cancellationToken)
        {
            if (_matchPublicId.HasValue && publicId == _matchPublicId.Value)
            {
                return Task.FromResult<BankCatalogLookup?>(new BankCatalogLookup(
                    _internalId, publicId, countryCode, _code!, _code!, null, null, null, true));
            }

            return Task.FromResult<BankCatalogLookup?>(null);
        }
    }
}
