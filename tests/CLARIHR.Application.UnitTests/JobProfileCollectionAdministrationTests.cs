using System.Text.Json;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class JobProfileCollectionAdministrationTests
{
    private readonly TestJobProfileRepository _profileRepository = new();
    private readonly TestJobCatalogRepository _catalogRepository = new();
    private readonly TestPositionDescriptionCatalogRepository _positionDescriptionCatalogRepository = new();
    private readonly TestJobProfileAuthorizationService _authService = new();
    private readonly TestAuditService _auditService = new();
    private readonly TestUnitOfWork _unitOfWork = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FixedTenantContext _tenantContext;

    public JobProfileCollectionAdministrationTests()
    {
        _tenantContext = new FixedTenantContext(_tenantId);
    }

    [Fact]
    public async Task AddRequirement_WhenProfileExists_ShouldAddAndReturnSuccess()
    {
        // Arrange
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        
        _profileRepository.Profiles[profileId] = profile;
        _profileRepository.Responses[profileId] = new JobProfileResponse(profileId, _tenantId, "JP-001", "Title", JobProfileStatus.Draft, 1, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, [], [], [], [], [], null, [], [], [], profile.ConcurrencyToken, DateTime.UtcNow, null);

        var handler = new AddJobProfileRequirementCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository,
            _catalogRepository);

        var command = new AddJobProfileRequirementCommand(
            profileId,
            JobRequirementType.Experience,
            null,
            null,
            null,
            null,
            "3 years",
            1,
            profile.ConcurrencyToken);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(profile.Requirements);
        Assert.Equal("3 years", result.Value.Item.Description);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Equal(2, _unitOfWork.SaveChangesCalls); // Once for entity, once for audit
    }

    [Fact]
    public async Task AddRequirement_WhenTokenMismatch_ShouldReturnConflict()
    {
        // Arrange
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileRequirementCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository,
            _catalogRepository);

        var command = new AddJobProfileRequirementCommand(
            profileId,
            JobRequirementType.Experience,
            null,
            null,
            null,
            null,
            "3 years",
            1,
            Guid.NewGuid()); // Wrong token

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(JobProfileErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddBenefit_WhenProfileExists_ShouldAddAndReturnSuccess()
    {
        // Arrange
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        
        _profileRepository.Profiles[profileId] = profile;
        _profileRepository.Responses[profileId] = new JobProfileResponse(profileId, _tenantId, "JP-001", "Title", JobProfileStatus.Draft, 1, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, [], [], [], [], [], null, [], [], [], profile.ConcurrencyToken, DateTime.UtcNow, null);

        var handler = new AddJobProfileBenefitCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var command = new AddJobProfileBenefitCommand(
            profileId,
            null,
            "Health Insurance",
            "Notes",
            1,
            profile.ConcurrencyToken);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(profile.Benefits);
        Assert.Equal("Health Insurance", result.Value.Item.Name);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
        Assert.Equal(2, _unitOfWork.SaveChangesCalls); // Once for entity, once for audit
    }

    [Fact]
    public async Task RemoveRequirement_WhenProfileExists_ShouldReturnParentConcurrencyToken()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var requirement = JobProfileRequirement.Create(JobRequirementType.Experience, null, null, null, "3 years", 1);
        profile.AddRequirement(requirement);
        var profileId = profile.PublicId;

        _profileRepository.Profiles[profileId] = profile;

        var handler = new RemoveJobProfileRequirementCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new RemoveJobProfileRequirementCommand(profileId, requirement.PublicId, profile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Requirements);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddFunction_WhenProfileExists_ShouldReturnLightweightItem()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        var frequencyId = Guid.NewGuid();
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileFunctionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository);

        var result = await handler.Handle(
            new AddJobProfileFunctionCommand(profileId, JobFunctionType.General, frequencyId, "Lead delivery", 1, profile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lead delivery", result.Value.Item.Description);
        Assert.Equal(frequencyId, result.Value.Item.FrequencyCatalogItemId);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task RemoveFunction_WhenProfileExists_ShouldReturnParentConcurrencyToken()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var function = JobProfileFunction.Create(JobFunctionType.General, null, "Lead delivery", 1);
        profile.AddFunction(function);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new RemoveJobProfileFunctionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new RemoveJobProfileFunctionCommand(profileId, function.PublicId, profile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Functions);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddTraining_WhenProfileExists_ShouldReturnLightweightItem()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileTrainingCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new AddJobProfileTrainingCommand(profileId, null, "Leadership", "Notes", 1, profile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Leadership", result.Value.Item.Name);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddCompetency_WhenProfileExists_ShouldReturnLegacyLightweightItem()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileCompetencyCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new AddJobProfileCompetencyCommand(profileId, null, "Communication", "Advanced", "Notes", 1, profile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Communication", result.Value.Item.Name);
        Assert.Equal("Advanced", result.Value.Item.ExpectedLevel);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddDependentPosition_WhenProfileExists_ShouldReturnLightweightItem()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        var dependentProfileId = Guid.NewGuid();
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileDependentPositionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new AddJobProfileDependentPositionCommand(profileId, dependentProfileId, 2, "Notes", profile.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Item.Quantity);
        Assert.Equal("JP-REF", result.Value.Item.DependentJobProfileCode);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddWorkingCondition_WhenProfileExists_ShouldReturnLightweightItem()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileWorkingConditionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository,
            _positionDescriptionCatalogRepository);

        var result = await handler.Handle(
            new AddJobProfileWorkingConditionCommand(profileId, null, null, "Remote", "Notes", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Remote", result.Value.Item.Name);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task MarkPrinted_WhenProfileExists_ShouldWriteAuditAndCommit()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        _profileRepository.EntityResponses[profileId] = new JobProfileEntityResponse(
            profileId,
            _tenantId,
            "JP-PRINT",
            "Printable profile",
            JobProfileStatus.Draft,
            Version: 1,
            Objective: null,
            OrgUnitId: Guid.NewGuid(),
            ReportsToJobProfileId: null,
            PositionCategoryId: null,
            StrategicObjectiveCatalogItemId: null,
            AssignedWorkEquipmentCatalogItemId: null,
            ResponsibilityCatalogItemId: null,
            DecisionScope: null,
            AssignedResources: null,
            Responsibilities: null,
            BenefitsSummary: null,
            WorkingConditionSummary: null,
            MarketSalaryReference: null,
            ValuationNotes: null,
            EffectiveFromUtc: null,
            EffectiveToUtc: null,
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null);

        var handler = new MarkJobProfilePrintedCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        // Act
        var result = await handler.Handle(new MarkJobProfilePrintedCommand(profileId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
        var entry = Assert.Single(_auditService.Entries);
        Assert.Equal(AuditEventTypes.ReportPrinted, entry.EventType);
        Assert.Equal(AuditEntityTypes.JobProfile, entry.EntityType);
        Assert.Equal(profileId, entry.EntityId);
        Assert.Equal(JobProfilePermissionCodes.ResourceKey, entry.EntityKey);
        Assert.Equal(AuditActions.Print, entry.Action);
        Assert.Empty(_auditService.TenantEntries);
    }

    [Fact]
    public async Task Patch_WhenOnlyTitleChanges_ShouldPreserveExistingCompensationReference()
    {
        // Arrange
        var salaryTabulatorRepository = new TestSalaryTabulatorRepository();
        var profile = JobProfile.Create("JP-COMP", "Original title");
        profile.SetTenantId(_tenantId);
        profile.UpdateCore(
            "JP-COMP",
            "Original title",
            objective: "Objective",
            orgUnitId: 1,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsibilities",
            benefitsSummary: null,
            workingConditionSummary: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);
        profile.SetCompensationReference(99, salaryClassCatalogItem: null, "S1");

        var profileId = profile.PublicId;
        var salaryClassPublicId = Guid.NewGuid();
        var salaryLinePublicId = Guid.NewGuid();
        _profileRepository.Profiles[profileId] = profile;
        _profileRepository.CoreCompensations[profileId] = new JobProfileCompensationResponse(
            salaryClassPublicId,
            SalaryClassName: "Clase salarial",
            SalaryScaleCode: "S1",
            salaryLinePublicId,
            CurrencyCode: "USD",
            BaseAmount: 100_000m,
            MinAmount: 90_000m,
            MaxAmount: 110_000m,
            ResolvedEffectiveFromUtc: DateTime.UtcNow.Date,
            ResolvedEffectiveToUtc: null);

        var handler = new PatchJobProfileCommandHandler(
            _authService,
            _profileRepository,
            _catalogRepository,
            new TestInternalCatalogRepository(),
            _positionDescriptionCatalogRepository,
            salaryTabulatorRepository,
            _auditService,
            new TestJobProfileDateTimeProvider(DateTime.UtcNow),
            _tenantContext,
            _unitOfWork);

        var command = new PatchJobProfileCommand(
            profileId,
            [
                new JobProfilePatchOperation(
                    "replace",
                    "/title",
                    From: null,
                    JsonSerializer.SerializeToElement("Patched title")),
                new JobProfilePatchOperation(
                    "replace",
                    "/concurrencyToken",
                    From: null,
                    JsonSerializer.SerializeToElement(profile.ConcurrencyToken.ToString()))
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Patched title", profile.Title);
        Assert.Equal(99, profile.SalaryClassCatalogItemId);
        Assert.Equal("S1", profile.SalaryScaleCode);
        Assert.Equal("S1", profile.NormalizedSalaryScaleCode);
        Assert.Equal(0, salaryTabulatorRepository.GetLineByIdCalls);
        Assert.NotNull(result.Value.Compensation);
        Assert.Equal(salaryLinePublicId, result.Value.Compensation!.SalaryTabulatorLineId);
        Assert.Equal(2, _unitOfWork.SaveChangesCalls);
        Assert.True(_unitOfWork.Transaction.CommitCalled);
        Assert.False(_unitOfWork.Transaction.RollbackCalled);
    }
}
