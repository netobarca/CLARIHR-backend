using System.Text.Json;
using System.Text.Json.Nodes;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Reports;
using FluentValidation;

namespace CLARIHR.Application.Features.Reports;

public sealed record ReportExportJobResponse(
    Guid Id,
    string ResourceKey,
    string Format,
    ReportExportJobStatus Status,
    DateTime QueuedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    DateTime? ExpiresUtc,
    int Attempts,
    int? RowCount,
    string? FileName,
    long? SizeBytes,
    string? LastErrorCode,
    string? LastErrorMessage,
    Guid ConcurrencyToken);

public sealed record ReportExportJobDownloadResponse(
    Guid Id,
    string BlobName,
    string FileName,
    string ContentType);

public sealed record CreateReportExportJobCommand(
    Guid CompanyId,
    string ResourceKey,
    string Format,
    string ParametersJson)
    : ICommand<ReportExportJobResponse>;

public sealed record SearchReportExportJobsQuery(
    Guid CompanyId,
    ReportExportJobStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResponse<ReportExportJobResponse>>;

public sealed record GetReportExportJobQuery(Guid JobId)
    : IQuery<ReportExportJobResponse>;

public sealed record GetReportExportJobDownloadQuery(Guid JobId)
    : IQuery<ReportExportJobDownloadResponse>;

public sealed record CancelReportExportJobRequest(Guid ConcurrencyToken);

public sealed record CancelReportExportJobCommand(Guid JobId, Guid ConcurrencyToken)
    : ICommand<ReportExportJobResponse>;

internal sealed class CreateReportExportJobCommandValidator : AbstractValidator<CreateReportExportJobCommand>
{
    public CreateReportExportJobCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.ResourceKey)
            .NotEmpty()
            .MaximumLength(120)
            .Must(ReportExportResources.IsSupported)
            .WithMessage("ResourceKey is not supported for report exports.");
        RuleFor(command => command.Format)
            .NotEmpty()
            .MaximumLength(20)
            .Must(static format => ReportExportFormats.TryNormalize(format, out _))
            .WithMessage("Format is not supported.");
        RuleFor(command => command)
            .Must(BeCompatibleResourceFormat)
            .WithName("Format")
            .WithMessage("The requested format is not compatible with the selected resource.");
        RuleFor(command => command.ParametersJson)
            .MaximumLength(20_000)
            .Must(BeValidJsonObject)
            .WithMessage("Parameters must be a valid JSON object.");
    }

    private static bool BeCompatibleResourceFormat(CreateReportExportJobCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ResourceKey) ||
            !ReportExportResources.IsSupported(command.ResourceKey))
        {
            return true;
        }

        if (!ReportExportFormats.TryNormalize(command.Format, out var normalizedFormat))
        {
            return true;
        }

        var normalizedResourceKey = ReportExportResources.Normalize(command.ResourceKey);
        var isDocumentResource = ReportExportResources.IsDocumentResource(normalizedResourceKey);
        var isPdfFormat = normalizedFormat == ReportExportFormats.Pdf;

        return isDocumentResource == isPdfFormat;
    }

    private static bool BeValidJsonObject(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(parametersJson);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

internal sealed class SearchReportExportJobsQueryValidator : AbstractValidator<SearchReportExportJobsQuery>
{
    public SearchReportExportJobsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetReportExportJobQueryValidator : AbstractValidator<GetReportExportJobQuery>
{
    public GetReportExportJobQueryValidator()
    {
        RuleFor(query => query.JobId).NotEmpty();
    }
}

internal sealed class GetReportExportJobDownloadQueryValidator : AbstractValidator<GetReportExportJobDownloadQuery>
{
    public GetReportExportJobDownloadQueryValidator()
    {
        RuleFor(query => query.JobId).NotEmpty();
    }
}

internal sealed class CancelReportExportJobCommandValidator : AbstractValidator<CancelReportExportJobCommand>
{
    public CancelReportExportJobCommandValidator()
    {
        RuleFor(command => command.JobId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class CreateReportExportJobCommandHandler(
    IReportExportJobRepository repository,
    IFilePurposeRuleProvider ruleProvider,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IPersonnelFileAuthorizationService personnelFileAuthorizationService,
    IOrgUnitAuthorizationService orgUnitAuthorizationService,
    IPositionSlotAuthorizationService positionSlotAuthorizationService,
    ISalaryTabulatorAuthorizationService salaryTabulatorAuthorizationService,
    ICostCenterAuthorizationService costCenterAuthorizationService,
    ILegalRepresentativeAuthorizationService legalRepresentativeAuthorizationService,
    ICompetencyFrameworkAuthorizationService competencyFrameworkAuthorizationService,
    IJobProfileAuthorizationService jobProfileAuthorizationService)
    : ICommandHandler<CreateReportExportJobCommand, ReportExportJobResponse>
{
    public async Task<Result<ReportExportJobResponse>> Handle(
        CreateReportExportJobCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result<ReportExportJobResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ReportExportJobResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != command.CompanyId)
        {
            return Result<ReportExportJobResponse>.Failure(AuthorizationErrors.TenantMismatch("REPORT_EXPORT_JOBS", RbacPermissionAction.Create));
        }

        if (ruleProvider.GetRule(FilePurpose.ReportExport) is null)
        {
            return Result<ReportExportJobResponse>.Failure(ReportPolicyErrors.ExportStorageNotConfigured);
        }

        var normalizedResourceKey = ReportExportResources.Normalize(command.ResourceKey);
        var authorizationResult = await AuthorizeReadAsync(normalizedResourceKey, command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ReportExportJobResponse>.Failure(authorizationResult.Error);
        }

        if (!ReportExportFormats.TryNormalize(command.Format, out var normalizedFormat))
        {
            return Result<ReportExportJobResponse>.Failure(ReportPolicyErrors.FormatNotSupported);
        }

        var effectiveParametersJson = await BuildEffectiveParametersAsync(
            normalizedResourceKey, command.CompanyId, command.ParametersJson, cancellationToken);

        var job = ReportExportJob.Create(
            command.CompanyId,
            normalizedResourceKey,
            normalizedFormat,
            effectiveParametersJson,
            currentUserService.UserId!,
            dateTimeProvider.UtcNow);

        repository.Add(job);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ReportExportJobResponse>.Success(ReportExportJobMapper.Map(job));
    }

    private Task<Result> AuthorizeReadAsync(string resourceKey, Guid companyId, CancellationToken cancellationToken) =>
        resourceKey switch
        {
            ReportExportResources.PersonnelFiles or
            ReportExportResources.PersonnelFilePersonnelActions or
            ReportExportResources.PersonnelFilePayrollTransactions =>
                personnelFileAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.OrgUnits => orgUnitAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.PositionSlots => positionSlotAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.SalaryTabulator => salaryTabulatorAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.CostCenters => costCenterAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.LegalRepresentatives => legalRepresentativeAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.JobProfileCompetencyMatrix => competencyFrameworkAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            ReportExportResources.JobProfilePdf => jobProfileAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            _ => Task.FromResult(Result.Failure(ReportPolicyErrors.ResourceNotSupported))
        };

    /// <summary>
    /// Returns the parameter payload persisted on the job. For the job-profile
    /// PDF it stamps the server-controlled <c>includeCompensation</c> flag:
    /// salary data (PII) is only embedded in the exported document when the
    /// requester can manage profiles (same RBAC bar that gates compensation
    /// writes). The decision is taken here — where the user/JWT context exists —
    /// because the export worker has none. Any client-supplied
    /// <c>includeCompensation</c> is overridden. See technical-debt doc 01 §N2.
    /// </summary>
    private async Task<string> BuildEffectiveParametersAsync(
        string normalizedResourceKey,
        Guid companyId,
        string? parametersJson,
        CancellationToken cancellationToken)
    {
        if (normalizedResourceKey != ReportExportResources.JobProfilePdf)
        {
            return string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
        }

        JsonObject root;
        try
        {
            root = string.IsNullOrWhiteSpace(parametersJson)
                ? []
                : JsonNode.Parse(parametersJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            root = [];
        }

        var canViewCompensation = await jobProfileAuthorizationService
            .EnsureCanManageProfilesAsync(companyId, cancellationToken);

        root["includeCompensation"] = canViewCompensation.IsSuccess;

        return root.ToJsonString();
    }
}

internal sealed class SearchReportExportJobsQueryHandler(
    IReportExportJobRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<SearchReportExportJobsQuery, PagedResponse<ReportExportJobResponse>>
{
    public async Task<Result<PagedResponse<ReportExportJobResponse>>> Handle(
        SearchReportExportJobsQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PagedResponse<ReportExportJobResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != query.CompanyId)
        {
            return Result<PagedResponse<ReportExportJobResponse>>.Failure(
                AuthorizationErrors.TenantMismatch("REPORT_EXPORT_JOBS", RbacPermissionAction.Read));
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.Status,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<ReportExportJobResponse>>.Success(response);
    }
}

internal sealed class GetReportExportJobQueryHandler(
    IReportExportJobRepository repository)
    : IQueryHandler<GetReportExportJobQuery, ReportExportJobResponse>
{
    public async Task<Result<ReportExportJobResponse>> Handle(
        GetReportExportJobQuery query,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByPublicIdAsync(query.JobId, cancellationToken);
        return job is null
            ? Result<ReportExportJobResponse>.Failure(ReportPolicyErrors.ExportJobNotFound)
            : Result<ReportExportJobResponse>.Success(ReportExportJobMapper.Map(job));
    }
}

internal sealed class GetReportExportJobDownloadQueryHandler(
    IReportExportJobRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetReportExportJobDownloadQuery, ReportExportJobDownloadResponse>
{
    public async Task<Result<ReportExportJobDownloadResponse>> Handle(
        GetReportExportJobDownloadQuery query,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByPublicIdAsync(query.JobId, cancellationToken);
        if (job is null)
        {
            return Result<ReportExportJobDownloadResponse>.Failure(ReportPolicyErrors.ExportJobNotFound);
        }

        if (job.Status != ReportExportJobStatus.Succeeded)
        {
            return Result<ReportExportJobDownloadResponse>.Failure(
                job.Status == ReportExportJobStatus.Expired
                    ? ReportPolicyErrors.ExportJobExpired
                    : ReportPolicyErrors.ExportJobNotReady);
        }

        if (job.ExpiresUtc.HasValue && job.ExpiresUtc.Value <= dateTimeProvider.UtcNow)
        {
            return Result<ReportExportJobDownloadResponse>.Failure(ReportPolicyErrors.ExportJobExpired);
        }

        if (string.IsNullOrWhiteSpace(job.ArtifactBlobName) ||
            string.IsNullOrWhiteSpace(job.ArtifactFileName) ||
            string.IsNullOrWhiteSpace(job.ArtifactContentType))
        {
            return Result<ReportExportJobDownloadResponse>.Failure(ReportPolicyErrors.ExportJobNotReady);
        }

        return Result<ReportExportJobDownloadResponse>.Success(
            new ReportExportJobDownloadResponse(
                job.PublicId,
                job.ArtifactBlobName,
                job.ArtifactFileName,
                job.ArtifactContentType));
    }
}

internal sealed class CancelReportExportJobCommandHandler(
    IReportExportJobRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CancelReportExportJobCommand, ReportExportJobResponse>
{
    public async Task<Result<ReportExportJobResponse>> Handle(
        CancelReportExportJobCommand command,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByPublicIdAsync(command.JobId, cancellationToken);
        if (job is null)
        {
            return Result<ReportExportJobResponse>.Failure(ReportPolicyErrors.ExportJobNotFound);
        }

        if (job.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<ReportExportJobResponse>.Failure(ReportPolicyErrors.ConcurrencyConflict);
        }

        job.Cancel(dateTimeProvider.UtcNow);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ReportExportJobResponse>.Success(ReportExportJobMapper.Map(job));
    }
}

public static class ReportExportJobMapper
{
    public static ReportExportJobResponse Map(ReportExportJob job) =>
        new(
            job.PublicId,
            job.ResourceKey,
            job.Format,
            job.Status,
            job.QueuedUtc,
            job.StartedUtc,
            job.CompletedUtc,
            job.ExpiresUtc,
            job.Attempts,
            job.RowCount,
            job.ArtifactFileName,
            job.ArtifactSizeBytes,
            job.LastErrorCode,
            job.LastErrorMessage,
            job.ConcurrencyToken);
}
