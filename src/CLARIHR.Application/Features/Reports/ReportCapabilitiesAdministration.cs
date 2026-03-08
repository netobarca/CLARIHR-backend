using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.Reports;

public sealed record GetReportCapabilitiesQuery(Guid CompanyId, string ResourceKey)
    : IQuery<ReportCapabilitiesResponse>;

internal sealed class GetReportCapabilitiesQueryValidator : AbstractValidator<GetReportCapabilitiesQuery>
{
    public GetReportCapabilitiesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ResourceKey).NotEmpty().MaximumLength(120);
    }
}

internal sealed class GetReportCapabilitiesQueryHandler(
    IReportCapabilityRegistry reportCapabilityRegistry,
    IOrgUnitAuthorizationService orgUnitAuthorizationService,
    IJobProfileAuthorizationService jobProfileAuthorizationService,
    IPositionSlotAuthorizationService positionSlotAuthorizationService,
    ISalaryTabulatorAuthorizationService salaryTabulatorAuthorizationService,
    ICostCenterAuthorizationService costCenterAuthorizationService,
    ILegalRepresentativeAuthorizationService legalRepresentativeAuthorizationService)
    : IQueryHandler<GetReportCapabilitiesQuery, ReportCapabilitiesResponse>
{
    public async Task<Result<ReportCapabilitiesResponse>> Handle(
        GetReportCapabilitiesQuery query,
        CancellationToken cancellationToken)
    {
        if (!reportCapabilityRegistry.TryGet(query.ResourceKey, out var definition))
        {
            return Result<ReportCapabilitiesResponse>.Failure(ReportPolicyErrors.ResourceNotSupported);
        }

        var authorizationResult = await AuthorizeReadAsync(
            definition.Capabilities.ResourceKey,
            query.CompanyId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ReportCapabilitiesResponse>.Failure(authorizationResult.Error);
        }

        return Result<ReportCapabilitiesResponse>.Success(definition.Capabilities);
    }

    private Task<Result> AuthorizeReadAsync(string resourceKey, Guid companyId, CancellationToken cancellationToken) =>
        resourceKey.ToUpperInvariant() switch
        {
            OrgUnitPermissionCodes.ResourceKey => orgUnitAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            JobProfilePermissionCodes.ResourceKey => jobProfileAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            PositionSlotPermissionCodes.ResourceKey => positionSlotAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            SalaryTabulatorPermissionCodes.ResourceKey => salaryTabulatorAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            CostCenterPermissionCodes.ResourceKey => costCenterAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            LegalRepresentativePermissionCodes.ResourceKey => legalRepresentativeAuthorizationService.EnsureCanReadAsync(companyId, cancellationToken),
            _ => Task.FromResult(Result.Failure(ReportPolicyErrors.Forbidden))
        };
}
