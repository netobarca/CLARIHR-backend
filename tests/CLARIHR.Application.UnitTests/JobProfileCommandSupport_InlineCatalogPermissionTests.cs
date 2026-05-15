using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit tests for <see cref="JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync"/>.
///
/// Verifies the security contract for AllowInlineCatalogCreate:
/// - The flag only has effect when inline catalog references are present in the request.
/// - Even when the flag is true, the caller must hold JobCatalogs.Admin.
/// - When the caller lacks that permission, the response is Forbidden with JOB_CATALOG_INLINE_CREATE_FORBIDDEN.
/// </summary>
public sealed class JobProfileCommandSupport_InlineCatalogPermissionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // -----------------------------------------------------------------------
    // Scenario 1: No inline catalog references — flag and permission are irrelevant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NoInlineRefs_FlagFalse_ReturnsSuccessFalse()
    {
        // Arrange: no inline references, flag is false — nothing to evaluate
        var authService = AuthServiceThatAllowsCatalogs();

        // Act
        var result = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            allowInlineCatalogCreate: false,
            inlineCatalogRequested: false,
            TenantId,
            authService,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task NoInlineRefs_FlagTrue_ReturnsSuccessFalse_PermissionNotEvaluated()
    {
        // Arrange: flag is true but no inline references — permission check must be skipped
        var authService = AuthServiceThatDeniedCatalogs();

        // Act
        var result = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            allowInlineCatalogCreate: true,
            inlineCatalogRequested: false,
            TenantId,
            authService,
            CancellationToken.None);

        // Assert: even with a catalog-denying service, result is Success(false) because there is nothing to create
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    // -----------------------------------------------------------------------
    // Scenario 2: Inline references present, flag is false → forbidden (intent not declared)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WithInlineRefs_FlagFalse_ReturnsInlineForbidden()
    {
        // Arrange: caller sent inline references but did not set the flag to true
        var authService = AuthServiceThatAllowsCatalogs();

        // Act
        var result = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            allowInlineCatalogCreate: false,
            inlineCatalogRequested: true,
            TenantId,
            authService,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(JobProfileErrors.InlineCatalogCreateForbidden.Code, result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    // -----------------------------------------------------------------------
    // Scenario 3: Inline references + flag true, but caller lacks JobCatalogs.Admin
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WithInlineRefs_FlagTrue_NoCatalogPermission_ReturnsInlineForbidden()
    {
        // Arrange: flag is true, references exist, but caller does NOT hold JobCatalogs.Admin
        var authService = AuthServiceThatDeniedCatalogs();

        // Act
        var result = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            allowInlineCatalogCreate: true,
            inlineCatalogRequested: true,
            TenantId,
            authService,
            CancellationToken.None);

        // Assert: the backend maps the catalog Forbidden to InlineCatalogCreateForbidden (no information leak)
        Assert.True(result.IsFailure);
        Assert.Equal(JobProfileErrors.InlineCatalogCreateForbidden.Code, result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    // -----------------------------------------------------------------------
    // Scenario 4: Full happy path — flag true, references present, permission granted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WithInlineRefs_FlagTrue_WithCatalogPermission_ReturnsSuccessTrue()
    {
        // Arrange: caller holds both JobProfiles.Admin and JobCatalogs.Admin
        var authService = AuthServiceThatAllowsCatalogs();

        // Act
        var result = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            allowInlineCatalogCreate: true,
            inlineCatalogRequested: true,
            TenantId,
            authService,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    // -----------------------------------------------------------------------
    // Test double factories
    // -----------------------------------------------------------------------

    private static IJobProfileAuthorizationService AuthServiceThatAllowsCatalogs() =>
        new StubJobProfileAuthorizationService(canManageCatalogs: true);

    private static IJobProfileAuthorizationService AuthServiceThatDeniedCatalogs() =>
        new StubJobProfileAuthorizationService(canManageCatalogs: false);

    private sealed class StubJobProfileAuthorizationService(bool canManageCatalogs) : IJobProfileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageProfilesAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageCatalogsAsync(Guid companyId, CancellationToken cancellationToken) =>
            canManageCatalogs
                ? Task.FromResult(Result.Success())
                : Task.FromResult(Result.Failure(JobProfileErrors.Forbidden));

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TenantMismatch", "Tenant mismatch.", ErrorType.Forbidden);
    }
}
