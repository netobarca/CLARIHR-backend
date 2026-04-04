using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.InternalCatalogs;
using CLARIHR.Application.Features.InternalCatalogs.Common;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class InternalCatalogAdministrationTests
{
    [Fact]
    public void NormalizeValue_ShouldStripDiacriticsPunctuationAndCollapseWhitespace()
    {
        var normalized = InternalCatalogValue.InternalCatalogNormalization.NormalizeValue("  Azure,   IA  Fundaméntals!  ");

        Assert.Equal("AZURE IA FUNDAMENTALS", normalized);
    }

    [Fact]
    public void Registry_ShouldExposeExpectedJobProfileRequirementDefinitions()
    {
        Assert.True(InternalCatalogRegistry.TryGetRequirementDefinition(JobRequirementType.Certification, out var certification));
        Assert.NotNull(certification);
        Assert.Equal(InternalCatalogRenderType.Search, certification!.RenderType);
        Assert.Equal("job-profile.requirements.certification", certification.CatalogKey);
        Assert.True(certification.AllowCreate);

        Assert.True(InternalCatalogRegistry.TryGetRequirementDefinition(JobRequirementType.Other, out var other));
        Assert.NotNull(other);
        Assert.Equal(InternalCatalogRenderType.FreeText, other!.RenderType);
        Assert.Null(other.CatalogKey);
    }

    [Fact]
    public async Task ResolveForCreateAsync_WhenExactMatchExists_ShouldReuseExistingValue()
    {
        var repository = new FakeInternalCatalogRepository();
        var existing = InternalCatalogValue.Create(
            "job-profile.requirements.certification",
            "Azure AI Fundamentals",
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        repository.Values.Add(existing);

        var result = await InternalCatalogValueResolver.ResolveForCreateAsync(
            "job-profile.requirements.certification",
            "Azure AI Fundamentals",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            repository,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InternalCatalogCreateOutcome.ReusedExact, result.Value.Outcome);
        Assert.Same(existing, result.Value.ExistingValue);
        Assert.Empty(result.Value.Suggestions);
    }

    [Fact]
    public async Task ResolveForCreateAsync_WhenSimilarValueExists_ShouldRejectAndUse090Threshold()
    {
        var repository = new FakeInternalCatalogRepository
        {
            SimilarMatches =
            [
                new InternalCatalogSearchResult(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    "Azure AI Fundamental",
                    0.94d,
                    IsExactMatch: false,
                    IsPrefixMatch: true,
                    UsageCount: 7)
            ]
        };

        var result = await InternalCatalogValueResolver.ResolveForCreateAsync(
            "job-profile.requirements.certification",
            "Azure AI Fundamentals",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            repository,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InternalCatalogCreateOutcome.RejectedSimilar, result.Value.Outcome);
        Assert.Equal(0.90d, repository.LastSimilarityThreshold);
        Assert.Single(result.Value.Suggestions);
        Assert.Equal("Azure AI Fundamental", result.Value.Suggestions.Single().Value);
    }

    [Fact]
    public async Task SearchQueryHandler_ShouldUse070ThresholdAndReturnMatches()
    {
        var repository = new FakeInternalCatalogRepository
        {
            SearchMatches =
            [
                new InternalCatalogSearchResult(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    "Azure AI Fundamentals",
                    0.88d,
                    IsExactMatch: false,
                    IsPrefixMatch: true,
                    UsageCount: 4)
            ]
        };
        var currentUserService = new FakeCurrentUserService(isAuthenticated: true);
        var handler = new SearchInternalCatalogValuesQueryHandler(currentUserService, repository);

        var result = await handler.Handle(
            new SearchInternalCatalogValuesQuery("job-profile.requirements.certification", "Azure AI Fundamentals"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.70d, repository.LastSearchThreshold);
        var match = Assert.Single(result.Value);
        Assert.Equal("Azure AI Fundamentals", match.Value);
        Assert.Equal(0.88d, match.Score);
    }

    [Fact]
    public async Task ResolveForUsageAsync_WhenSimilarValueExists_ShouldReuseExistingValueAndRegisterUsage()
    {
        var existing = InternalCatalogValue.Create(
            "job-profile.requirements.knowledge",
            "Machine Learning",
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var repository = new FakeInternalCatalogRepository
        {
            SimilarMatches =
            [
                new InternalCatalogSearchResult(
                    existing.PublicId,
                    existing.Value,
                    0.93d,
                    IsExactMatch: false,
                    IsPrefixMatch: true,
                    UsageCount: 1)
            ]
        };
        repository.Values.Add(existing);
        var dateTimeProvider = new FixedDateTimeProvider(DateTime.Parse("2026-04-03T18:30:00Z").ToUniversalTime());

        var result = await InternalCatalogValueResolver.ResolveForUsageAsync(
            "job-profile.requirements.knowledge",
            "Machine Learnin",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            repository,
            dateTimeProvider,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InternalCatalogUsageOutcome.ReusedSimilar, result.Value.Outcome);
        Assert.Equal("Machine Learning", result.Value.ResolvedValue);
        Assert.Equal(1, existing.UsageCount);
        Assert.Equal(dateTimeProvider.UtcNow, existing.LastUsedAtUtc);
        Assert.Equal(0.90d, repository.LastSimilarityThreshold);
    }

    [Fact]
    public async Task SearchQueryHandler_WhenUserIsUnauthenticated_ShouldReturnUnauthorizedFailure()
    {
        var handler = new SearchInternalCatalogValuesQueryHandler(new FakeCurrentUserService(isAuthenticated: false), new FakeInternalCatalogRepository());

        var result = await handler.Handle(
            new SearchInternalCatalogValuesQuery("job-profile.requirements.certification", "Azure"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    private sealed class FakeInternalCatalogRepository : IInternalCatalogRepository
    {
        public List<InternalCatalogValue> Values { get; } = [];

        public IReadOnlyCollection<InternalCatalogSearchResult> SearchMatches { get; init; } = [];

        public IReadOnlyCollection<InternalCatalogSearchResult> SimilarMatches { get; init; } = [];

        public double? LastSearchThreshold { get; private set; }

        public double? LastSimilarityThreshold { get; private set; }

        public void Add(InternalCatalogValue value) => Values.Add(value);

        public Task<InternalCatalogValue?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InternalCatalogValue?>(Values.SingleOrDefault(value => value.PublicId == publicId));

        public Task<InternalCatalogValue?> FindActiveByExactValueAsync(
            string catalogKey,
            string normalizedValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<InternalCatalogValue?>(
                Values.SingleOrDefault(value =>
                    value.IsActive &&
                    string.Equals(value.CatalogKey, catalogKey, StringComparison.Ordinal) &&
                    string.Equals(value.NormalizedValue, normalizedValue, StringComparison.Ordinal)));

        public Task<IReadOnlyCollection<InternalCatalogSearchResult>> SearchAsync(
            string catalogKey,
            string normalizedValue,
            int limit,
            double minimumScore,
            CancellationToken cancellationToken = default)
        {
            LastSearchThreshold = minimumScore;
            return Task.FromResult(SearchMatches.Take(limit).ToArray() as IReadOnlyCollection<InternalCatalogSearchResult>);
        }

        public Task<IReadOnlyCollection<InternalCatalogSearchResult>> FindSimilarAsync(
            string catalogKey,
            string normalizedValue,
            int limit,
            double minimumScore,
            CancellationToken cancellationToken = default)
        {
            LastSimilarityThreshold = minimumScore;
            return Task.FromResult(SimilarMatches.Take(limit).ToArray() as IReadOnlyCollection<InternalCatalogSearchResult>);
        }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
            UserId = isAuthenticated ? Guid.NewGuid().ToString() : null;
        }

        public bool IsAuthenticated { get; }

        public string? UserId { get; }

        public IReadOnlyCollection<string> Roles { get; } = [];

        public IReadOnlyCollection<string> Permissions { get; } = [];
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
