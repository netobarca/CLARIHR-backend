using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Banks;
using CLARIHR.Domain.DocumentTypeCatalogs;
using CLARIHR.Domain.EducationCatalogs;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileRepository(ApplicationDbContext dbContext, IMemoryCache memoryCache) : IPersonnelFileRepository
{
    public void Add(PersonnelFile personnelFile) => dbContext.Set<PersonnelFile>().Add(personnelFile);

    public Task<int> CountActiveEmployeesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .CountAsync(
                file => file.TenantId == tenantId &&
                        file.IsActive &&
                        file.RecordType == PersonnelFileRecordType.Employee &&
                        file.LifecycleStatus == PersonnelFileLifecycleStatus.Completed,
                cancellationToken);

    public Task<PersonnelFile?> GetByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFile>()
            .Include(file => file.Identifications)
            .Include(file => file.Addresses)
            .Include(file => file.EmergencyContacts)
            .Include(file => file.FamilyMembers)
            .Include(file => file.Hobbies)
            .Include(file => file.EmployeeRelations)
            .Include(file => file.BankAccounts)
            .Include(file => file.Associations)
            .Include(file => file.Educations)
            .Include(file => file.Languages)
            .Include(file => file.Trainings)
            .Include(file => file.PreviousEmployments)
            .Include(file => file.References)
            .Include(file => file.Documents)
            .Include(file => file.Observations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(file => file.PublicId == personnelFileId, cancellationToken);

    public Task<PersonnelFile?> GetForAccessCheckAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFile>()
            .SingleOrDefaultAsync(file => file.PublicId == personnelFileId, cancellationToken);

    public Task<PersonnelFile?> GetForProfileSectionUpdateAsync(
        Guid personnelFileId,
        PersonnelFileTrackedSection section,
        CancellationToken cancellationToken) =>
        BuildProfileSectionUpdateQuery(section)
            .SingleOrDefaultAsync(file => file.PublicId == personnelFileId, cancellationToken);

    public Task<PersonnelFile?> GetByLinkedUserIdAsync(Guid tenantId, Guid linkedUserPublicId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFile>()
            .SingleOrDefaultAsync(
                file => file.TenantId == tenantId && file.LinkedUserPublicId == linkedUserPublicId,
                cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFile>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(file => file.PublicId == personnelFileId, cancellationToken);

    public Task<bool> IdentificationExistsAsync(
        Guid tenantId,
        string identificationType,
        string normalizedIdentificationNumber,
        long? excludingPersonnelFileId,
        CancellationToken cancellationToken)
    {
        var normalizedType = identificationType.Trim();
        return dbContext.Set<PersonnelFileIdentification>()
            // Intentional tenant filter bypass: applies explicit tenantId filter for uniqueness checks across filtered rows.
            .IgnoreQueryFilters()
            .AnyAsync(
                identification => identification.TenantId == tenantId &&
                                  identification.IdentificationType == normalizedType &&
                                  identification.NormalizedIdentificationNumber == normalizedIdentificationNumber &&
                                  (!excludingPersonnelFileId.HasValue || identification.PersonnelFileId != excludingPersonnelFileId.Value),
                cancellationToken);
    }

    public async Task<PagedResponse<PersonnelFileListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildBaseQuery(tenantId);
        query = ApplyStandardFilters(query, isActive, recordType, orgUnitId, minAge, maxAge, maritalStatus, nationality, profession, createdFromUtc, createdToUtc);
        query = ApplySearch(query, search, includeIdentificationMatch: true);

        var orderedQuery = ApplySorting(query, sortBy, sortDirection);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(file => new
            {
                file.PublicId,
                file.TenantId,
                file.RecordType,
                file.LifecycleStatus,
                file.FullName,
                file.BirthDate,
                file.MaritalStatus,
                file.Profession,
                file.OrgUnitPublicId,
                file.LinkedUserPublicId,
                file.IsActive,
                file.ConcurrencyToken,
                file.CreatedUtc,
                file.ModifiedUtc
            })
            .ToListAsync(cancellationToken);

        var maritalStatusNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.MaritalStatus,
            rows.Select(static row => row.MaritalStatus),
            cancellationToken);
        var professionNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Profession,
            rows.Select(static row => row.Profession),
            cancellationToken);

        var items = rows
            .Select(file => new PersonnelFileListItemResponse(
                file.PublicId,
                file.TenantId,
                file.RecordType,
                file.LifecycleStatus,
                file.FullName,
                PersonnelFileValidationRules.CalculateAge(file.BirthDate, DateTime.UtcNow),
                file.MaritalStatus,
                TryResolveName(maritalStatusNames, file.MaritalStatus),
                file.Profession,
                TryResolveName(professionNames, file.Profession),
                file.OrgUnitPublicId,
                file.LinkedUserPublicId,
                file.IsActive,
                file.CreatedUtc,
                file.ModifiedUtc))
            .ToArray();

        return new PagedResponse<PersonnelFileListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<PersonnelFileShellResponse?> GetShellByIdAsync(Guid personnelFileId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .Where(item => item.PublicId == personnelFileId)
            .Select(item => new PersonnelFileShellResponse(
                item.PublicId,
                item.TenantId,
                item.RecordType,
                item.LifecycleStatus,
                item.FullName,
                item.PhotoFilePublicId.HasValue ? item.PhotoFilePublicId.Value.ToString() : null,
                item.IsActive,
                item.OrgUnitPublicId,
                item.LinkedUserPublicId,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PersonnelFileResponse?> GetResponseByIdAsync(Guid personnelFileId, CancellationToken cancellationToken)
    {
        var file = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .Include(item => item.Identifications)
            .Include(item => item.Addresses)
            .Include(item => item.EmergencyContacts)
            .Include(item => item.FamilyMembers)
            .Include(item => item.Hobbies)
            .Include(item => item.EmployeeRelations)
                .ThenInclude(item => item.RelatedPersonnelFile)
            .Include(item => item.BankAccounts)
            .Include(item => item.Associations)
            .Include(item => item.Educations).ThenInclude(item => item.EducationStatusCatalogItem)
            .Include(item => item.Educations).ThenInclude(item => item.EducationStudyTypeCatalogItem)
            .Include(item => item.Educations).ThenInclude(item => item.EducationCareerCatalogItem)
            .Include(item => item.Educations).ThenInclude(item => item.EducationShiftCatalogItem)
            .Include(item => item.Educations).ThenInclude(item => item.EducationModalityCatalogItem)
            .Include(item => item.Languages)
            .Include(item => item.Trainings)
            .Include(item => item.PreviousEmployments)
            .Include(item => item.References)
            .Include(item => item.Documents)
            .Include(item => item.Observations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.PublicId == personnelFileId, cancellationToken);

        if (file is null)
        {
            return null;
        }

        var maritalStatusNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.MaritalStatus,
            [file.MaritalStatus],
            cancellationToken);
        var professionNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Profession,
            [file.Profession],
            cancellationToken);
        var identificationTypeNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.IdentificationType,
            file.Identifications.Select(static item => item.IdentificationType),
            cancellationToken);
        var birthCountryNames = await ResolveCountryNamesByCodeAsync([file.BirthCountry], cancellationToken);
        var birthDepartmentNames = await ResolveReferenceNamesByCodeAsync(
            file.BirthCountry ?? LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Department,
            [file.BirthDepartment],
            cancellationToken);
        var birthMunicipalityNames = await ResolveReferenceNamesByCodeAsync(
            file.BirthCountry ?? LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Municipality,
            [file.BirthMunicipality],
            cancellationToken);

        return new PersonnelFileResponse(
            file.PublicId,
            file.TenantId,
            file.RecordType,
            file.LifecycleStatus,
            file.FirstName,
            file.LastName,
            file.FullName,
            file.BirthDate,
            PersonnelFileValidationRules.CalculateAge(file.BirthDate, DateTime.UtcNow),
            file.MaritalStatus,
            TryResolveName(maritalStatusNames, file.MaritalStatus),
            file.Profession,
            TryResolveName(professionNames, file.Profession),
            file.Nationality,
            file.PersonalEmail,
            file.InstitutionalEmail,
            file.PersonalPhone,
            file.InstitutionalPhone,
            file.BirthCountry,
            TryResolveName(birthCountryNames, file.BirthCountry),
            file.BirthDepartment,
            TryResolveName(birthDepartmentNames, file.BirthDepartment),
            file.BirthMunicipality,
            TryResolveName(birthMunicipalityNames, file.BirthMunicipality),
            file.PhotoFilePublicId?.ToString(),
            file.OrgUnitPublicId,
            file.LinkedUserPublicId,
            file.IsActive,
            file.ConcurrencyToken,
            file.CreatedUtc,
            file.ModifiedUtc,
            file.Identifications
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.IdentificationType)
                .ThenBy(item => item.IdentificationNumber)
                .Select(item => new PersonnelFileIdentificationResponse(
                    item.PublicId,
                    item.IdentificationType,
                    TryResolveName(identificationTypeNames, item.IdentificationType),
                    item.IdentificationNumber,
                    item.IssuedDate,
                    item.ExpiryDate,
                    item.Issuer,
                    item.IsPrimary,
                    item.ConcurrencyToken))
                .ToArray(),
            file.Addresses
                .OrderByDescending(item => item.IsCurrent)
                .ThenBy(item => item.AddressLine)
                .Select(item => new PersonnelFileAddressResponse(
                    item.PublicId,
                    item.AddressLine,
                    item.Country,
                    item.Department,
                    item.Municipality,
                    item.PostalCode,
                    item.IsCurrent,
                    item.ConcurrencyToken))
                .ToArray(),
            file.EmergencyContacts
                .OrderBy(item => item.Name)
                .Select(item => new PersonnelFileEmergencyContactResponse(
                    item.PublicId,
                    item.Name,
                    item.Relationship,
                    item.Phone,
                    item.Address,
                    item.Workplace,
                    item.ConcurrencyToken))
                .ToArray(),
            file.FamilyMembers
                .OrderBy(item => item.FullName)
                .Select(item => new PersonnelFileFamilyMemberResponse(
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
                    item.ConcurrencyToken))
                .ToArray(),
            file.Hobbies
                .OrderBy(item => item.HobbyName)
                .Select(item => new PersonnelFileHobbyResponse(item.PublicId, item.HobbyName, item.ConcurrencyToken))
                .ToArray(),
            file.EmployeeRelations
                .OrderBy(item => item.RelatedPersonnelFile.FullName)
                .ThenBy(item => item.PublicId)
                .Select(item => new PersonnelFileEmployeeRelationResponse(
                    item.PublicId,
                    item.RelatedPersonnelFile.PublicId,
                    item.RelatedPersonnelFile.FullName,
                    item.Relationship,
                    item.ConcurrencyToken))
                .ToArray(),
            file.BankAccounts
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.BankCode)
                .Select(item => new PersonnelFileBankAccountResponse(
                    item.PublicId,
                    item.BankCatalogItem != null ? item.BankCatalogItem.PublicId : null,
                    item.BankCode,
                    item.BankCatalogItem != null ? item.BankCatalogItem.Name : null,
                    item.BankCatalogItem != null ? item.BankCatalogItem.Alias : null,
                    item.BankCatalogItem != null ? item.BankCatalogItem.SwiftCode : null,
                    item.BankCatalogItem != null ? item.BankCatalogItem.RoutingCode : null,
                    item.CurrencyCode,
                    item.AccountNumber,
                    item.AccountTypeCode,
                    item.IsPrimary,
                    item.ConcurrencyToken))
                .ToArray(),
            file.Associations
                .OrderBy(item => item.AssociationName)
                .Select(item => new PersonnelFileAssociationResponse(
                    item.PublicId,
                    item.AssociationName,
                    item.Role,
                    item.JoinedDate,
                    item.LeftDate,
                    item.Payment,
                    item.ConcurrencyToken))
                .ToArray(),
            file.Educations
                .OrderByDescending(item => item.StartDate)
                .ThenBy(item => item.EducationCareerCatalogItem.Name)
                .Select(MapEducationResponse)
                .ToArray(),
            file.Languages
                .OrderBy(item => item.LanguageCode)
                .Select(item => new PersonnelFileLanguageResponse(
                    item.PublicId,
                    item.LanguageCode,
                    item.LevelCode,
                    item.Speaks,
                    item.Writes,
                    item.Reads,
                    item.ConcurrencyToken))
                .ToArray(),
            file.Trainings
                .OrderByDescending(item => item.StartDate)
                .ThenBy(item => item.TrainingName)
                .Select(item => new PersonnelFileTrainingResponse(
                    item.PublicId,
                    item.TrainingName,
                    item.TrainingTypeCode,
                    item.Description,
                    item.Topic,
                    item.Institution,
                    item.Instructors,
                    item.Score,
                    item.StartDate,
                    item.EndDate,
                    item.IsInternal,
                    item.IsLocal,
                    item.CountryCode,
                    item.DurationValue,
                    item.DurationUnitCode,
                    item.CostAmount,
                    item.CostCurrencyCode,
                    item.ConcurrencyToken))
                .ToArray(),
            file.PreviousEmployments
                .OrderByDescending(item => item.EntryDate)
                .ThenBy(item => item.Institution)
                .Select(item => new PersonnelFilePreviousEmploymentResponse(
                    item.PublicId,
                    item.Institution,
                    item.Place,
                    item.LastPosition,
                    item.ManagerName,
                    item.EntryDate,
                    item.RetirementDate,
                    item.CompanyPhone,
                    item.ExitReason,
                    item.FirstSalaryAmount,
                    item.LastSalaryAmount,
                    item.AverageCommissionAmount,
                    item.CurrencyCode,
                    item.ConcurrencyToken))
                .ToArray(),
            file.References
                .OrderBy(item => item.PersonName)
                .Select(item => new PersonnelFileReferenceResponse(
                    item.PublicId,
                    item.PersonName,
                    item.Address,
                    item.Phone,
                    item.ReferenceTypeCode,
                    item.Occupation,
                    item.Workplace,
                    item.WorkPhone,
                    item.KnownTimeYears,
                    item.ConcurrencyToken))
                .ToArray(),
            file.Documents
                .OrderByDescending(item => item.CreatedUtc)
                .Select(item => new PersonnelFileDocumentMetadataResponse(
                    item.PublicId,
                    item.DocumentTypeCatalogItem?.PublicId,
                    item.DocumentTypeCatalogItem?.Code,
                    item.DocumentTypeCatalogItem?.Name,
                    item.DocumentType,
                    item.Observations,
                    item.FilePublicId,
                    item.FileName,
                    item.ContentType,
                    item.SizeBytes,
                    item.IsActive,
                    item.ConcurrencyToken,
                    item.CreatedUtc,
                    item.ModifiedUtc))
                .ToArray(),
            file.Observations
                .OrderByDescending(item => item.CreatedUtc)
                .Select(item => new PersonnelFileObservationResponse(
                    item.PublicId,
                    item.AuthorUserPublicId,
                    item.Note,
                    item.CreatedUtc))
                .ToArray());
    }

    public async Task<PersonnelFilePersonalInfoResponse?> GetPersonalInfoAsync(Guid personnelFileId, CancellationToken cancellationToken)
    {
        var file = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PublicId == personnelFileId, cancellationToken);

        if (file is null)
        {
            return null;
        }

        var maritalStatusNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.MaritalStatus,
            [file.MaritalStatus],
            cancellationToken);
        var professionNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Profession,
            [file.Profession],
            cancellationToken);
        var birthCountryNames = await ResolveCountryNamesByCodeAsync([file.BirthCountry], cancellationToken);
        var birthDepartmentNames = await ResolveReferenceNamesByCodeAsync(
            file.BirthCountry ?? LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Department,
            [file.BirthDepartment],
            cancellationToken);
        var birthMunicipalityNames = await ResolveReferenceNamesByCodeAsync(
            file.BirthCountry ?? LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Municipality,
            [file.BirthMunicipality],
            cancellationToken);

        return new PersonnelFilePersonalInfoResponse(
            file.PublicId,
            file.TenantId,
            file.RecordType,
            file.LifecycleStatus,
            file.FirstName,
            file.LastName,
            file.FullName,
            file.BirthDate,
            PersonnelFileValidationRules.CalculateAge(file.BirthDate, DateTime.UtcNow),
            file.MaritalStatus,
            TryResolveName(maritalStatusNames, file.MaritalStatus),
            file.Profession,
            TryResolveName(professionNames, file.Profession),
            file.Nationality,
            file.PersonalEmail,
            file.InstitutionalEmail,
            file.PersonalPhone,
            file.InstitutionalPhone,
            file.BirthCountry,
            TryResolveName(birthCountryNames, file.BirthCountry),
            file.BirthDepartment,
            TryResolveName(birthDepartmentNames, file.BirthDepartment),
            file.BirthMunicipality,
            TryResolveName(birthMunicipalityNames, file.BirthMunicipality),
            file.PhotoFilePublicId?.ToString(),
            file.OrgUnitPublicId,
            file.LinkedUserPublicId,
            file.IsActive,
            file.ConcurrencyToken,
            file.CreatedUtc,
            file.ModifiedUtc);
    }

    public async Task<IReadOnlyCollection<PersonnelFileIdentificationResponse>> GetIdentificationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<PersonnelFileIdentification>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.IdentificationType)
            .ThenBy(item => item.IdentificationNumber)
            .Select(item => new
            {
                item.PublicId,
                item.IdentificationType,
                item.IdentificationNumber,
                item.IssuedDate,
                item.ExpiryDate,
                item.Issuer,
                item.IsPrimary,
                item.ConcurrencyToken
            })
            .ToListAsync(cancellationToken);

        var identificationTypeNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.IdentificationType,
            rows.Select(static item => item.IdentificationType),
            cancellationToken);

        return rows
            .Select(item => new PersonnelFileIdentificationResponse(
                item.PublicId,
                item.IdentificationType,
                TryResolveName(identificationTypeNames, item.IdentificationType),
                item.IdentificationNumber,
                item.IssuedDate,
                item.ExpiryDate,
                item.Issuer,
                item.IsPrimary,
                item.ConcurrencyToken))
            .ToArray();
    }

    public async Task<PersonnelFileIdentificationResponse?> GetIdentificationAsync(
        Guid personnelFileId,
        Guid identificationPublicId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Set<PersonnelFileIdentification>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == identificationPublicId)
            .Select(item => new
            {
                item.PublicId,
                item.IdentificationType,
                item.IdentificationNumber,
                item.IssuedDate,
                item.ExpiryDate,
                item.Issuer,
                item.IsPrimary,
                item.ConcurrencyToken
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var identificationTypeNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.IdentificationType,
            [row.IdentificationType],
            cancellationToken);

        return new PersonnelFileIdentificationResponse(
            row.PublicId,
            row.IdentificationType,
            TryResolveName(identificationTypeNames, row.IdentificationType),
            row.IdentificationNumber,
            row.IssuedDate,
            row.ExpiryDate,
            row.Issuer,
            row.IsPrimary,
            row.ConcurrencyToken);
    }

    public async Task<IReadOnlyCollection<PersonnelFileAddressResponse>> GetAddressesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAddress>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsCurrent)
            .ThenBy(item => item.AddressLine)
            .Select(item => new PersonnelFileAddressResponse(
                item.PublicId,
                item.AddressLine,
                item.Country,
                item.Department,
                item.Municipality,
                item.PostalCode,
                item.IsCurrent,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileAddressResponse?> GetAddressAsync(
        Guid personnelFileId,
        Guid addressPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAddress>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == addressPublicId)
            .Select(item => new PersonnelFileAddressResponse(
                item.PublicId,
                item.AddressLine,
                item.Country,
                item.Department,
                item.Municipality,
                item.PostalCode,
                item.IsCurrent,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>> GetEmergencyContactsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEmergencyContact>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.Name)
            .Select(item => new PersonnelFileEmergencyContactResponse(
                item.PublicId,
                item.Name,
                item.Relationship,
                item.Phone,
                item.Address,
                item.Workplace,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileEmergencyContactResponse?> GetEmergencyContactAsync(
        Guid personnelFileId,
        Guid emergencyContactPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEmergencyContact>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == emergencyContactPublicId)
            .Select(item => new PersonnelFileEmergencyContactResponse(
                item.PublicId,
                item.Name,
                item.Relationship,
                item.Phone,
                item.Address,
                item.Workplace,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>> GetFamilyMembersAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileFamilyMember>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.FullName)
            .Select(item => new PersonnelFileFamilyMemberResponse(
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
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileFamilyMemberResponse?> GetFamilyMemberAsync(
        Guid personnelFileId,
        Guid familyMemberPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileFamilyMember>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == familyMemberPublicId)
            .Select(item => new PersonnelFileFamilyMemberResponse(
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
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileHobbyResponse>> GetHobbiesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileHobby>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.HobbyName)
            .Select(item => new PersonnelFileHobbyResponse(item.PublicId, item.HobbyName, item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileHobbyResponse?> GetHobbyAsync(
        Guid personnelFileId,
        Guid hobbyPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileHobby>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == hobbyPublicId)
            .Select(item => new PersonnelFileHobbyResponse(item.PublicId, item.HobbyName, item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEmployeeRelation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.RelatedPersonnelFile.FullName)
            .ThenBy(item => item.PublicId)
            .Select(item => new PersonnelFileEmployeeRelationResponse(
                item.PublicId,
                item.RelatedPersonnelFile.PublicId,
                item.RelatedPersonnelFile.FullName,
                item.Relationship,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileEmployeeRelationResponse?> GetEmployeeRelationAsync(
        Guid personnelFileId,
        Guid employeeRelationPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEmployeeRelation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == employeeRelationPublicId)
            .Select(item => new PersonnelFileEmployeeRelationResponse(
                item.PublicId,
                item.RelatedPersonnelFile.PublicId,
                item.RelatedPersonnelFile.FullName,
                item.Relationship,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileBankAccountResponse>> GetBankAccountsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileBankAccount>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.BankCode)
            .Select(item => new PersonnelFileBankAccountResponse(
                item.PublicId,
                item.BankCatalogItem != null ? item.BankCatalogItem.PublicId : null,
                item.BankCode,
                item.BankCatalogItem != null ? item.BankCatalogItem.Name : null,
                item.BankCatalogItem != null ? item.BankCatalogItem.Alias : null,
                item.BankCatalogItem != null ? item.BankCatalogItem.SwiftCode : null,
                item.BankCatalogItem != null ? item.BankCatalogItem.RoutingCode : null,
                item.CurrencyCode,
                item.AccountNumber,
                item.AccountTypeCode,
                item.IsPrimary,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileBankAccountResponse?> GetBankAccountAsync(
        Guid personnelFileId,
        Guid bankAccountPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileBankAccount>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == bankAccountPublicId)
            .Select(item => new PersonnelFileBankAccountResponse(
                item.PublicId,
                item.BankCatalogItem != null ? item.BankCatalogItem.PublicId : null,
                item.BankCode,
                item.BankCatalogItem != null ? item.BankCatalogItem.Name : null,
                item.BankCatalogItem != null ? item.BankCatalogItem.Alias : null,
                item.BankCatalogItem != null ? item.BankCatalogItem.SwiftCode : null,
                item.BankCatalogItem != null ? item.BankCatalogItem.RoutingCode : null,
                item.CurrencyCode,
                item.AccountNumber,
                item.AccountTypeCode,
                item.IsPrimary,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetBankAccountIdsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileBankAccount>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .Select(item => item.PublicId)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileAssociationResponse>> GetAssociationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAssociation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.AssociationName)
            .Select(item => new PersonnelFileAssociationResponse(
                item.PublicId,
                item.AssociationName,
                item.Role,
                item.JoinedDate,
                item.LeftDate,
                item.Payment,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileAssociationResponse?> GetAssociationAsync(
        Guid personnelFileId,
        Guid associationPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAssociation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == associationPublicId)
            .Select(item => new PersonnelFileAssociationResponse(
                item.PublicId,
                item.AssociationName,
                item.Role,
                item.JoinedDate,
                item.LeftDate,
                item.Payment,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileEducationResponse>> GetEducationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEducation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.StartDate)
            .ThenBy(item => item.EducationCareerCatalogItem.Name)
            .Select(item => new PersonnelFileEducationResponse(
                item.PublicId,
                new PersonnelEducationCatalogReferenceResponse(
                    item.EducationStatusCatalogItem.PublicId,
                    item.EducationStatusCatalogItem.Code,
                    item.EducationStatusCatalogItem.Name,
                    item.EducationStatusCatalogItem.IsActive),
                item.DegreeTitle,
                new PersonnelEducationCatalogReferenceResponse(
                    item.EducationStudyTypeCatalogItem.PublicId,
                    item.EducationStudyTypeCatalogItem.Code,
                    item.EducationStudyTypeCatalogItem.Name,
                    item.EducationStudyTypeCatalogItem.IsActive),
                new PersonnelEducationCatalogReferenceResponse(
                    item.EducationCareerCatalogItem.PublicId,
                    item.EducationCareerCatalogItem.Code,
                    item.EducationCareerCatalogItem.Name,
                    item.EducationCareerCatalogItem.IsActive),
                item.Institution,
                item.CountryCode,
                item.Specialty,
                item.IsCurrentlyStudying,
                item.StartDate,
                item.EndDate,
                item.EducationShiftCatalogItemId.HasValue
                    ? new PersonnelEducationCatalogReferenceResponse(
                        item.EducationShiftCatalogItem!.PublicId,
                        item.EducationShiftCatalogItem.Code,
                        item.EducationShiftCatalogItem.Name,
                        item.EducationShiftCatalogItem.IsActive)
                    : null,
                item.EducationModalityCatalogItemId.HasValue
                    ? new PersonnelEducationCatalogReferenceResponse(
                        item.EducationModalityCatalogItem!.PublicId,
                        item.EducationModalityCatalogItem.Code,
                        item.EducationModalityCatalogItem.Name,
                        item.EducationModalityCatalogItem.IsActive)
                    : null,
                item.TotalSubjects,
                item.ApprovedSubjects,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileEducationResponse?> GetEducationAsync(
        Guid personnelFileId,
        Guid educationPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEducation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == educationPublicId)
            .Select(item => new PersonnelFileEducationResponse(
                item.PublicId,
                new PersonnelEducationCatalogReferenceResponse(
                    item.EducationStatusCatalogItem.PublicId,
                    item.EducationStatusCatalogItem.Code,
                    item.EducationStatusCatalogItem.Name,
                    item.EducationStatusCatalogItem.IsActive),
                item.DegreeTitle,
                new PersonnelEducationCatalogReferenceResponse(
                    item.EducationStudyTypeCatalogItem.PublicId,
                    item.EducationStudyTypeCatalogItem.Code,
                    item.EducationStudyTypeCatalogItem.Name,
                    item.EducationStudyTypeCatalogItem.IsActive),
                new PersonnelEducationCatalogReferenceResponse(
                    item.EducationCareerCatalogItem.PublicId,
                    item.EducationCareerCatalogItem.Code,
                    item.EducationCareerCatalogItem.Name,
                    item.EducationCareerCatalogItem.IsActive),
                item.Institution,
                item.CountryCode,
                item.Specialty,
                item.IsCurrentlyStudying,
                item.StartDate,
                item.EndDate,
                item.EducationShiftCatalogItemId.HasValue
                    ? new PersonnelEducationCatalogReferenceResponse(
                        item.EducationShiftCatalogItem!.PublicId,
                        item.EducationShiftCatalogItem.Code,
                        item.EducationShiftCatalogItem.Name,
                        item.EducationShiftCatalogItem.IsActive)
                    : null,
                item.EducationModalityCatalogItemId.HasValue
                    ? new PersonnelEducationCatalogReferenceResponse(
                        item.EducationModalityCatalogItem!.PublicId,
                        item.EducationModalityCatalogItem.Code,
                        item.EducationModalityCatalogItem.Name,
                        item.EducationModalityCatalogItem.IsActive)
                    : null,
                item.TotalSubjects,
                item.ApprovedSubjects,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileLanguageResponse>> GetLanguagesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileLanguage>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.LanguageCode)
            .Select(item => new PersonnelFileLanguageResponse(
                item.PublicId,
                item.LanguageCode,
                item.LevelCode,
                item.Speaks,
                item.Writes,
                item.Reads,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileLanguageResponse?> GetLanguageAsync(
        Guid personnelFileId,
        Guid languagePublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileLanguage>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == languagePublicId)
            .Select(item => new PersonnelFileLanguageResponse(
                item.PublicId,
                item.LanguageCode,
                item.LevelCode,
                item.Speaks,
                item.Writes,
                item.Reads,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileTrainingResponse>> GetTrainingsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileTraining>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.StartDate)
            .ThenBy(item => item.TrainingName)
            .Select(item => new PersonnelFileTrainingResponse(
                item.PublicId,
                item.TrainingName,
                item.TrainingTypeCode,
                item.Description,
                item.Topic,
                item.Institution,
                item.Instructors,
                item.Score,
                item.StartDate,
                item.EndDate,
                item.IsInternal,
                item.IsLocal,
                item.CountryCode,
                item.DurationValue,
                item.DurationUnitCode,
                item.CostAmount,
                item.CostCurrencyCode,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileTrainingResponse?> GetTrainingAsync(
        Guid personnelFileId,
        Guid trainingPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileTraining>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == trainingPublicId)
            .Select(item => new PersonnelFileTrainingResponse(
                item.PublicId,
                item.TrainingName,
                item.TrainingTypeCode,
                item.Description,
                item.Topic,
                item.Institution,
                item.Instructors,
                item.Score,
                item.StartDate,
                item.EndDate,
                item.IsInternal,
                item.IsLocal,
                item.CountryCode,
                item.DurationValue,
                item.DurationUnitCode,
                item.CostAmount,
                item.CostCurrencyCode,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePreviousEmployment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.EntryDate)
            .ThenBy(item => item.Institution)
            .Select(item => new PersonnelFilePreviousEmploymentResponse(
                item.PublicId,
                item.Institution,
                item.Place,
                item.LastPosition,
                item.ManagerName,
                item.EntryDate,
                item.RetirementDate,
                item.CompanyPhone,
                item.ExitReason,
                item.FirstSalaryAmount,
                item.LastSalaryAmount,
                item.AverageCommissionAmount,
                item.CurrencyCode,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFilePreviousEmploymentResponse?> GetPreviousEmploymentAsync(
        Guid personnelFileId,
        Guid previousEmploymentPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePreviousEmployment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == previousEmploymentPublicId)
            .Select(item => new PersonnelFilePreviousEmploymentResponse(
                item.PublicId,
                item.Institution,
                item.Place,
                item.LastPosition,
                item.ManagerName,
                item.EntryDate,
                item.RetirementDate,
                item.CompanyPhone,
                item.ExitReason,
                item.FirstSalaryAmount,
                item.LastSalaryAmount,
                item.AverageCommissionAmount,
                item.CurrencyCode,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileReferenceResponse>> GetReferencesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileReference>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.PersonName)
            .Select(item => new PersonnelFileReferenceResponse(
                item.PublicId,
                item.PersonName,
                item.Address,
                item.Phone,
                item.ReferenceTypeCode,
                item.Occupation,
                item.Workplace,
                item.WorkPhone,
                item.KnownTimeYears,
                item.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileReferenceResponse?> GetReferenceAsync(
        Guid personnelFileId,
        Guid referencePublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileReference>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == referencePublicId)
            .Select(item => new PersonnelFileReferenceResponse(
                item.PublicId,
                item.PersonName,
                item.Address,
                item.Phone,
                item.ReferenceTypeCode,
                item.Occupation,
                item.Workplace,
                item.WorkPhone,
                item.KnownTimeYears,
                item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> GetDocumentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileDocument>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.CreatedUtc)
            .Select(item => new PersonnelFileDocumentMetadataResponse(
                item.PublicId,
                item.DocumentTypeCatalogItem != null ? item.DocumentTypeCatalogItem.PublicId : null,
                item.DocumentTypeCatalogItem != null ? item.DocumentTypeCatalogItem.Code : null,
                item.DocumentTypeCatalogItem != null ? item.DocumentTypeCatalogItem.Name : null,
                item.DocumentType,
                item.Observations,
                item.FilePublicId,
                item.FileName,
                item.ContentType,
                item.SizeBytes,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToArrayAsync(cancellationToken);

    public Task<PersonnelFileDocumentMetadataResponse?> GetDocumentMetadataByIdAsync(
        Guid personnelFileId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileDocument>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == documentId)
            .Select(item => new PersonnelFileDocumentMetadataResponse(
                item.PublicId,
                item.DocumentTypeCatalogItem != null ? item.DocumentTypeCatalogItem.PublicId : null,
                item.DocumentTypeCatalogItem != null ? item.DocumentTypeCatalogItem.Code : null,
                item.DocumentTypeCatalogItem != null ? item.DocumentTypeCatalogItem.Name : null,
                item.DocumentType,
                item.Observations,
                item.FilePublicId,
                item.FileName,
                item.ContentType,
                item.SizeBytes,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileObservationResponse>> GetObservationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileObservation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.CreatedUtc)
            .Select(item => new PersonnelFileObservationResponse(
                item.PublicId,
                item.AuthorUserPublicId,
                item.Note,
                item.CreatedUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCatalogItemsAsync(
        string? countryCode,
        string category,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = category.Trim().ToUpperInvariant();
        if (normalizedCategory == "COUNTRY")
        {
            return await GetCountryCatalogItemsAsync("Country", cancellationToken);
        }

        // System-scoped (global) catalogs are the same for every tenant/country, so they resolve with
        // no country context — this is what lets the company-less surface serve them during onboarding.
        switch (normalizedCategory)
        {
            case "CURRICULUMEDUCATIONSTATUS":
                return await GetSystemScopedCatalogItemsAsync<EducationStatusCatalogItem>("CurriculumEducationStatus", cancellationToken);
            case "CURRICULUMSTUDYTYPE":
                return await GetSystemScopedCatalogItemsAsync<EducationStudyTypeCatalogItem>("CurriculumStudyType", cancellationToken);
            case "CURRICULUMSHIFT":
                return await GetSystemScopedCatalogItemsAsync<EducationShiftCatalogItem>("CurriculumShift", cancellationToken);
            case "CURRICULUMMODALITY":
                return await GetSystemScopedCatalogItemsAsync<EducationModalityCatalogItem>("CurriculumModality", cancellationToken);
            case "CURRICULUMCAREER":
                return await GetSystemScopedCatalogItemsAsync<EducationCareerCatalogItem>("CurriculumCareer", cancellationToken);
            case "FILEDOCUMENTTYPE":
                return await GetSystemScopedCatalogItemsAsync<DocumentTypeCatalogItem>("FileDocumentType", cancellationToken);
        }

        // Country-scoped catalogs require a country, now supplied explicitly via countryCode (the caller
        // no longer needs a company); an unknown/missing code resolves to no items rather than an error.
        var countryCatalogItemId = await ResolveCountryCatalogItemIdAsync(countryCode, cancellationToken);
        if (countryCatalogItemId is null)
        {
            return [];
        }

        return normalizedCategory switch
        {
            "CURRICULUMLANGUAGE" => await GetCountryScopedCatalogItemsAsync<LanguageCatalogItem>(countryCatalogItemId.Value, "CurriculumLanguage", cancellationToken),
            "CURRICULUMLANGUAGELEVEL" => await GetCountryScopedCatalogItemsAsync<LanguageLevelCatalogItem>(countryCatalogItemId.Value, "CurriculumLanguageLevel", cancellationToken),
            "CURRICULUMTRAININGTYPE" => await GetCountryScopedCatalogItemsAsync<TrainingTypeCatalogItem>(countryCatalogItemId.Value, "CurriculumTrainingType", cancellationToken),
            "CURRICULUMASSIGNMENTTYPE" => await GetCountryScopedCatalogItemsAsync<AssignmentTypeCatalogItem>(countryCatalogItemId.Value, "CurriculumAssignmentType", cancellationToken),
            "CURRICULUMSUBSTITUTIONTYPE" => await GetCountryScopedCatalogItemsAsync<SubstitutionTypeCatalogItem>(countryCatalogItemId.Value, "CurriculumSubstitutionType", cancellationToken),
            "EMPLOYMENTSTATUS" => await GetCountryScopedCatalogItemsAsync<EmploymentStatusCatalogItem>(countryCatalogItemId.Value, "EmploymentStatus", cancellationToken),
            "CURRICULUMDURATIONUNIT" => await GetCountryScopedCatalogItemsAsync<DurationUnitCatalogItem>(countryCatalogItemId.Value, "CurriculumDurationUnit", cancellationToken),
            "CURRICULUMREFERENCETYPE" => await GetCountryScopedCatalogItemsAsync<ReferenceTypeCatalogItem>(countryCatalogItemId.Value, "CurriculumReferenceType", cancellationToken),
            "CURRENCY" => await GetCountryScopedCatalogItemsAsync<CurrencyCatalogItem>(countryCatalogItemId.Value, "Currency", cancellationToken),
            "BANK" => await GetCountryScopedCatalogItemsAsync<BankCatalogItem>(countryCatalogItemId.Value, "Bank", cancellationToken),
            "COMPENSATIONCONCEPTTYPE" => await GetCountryScopedCatalogItemsAsync<CLARIHR.Domain.Compensation.CompensationConceptTypeCatalogItem>(countryCatalogItemId.Value, "CompensationConceptType", cancellationToken),
            "PAYPERIOD" => await GetCountryScopedCatalogItemsAsync<PayPeriodCatalogItem>(countryCatalogItemId.Value, "PayPeriod", cancellationToken),
            "CALCULATIONBASE" => await GetCountryScopedCatalogItemsAsync<CalculationBaseCatalogItem>(countryCatalogItemId.Value, "CalculationBase", cancellationToken),
            "PAYMENTMETHOD" => await GetCountryScopedCatalogItemsAsync<PaymentMethodCatalogItem>(countryCatalogItemId.Value, "PaymentMethod", cancellationToken),
            "ASSETACCESSTYPE" => await GetCountryScopedCatalogItemsAsync<AssetAccessTypeCatalogItem>(countryCatalogItemId.Value, "AssetAccessType", cancellationToken),
            "DELIVERYSTATUS" => await GetCountryScopedCatalogItemsAsync<DeliveryStatusCatalogItem>(countryCatalogItemId.Value, "DeliveryStatus", cancellationToken),
            _ => []
        };
    }

    public async Task<IReadOnlyCollection<CompensationConceptTypeResponse>> GetCompensationConceptTypesAsync(
        string? countryCode,
        CompensationNature? nature,
        CancellationToken cancellationToken)
    {
        var countryCatalogItemId = await ResolveCountryCatalogItemIdAsync(countryCode, cancellationToken);
        if (countryCatalogItemId is null)
        {
            return [];
        }

        var query = dbContext.Set<CLARIHR.Domain.Compensation.CompensationConceptTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId.Value);

        if (nature is not null)
        {
            query = query.Where(item => item.Nature == nature.Value);
        }

        return await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NormalizedCode)
            .Select(item => new CompensationConceptTypeResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Nature,
                item.IsStatutory,
                item.DefaultDeductionClass,
                item.DefaultCalculationType,
                item.DefaultCalculationBaseCode,
                item.DefaultEmployeeRate,
                item.DefaultEmployerRate,
                item.ContributionCap,
                item.IsActive,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetReferenceCatalogItemsAsync(
        string countryCode,
        string category,
        string? parentCode,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = category.Trim().ToUpperInvariant();
        var normalizedParentCode = string.IsNullOrWhiteSpace(parentCode)
            ? null
            : parentCode.Trim().ToUpperInvariant();

        var countryCatalogItemId = await ResolveCountryCatalogItemIdAsync(countryCode, cancellationToken);
        if (countryCatalogItemId is null)
        {
            return [];
        }

        return normalizedCategory switch
        {
            "IDENTIFICATIONTYPE" => await GetFlatReferenceCatalogItemsAsync<IdentificationTypeCatalogItem>(countryCatalogItemId.Value, cancellationToken),
            "PROFESSION" => await GetFlatReferenceCatalogItemsAsync<ProfessionCatalogItem>(countryCatalogItemId.Value, cancellationToken),
            "MARITALSTATUS" => await GetFlatReferenceCatalogItemsAsync<MaritalStatusCatalogItem>(countryCatalogItemId.Value, cancellationToken),
            "KINSHIP" => await GetFlatReferenceCatalogItemsAsync<KinshipCatalogItem>(countryCatalogItemId.Value, cancellationToken),
            "DEPARTMENT" => await GetFlatReferenceCatalogItemsAsync<DepartmentCatalogItem>(countryCatalogItemId.Value, cancellationToken),
            "MUNICIPALITY" => await GetMunicipalityCatalogItemsAsync(countryCatalogItemId.Value, normalizedParentCode, cancellationToken),
            _ => []
        };
    }

    public Task<string?> GetCompanyCountryCodeAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == companyId)
            .Select(company => company.CountryCode)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> CatalogCodeIsActiveAsync(
        Guid companyId,
        string category,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = category.Trim().ToUpperInvariant();
        var normalizedCode = code.Trim().ToUpperInvariant();

        if (normalizedCategory == "COUNTRY")
        {
            return await dbContext.CountryCatalogItems
                .AsNoTracking()
                .AnyAsync(item => item.IsActive && item.NormalizedCode == normalizedCode, cancellationToken);
        }

        var companyCountry = await GetCompanyCountryLookupAsync(companyId, cancellationToken);
        if (companyCountry is null)
        {
            return false;
        }

        return normalizedCategory switch
        {
            "CURRICULUMLANGUAGE" => await IsCountryScopedCatalogCodeActiveAsync<LanguageCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRICULUMLANGUAGELEVEL" => await IsCountryScopedCatalogCodeActiveAsync<LanguageLevelCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRICULUMTRAININGTYPE" => await IsCountryScopedCatalogCodeActiveAsync<TrainingTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRICULUMASSIGNMENTTYPE" => await IsCountryScopedCatalogCodeActiveAsync<AssignmentTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRICULUMSUBSTITUTIONTYPE" => await IsCountryScopedCatalogCodeActiveAsync<SubstitutionTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "EMPLOYMENTSTATUS" => await IsCountryScopedCatalogCodeActiveAsync<EmploymentStatusCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRICULUMDURATIONUNIT" => await IsCountryScopedCatalogCodeActiveAsync<DurationUnitCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRICULUMREFERENCETYPE" => await IsCountryScopedCatalogCodeActiveAsync<ReferenceTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CURRENCY" => await IsCountryScopedCatalogCodeActiveAsync<CurrencyCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "COMPENSATIONCONCEPTTYPE" => await IsCountryScopedCatalogCodeActiveAsync<CLARIHR.Domain.Compensation.CompensationConceptTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "PAYPERIOD" => await IsCountryScopedCatalogCodeActiveAsync<PayPeriodCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "CALCULATIONBASE" => await IsCountryScopedCatalogCodeActiveAsync<CalculationBaseCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "PAYMENTMETHOD" => await IsCountryScopedCatalogCodeActiveAsync<PaymentMethodCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "ASSETACCESSTYPE" => await IsCountryScopedCatalogCodeActiveAsync<AssetAccessTypeCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            "DELIVERYSTATUS" => await IsCountryScopedCatalogCodeActiveAsync<DeliveryStatusCatalogItem>(companyCountry.CountryCatalogItemId, normalizedCode, cancellationToken),
            _ => false
        };
    }

    public Task<bool> CountryCodeIsActiveAsync(string countryCode, CancellationToken cancellationToken)
    {
        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();

        return dbContext.CountryCatalogItems
            .AsNoTracking()
            .AnyAsync(
                item => item.IsActive && item.NormalizedCode == normalizedCountryCode,
                cancellationToken);
    }

    public Task<bool> ReferenceCatalogCodeIsActiveAsync(
        string countryCode,
        string category,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        var normalizedCategory = category.Trim().ToUpperInvariant();
        var normalizedCode = code.Trim().ToUpperInvariant();

        return normalizedCategory switch
        {
            "IDENTIFICATIONTYPE" => IsCountryScopedCatalogCodeActiveAsync<IdentificationTypeCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
            "PROFESSION" => IsCountryScopedCatalogCodeActiveAsync<ProfessionCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
            "MARITALSTATUS" => IsCountryScopedCatalogCodeActiveAsync<MaritalStatusCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
            "KINSHIP" => IsCountryScopedCatalogCodeActiveAsync<KinshipCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
            "DEPARTMENT" => IsCountryScopedCatalogCodeActiveAsync<DepartmentCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
            "MUNICIPALITY" => IsCountryScopedCatalogCodeActiveAsync<MunicipalityCatalogItem>(normalizedCountryCode, normalizedCode, cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    public Task<bool> ReferenceMunicipalityBelongsToDepartmentAsync(
        string countryCode,
        string departmentCode,
        string municipalityCode,
        CancellationToken cancellationToken)
    {
        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        var normalizedDepartmentCode = departmentCode.Trim().ToUpperInvariant();
        var normalizedMunicipalityCode = municipalityCode.Trim().ToUpperInvariant();
        var query =
            from municipality in dbContext.MunicipalityCatalogItems.AsNoTracking()
            join department in dbContext.DepartmentCatalogItems.AsNoTracking()
                on municipality.DepartmentCatalogItemId equals department.Id
            where municipality.IsActive &&
                  department.IsActive &&
                  municipality.CountryCode == normalizedCountryCode &&
                  department.CountryCode == normalizedCountryCode &&
                  municipality.NormalizedCode == normalizedMunicipalityCode &&
                  department.NormalizedCode == normalizedDepartmentCode
            select municipality.Id;

        return query.AnyAsync(cancellationToken);
    }

    public Task<PersonnelFileDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileDocument>()
            .Include(item => item.PersonnelFile)
            .SingleOrDefaultAsync(item => item.PublicId == documentId, cancellationToken);

    public Task<bool> DocumentExistsOutsideTenantAsync(Guid documentId, CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileDocument>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == documentId, cancellationToken);

    public async Task<PersonnelFileDynamicQueryResponse> DynamicQueryAsync(
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileDynamicFilterInput> filters,
        IReadOnlyCollection<string> groupBy,
        IReadOnlyCollection<PersonnelFileDynamicSortInput> sort,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildBaseQuery(tenantId);
        query = ApplySearch(query, search, includeIdentificationMatch: true);

        foreach (var filter in filters)
        {
            query = ApplyDynamicFilter(query, filter);
        }

        var requestedGroups = groupBy
            .Select(PersonnelFileDynamicQuerySpec.NormalizeField)
            .Where(PersonnelFileDynamicQuerySpec.IsGroupableField)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(PersonnelFileDynamicQuerySpec.MaxGroupFields)
            .ToArray();

        var groups = new List<PersonnelFileDynamicGroupResponse>(requestedGroups.Length);
        foreach (var groupField in requestedGroups)
        {
            groups.Add(await BuildGroupResponseAsync(query, groupField, cancellationToken));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = ApplySortSequence(query, sort);
        var rows = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(file => new
            {
                file.PublicId,
                file.TenantId,
                file.RecordType,
                file.LifecycleStatus,
                file.FullName,
                file.BirthDate,
                file.MaritalStatus,
                file.Profession,
                file.OrgUnitPublicId,
                file.LinkedUserPublicId,
                file.IsActive,
                file.ConcurrencyToken,
                file.CreatedUtc,
                file.ModifiedUtc
            })
            .ToListAsync(cancellationToken);

        var maritalStatusNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.MaritalStatus,
            rows.Select(static row => row.MaritalStatus),
            cancellationToken);
        var professionNames = await ResolveReferenceNamesByCodeAsync(
            LocationValidationRules.ElSalvadorCountryCode,
            PersonnelReferenceCatalogCategories.Profession,
            rows.Select(static row => row.Profession),
            cancellationToken);

        var items = rows
            .Select(file => new PersonnelFileListItemResponse(
                file.PublicId,
                file.TenantId,
                file.RecordType,
                file.LifecycleStatus,
                file.FullName,
                PersonnelFileValidationRules.CalculateAge(file.BirthDate, DateTime.UtcNow),
                file.MaritalStatus,
                TryResolveName(maritalStatusNames, file.MaritalStatus),
                file.Profession,
                TryResolveName(professionNames, file.Profession),
                file.OrgUnitPublicId,
                file.LinkedUserPublicId,
                file.IsActive,
                file.CreatedUtc,
                file.ModifiedUtc))
            .ToArray();

        return new PersonnelFileDynamicQueryResponse(items, groups, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyCollection<PersonnelFileExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query = BuildBaseQuery(tenantId);
        query = ApplyStandardFilters(query, isActive, recordType, orgUnitId, minAge, maxAge, maritalStatus, nationality, profession, createdFromUtc, createdToUtc);
        query = ApplySearch(query, search, includeIdentificationMatch: false);
        var orderedQuery = ApplySorting(query, sortBy, sortDirection);

        IQueryable<PersonnelFile> limitedQuery = orderedQuery;
        if (maxRows.HasValue)
        {
            limitedQuery = limitedQuery.Take(maxRows.Value);
        }

        var rows = await limitedQuery
            .Select(file => new PersonnelFileExportRow(
                file.PublicId,
                file.RecordType,
                file.LifecycleStatus,
                file.FirstName,
                file.LastName,
                file.FullName,
                file.BirthDate,
                PersonnelFileValidationRules.CalculateAge(file.BirthDate, DateTime.UtcNow),
                file.MaritalStatus,
                file.Profession,
                file.Nationality,
                file.PersonalEmail,
                file.InstitutionalEmail,
                file.PersonalPhone,
                file.InstitutionalPhone,
                file.OrgUnitPublicId,
                file.LinkedUserPublicId,
                file.IsActive,
                file.CreatedUtc,
                file.ModifiedUtc))
            .ToArrayAsync(cancellationToken);

        return rows;
    }

    public async Task<PersonnelFileAnalyticsSummaryResponse> GetAnalyticsSummaryAsync(
        Guid tenantId,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = BuildBaseQuery(tenantId);
        query = ApplyStandardFilters(
            query,
            isActive,
            recordType,
            orgUnitId,
            minAge,
            maxAge,
            maritalStatus: null,
            nationality: null,
            profession: null,
            createdFromUtc: null,
            createdToUtc: null);
        query = ApplySearch(query, search, includeIdentificationMatch: false);

        var today = DateTime.UtcNow.Date;
        var age18BirthDate = today.AddYears(-18);
        var age26BirthDate = today.AddYears(-26);
        var age36BirthDate = today.AddYears(-36);
        var age46BirthDate = today.AddYears(-46);
        var age56BirthDate = today.AddYears(-56);

        // Single-pass rollup: total + active + the six mutually-exclusive age buckets in ONE
        // aggregate query (PostgreSQL COUNT(*) FILTER (WHERE …)) instead of eight sequential
        // full scans. GroupBy(_ => 1) collapses everything into a single group; an empty set
        // yields no row, so coalesce the null result to zeros.
        var rollup = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(file => file.IsActive),
                AgeUnder18 = group.Count(file => file.BirthDate > age18BirthDate),
                Age18To25 = group.Count(file => file.BirthDate <= age18BirthDate && file.BirthDate > age26BirthDate),
                Age26To35 = group.Count(file => file.BirthDate <= age26BirthDate && file.BirthDate > age36BirthDate),
                Age36To45 = group.Count(file => file.BirthDate <= age36BirthDate && file.BirthDate > age46BirthDate),
                Age46To55 = group.Count(file => file.BirthDate <= age46BirthDate && file.BirthDate > age56BirthDate),
                Age56Plus = group.Count(file => file.BirthDate <= age56BirthDate)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalCount = rollup?.Total ?? 0;
        var activeCount = rollup?.Active ?? 0;
        var inactiveCount = totalCount - activeCount;

        var recordTypeCounts = await query
            .GroupBy(file => file.RecordType)
            .Select(group => new
            {
                RecordType = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ToArrayAsync(cancellationToken);
        var byRecordType = recordTypeCounts
            .Select(item => new PersonnelFileAnalyticsBreakdownResponse(
                item.RecordType.ToString(),
                item.RecordType.ToString(),
                item.Count))
            .ToArray();

        var byAgeRange = new[]
        {
            new PersonnelFileAnalyticsBreakdownResponse("<18", "<18", rollup?.AgeUnder18 ?? 0),
            new PersonnelFileAnalyticsBreakdownResponse("18-25", "18-25", rollup?.Age18To25 ?? 0),
            new PersonnelFileAnalyticsBreakdownResponse("26-35", "26-35", rollup?.Age26To35 ?? 0),
            new PersonnelFileAnalyticsBreakdownResponse("36-45", "36-45", rollup?.Age36To45 ?? 0),
            new PersonnelFileAnalyticsBreakdownResponse("46-55", "46-55", rollup?.Age46To55 ?? 0),
            new PersonnelFileAnalyticsBreakdownResponse("56+", "56+", rollup?.Age56Plus ?? 0)
        };

        var orgUnitCounts = await query
            .GroupBy(file => file.OrgUnitPublicId)
            .Select(group => new
            {
                OrgUnitPublicId = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ToArrayAsync(cancellationToken);
        var byOrgUnit = orgUnitCounts
            .Select(item => new PersonnelFileAnalyticsBreakdownResponse(
                item.OrgUnitPublicId.HasValue ? item.OrgUnitPublicId.Value.ToString() : "UNASSIGNED",
                item.OrgUnitPublicId.HasValue ? item.OrgUnitPublicId.Value.ToString() : "Unassigned",
                item.Count))
            .ToArray();

        return new PersonnelFileAnalyticsSummaryResponse(
            totalCount,
            activeCount,
            inactiveCount,
            byRecordType,
            byAgeRange,
            byOrgUnit);
    }

    private IQueryable<PersonnelFile> BuildProfileSectionUpdateQuery(PersonnelFileTrackedSection section)
    {
        IQueryable<PersonnelFile> query = dbContext.Set<PersonnelFile>();

        query = section switch
        {
            PersonnelFileTrackedSection.Identifications => query.Include(file => file.Identifications),
            PersonnelFileTrackedSection.Addresses => query.Include(file => file.Addresses),
            PersonnelFileTrackedSection.EmergencyContacts => query.Include(file => file.EmergencyContacts),
            PersonnelFileTrackedSection.FamilyMembers => query.Include(file => file.FamilyMembers),
            PersonnelFileTrackedSection.Hobbies => query.Include(file => file.Hobbies),
            PersonnelFileTrackedSection.EmployeeRelations => query.Include(file => file.EmployeeRelations),
            PersonnelFileTrackedSection.BankAccounts => query.Include(file => file.BankAccounts),
            PersonnelFileTrackedSection.Associations => query.Include(file => file.Associations),
            PersonnelFileTrackedSection.Educations => query.Include(file => file.Educations),
            PersonnelFileTrackedSection.Languages => query.Include(file => file.Languages),
            PersonnelFileTrackedSection.Trainings => query.Include(file => file.Trainings),
            PersonnelFileTrackedSection.PreviousEmployments => query.Include(file => file.PreviousEmployments),
            PersonnelFileTrackedSection.References => query.Include(file => file.References),
            PersonnelFileTrackedSection.Documents => query.Include(file => file.Documents),
            _ => query
        };

        return query;
    }

    private IQueryable<PersonnelFile> BuildBaseQuery(Guid tenantId) =>
        dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .Where(file => file.TenantId == tenantId);

    private static IQueryable<PersonnelFile> ApplyStandardFilters(
        IQueryable<PersonnelFile> query,
        bool? isActive,
        PersonnelFileRecordType? recordType,
        Guid? orgUnitId,
        int? minAge,
        int? maxAge,
        string? maritalStatus,
        string? nationality,
        string? profession,
        DateTime? createdFromUtc,
        DateTime? createdToUtc)
    {
        if (isActive.HasValue)
        {
            query = query.Where(file => file.IsActive == isActive.Value);
        }

        if (recordType.HasValue)
        {
            query = query.Where(file => file.RecordType == recordType.Value);
        }

        if (orgUnitId.HasValue)
        {
            query = query.Where(file => file.OrgUnitPublicId == orgUnitId.Value);
        }

        if (minAge.HasValue)
        {
            var maxBirthDate = DateTime.UtcNow.Date.AddYears(-minAge.Value);
            query = query.Where(file => file.BirthDate <= maxBirthDate);
        }

        if (maxAge.HasValue)
        {
            var minBirthDate = DateTime.UtcNow.Date.AddYears(-maxAge.Value - 1).AddDays(1);
            query = query.Where(file => file.BirthDate >= minBirthDate);
        }

        if (!string.IsNullOrWhiteSpace(maritalStatus))
        {
            var normalizedMaritalStatus = maritalStatus.Trim().ToUpperInvariant();
            query = query.Where(file => (file.MaritalStatus ?? string.Empty).ToUpper() == normalizedMaritalStatus);
        }

        if (!string.IsNullOrWhiteSpace(nationality))
        {
            var normalizedNationality = nationality.Trim().ToUpperInvariant();
            query = query.Where(file => (file.Nationality ?? string.Empty).ToUpper() == normalizedNationality);
        }

        if (!string.IsNullOrWhiteSpace(profession))
        {
            var normalizedProfession = profession.Trim().ToUpperInvariant();
            query = query.Where(file => (file.Profession ?? string.Empty).ToUpper().Contains(normalizedProfession));
        }

        if (createdFromUtc.HasValue)
        {
            query = query.Where(file => file.CreatedUtc >= createdFromUtc.Value);
        }

        if (createdToUtc.HasValue)
        {
            query = query.Where(file => file.CreatedUtc <= createdToUtc.Value);
        }

        return query;
    }

    private IQueryable<PersonnelFile> ApplySearch(
        IQueryable<PersonnelFile> query,
        string? search,
        bool includeIdentificationMatch)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim().ToUpperInvariant();
        if (!includeIdentificationMatch)
        {
            return query.Where(file => file.NormalizedFullName.Contains(normalizedSearch));
        }

        return query.Where(file =>
            file.NormalizedFullName.Contains(normalizedSearch) ||
            dbContext.Set<PersonnelFileIdentification>().Any(identification =>
                identification.PersonnelFileId == file.Id &&
                identification.NormalizedIdentificationNumber.Contains(normalizedSearch)));
    }

    private static IOrderedQueryable<PersonnelFile> ApplySorting(
        IQueryable<PersonnelFile> query,
        string? sortBy,
        PersonnelFileSortDirection sortDirection)
    {
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy)
            ? "fullname"
            : PersonnelFileDynamicQuerySpec.NormalizeField(sortBy);

        var descending = sortDirection == PersonnelFileSortDirection.Desc;
        return normalizedSortBy switch
        {
            "firstname" => descending
                ? query.OrderByDescending(file => file.FirstName).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.FirstName).ThenBy(file => file.PublicId),
            "lastname" => descending
                ? query.OrderByDescending(file => file.LastName).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.LastName).ThenBy(file => file.PublicId),
            "birthdate" => descending
                ? query.OrderByDescending(file => file.BirthDate).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.BirthDate).ThenBy(file => file.PublicId),
            "age" => descending
                ? query.OrderBy(file => file.BirthDate).ThenBy(file => file.PublicId)
                : query.OrderByDescending(file => file.BirthDate).ThenByDescending(file => file.PublicId),
            "recordtype" => descending
                ? query.OrderByDescending(file => file.RecordType).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.RecordType).ThenBy(file => file.PublicId),
            "maritalstatus" => descending
                ? query.OrderByDescending(file => file.MaritalStatus).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.MaritalStatus).ThenBy(file => file.PublicId),
            "nationality" => descending
                ? query.OrderByDescending(file => file.Nationality).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.Nationality).ThenBy(file => file.PublicId),
            "profession" => descending
                ? query.OrderByDescending(file => file.Profession).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.Profession).ThenBy(file => file.PublicId),
            "orgunitid" => descending
                ? query.OrderByDescending(file => file.OrgUnitPublicId).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.OrgUnitPublicId).ThenBy(file => file.PublicId),
            "isactive" => descending
                ? query.OrderByDescending(file => file.IsActive).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.IsActive).ThenBy(file => file.PublicId),
            "createdatutc" => descending
                ? query.OrderByDescending(file => file.CreatedUtc).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.CreatedUtc).ThenBy(file => file.PublicId),
            "modifiedatutc" => descending
                ? query.OrderByDescending(file => file.ModifiedUtc ?? DateTime.MinValue).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.ModifiedUtc ?? DateTime.MinValue).ThenBy(file => file.PublicId),
            _ => descending
                ? query.OrderByDescending(file => file.FullName).ThenByDescending(file => file.PublicId)
                : query.OrderBy(file => file.FullName).ThenBy(file => file.PublicId)
        };
    }

    private static IOrderedQueryable<PersonnelFile> ApplySortSequence(
        IQueryable<PersonnelFile> query,
        IReadOnlyCollection<PersonnelFileDynamicSortInput> sort)
    {
        IOrderedQueryable<PersonnelFile>? ordered = null;
        foreach (var item in sort)
        {
            if (!PersonnelFileDynamicQuerySpec.IsSortableField(item.Field))
            {
                continue;
            }

            var normalizedField = PersonnelFileDynamicQuerySpec.NormalizeField(item.Field);
            if (ordered is null)
            {
                ordered = ApplySorting(query, normalizedField, item.Direction);
                continue;
            }

            ordered = ApplyThenBy(ordered, normalizedField, item.Direction);
        }

        return ordered ?? ApplySorting(query, sortBy: null, PersonnelFileSortDirection.Asc);
    }

    private static IOrderedQueryable<PersonnelFile> ApplyThenBy(
        IOrderedQueryable<PersonnelFile> query,
        string normalizedField,
        PersonnelFileSortDirection direction)
    {
        var descending = direction == PersonnelFileSortDirection.Desc;
        return normalizedField switch
        {
            "firstname" => descending ? query.ThenByDescending(file => file.FirstName) : query.ThenBy(file => file.FirstName),
            "lastname" => descending ? query.ThenByDescending(file => file.LastName) : query.ThenBy(file => file.LastName),
            "birthdate" => descending ? query.ThenByDescending(file => file.BirthDate) : query.ThenBy(file => file.BirthDate),
            "age" => descending ? query.ThenBy(file => file.BirthDate) : query.ThenByDescending(file => file.BirthDate),
            "recordtype" => descending ? query.ThenByDescending(file => file.RecordType) : query.ThenBy(file => file.RecordType),
            "maritalstatus" => descending ? query.ThenByDescending(file => file.MaritalStatus) : query.ThenBy(file => file.MaritalStatus),
            "nationality" => descending ? query.ThenByDescending(file => file.Nationality) : query.ThenBy(file => file.Nationality),
            "profession" => descending ? query.ThenByDescending(file => file.Profession) : query.ThenBy(file => file.Profession),
            "orgunitid" => descending ? query.ThenByDescending(file => file.OrgUnitPublicId) : query.ThenBy(file => file.OrgUnitPublicId),
            "isactive" => descending ? query.ThenByDescending(file => file.IsActive) : query.ThenBy(file => file.IsActive),
            "createdatutc" => descending ? query.ThenByDescending(file => file.CreatedUtc) : query.ThenBy(file => file.CreatedUtc),
            "modifiedatutc" => descending ? query.ThenByDescending(file => file.ModifiedUtc ?? DateTime.MinValue) : query.ThenBy(file => file.ModifiedUtc ?? DateTime.MinValue),
            _ => descending ? query.ThenByDescending(file => file.FullName) : query.ThenBy(file => file.FullName)
        };
    }

    private static IQueryable<PersonnelFile> ApplyDynamicFilter(
        IQueryable<PersonnelFile> query,
        PersonnelFileDynamicFilterInput filter)
    {
        var field = PersonnelFileDynamicQuerySpec.NormalizeField(filter.Field);
        var @operator = PersonnelFileDynamicQuerySpec.NormalizeOperator(filter.Operator);

        return field switch
        {
            "recordtype" => ApplyRecordTypeFilter(query, @operator, filter),
            "maritalstatus" => ApplyStringFilter(query, @operator, filter, field),
            "nationality" => ApplyStringFilter(query, @operator, filter, field),
            "profession" => ApplyStringFilter(query, @operator, filter, field),
            "firstname" => ApplyStringFilter(query, @operator, filter, field),
            "lastname" => ApplyStringFilter(query, @operator, filter, field),
            "fullname" => ApplyStringFilter(query, @operator, filter, field),
            "orgunitid" => ApplyOrgUnitFilter(query, @operator, filter),
            "isactive" => ApplyIsActiveFilter(query, filter),
            "age" => ApplyAgeFilter(query, @operator, filter),
            "createdatutc" => ApplyCreatedDateFilter(query, @operator, filter),
            "birthdate" => ApplyBirthDateFilter(query, @operator, filter),
            _ => query
        };
    }

    private static IQueryable<PersonnelFile> ApplyRecordTypeFilter(
        IQueryable<PersonnelFile> query,
        string @operator,
        PersonnelFileDynamicFilterInput filter)
    {
        if (@operator == "eq" &&
            Enum.TryParse<PersonnelFileRecordType>(filter.Value, true, out var singleValue))
        {
            return query.Where(file => file.RecordType == singleValue);
        }

        if (@operator == "in")
        {
            var values = (filter.Values ?? [])
                .Select(value => Enum.TryParse<PersonnelFileRecordType>(value, true, out var parsed) ? parsed : (PersonnelFileRecordType?)null)
                .Where(parsed => parsed.HasValue)
                .Select(parsed => parsed!.Value)
                .Distinct()
                .ToArray();

            if (values.Length > 0)
            {
                return query.Where(file => values.Contains(file.RecordType));
            }
        }

        return query;
    }

    private static IQueryable<PersonnelFile> ApplyOrgUnitFilter(
        IQueryable<PersonnelFile> query,
        string @operator,
        PersonnelFileDynamicFilterInput filter)
    {
        if (@operator == "eq" && Guid.TryParse(filter.Value, out var orgUnitId))
        {
            return query.Where(file => file.OrgUnitPublicId == orgUnitId);
        }

        if (@operator == "in")
        {
            var values = (filter.Values ?? [])
                .Select(value => Guid.TryParse(value, out var parsed) ? parsed : (Guid?)null)
                .Where(parsed => parsed.HasValue)
                .Select(parsed => parsed!.Value)
                .Distinct()
                .ToArray();

            if (values.Length > 0)
            {
                return query.Where(file => file.OrgUnitPublicId.HasValue && values.Contains(file.OrgUnitPublicId.Value));
            }
        }

        return query;
    }

    private static IQueryable<PersonnelFile> ApplyIsActiveFilter(
        IQueryable<PersonnelFile> query,
        PersonnelFileDynamicFilterInput filter)
    {
        if (bool.TryParse(filter.Value, out var isActive))
        {
            return query.Where(file => file.IsActive == isActive);
        }

        return query;
    }

    private static IQueryable<PersonnelFile> ApplyAgeFilter(
        IQueryable<PersonnelFile> query,
        string @operator,
        PersonnelFileDynamicFilterInput filter)
    {
        if (@operator == "eq" && int.TryParse(filter.Value, out var equalAge))
        {
            var minBirthDate = DateTime.UtcNow.Date.AddYears(-equalAge - 1).AddDays(1);
            var maxBirthDate = DateTime.UtcNow.Date.AddYears(-equalAge);
            return query.Where(file => file.BirthDate >= minBirthDate && file.BirthDate <= maxBirthDate);
        }

        if (@operator == "gte" && int.TryParse(filter.Value, out var minAge))
        {
            var maxBirthDate = DateTime.UtcNow.Date.AddYears(-minAge);
            return query.Where(file => file.BirthDate <= maxBirthDate);
        }

        if (@operator == "lte" && int.TryParse(filter.Value, out var maxAge))
        {
            var minBirthDate = DateTime.UtcNow.Date.AddYears(-maxAge - 1).AddDays(1);
            return query.Where(file => file.BirthDate >= minBirthDate);
        }

        if (@operator == "between" &&
            int.TryParse(filter.Value, out var fromAge) &&
            int.TryParse(filter.ValueTo, out var toAge))
        {
            var min = Math.Min(fromAge, toAge);
            var max = Math.Max(fromAge, toAge);
            var maxBirthDate = DateTime.UtcNow.Date.AddYears(-min);
            var minBirthDate = DateTime.UtcNow.Date.AddYears(-max - 1).AddDays(1);
            return query.Where(file => file.BirthDate >= minBirthDate && file.BirthDate <= maxBirthDate);
        }

        return query;
    }

    private static IQueryable<PersonnelFile> ApplyCreatedDateFilter(
        IQueryable<PersonnelFile> query,
        string @operator,
        PersonnelFileDynamicFilterInput filter)
    {
        if (@operator == "eq" && DateTime.TryParse(filter.Value, out var equalDate))
        {
            var from = equalDate.Date;
            var to = from.AddDays(1);
            return query.Where(file => file.CreatedUtc >= from && file.CreatedUtc < to);
        }

        if (@operator == "gte" && DateTime.TryParse(filter.Value, out var fromDate))
        {
            return query.Where(file => file.CreatedUtc >= fromDate);
        }

        if (@operator == "lte" && DateTime.TryParse(filter.Value, out var toDate))
        {
            return query.Where(file => file.CreatedUtc <= toDate);
        }

        if (@operator == "between" &&
            DateTime.TryParse(filter.Value, out var rangeStart) &&
            DateTime.TryParse(filter.ValueTo, out var rangeEnd))
        {
            var from = rangeStart <= rangeEnd ? rangeStart : rangeEnd;
            var to = rangeStart <= rangeEnd ? rangeEnd : rangeStart;
            return query.Where(file => file.CreatedUtc >= from && file.CreatedUtc <= to);
        }

        return query;
    }

    private static IQueryable<PersonnelFile> ApplyBirthDateFilter(
        IQueryable<PersonnelFile> query,
        string @operator,
        PersonnelFileDynamicFilterInput filter)
    {
        if (@operator == "eq" && DateTime.TryParse(filter.Value, out var equalDate))
        {
            return query.Where(file => file.BirthDate == equalDate.Date);
        }

        if (@operator == "gte" && DateTime.TryParse(filter.Value, out var fromDate))
        {
            return query.Where(file => file.BirthDate >= fromDate.Date);
        }

        if (@operator == "lte" && DateTime.TryParse(filter.Value, out var toDate))
        {
            return query.Where(file => file.BirthDate <= toDate.Date);
        }

        if (@operator == "between" &&
            DateTime.TryParse(filter.Value, out var rangeStart) &&
            DateTime.TryParse(filter.ValueTo, out var rangeEnd))
        {
            var from = rangeStart <= rangeEnd ? rangeStart.Date : rangeEnd.Date;
            var to = rangeStart <= rangeEnd ? rangeEnd.Date : rangeStart.Date;
            return query.Where(file => file.BirthDate >= from && file.BirthDate <= to);
        }

        return query;
    }

    private static IQueryable<PersonnelFile> ApplyStringFilter(
        IQueryable<PersonnelFile> query,
        string @operator,
        PersonnelFileDynamicFilterInput filter,
        string field)
    {
        if (string.IsNullOrWhiteSpace(filter.Value))
        {
            return query;
        }

        var normalizedValue = filter.Value.Trim().ToUpperInvariant();
        return (field, @operator) switch
        {
            ("maritalstatus", "eq") => query.Where(file => (file.MaritalStatus ?? string.Empty).ToUpper() == normalizedValue),
            ("maritalstatus", "contains") => query.Where(file => (file.MaritalStatus ?? string.Empty).ToUpper().Contains(normalizedValue)),
            ("nationality", "eq") => query.Where(file => (file.Nationality ?? string.Empty).ToUpper() == normalizedValue),
            ("nationality", "contains") => query.Where(file => (file.Nationality ?? string.Empty).ToUpper().Contains(normalizedValue)),
            ("profession", "eq") => query.Where(file => (file.Profession ?? string.Empty).ToUpper() == normalizedValue),
            ("profession", "contains") => query.Where(file => (file.Profession ?? string.Empty).ToUpper().Contains(normalizedValue)),
            ("firstname", "eq") => query.Where(file => (file.FirstName ?? string.Empty).ToUpper() == normalizedValue),
            ("firstname", "contains") => query.Where(file => (file.FirstName ?? string.Empty).ToUpper().Contains(normalizedValue)),
            ("lastname", "eq") => query.Where(file => (file.LastName ?? string.Empty).ToUpper() == normalizedValue),
            ("lastname", "contains") => query.Where(file => (file.LastName ?? string.Empty).ToUpper().Contains(normalizedValue)),
            ("fullname", "eq") => query.Where(file => (file.FullName ?? string.Empty).ToUpper() == normalizedValue),
            ("fullname", "contains") => query.Where(file => (file.FullName ?? string.Empty).ToUpper().Contains(normalizedValue)),
            _ => query
        };
    }

    private static Task<PersonnelFileDynamicGroupResponse> BuildGroupResponseAsync(
        IQueryable<PersonnelFile> query,
        string field,
        CancellationToken cancellationToken)
    {
        return field switch
        {
            "recordtype" => BuildRecordTypeGroupAsync(query, cancellationToken),
            "maritalstatus" => BuildMaritalStatusGroupAsync(query, cancellationToken),
            "nationality" => BuildNationalityGroupAsync(query, cancellationToken),
            "orgunitid" => BuildOrgUnitGroupAsync(query, cancellationToken),
            "isactive" => BuildIsActiveGroupAsync(query, cancellationToken),
            _ => Task.FromResult(new PersonnelFileDynamicGroupResponse(field, Array.Empty<PersonnelFileDynamicGroupBucketResponse>()))
        };
    }

    private static async Task<PersonnelFileDynamicGroupResponse> BuildRecordTypeGroupAsync(
        IQueryable<PersonnelFile> query,
        CancellationToken cancellationToken)
    {
        var buckets = await query
            .GroupBy(file => file.RecordType)
            .Select(group => new PersonnelFileDynamicGroupBucketResponse(
                group.Key.ToString(),
                group.Key.ToString(),
                group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key)
            .Take(PersonnelFileDynamicQuerySpec.MaxGroupBucketsPerField)
            .ToArrayAsync(cancellationToken);

        return new PersonnelFileDynamicGroupResponse("recordtype", buckets);
    }

    private static async Task<PersonnelFileDynamicGroupResponse> BuildOrgUnitGroupAsync(
        IQueryable<PersonnelFile> query,
        CancellationToken cancellationToken)
    {
        var buckets = await query
            .GroupBy(file => file.OrgUnitPublicId)
            .Select(group => new PersonnelFileDynamicGroupBucketResponse(
                group.Key.HasValue ? group.Key.Value.ToString() : "UNASSIGNED",
                group.Key.HasValue ? group.Key.Value.ToString() : "Unassigned",
                group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key)
            .Take(PersonnelFileDynamicQuerySpec.MaxGroupBucketsPerField)
            .ToArrayAsync(cancellationToken);

        return new PersonnelFileDynamicGroupResponse("orgunitid", buckets);
    }

    private static async Task<PersonnelFileDynamicGroupResponse> BuildMaritalStatusGroupAsync(
        IQueryable<PersonnelFile> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(file => file.MaritalStatus ?? "UNSPECIFIED")
            .Select(group => new
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key)
            .Take(PersonnelFileDynamicQuerySpec.MaxGroupBucketsPerField)
            .ToArrayAsync(cancellationToken);

        var buckets = rows
            .Select(row => new PersonnelFileDynamicGroupBucketResponse(
                row.Key,
                row.Key.Equals("UNSPECIFIED", StringComparison.OrdinalIgnoreCase)
                    ? "Unspecified"
                    : row.Key,
                row.Count))
            .ToArray();

        return new PersonnelFileDynamicGroupResponse("maritalstatus", buckets);
    }

    private static async Task<PersonnelFileDynamicGroupResponse> BuildNationalityGroupAsync(
        IQueryable<PersonnelFile> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(file => file.Nationality ?? "UNSPECIFIED")
            .Select(group => new
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key)
            .Take(PersonnelFileDynamicQuerySpec.MaxGroupBucketsPerField)
            .ToArrayAsync(cancellationToken);

        var buckets = rows
            .Select(row => new PersonnelFileDynamicGroupBucketResponse(
                row.Key,
                row.Key.Equals("UNSPECIFIED", StringComparison.OrdinalIgnoreCase)
                    ? "Unspecified"
                    : row.Key,
                row.Count))
            .ToArray();

        return new PersonnelFileDynamicGroupResponse("nationality", buckets);
    }

    private static async Task<PersonnelFileDynamicGroupResponse> BuildIsActiveGroupAsync(
        IQueryable<PersonnelFile> query,
        CancellationToken cancellationToken)
    {
        var buckets = await query
            .GroupBy(file => file.IsActive)
            .Select(group => new PersonnelFileDynamicGroupBucketResponse(
                group.Key ? "true" : "false",
                group.Key ? "Active" : "Inactive",
                group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key)
            .Take(PersonnelFileDynamicQuerySpec.MaxGroupBucketsPerField)
            .ToArrayAsync(cancellationToken);

        return new PersonnelFileDynamicGroupResponse("isactive", buckets);
    }



    public async Task<IReadOnlyCollection<Guid>> GetLinkedUserIdsByAssignedPositionSlotAsync(
        Guid tenantId,
        Guid assignedPositionSlotId,
        CancellationToken cancellationToken) =>
        // An employee's position is the slot of their PRIMARY ACTIVE employment assignment (the
        // multi-plaza source of truth), so re-provisioning by slot resolves through that assignment.
        await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(assignment =>
                assignment.TenantId == tenantId &&
                assignment.IsActive &&
                assignment.IsPrimary &&
                assignment.PositionSlotPublicId == assignedPositionSlotId &&
                assignment.PersonnelFile.RecordType == PersonnelFileRecordType.Employee &&
                assignment.PersonnelFile.LifecycleStatus == PersonnelFileLifecycleStatus.Completed &&
                assignment.PersonnelFile.LinkedUserPublicId.HasValue)
            .Select(assignment => assignment.PersonnelFile.LinkedUserPublicId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    private Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetSystemScopedCatalogItemsAsync<TCatalogItem>(
        string category,
        CancellationToken cancellationToken)
        where TCatalogItem : SystemScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PersonnelCatalogItemResponse(
                item.PublicId,
                category,
                item.Code,
                item.Name,
                true,
                item.IsActive,
                item.SortOrder))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyCollection<PersonnelCatalogItemResponse>)task.Result, cancellationToken);

    private Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCountryScopedCatalogItemsAsync<TCatalogItem>(
        long countryCatalogItemId,
        string category,
        CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PersonnelCatalogItemResponse(
                item.PublicId,
                category,
                item.Code,
                item.Name,
                true,
                item.IsActive,
                item.SortOrder))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyCollection<PersonnelCatalogItemResponse>)task.Result, cancellationToken);

    private Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetFlatReferenceCatalogItemsAsync<TCatalogItem>(
        long countryCatalogItemId,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelReferenceCatalogItemBase =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PersonnelReferenceCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>)task.Result, cancellationToken);

    private Task<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>> GetMunicipalityCatalogItemsAsync(
        long countryCatalogItemId,
        string? parentCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MunicipalityCatalogItems
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.IsActive);

        if (!string.IsNullOrWhiteSpace(parentCode))
        {
            query = query.Where(item => item.DepartmentCatalogItem != null && item.DepartmentCatalogItem.NormalizedCode == parentCode);
        }

        return query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PersonnelReferenceCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>)task.Result, cancellationToken);
    }

    private Task<IReadOnlyCollection<PersonnelCatalogItemResponse>> GetCountryCatalogItemsAsync(
        string category,
        CancellationToken cancellationToken) =>
        dbContext.CountryCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PersonnelCatalogItemResponse(
                item.PublicId,
                category,
                item.Code,
                item.Name,
                true,
                item.IsActive,
                item.SortOrder))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyCollection<PersonnelCatalogItemResponse>)task.Result, cancellationToken);

    private Task<bool> IsCountryScopedCatalogCodeActiveAsync<TCatalogItem>(
        long countryCatalogItemId,
        string normalizedCode,
        CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .AnyAsync(
                item => item.CountryCatalogItemId == countryCatalogItemId &&
                        item.IsActive &&
                        item.NormalizedCode == normalizedCode,
                cancellationToken);

    private Task<bool> IsCountryScopedCatalogCodeActiveAsync<TCatalogItem>(
        string countryCode,
        string normalizedCode,
        CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .AnyAsync(
                item => item.CountryCode == countryCode &&
                        item.IsActive &&
                        item.NormalizedCode == normalizedCode,
                cancellationToken);

    private Task<CompanyCountryLookup?> GetCompanyCountryLookupAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == companyId)
            .Select(company => new CompanyCountryLookup(company.CountryCatalogItemId, company.CountryCode))
            .SingleOrDefaultAsync(cancellationToken);

    // Resolves the country catalog item id from an explicit ISO country code (replacing the former
    // company → country resolution for the company-less catalog surface). Returns null when the code is
    // missing or does not match an active country, so country-scoped catalog reads degrade to no items.
    private Task<long?> ResolveCountryCatalogItemIdAsync(string? countryCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Task.FromResult<long?>(null);
        }

        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        return dbContext.CountryCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.NormalizedCode == normalizedCountryCode)
            .Select(item => (long?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static PersonnelFileEducationResponse MapEducationResponse(PersonnelFileEducation item) =>
        new(
            item.PublicId,
            new PersonnelEducationCatalogReferenceResponse(
                item.EducationStatusCatalogItem.PublicId,
                item.EducationStatusCatalogItem.Code,
                item.EducationStatusCatalogItem.Name,
                item.EducationStatusCatalogItem.IsActive),
            item.DegreeTitle,
            new PersonnelEducationCatalogReferenceResponse(
                item.EducationStudyTypeCatalogItem.PublicId,
                item.EducationStudyTypeCatalogItem.Code,
                item.EducationStudyTypeCatalogItem.Name,
                item.EducationStudyTypeCatalogItem.IsActive),
            new PersonnelEducationCatalogReferenceResponse(
                item.EducationCareerCatalogItem.PublicId,
                item.EducationCareerCatalogItem.Code,
                item.EducationCareerCatalogItem.Name,
                item.EducationCareerCatalogItem.IsActive),
            item.Institution,
            item.CountryCode,
            item.Specialty,
            item.IsCurrentlyStudying,
            item.StartDate,
            item.EndDate,
            item.EducationShiftCatalogItemId.HasValue
                ? new PersonnelEducationCatalogReferenceResponse(
                    item.EducationShiftCatalogItem!.PublicId,
                    item.EducationShiftCatalogItem.Code,
                    item.EducationShiftCatalogItem.Name,
                    item.EducationShiftCatalogItem.IsActive)
                : null,
            item.EducationModalityCatalogItemId.HasValue
                ? new PersonnelEducationCatalogReferenceResponse(
                    item.EducationModalityCatalogItem!.PublicId,
                    item.EducationModalityCatalogItem.Code,
                    item.EducationModalityCatalogItem.Name,
                    item.EducationModalityCatalogItem.IsActive)
                : null,
            item.TotalSubjects,
            item.ApprovedSubjects,
            item.ConcurrencyToken);

    private async Task<Dictionary<string, string>> ResolveReferenceNamesByCodeAsync(
        string countryCode,
        string category,
        IEnumerable<string?> codes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = codes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalizedCountry = countryCode.Trim().ToUpperInvariant();
        var normalizedCategory = category.Trim().ToUpperInvariant();

        return normalizedCategory switch
        {
            "IDENTIFICATIONTYPE" => await ResolveReferenceNamesByCodeAsync<IdentificationTypeCatalogItem>(normalizedCountry, normalizedCodes, cancellationToken),
            "PROFESSION" => await ResolveReferenceNamesByCodeAsync<ProfessionCatalogItem>(normalizedCountry, normalizedCodes, cancellationToken),
            "MARITALSTATUS" => await ResolveReferenceNamesByCodeAsync<MaritalStatusCatalogItem>(normalizedCountry, normalizedCodes, cancellationToken),
            "KINSHIP" => await ResolveReferenceNamesByCodeAsync<KinshipCatalogItem>(normalizedCountry, normalizedCodes, cancellationToken),
            "DEPARTMENT" => await ResolveReferenceNamesByCodeAsync<DepartmentCatalogItem>(normalizedCountry, normalizedCodes, cancellationToken),
            "MUNICIPALITY" => await ResolveReferenceNamesByCodeAsync<MunicipalityCatalogItem>(normalizedCountry, normalizedCodes, cancellationToken),
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    // §PF-cache: reference catalogs (marital status, profession, identification type, department,
    // municipality) are country-scoped static data resolved on every detail read and list page. Cache
    // the full per-(country, catalog) name map once (short TTL bounds staleness for the rare admin
    // edit) and resolve requested codes in memory, replacing the 3-6 sequential DB lookups per read.
    private const string ReferenceCatalogCacheKeyPrefix = "personnel-files:refcat";
    private static readonly TimeSpan ReferenceCatalogCacheTtl = TimeSpan.FromMinutes(10);

    private async Task<IReadOnlyDictionary<string, string>> GetReferenceCatalogMapAsync<TCatalogItem>(
        string countryCode,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelReferenceCatalogItemBase
    {
        var cacheKey = $"{ReferenceCatalogCacheKeyPrefix}:{typeof(TCatalogItem).Name}:{countryCode}";
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var map = await dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive && item.CountryCode == countryCode)
            .ToDictionaryAsync(
                static item => item.NormalizedCode,
                static item => item.Name,
                StringComparer.Ordinal,
                cancellationToken);

        memoryCache.Set(cacheKey, (IReadOnlyDictionary<string, string>)map, ReferenceCatalogCacheTtl);
        return map;
    }

    private async Task<Dictionary<string, string>> ResolveReferenceNamesByCodeAsync<TCatalogItem>(
        string countryCode,
        IReadOnlyCollection<string> normalizedCodes,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelReferenceCatalogItemBase
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (normalizedCodes.Count == 0)
        {
            return result;
        }

        var map = await GetReferenceCatalogMapAsync<TCatalogItem>(countryCode, cancellationToken);
        foreach (var code in normalizedCodes)
        {
            if (map.TryGetValue(code, out var name))
            {
                result[code] = name;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, string>> ResolveCountryNamesByCodeAsync(
        IEnumerable<string?> codes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = codes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var map = await GetCountryCatalogMapAsync(cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var code in normalizedCodes)
        {
            if (map.TryGetValue(code, out var name))
            {
                result[code] = name;
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> GetCountryCatalogMapAsync(CancellationToken cancellationToken)
    {
        var cacheKey = $"{ReferenceCatalogCacheKeyPrefix}:countries";
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var map = await dbContext.CountryCatalogItems
            .AsNoTracking()
            .Where(static item => item.IsActive)
            .ToDictionaryAsync(
                static item => item.NormalizedCode,
                static item => item.Name,
                StringComparer.Ordinal,
                cancellationToken);

        memoryCache.Set(cacheKey, (IReadOnlyDictionary<string, string>)map, ReferenceCatalogCacheTtl);
        return map;
    }

    private static string? TryResolveName(
        IReadOnlyDictionary<string, string> lookup,
        string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return lookup.TryGetValue(code.Trim().ToUpperInvariant(), out var name) ? name : null;
    }
}

internal sealed record CompanyCountryLookup(long CountryCatalogItemId, string CountryCode);
