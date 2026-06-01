using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFilePositionHierarchyResponse(
    Guid PersonnelFileId,
    Guid? OrgUnitId,
    Guid? ImmediateSupervisorPersonnelFileId,
    string? ImmediateSupervisorName,
    IReadOnlyCollection<PersonnelFilePositionHierarchySubordinateResponse> Subordinates);

public sealed record PersonnelFilePositionHierarchySubordinateResponse(
    Guid PersonnelFileId,
    string FullName,
    Guid? OrgUnitId);

public sealed record GetPersonnelFilePositionHierarchyQuery(Guid PersonnelFileId)
    : IQuery<PersonnelFilePositionHierarchyResponse>;

internal sealed class GetPersonnelFilePositionHierarchyQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFilePositionHierarchyQuery, PersonnelFilePositionHierarchyResponse>
{
    public async Task<Result<PersonnelFilePositionHierarchyResponse>> Handle(
        GetPersonnelFilePositionHierarchyQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<PersonnelFilePositionHierarchyResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFilePositionHierarchyResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var response = await employeeRepository.GetPositionHierarchyAsync(personnelFile!.PublicId, cancellationToken);
        return Result<PersonnelFilePositionHierarchyResponse>.Success(response);
    }
}

