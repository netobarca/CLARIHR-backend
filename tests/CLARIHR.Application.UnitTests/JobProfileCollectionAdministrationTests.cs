using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Common.Errors;
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
        Assert.Equal(2, _unitOfWork.SaveChangesCalls); // Once for entity, once for audit
    }
}
