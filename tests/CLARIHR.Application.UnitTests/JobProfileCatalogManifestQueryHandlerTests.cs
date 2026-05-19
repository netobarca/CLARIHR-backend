using CLARIHR.Application.Abstractions.CatalogTypes;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using Xunit;

namespace CLARIHR.Application.UnitTests;

public class JobProfileCatalogManifestQueryHandlerTests
{
    private static GetJobProfileCatalogManifestQueryHandler CreateHandler(
        IReadOnlyList<CatalogTypeDescriptorLookup> registry,
        Guid? tenantId = null) =>
        new(
            new FakeCatalogTypeDescriptorRepository(registry),
            new FakeTenantContext(tenantId));

    private static IReadOnlyList<CatalogTypeDescriptorLookup> RegistryFromCanonical(
        Func<string, bool> isActiveByCode) =>
        JobProfileCatalogBindingMap.CanonicalTypes
            .Select(definition => new CatalogTypeDescriptorLookup(
                definition.RegistryCode.Trim().ToUpperInvariant(),
                definition.DisplayName,
                isActiveByCode(definition.RegistryCode)))
            .ToList();

    [Fact]
    public async Task Handle_ShouldGroupByEverySubResource_InCanonicalOrder()
    {
        var handler = CreateHandler(RegistryFromCanonical(_ => true));

        var result = await handler.Handle(new GetJobProfileCatalogManifestQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            JobProfileCatalogBindingMap.SubResources,
            result.Value.SubResources.Select(group => group.SubResource).ToArray());
    }

    [Fact]
    public async Task Handle_ShouldEmitEmptyFields_ForCatalogLessSubResources()
    {
        var handler = CreateHandler(RegistryFromCanonical(_ => true));

        var result = await handler.Handle(new GetJobProfileCatalogManifestQuery(), CancellationToken.None);

        foreach (var subResource in new[] { "dependentPosition", "compensation" })
        {
            var group = result.Value.SubResources.Single(g => g.SubResource == subResource);
            Assert.Empty(group.Fields);
        }
    }

    [Fact]
    public async Task Handle_ShouldProjectSlugFamilyAndEndpoint_PerCatalogFamily()
    {
        var handler = CreateHandler(RegistryFromCanonical(_ => true));

        var result = await handler.Handle(new GetJobProfileCatalogManifestQuery(), CancellationToken.None);

        var jobProfile = result.Value.SubResources.Single(g => g.SubResource == "jobProfile");
        var strategic = jobProfile.Fields.Single(f => f.FieldName == "strategicObjectiveCatalogItemPublicId");
        Assert.Equal("strategic-objectives", strategic.Slug);
        Assert.Equal(CatalogFamilies.PositionDescription, strategic.Family);
        Assert.Equal(
            "/api/v1/companies/{companyId}/position-description-catalogs/strategic-objectives/items",
            strategic.ApiEndpointTemplate);
        Assert.True(strategic.IsActive);

        var competency = result.Value.SubResources
            .Single(g => g.SubResource == "competency")
            .Fields.Single();
        Assert.Equal("Competency", competency.Slug);
        Assert.Equal(CatalogFamilies.JobCatalog, competency.Family);
        Assert.Equal("/api/v1/companies/{companyId}/job-catalogs/Competency", competency.ApiEndpointTemplate);

        var internalEducation = result.Value.SubResources
            .Single(g => g.SubResource == "requirement")
            .Fields.Single(f => f.Slug == "job-profile.requirements.education");
        Assert.Equal(CatalogFamilies.Internal, internalEducation.Family);
        Assert.Equal(
            "/api/account/internal-catalogs/job-profile.requirements.education/values",
            internalEducation.ApiEndpointTemplate);
    }

    [Fact]
    public async Task Handle_ShouldKeepCompanyIdPlaceholder_WhenNoTenantOnRequest()
    {
        var handler = CreateHandler(RegistryFromCanonical(_ => true), tenantId: null);

        var result = await handler.Handle(new GetJobProfileCatalogManifestQuery(), CancellationToken.None);

        var strategic = result.Value.SubResources
            .Single(g => g.SubResource == "jobProfile")
            .Fields.Single(f => f.FieldName == "strategicObjectiveCatalogItemPublicId");
        Assert.Equal(
            "/api/v1/companies/{companyId}/position-description-catalogs/strategic-objectives/items",
            strategic.ApiEndpointTemplate);
    }

    [Fact]
    public async Task Handle_ShouldResolveCompanyIdPlaceholder_WhenTenantPresent()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(RegistryFromCanonical(_ => true), tenantId);

        var result = await handler.Handle(new GetJobProfileCatalogManifestQuery(), CancellationToken.None);

        var jobProfile = result.Value.SubResources.Single(g => g.SubResource == "jobProfile");
        var strategic = jobProfile.Fields.Single(f => f.FieldName == "strategicObjectiveCatalogItemPublicId");
        Assert.Equal(
            $"/api/v1/companies/{tenantId}/position-description-catalogs/strategic-objectives/items",
            strategic.ApiEndpointTemplate);

        var competency = result.Value.SubResources
            .Single(g => g.SubResource == "competency")
            .Fields.Single();
        Assert.Equal($"/api/v1/companies/{tenantId}/job-catalogs/Competency", competency.ApiEndpointTemplate);

        // Internal family has no {companyId} placeholder — must be left untouched.
        var internalEducation = result.Value.SubResources
            .Single(g => g.SubResource == "requirement")
            .Fields.Single(f => f.Slug == "job-profile.requirements.education");
        Assert.Equal(
            "/api/account/internal-catalogs/job-profile.requirements.education/values",
            internalEducation.ApiEndpointTemplate);
        Assert.DoesNotContain("{companyId}", strategic.ApiEndpointTemplate);
    }

    [Fact]
    public async Task Handle_ShouldReportIsActiveFalse_WhenRegistryRowInactiveOrMissing()
    {
        // "StrategicObjective" inactive in the registry; "WorkEquipment" absent entirely.
        var registry = JobProfileCatalogBindingMap.CanonicalTypes
            .Where(definition => definition.RegistryCode != "WorkEquipment")
            .Select(definition => new CatalogTypeDescriptorLookup(
                definition.RegistryCode.Trim().ToUpperInvariant(),
                definition.DisplayName,
                IsActive: definition.RegistryCode != "StrategicObjective"))
            .ToList();

        var handler = CreateHandler(registry);

        var result = await handler.Handle(new GetJobProfileCatalogManifestQuery(), CancellationToken.None);

        var jobProfile = result.Value.SubResources.Single(g => g.SubResource == "jobProfile");
        Assert.False(jobProfile.Fields.Single(f => f.FieldName == "strategicObjectiveCatalogItemPublicId").IsActive);
        Assert.False(jobProfile.Fields.Single(f => f.FieldName == "assignedWorkEquipmentCatalogItemPublicId").IsActive);
    }

    private sealed class FakeCatalogTypeDescriptorRepository(
        IReadOnlyList<CatalogTypeDescriptorLookup> registry)
        : ICatalogTypeDescriptorRepository
    {
        public Task<IReadOnlyList<CatalogTypeDescriptorLookup>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(registry);

        public void Add(CLARIHR.Domain.CatalogTypes.CatalogTypeDescriptor item) =>
            throw new NotSupportedException();

        public Task<CLARIHR.Domain.CatalogTypes.CatalogTypeDescriptor?> GetByIdAsync(
            Guid publicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CodeExistsAsync(
            string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<JobProfileCatalogTypeResponse>> SearchAsync(
            bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<JobProfileCatalogTypeResponse?> GetResponseByIdAsync(
            Guid publicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Invalidate() => throw new NotSupportedException();
    }

    private sealed class FakeTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }
}
