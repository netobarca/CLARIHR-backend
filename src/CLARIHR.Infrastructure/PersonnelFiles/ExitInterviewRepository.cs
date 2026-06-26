using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class ExitInterviewRepository(ApplicationDbContext dbContext) : IExitInterviewRepository
{
    public Task<bool> FormNameExistsAsync(Guid tenantId, string normalizedName, Guid? excludingFormPublicId, CancellationToken cancellationToken) =>
        dbContext.ExitInterviewForms.AnyAsync(
            form => form.TenantId == tenantId
                && form.IsActive
                && form.NormalizedName == normalizedName
                && (!excludingFormPublicId.HasValue || form.PublicId != excludingFormPublicId.Value),
            cancellationToken);

    public void AddForm(ExitInterviewForm form) => dbContext.ExitInterviewForms.Add(form);

    public void AddGroup(ExitInterviewFormGroup group) => dbContext.ExitInterviewFormGroups.Add(group);

    public void AddField(ExitInterviewFormField field) => dbContext.ExitInterviewFormFields.Add(field);

    public void AddOption(ExitInterviewFormFieldOption option) => dbContext.ExitInterviewFormFieldOptions.Add(option);

    public Task<ExitInterviewForm?> GetFormEntityAsync(Guid tenantId, Guid formPublicId, CancellationToken cancellationToken) =>
        dbContext.ExitInterviewForms.SingleOrDefaultAsync(
            form => form.TenantId == tenantId && form.PublicId == formPublicId,
            cancellationToken);

    public async Task RemoveDefinitionChildrenAsync(Guid tenantId, long formId, CancellationToken cancellationToken)
    {
        var fieldIds = await dbContext.ExitInterviewFormFields
            .Where(field => field.ExitInterviewFormId == formId)
            .Select(field => field.Id)
            .ToListAsync(cancellationToken);

        if (fieldIds.Count > 0)
        {
            await dbContext.ExitInterviewFormFieldOptions
                .Where(option => fieldIds.Contains(option.ExitInterviewFormFieldId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.ExitInterviewFormFields
            .Where(field => field.ExitInterviewFormId == formId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.ExitInterviewFormGroups
            .Where(group => group.ExitInterviewFormId == formId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<ExitInterviewFormResponse?> GetFormResponseAsync(Guid tenantId, Guid formPublicId, CancellationToken cancellationToken)
    {
        var form = await dbContext.ExitInterviewForms
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.PublicId == formPublicId, cancellationToken);
        if (form is null)
        {
            return null;
        }

        var groups = await dbContext.ExitInterviewFormGroups
            .AsNoTracking()
            .Where(group => group.ExitInterviewFormId == form.Id)
            .OrderBy(group => group.DisplayOrder)
            .Select(group => new ExitInterviewFormGroupResponse(group.PublicId, group.Title, group.Description, group.DisplayOrder))
            .ToListAsync(cancellationToken);

        var fields = await dbContext.ExitInterviewFormFields
            .AsNoTracking()
            .Where(field => field.ExitInterviewFormId == form.Id)
            .OrderBy(field => field.DisplayOrder)
            .Select(field => new
            {
                field.Id,
                field.PublicId,
                GroupPublicId = field.ExitInterviewFormGroup != null ? (Guid?)field.ExitInterviewFormGroup.PublicId : null,
                field.ControlTypeCode,
                field.FieldKey,
                field.Title,
                field.Description,
                field.Weight,
                field.IsRequired,
                field.DisplayOrder,
                field.MinValue,
                field.MaxValue,
                field.MaxLength,
                field.ScaleMax,
                field.IsActive
            })
            .ToListAsync(cancellationToken);

        var fieldIds = fields.Select(field => field.Id).ToList();
        var options = await dbContext.ExitInterviewFormFieldOptions
            .AsNoTracking()
            .Where(option => fieldIds.Contains(option.ExitInterviewFormFieldId))
            .OrderBy(option => option.DisplayOrder)
            .Select(option => new
            {
                option.ExitInterviewFormFieldId,
                Response = new ExitInterviewFormFieldOptionResponse(option.PublicId, option.OptionCode, option.Label, option.Score, option.DisplayOrder, option.IsActive)
            })
            .ToListAsync(cancellationToken);

        var optionsByField = options
            .GroupBy(option => option.ExitInterviewFormFieldId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<ExitInterviewFormFieldOptionResponse>)group.Select(item => item.Response).ToList());

        var fieldResponses = fields
            .Select(field => new ExitInterviewFormFieldResponse(
                field.PublicId,
                field.GroupPublicId,
                field.ControlTypeCode,
                field.FieldKey,
                field.Title,
                field.Description,
                field.Weight,
                field.IsRequired,
                field.DisplayOrder,
                field.MinValue,
                field.MaxValue,
                field.MaxLength,
                field.ScaleMax,
                field.IsActive,
                optionsByField.TryGetValue(field.Id, out var fieldOptions) ? fieldOptions : []))
            .ToList();

        return new ExitInterviewFormResponse(
            form.PublicId,
            form.Name,
            form.Description,
            form.IsAnonymous,
            form.Status.ToString(),
            form.Version,
            form.RetirementReasonCode,
            form.IsActiveForReason,
            form.IsActive,
            form.ConcurrencyToken,
            groups,
            fieldResponses);
    }

    public async Task<IReadOnlyCollection<ExitInterviewFormListItemResponse>> ListFormsAsync(
        Guid tenantId,
        ExitInterviewFormStatus? status,
        string? reasonCode,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ExitInterviewForms.AsNoTracking().Where(form => form.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(form => form.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            var normalizedReason = reasonCode.Trim().ToUpperInvariant();
            query = query.Where(form => form.RetirementReasonCode == normalizedReason);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(form => form.NormalizedName.Contains(normalizedSearch));
        }

        return await query
            .OrderByDescending(form => form.CreatedUtc)
            .Select(form => new ExitInterviewFormListItemResponse(
                form.PublicId,
                form.Name,
                form.Status.ToString(),
                form.Version,
                form.RetirementReasonCode,
                form.IsActiveForReason,
                form.IsActive,
                0,
                form.ConcurrencyToken))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ControlTypeCapability>> GetControlTypeCapabilitiesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var countryCode = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => company.CountryCode)
            .SingleOrDefaultAsync(cancellationToken);

        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode)
            ? "SV"
            : countryCode.Trim().ToUpperInvariant();

        var capabilities = await dbContext.Set<FormControlTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCode == normalizedCountry && item.IsActive)
            .Select(item => new ControlTypeCapability(item.NormalizedCode, item.SupportsOptions, item.SupportsRange, item.SupportsMultiple))
            .ToListAsync(cancellationToken);

        return capabilities.ToDictionary(capability => capability.Code, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyCollection<PublishCandidateField>> GetPublishCandidateFieldsAsync(
        Guid tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        var fields = await dbContext.ExitInterviewFormFields
            .AsNoTracking()
            .Where(field => field.ExitInterviewFormId == formId)
            .Select(field => new { field.Id, field.ControlTypeCode, field.MinValue, field.MaxValue })
            .ToListAsync(cancellationToken);

        var fieldIds = fields.Select(field => field.Id).ToList();
        var optionCounts = fieldIds.Count == 0
            ? new Dictionary<long, int>()
            : (await dbContext.ExitInterviewFormFieldOptions
                .AsNoTracking()
                .Where(option => fieldIds.Contains(option.ExitInterviewFormFieldId) && option.IsActive)
                .GroupBy(option => option.ExitInterviewFormFieldId)
                .Select(group => new { FieldId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.FieldId, item => item.Count);

        return fields
            .Select(field => new PublishCandidateField(
                field.ControlTypeCode,
                field.MinValue,
                field.MaxValue,
                optionCounts.TryGetValue(field.Id, out var count) ? count : 0))
            .ToList();
    }

    public Task<ExitInterviewForm?> GetActiveFormForReasonAsync(
        Guid tenantId,
        string retirementReasonCode,
        Guid? excludingFormPublicId,
        CancellationToken cancellationToken)
    {
        var normalizedReason = retirementReasonCode.Trim().ToUpperInvariant();
        return dbContext.ExitInterviewForms.FirstOrDefaultAsync(
            form => form.TenantId == tenantId
                && form.RetirementReasonCode == normalizedReason
                && form.IsActiveForReason
                && form.Status == ExitInterviewFormStatus.Published
                && (!excludingFormPublicId.HasValue || form.PublicId != excludingFormPublicId.Value),
            cancellationToken);
    }

    public void AddSubmission(ExitInterviewSubmission submission) => dbContext.ExitInterviewSubmissions.Add(submission);

    public void AddAnswer(ExitInterviewAnswer answer) => dbContext.ExitInterviewAnswers.Add(answer);

    public async Task RemoveAnswersAsync(Guid tenantId, long submissionId, CancellationToken cancellationToken) =>
        await dbContext.ExitInterviewAnswers
            .Where(answer => answer.ExitInterviewSubmissionId == submissionId)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<ExitInterviewSubmission?> GetActiveSubmissionForFileAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken) =>
        dbContext.ExitInterviewSubmissions
            .Where(submission => submission.TenantId == tenantId
                && submission.PersonnelFileId == personnelFileId
                && submission.Status != ExitInterviewSubmissionStatus.Archived)
            .OrderByDescending(submission => submission.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ExitInterviewSubmission?> GetSubmissionEntityAsync(Guid tenantId, Guid submissionPublicId, CancellationToken cancellationToken) =>
        dbContext.ExitInterviewSubmissions
            .SingleOrDefaultAsync(submission => submission.TenantId == tenantId && submission.PublicId == submissionPublicId, cancellationToken);

    public async Task<ExitInterviewSubmissionResponse?> GetSubmissionResponseAsync(Guid tenantId, Guid submissionPublicId, CancellationToken cancellationToken)
    {
        var header = await dbContext.ExitInterviewSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId && submission.PublicId == submissionPublicId)
            .Select(submission => new
            {
                submission.Id,
                submission.PublicId,
                FormPublicId = submission.ExitInterviewForm.PublicId,
                submission.FormVersion,
                submission.IsAnonymous,
                FilePublicId = submission.PersonnelFile != null ? (Guid?)submission.PersonnelFile.PublicId : null,
                submission.Status,
                submission.RetirementReasonCode,
                submission.RetirementCategoryCode,
                submission.SeparationType,
                submission.Period,
                submission.SubmittedUtc,
                submission.TotalScore,
                submission.ConcurrencyToken
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null)
        {
            return null;
        }

        var rawAnswers = await dbContext.ExitInterviewAnswers
            .AsNoTracking()
            .Where(answer => answer.ExitInterviewSubmissionId == header.Id)
            .OrderBy(answer => answer.Id)
            .Select(answer => new
            {
                answer.PublicId,
                answer.FieldKeySnapshot,
                answer.TitleSnapshot,
                answer.ControlTypeCode,
                answer.ValueText,
                answer.ValueNumber,
                answer.ValueDate,
                answer.ValueBool,
                answer.SelectedOptionCodes,
                answer.NormalizedScore
            })
            .ToListAsync(cancellationToken);

        var answers = rawAnswers
            .Select(answer => new ExitInterviewAnswerResponse(
                answer.PublicId,
                answer.FieldKeySnapshot,
                answer.TitleSnapshot,
                answer.ControlTypeCode,
                answer.ValueText,
                answer.ValueNumber,
                answer.ValueDate,
                answer.ValueBool,
                SplitOptionCodes(answer.SelectedOptionCodes),
                answer.NormalizedScore))
            .ToList();

        return new ExitInterviewSubmissionResponse(
            header.PublicId,
            header.FormPublicId,
            header.FormVersion,
            header.IsAnonymous,
            header.FilePublicId,
            header.Status.ToString(),
            header.RetirementReasonCode,
            header.RetirementCategoryCode,
            header.SeparationType,
            header.Period,
            header.SubmittedUtc,
            header.TotalScore,
            header.ConcurrencyToken,
            answers);
    }

    public async Task<ExitInterviewSubmissionResponse?> GetSubmissionResponseForFileAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
    {
        var publicId = await dbContext.ExitInterviewSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId
                && submission.PersonnelFileId == personnelFileId
                && submission.Status != ExitInterviewSubmissionStatus.Archived)
            .OrderByDescending(submission => submission.CreatedUtc)
            .Select(submission => (Guid?)submission.PublicId)
            .FirstOrDefaultAsync(cancellationToken);

        return publicId is null ? null : await GetSubmissionResponseAsync(tenantId, publicId.Value, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>> ListSubmissionsAsync(
        Guid tenantId,
        string? reasonCode,
        string? period,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ExitInterviewSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId && submission.Status != ExitInterviewSubmissionStatus.Archived);

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            var normalizedReason = reasonCode.Trim().ToUpperInvariant();
            query = query.Where(submission => submission.RetirementReasonCode == normalizedReason);
        }

        if (!string.IsNullOrWhiteSpace(period))
        {
            var normalizedPeriod = period.Trim();
            query = query.Where(submission => submission.Period == normalizedPeriod);
        }

        return await query
            .OrderByDescending(submission => submission.SubmittedUtc ?? submission.CreatedUtc)
            .Select(submission => new ExitInterviewSubmissionListItemResponse(
                submission.PublicId,
                submission.ExitInterviewForm.PublicId,
                submission.IsAnonymous,
                submission.PersonnelFile != null ? (Guid?)submission.PersonnelFile.PublicId : null,
                submission.Status.ToString(),
                submission.RetirementReasonCode,
                submission.RetirementCategoryCode,
                submission.SeparationType,
                submission.Period,
                submission.SubmittedUtc,
                submission.TotalScore))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExitInterviewSubmissionSnapshot> GetSubmissionSnapshotAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId)
            .Select(item => new { item.RetirementReasonCode, item.RetirementCategoryCode, item.RetirementDate })
            .SingleOrDefaultAsync(cancellationToken);

        string? separationType = null;
        if (!string.IsNullOrWhiteSpace(profile?.RetirementCategoryCode))
        {
            var country = await GetTenantCountryAsync(tenantId, cancellationToken);
            var normalizedCategory = profile.RetirementCategoryCode.Trim().ToUpperInvariant();
            separationType = await dbContext.Set<RetirementCategoryCatalogItem>()
                .AsNoTracking()
                .Where(item => item.CountryCode == country && item.NormalizedCode == normalizedCategory)
                .Select(item => item.SeparationType.ToString())
                .FirstOrDefaultAsync(cancellationToken);
        }

        var positionSlotPublicId = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId && item.IsActive)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.PositionSlotPublicId)
            .FirstOrDefaultAsync(cancellationToken);

        return new ExitInterviewSubmissionSnapshot(
            profile?.RetirementReasonCode,
            profile?.RetirementCategoryCode,
            separationType,
            positionSlotPublicId,
            profile?.RetirementDate);
    }

    public Task<int> ArchiveSubmissionsForFileAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken) =>
        dbContext.ExitInterviewSubmissions
            .Where(submission => submission.TenantId == tenantId
                && submission.PersonnelFileId == personnelFileId
                && submission.Status != ExitInterviewSubmissionStatus.Archived)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(submission => submission.Status, ExitInterviewSubmissionStatus.Archived),
                cancellationToken);

    private static IReadOnlyCollection<string> SplitOptionCodes(string? selectedOptionCodes) =>
        string.IsNullOrWhiteSpace(selectedOptionCodes)
            ? []
            : selectedOptionCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task<string> GetTenantCountryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var countryCode = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => company.CountryCode)
            .SingleOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(countryCode) ? "SV" : countryCode.Trim().ToUpperInvariant();
    }
}
