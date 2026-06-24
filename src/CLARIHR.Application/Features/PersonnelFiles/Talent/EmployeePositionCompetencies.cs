using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// The "Competencias del puesto" consultation (RF-002): the competencies expected for the employee's assigned
/// position — derived from the job-profile competency matrix (by hierarchical level) — combined with the
/// employee's achieved scores, with the gap computed (expected − achieved) and the evaluation history per
/// competency, grouped by competency type (gestión / organizacional / técnica).
/// </summary>
public sealed record EmployeePositionCompetenciesResponse(
    Guid PersonnelFileId,
    Guid? JobProfilePublicId,
    string? JobProfileCode,
    string? JobProfileTitle,
    bool HasAssignedPosition,
    IReadOnlyCollection<EmployeePositionCompetencyTypeGroupResponse> Groups);

public sealed record EmployeePositionCompetencyTypeGroupResponse(
    Guid CompetencyTypePublicId,
    string CompetencyTypeCode,
    string CompetencyTypeName,
    IReadOnlyCollection<EmployeePositionCompetencyResponse> Competencies);

public sealed record EmployeePositionCompetencyResponse(
    Guid ExpectationPublicId,
    Guid CompetencyPublicId,
    string CompetencyCode,
    string CompetencyName,
    Guid OccupationalPyramidLevelPublicId,
    string OccupationalPyramidLevelCode,
    string OccupationalPyramidLevelName,
    int OccupationalPyramidLevelOrder,
    Guid BehaviorLevelPublicId,
    string BehaviorLevelCode,
    string BehaviorLevelName,
    string? ExpectedEvidence,
    decimal? ExpectedScore,
    decimal? AchievedScore,
    decimal? GapScore,
    DateTime? EvaluationDateUtc,
    IReadOnlyCollection<string> DesiredBehaviors,
    IReadOnlyCollection<EmployeePositionCompetencyHistoryEntryResponse> History);

public sealed record EmployeePositionCompetencyHistoryEntryResponse(
    Guid PositionCompetencyResultPublicId,
    decimal? ExpectedScore,
    decimal AchievedScore,
    decimal? GapScore,
    DateTime EvaluationDateUtc);

public sealed record GetEmployeePositionCompetenciesQuery(Guid PersonnelFileId)
    : IQuery<EmployeePositionCompetenciesResponse>;

internal sealed class GetEmployeePositionCompetenciesQueryValidator : AbstractValidator<GetEmployeePositionCompetenciesQuery>
{
    public GetEmployeePositionCompetenciesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetEmployeePositionCompetenciesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetEmployeePositionCompetenciesQuery, EmployeePositionCompetenciesResponse>
{
    public async Task<Result<EmployeePositionCompetenciesResponse>> Handle(
        GetEmployeePositionCompetenciesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompetencyReadAsync<EmployeePositionCompetenciesResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            currentUserService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmployeePositionCompetenciesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<EmployeePositionCompetenciesResponse>.Success(response);
    }
}
