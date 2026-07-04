using System.Globalization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class GetExitInterviewForFileQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : IQueryHandler<GetExitInterviewForFileQuery, ExitInterviewForFileResponse>
{
    public async Task<Result<ExitInterviewForFileResponse>> Handle(GetExitInterviewForFileQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewForFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(query.PersonnelFilePublicId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<ExitInterviewForFileResponse>.Failure(PersonnelFileErrors.NotFound);
        }

        var isSelf = IsSelf(personnelFile, currentUserService);
        var canManage = (await authorizationService.EnsureCanManageExitInterviewsAsync(tenantId, cancellationToken)).IsSuccess;
        var canView = canManage || (await authorizationService.EnsureCanViewExitInterviewsAsync(tenantId, cancellationToken)).IsSuccess;
        if (!canView && !isSelf)
        {
            return Result<ExitInterviewForFileResponse>.Failure(PersonnelFileErrors.Forbidden);
        }

        var snapshot = await repository.GetSubmissionSnapshotAsync(tenantId, personnelFile.Id, cancellationToken);
        var currentSubmission = await repository.GetSubmissionResponseForFileAsync(tenantId, personnelFile.Id, cancellationToken);

        if (string.IsNullOrWhiteSpace(snapshot.RetirementReasonCode))
        {
            return Result<ExitInterviewForFileResponse>.Success(new ExitInterviewForFileResponse(false, null, currentSubmission));
        }

        var form = await repository.GetActiveFormForReasonAsync(tenantId, snapshot.RetirementReasonCode!, excludingFormPublicId: null, cancellationToken);
        if (form is null)
        {
            return Result<ExitInterviewForFileResponse>.Success(new ExitInterviewForFileResponse(false, null, currentSubmission));
        }

        var formResponse = await repository.GetFormResponseAsync(tenantId, form.PublicId, cancellationToken);
        return Result<ExitInterviewForFileResponse>.Success(new ExitInterviewForFileResponse(formResponse is not null, formResponse, currentSubmission));
    }

    internal static bool IsSelf(PersonnelFile personnelFile, ICurrentUserService currentUserService) =>
        personnelFile.LinkedUserPublicId is { } linkedUserPublicId
        && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
        && linkedUserPublicId == callerUserPublicId;
}

internal sealed class SaveExitInterviewSubmissionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SaveExitInterviewSubmissionCommand, ExitInterviewSubmissionResponse>
{
    public async Task<Result<ExitInterviewSubmissionResponse>> Handle(SaveExitInterviewSubmissionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(command.PersonnelFilePublicId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(PersonnelFileErrors.NotFound);
        }

        var canManage = (await authorizationService.EnsureCanManageExitInterviewsAsync(tenantId, cancellationToken)).IsSuccess;
        if (!canManage && !GetExitInterviewForFileQueryHandler.IsSelf(personnelFile, currentUserService))
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(PersonnelFileErrors.Forbidden);
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var snapshot = await repository.GetSubmissionSnapshotAsync(tenantId, personnelFile.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(snapshot.RetirementReasonCode))
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(ErrorCatalog.Validation(
                new Dictionary<string, string[]> { ["personnelFile"] = ["The employee has no retirement request in force (AUTORIZADA/EJECUTADA); register and authorize the retirement first."] }));
        }

        var form = await repository.GetActiveFormForReasonAsync(tenantId, snapshot.RetirementReasonCode!, excludingFormPublicId: null, cancellationToken);
        if (form is null)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(ErrorCatalog.Validation(
                new Dictionary<string, string[]> { ["form"] = ["No exit-interview form is configured for the employee's retirement reason."] }));
        }

        var formDefinition = await repository.GetFormResponseAsync(tenantId, form.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Exit-interview form definition could not be resolved.");

        // Anonymous forms cannot be resumed (no link to the employee), so they are submitted in one shot.
        if (formDefinition.IsAnonymous && !command.Submit)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(ErrorCatalog.Validation(
                new Dictionary<string, string[]> { ["submit"] = ["An anonymous exit interview cannot be saved as a draft; it must be submitted."] }));
        }

        var build = ExitInterviewSubmissionBuilder.BuildAndScore(formDefinition, command.Answers, requireRequired: command.Submit);
        if (build.Validation.IsFailure)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(build.Validation.Error);
        }

        Guid? submittedByUserId = Guid.TryParse(currentUserService.UserId, out var callerId) ? callerId : null;
        var period = (snapshot.RetirementDate ?? dateTimeProvider.UtcNow).ToString("yyyy-MM", CultureInfo.InvariantCulture);

        ExitInterviewSubmission? existing = formDefinition.IsAnonymous
            ? null
            : await repository.GetActiveSubmissionForFileAsync(tenantId, personnelFile.Id, cancellationToken);

        if (existing is { Status: ExitInterviewSubmissionStatus.Submitted })
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(ExitInterviewErrors.SubmissionAlreadySubmitted);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            ExitInterviewSubmission submission;
            if (existing is not null)
            {
                submission = existing;
                submission.UpdateSnapshot(
                    form.Version,
                    formDefinition.IsAnonymous,
                    personnelFile.Id,
                    submittedByUserId,
                    snapshot.RetirementReasonCode!,
                    snapshot.RetirementCategoryCode,
                    snapshot.SeparationType,
                    snapshot.PositionSlotPublicId,
                    plazaSnapshot: null,
                    period);
                await repository.RemoveAnswersAsync(tenantId, submission.Id, cancellationToken);
            }
            else
            {
                submission = ExitInterviewSubmission.Create(
                    form.Id,
                    form.Version,
                    formDefinition.IsAnonymous,
                    personnelFile.Id,
                    submittedByUserId,
                    snapshot.RetirementReasonCode!,
                    snapshot.RetirementCategoryCode,
                    snapshot.SeparationType,
                    snapshot.PositionSlotPublicId,
                    plazaSnapshot: null,
                    period);
                submission.SetTenantId(tenantId);
                repository.AddSubmission(submission);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var built in build.Answers)
            {
                var answer = ExitInterviewAnswer.Create(
                    built.FieldKey,
                    built.Title,
                    built.ControlTypeCode,
                    built.ValueText,
                    built.ValueNumber,
                    built.ValueDate,
                    built.ValueBool,
                    built.SelectedOptionCodes,
                    built.WeightSnapshot,
                    built.NormalizedScore);
                answer.BindToSubmission(submission.Id);
                answer.SetTenantId(tenantId);
                repository.AddAnswer(answer);
            }

            if (command.Submit)
            {
                submission.MarkSubmitted(dateTimeProvider.UtcNow, build.TotalScore);
            }
            else
            {
                submission.SetTotalScore(build.TotalScore);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetSubmissionResponseAsync(tenantId, submission.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Exit-interview submission response could not be resolved after save.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    command.Submit ? AuditEventTypes.ExitInterviewSubmitted : AuditEventTypes.ExitInterviewSubmissionSaved,
                    AuditEntityTypes.ExitInterviewSubmission,
                    submission.PublicId,
                    formDefinition.Name,
                    command.Submit ? AuditActions.Create : AuditActions.Update,
                    command.Submit
                        ? $"Submitted exit interview for reason {snapshot.RetirementReasonCode}."
                        : $"Saved exit-interview draft for reason {snapshot.RetirementReasonCode}.",
                    After: formDefinition.IsAnonymous ? null : response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<ExitInterviewSubmissionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ListExitInterviewSubmissionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<ListExitInterviewSubmissionsQuery, IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>> Handle(ListExitInterviewSubmissionsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        // Reading others' submissions is RRHH-only (D-14).
        var authorizationResult = await authorizationService.EnsureCanViewExitInterviewsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>.Failure(authorizationResult.Error);
        }

        var submissions = await repository.ListSubmissionsAsync(tenantId, query.ReasonCode, query.Period, cancellationToken);
        return Result<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>.Success(submissions);
    }
}

internal sealed class GetExitInterviewSubmissionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IExitInterviewRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetExitInterviewSubmissionByIdQuery, ExitInterviewSubmissionResponse>
{
    public async Task<Result<ExitInterviewSubmissionResponse>> Handle(GetExitInterviewSubmissionByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanViewExitInterviewsAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ExitInterviewSubmissionResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetSubmissionResponseAsync(tenantId, query.SubmissionId, cancellationToken);
        return response is null
            ? Result<ExitInterviewSubmissionResponse>.Failure(ExitInterviewErrors.SubmissionNotFound)
            : Result<ExitInterviewSubmissionResponse>.Success(response);
    }
}

/// <summary>Builds the answer snapshots and the weighted 0–100 index from a form definition + answer inputs.</summary>
internal static class ExitInterviewSubmissionBuilder
{
    internal sealed record BuiltAnswer(
        string FieldKey,
        string Title,
        string ControlTypeCode,
        string? ValueText,
        decimal? ValueNumber,
        DateTime? ValueDate,
        bool? ValueBool,
        string? SelectedOptionCodes,
        decimal? WeightSnapshot,
        decimal? NormalizedScore);

    private const string ControlScale = "ESCALA";

    public static (Result Validation, IReadOnlyList<BuiltAnswer> Answers, decimal? TotalScore) BuildAndScore(
        ExitInterviewFormResponse form,
        IReadOnlyCollection<ExitInterviewAnswerInput> answers,
        bool requireRequired)
    {
        var fieldsByKey = form.Fields
            .Where(field => field.IsActive)
            .ToDictionary(field => field.FieldKey.Trim().ToUpperInvariant(), StringComparer.Ordinal);

        var answersByKey = new Dictionary<string, ExitInterviewAnswerInput>(StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            var key = answer.FieldKey.Trim().ToUpperInvariant();
            if (!fieldsByKey.ContainsKey(key))
            {
                return (Result.Failure(ErrorCatalog.Validation(
                    new Dictionary<string, string[]> { ["answers"] = [$"Answer references an unknown field '{answer.FieldKey}'."] })), [], null);
            }

            answersByKey[key] = answer;
        }

        var built = new List<BuiltAnswer>();
        var scored = new List<(decimal Weight, decimal NormalizedScore)>();
        var missingRequired = new List<string>();

        foreach (var (key, field) in fieldsByKey)
        {
            answersByKey.TryGetValue(key, out var answer);
            var hasValue = HasValue(field, answer);

            if (requireRequired && field.IsRequired && !hasValue)
            {
                missingRequired.Add(field.FieldKey);
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            var optionByCode = field.Options.ToDictionary(option => option.OptionCode.Trim().ToUpperInvariant(), StringComparer.Ordinal);
            string? selectedCodes = null;
            decimal? normalizedScore = null;

            if (field.Options.Count > 0)
            {
                var selected = (answer!.SelectedOptionCodes ?? [])
                    .Select(code => code.Trim().ToUpperInvariant())
                    .Where(code => code.Length > 0)
                    .ToList();
                foreach (var code in selected)
                {
                    if (!optionByCode.ContainsKey(code))
                    {
                        return (Result.Failure(ErrorCatalog.Validation(
                            new Dictionary<string, string[]> { [field.FieldKey] = [$"Option '{code}' is not valid for this field."] })), [], null);
                    }
                }

                selectedCodes = string.Join(',', selected);
                var optionScores = selected
                    .Select(code => optionByCode[code].Score)
                    .Where(score => score.HasValue)
                    .Select(score => score!.Value)
                    .ToList();
                normalizedScore = ExitInterviewScoring.NormalizeOptions(optionScores);
            }
            else if (string.Equals(field.ControlTypeCode, ControlScale, StringComparison.Ordinal))
            {
                normalizedScore = ExitInterviewScoring.NormalizeScale(answer!.ValueNumber, field.ScaleMax);
            }
            else if (answer!.ValueNumber is { } number)
            {
                if ((field.MinValue is { } min && number < min) || (field.MaxValue is { } max && number > max))
                {
                    return (Result.Failure(ErrorCatalog.Validation(
                        new Dictionary<string, string[]> { [field.FieldKey] = ["The numeric value is out of the allowed range."] })), [], null);
                }
            }

            built.Add(new BuiltAnswer(
                field.FieldKey,
                field.Title,
                field.ControlTypeCode,
                answer!.ValueText,
                answer.ValueNumber,
                answer.ValueDate,
                answer.ValueBool,
                selectedCodes,
                field.Weight,
                normalizedScore));

            if (normalizedScore is { } score)
            {
                scored.Add((field.Weight, score));
            }
        }

        if (missingRequired.Count > 0)
        {
            return (Result.Failure(ErrorCatalog.Validation(
                new Dictionary<string, string[]> { ["answers"] = missingRequired.Select(key => $"Field '{key}' is required.").ToArray() })), [], null);
        }

        return (Result.Success(), built, ExitInterviewScoring.ComputeIndex(scored));
    }

    private static bool HasValue(ExitInterviewFormFieldResponse field, ExitInterviewAnswerInput? answer)
    {
        if (answer is null)
        {
            return false;
        }

        if (field.Options.Count > 0)
        {
            return answer.SelectedOptionCodes is { Count: > 0 };
        }

        return !string.IsNullOrWhiteSpace(answer.ValueText)
            || answer.ValueNumber.HasValue
            || answer.ValueDate.HasValue
            || answer.ValueBool.HasValue;
    }
}
