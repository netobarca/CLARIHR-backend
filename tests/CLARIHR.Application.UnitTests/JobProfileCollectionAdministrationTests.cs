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
    private readonly TestInternalCatalogRepository _internalCatalogRepository = new();
    private readonly TestJobProfileAuthorizationService _authService = new();
    private readonly TestAuditService _auditService = new();
    private readonly TestUnitOfWork _unitOfWork = new();
    private readonly TestJobProfileCurrentUserService _currentUserService = new(Guid.NewGuid());
    private readonly TestJobProfileDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));
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
        _profileRepository.Responses[profileId] = new JobProfileResponse(profileId, _tenantId, "JP-001", "Title", JobProfileStatus.Draft, 1, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, [], [], [], [], [], null, [], [], [], profile.ConcurrencyToken, DateTime.UtcNow, null);

        var handler = new AddJobProfileRequirementCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository,
            _catalogRepository,
            _internalCatalogRepository,
            _currentUserService,
            _dateTimeProvider);

        var command = new AddJobProfileRequirementCommand(
            profileId,
            JobRequirementType.Experience,
            null,
            null,
            null,
            null,
            "3 years",
            1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(profile.Requirements);
        Assert.Equal("3 years", result.Value.Description);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        Assert.Equal(2, _unitOfWork.SaveChangesCalls); // Once for entity, once for audit
    }

    [Fact]
    public async Task UpdateRequirement_WhenTokenMismatch_ShouldReturnConflict()
    {
        // Arrange
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var requirement = JobProfileRequirement.Create(JobRequirementType.Experience, null, null, null, "3 years", 1);
        profile.AddRequirement(requirement);
        var profileId = profile.PublicId;
        
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileRequirementCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository,
            _catalogRepository,
            _internalCatalogRepository,
            _currentUserService,
            _dateTimeProvider);

        var command = new UpdateJobProfileRequirementCommand(
            profileId,
            requirement.PublicId,
            JobRequirementType.Experience,
            null,
            null,
            null,
            null,
            "4 years",
            1,
            Guid.NewGuid()); // Wrong token

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(JobProfileErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddBenefit_WhenProfileExists_ShouldReturnCreatedEntity()
    {
        // Arrange
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;

        _profileRepository.Profiles[profileId] = profile;

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
            1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Health Insurance", result.Value.Name);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var benefit = Assert.Single(profile.Benefits);
        Assert.Equal(benefit.PublicId, result.Value.Id);
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
            new RemoveJobProfileRequirementCommand(profileId, requirement.PublicId, requirement.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Requirements);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task GetRequirements_WhenProfileExists_ShouldReturnPagedRequirements()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddRequirement(JobProfileRequirement.Create(JobRequirementType.Experience, null, null, null, "3 years", 1));
        profile.AddRequirement(JobProfileRequirement.Create(JobRequirementType.Education, null, null, null, "Degree", 2));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileRequirementsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileRequirementsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var requirement = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Degree", requirement.Description);
        Assert.NotEqual(Guid.Empty, requirement.ConcurrencyToken);
    }

    [Fact]
    public async Task PatchRequirement_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var requirement = JobProfileRequirement.Create(JobRequirementType.Experience, null, null, null, "3 years", 1);
        profile.AddRequirement(requirement);
        var profileId = profile.PublicId;
        var token = requirement.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new PatchJobProfileRequirementCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository,
            _catalogRepository,
            _internalCatalogRepository,
            _currentUserService,
            _dateTimeProvider);

        var result = await handler.Handle(
            new PatchJobProfileRequirementCommand(
                profileId,
                requirement.PublicId,
                token,
                [
                    new JobProfileRequirementPatchOperation("replace", "/description", null, JsonSerializer.SerializeToElement("5 years"))
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("5 years", result.Value.Description);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task AddFunction_WhenProfileExists_ShouldReturnCreatedEntity()
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
            new AddJobProfileFunctionCommand(profileId, JobFunctionType.General, frequencyId, "Lead delivery", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lead delivery", result.Value.Description);
        Assert.Equal(JobFunctionType.General, result.Value.FunctionType);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var function = Assert.Single(profile.Functions);
        Assert.Equal(function.PublicId, result.Value.FunctionPublicId);
    }

    [Fact]
    public async Task UpdateFunction_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var function = JobProfileFunction.Create(JobFunctionType.General, null, "Lead delivery", 1);
        profile.AddFunction(function);
        var profileId = profile.PublicId;
        var token = function.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileFunctionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository);

        var result = await handler.Handle(
            new UpdateJobProfileFunctionCommand(profileId, function.PublicId, JobFunctionType.Specific, null, "Lead delivery roadmap", 2, token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lead delivery roadmap", result.Value.Description);
        Assert.Equal(JobFunctionType.Specific, result.Value.FunctionType);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateFunction_WhenTokenStale_ShouldFailWithConcurrencyConflict()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var function = JobProfileFunction.Create(JobFunctionType.General, null, "Lead delivery", 1);
        profile.AddFunction(function);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileFunctionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository);

        var result = await handler.Handle(
            new UpdateJobProfileFunctionCommand(profileId, function.PublicId, JobFunctionType.Specific, null, "Lead delivery roadmap", 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobProfileErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task PatchFunction_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var function = JobProfileFunction.Create(JobFunctionType.General, null, "Lead delivery", 1);
        profile.AddFunction(function);
        var profileId = profile.PublicId;
        var token = function.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new PatchJobProfileFunctionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _positionDescriptionCatalogRepository);

        var result = await handler.Handle(
            new PatchJobProfileFunctionCommand(
                profileId,
                function.PublicId,
                token,
                [
                    new JobProfileFunctionPatchOperation("replace", "/description", null, JsonSerializer.SerializeToElement("Lead delivery roadmap"))
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lead delivery roadmap", result.Value.Description);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task GetFunctions_WhenProfileExists_ShouldReturnPagedFunctionsWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddFunction(JobProfileFunction.Create(JobFunctionType.General, null, "Lead delivery", 1));
        profile.AddFunction(JobProfileFunction.Create(JobFunctionType.Specific, null, "Mentor team", 2));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileFunctionsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileFunctionsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var function = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Mentor team", function.Description);
        Assert.NotEqual(Guid.Empty, function.ConcurrencyToken);
    }

    [Fact]
    public async Task RemoveFunction_WhenTokenMatches_ShouldRemoveAndReturnParentConcurrencyToken()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var function = JobProfileFunction.Create(JobFunctionType.General, null, "Lead delivery", 1);
        profile.AddFunction(function);
        var profileId = profile.PublicId;
        var token = function.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new RemoveJobProfileFunctionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new RemoveJobProfileFunctionCommand(profileId, function.PublicId, token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Functions);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddRelation_WhenProfileExists_ShouldReturnCreatedEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileRelationCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new AddJobProfileRelationCommand(profileId, JobRelationType.Internal, null, "Gerente de Producto", "Coordina prioridades", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Gerente de Producto", result.Value.Counterpart);
        Assert.Equal(JobRelationType.Internal, result.Value.RelationType);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var relation = Assert.Single(profile.Relations);
        Assert.Equal(relation.PublicId, result.Value.RelationPublicId);
    }

    [Fact]
    public async Task UpdateRelation_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var relation = JobProfileRelation.Create(JobRelationType.Internal, null, null, "Gerente de Producto", "Coordina prioridades", 1);
        profile.AddRelation(relation);
        var profileId = profile.PublicId;
        var token = relation.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileRelationCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new UpdateJobProfileRelationCommand(profileId, relation.PublicId, JobRelationType.External, null, "Proveedores cloud", "Negociación de contratos", 2, token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Proveedores cloud", result.Value.Counterpart);
        Assert.Equal(JobRelationType.External, result.Value.RelationType);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateRelation_WhenTokenStale_ShouldFailWithConcurrencyConflict()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var relation = JobProfileRelation.Create(JobRelationType.Internal, null, null, "Gerente de Producto", null, 1);
        profile.AddRelation(relation);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileRelationCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new UpdateJobProfileRelationCommand(profileId, relation.PublicId, JobRelationType.External, null, "Proveedores cloud", null, 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobProfileErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task PatchRelation_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var relation = JobProfileRelation.Create(JobRelationType.Internal, null, null, "Gerente de Producto", null, 1);
        profile.AddRelation(relation);
        var profileId = profile.PublicId;
        var token = relation.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new PatchJobProfileRelationCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new PatchJobProfileRelationCommand(
                profileId,
                relation.PublicId,
                token,
                [
                    new JobProfileRelationPatchOperation("replace", "/counterpart", null, JsonSerializer.SerializeToElement("Gerente de Producto Senior"))
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Gerente de Producto Senior", result.Value.Counterpart);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task GetRelations_WhenProfileExists_ShouldReturnPagedRelationsWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddRelation(JobProfileRelation.Create(JobRelationType.Internal, null, null, "Gerente de Producto", null, 1));
        profile.AddRelation(JobProfileRelation.Create(JobRelationType.External, null, null, "Proveedor", null, 2));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileRelationsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileRelationsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var relation = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Proveedor", relation.Counterpart);
        Assert.NotEqual(Guid.Empty, relation.ConcurrencyToken);
    }

    [Fact]
    public async Task RemoveRelation_WhenTokenMatches_ShouldRemoveAndReturnParentConcurrencyToken()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var relation = JobProfileRelation.Create(JobRelationType.Internal, null, null, "Gerente de Producto", null, 1);
        profile.AddRelation(relation);
        var profileId = profile.PublicId;
        var token = relation.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new RemoveJobProfileRelationCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new RemoveJobProfileRelationCommand(profileId, relation.PublicId, token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Relations);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddTraining_WhenProfileExists_ShouldReturnCreatedEntity()
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
            new AddJobProfileTrainingCommand(profileId, null, "Leadership", "Notes", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Leadership", result.Value.Name);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var training = Assert.Single(profile.Trainings);
        Assert.Equal(training.PublicId, result.Value.Id);
    }

    [Fact]
    public async Task GetTrainings_WhenProfileExists_ShouldReturnPagedTrainingsWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddTraining(JobProfileTraining.Create(null, null, "Leadership", null, 1));
        profile.AddTraining(JobProfileTraining.Create(null, null, "Security", null, 2));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileTrainingsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileTrainingsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var training = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Security", training.Name);
        Assert.NotEqual(Guid.Empty, training.ConcurrencyToken);
    }

    [Fact]
    public async Task AddCompetency_WhenProfileExists_ShouldReturnCreatedEntity()
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
            new AddJobProfileCompetencyCommand(profileId, null, "Communication", "Advanced", "Notes", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Communication", result.Value.Name);
        Assert.Equal("Advanced", result.Value.ExpectedLevel);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var competency = Assert.Single(profile.Competencies);
        Assert.Equal(competency.PublicId, result.Value.CompetencyPublicId);
    }

    [Fact]
    public async Task UpdateCompetency_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var competency = JobProfileCompetency.Create(null, null, "Communication", "Advanced", "Notes", 1);
        profile.AddCompetency(competency);
        var profileId = profile.PublicId;
        var token = competency.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileCompetencyCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new UpdateJobProfileCompetencyCommand(profileId, competency.PublicId, null, "Communication Plus", "Expert", "Updated", 2, token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Communication Plus", result.Value.Name);
        Assert.Equal("Expert", result.Value.ExpectedLevel);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateCompetency_WhenTokenStale_ShouldFailWithConcurrencyConflict()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var competency = JobProfileCompetency.Create(null, null, "Communication", null, null, 1);
        profile.AddCompetency(competency);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new UpdateJobProfileCompetencyCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new UpdateJobProfileCompetencyCommand(profileId, competency.PublicId, null, "Communication Plus", null, null, 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobProfileErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task PatchCompetency_WhenTokenMatches_ShouldUpdateAndReturnEntity()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var competency = JobProfileCompetency.Create(null, null, "Communication", null, null, 1);
        profile.AddCompetency(competency);
        var profileId = profile.PublicId;
        var token = competency.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new PatchJobProfileCompetencyCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork,
            _catalogRepository);

        var result = await handler.Handle(
            new PatchJobProfileCompetencyCommand(
                profileId,
                competency.PublicId,
                token,
                [
                    new JobProfileCompetencyPatchOperation("replace", "/expectedLevel", null, JsonSerializer.SerializeToElement("Expert"))
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Expert", result.Value.ExpectedLevel);
        Assert.NotEqual(token, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task GetCompetencies_WhenProfileExists_ShouldReturnPagedCompetenciesWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddCompetency(JobProfileCompetency.Create(null, null, "Communication", null, null, 1));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileCompetenciesQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        profile.AddCompetency(JobProfileCompetency.Create(null, null, "Leadership", null, null, 2));

        var result = await handler.Handle(new GetJobProfileCompetenciesQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var competency = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Leadership", competency.Name);
        Assert.NotEqual(Guid.Empty, competency.ConcurrencyToken);
    }

    [Fact]
    public async Task RemoveCompetency_WhenTokenMatches_ShouldRemoveAndReturnParentConcurrencyToken()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        var competency = JobProfileCompetency.Create(null, null, "Communication", null, null, 1);
        profile.AddCompetency(competency);
        var profileId = profile.PublicId;
        var token = competency.ConcurrencyToken;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new RemoveJobProfileCompetencyCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new RemoveJobProfileCompetencyCommand(profileId, competency.PublicId, token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Competencies);
        Assert.Equal(profile.ConcurrencyToken, result.Value.ParentConcurrencyToken);
    }

    [Fact]
    public async Task AddDependentPosition_WhenProfileExists_ShouldReturnCreatedEntity()
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
            new AddJobProfileDependentPositionCommand(profileId, dependentProfileId, 2, "Notes"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Quantity);
        Assert.Equal("JP-REF", result.Value.DependentJobProfileCode);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var dependentPosition = Assert.Single(profile.DependentPositions);
        Assert.Equal(dependentPosition.PublicId, result.Value.Id);
    }

    [Fact]
    public async Task AddDependentPosition_WhenDependentProfileIsReportsTo_ShouldReturnDependencyCycle()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.UpdateCore(
            "JP-001",
            "Title",
            objective: "Objective",
            orgUnitId: 1,
            reportsToJobProfileId: 1,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsibilities",
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new AddJobProfileDependentPositionCommandHandler(
            _authService,
            _profileRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var result = await handler.Handle(
            new AddJobProfileDependentPositionCommand(profileId, Guid.NewGuid(), 1, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobProfileErrors.DependencyCycle.Code, result.Error.Code);
        Assert.Empty(profile.DependentPositions);
    }

    [Fact]
    public async Task GetDependentPositions_WhenProfileExists_ShouldReturnPagedDependentPositionsWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddDependentPosition(JobProfileDependentPosition.Create(1, 1, "First"));
        profile.AddDependentPosition(JobProfileDependentPosition.Create(2, 2, "Second"));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileDependentPositionsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileDependentPositionsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal(2, position.Quantity);
        Assert.NotEqual(Guid.Empty, position.ConcurrencyToken);
    }

    [Fact]
    public async Task AddWorkingCondition_WhenProfileExists_ShouldReturnCreatedEntity()
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
            _positionDescriptionCatalogRepository);

        var result = await handler.Handle(
            new AddJobProfileWorkingConditionCommand(profileId, null, null, "Remote", "Notes", 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Remote", result.Value.Name);
        Assert.NotEqual(Guid.Empty, result.Value.ConcurrencyToken);
        var workingCondition = Assert.Single(profile.WorkingConditions);
        Assert.Equal(workingCondition.PublicId, result.Value.Id);
    }

    [Fact]
    public async Task GetBenefits_WhenProfileExists_ShouldReturnPagedBenefitsWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddBenefit(JobProfileBenefit.Create(null, null, "Health", null, 1));
        profile.AddBenefit(JobProfileBenefit.Create(null, null, "Transport", null, 2));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileBenefitsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileBenefitsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var benefit = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Transport", benefit.Name);
        Assert.NotEqual(Guid.Empty, benefit.ConcurrencyToken);
    }

    [Fact]
    public async Task GetWorkingConditions_WhenProfileExists_ShouldReturnPagedWorkingConditionsWithConcurrencyTokens()
    {
        var profile = JobProfile.Create("JP-001", "Title");
        profile.SetTenantId(_tenantId);
        profile.AddWorkingCondition(JobProfileWorkingCondition.Create(null, null, "Hybrid", null, 1));
        profile.AddWorkingCondition(JobProfileWorkingCondition.Create(null, null, "Remote", null, 2));
        var profileId = profile.PublicId;
        _profileRepository.Profiles[profileId] = profile;

        var handler = new GetJobProfileWorkingConditionsQueryHandler(
            _authService,
            _profileRepository,
            _tenantContext);

        var result = await handler.Handle(new GetJobProfileWorkingConditionsQuery(profileId, PageNumber: 2, PageSize: 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var condition = Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal("Remote", condition.Name);
        Assert.NotEqual(Guid.Empty, condition.ConcurrencyToken);
    }

    [Fact]
    public async Task Patch_WhenOnlyTitleChanges_ShouldPreserveExistingCompensationReference()
    {
        // Arrange
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
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);
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
            _positionDescriptionCatalogRepository,
            _auditService,
            _tenantContext,
            _unitOfWork);

        var command = new PatchJobProfileCommand(
            profileId,
            profile.ConcurrencyToken,
            [
                new JobProfilePatchOperation(
                    "replace",
                    "/title",
                    From: null,
                    JsonSerializer.SerializeToElement("Patched title"))
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Patched title", profile.Title);
        Assert.NotNull(result.Value.Compensation);
        Assert.Equal(salaryLinePublicId, result.Value.Compensation!.SalaryTabulatorLineId);
        Assert.Equal(2, _unitOfWork.SaveChangesCalls);
        Assert.True(_unitOfWork.Transaction.CommitCalled);
        Assert.False(_unitOfWork.Transaction.RollbackCalled);
    }
}
