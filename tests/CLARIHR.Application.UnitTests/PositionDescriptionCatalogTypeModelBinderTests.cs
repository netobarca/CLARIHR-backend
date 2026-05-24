using CLARIHR.Api.Common.Binders;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §X-LOG (🟡 hardening). The catalog-type model binder logs the raw client-supplied slug
/// at Warning level when it fails to resolve. Without a bound, a single bad request could
/// write an arbitrarily long attacker-controlled string to the logs (log-volume/noise
/// vector). These tests pin the truncation that caps the logged slug at
/// <c>MaxLoggedSlugLength</c> (64) with an explicit ellipsis marker. The binder type is
/// internal (visible via InternalsVisibleTo), so the hardening is exercised directly
/// without pulling MVC model-binding plumbing into this non-Web unit project.
/// </summary>
public sealed class PositionDescriptionCatalogTypeModelBinderTests
{
    [Fact]
    public void Truncate_LeavesShortSlugVerbatim()
    {
        const string slug = "position-function-types";
        Assert.Equal(slug, PositionDescriptionCatalogTypeModelBinder.Truncate(slug));
    }

    [Fact]
    public void Truncate_LeavesBoundaryLengthSlugVerbatim()
    {
        var slug = new string('a', 64);
        Assert.Equal(slug, PositionDescriptionCatalogTypeModelBinder.Truncate(slug));
    }

    [Fact]
    public void Truncate_CapsOverlongSlugAndAppendsEllipsis()
    {
        var slug = new string('x', 5_000);

        var truncated = PositionDescriptionCatalogTypeModelBinder.Truncate(slug);

        Assert.NotNull(truncated);
        Assert.Equal(65, truncated!.Length); // 64 chars + the '…' marker
        Assert.Equal(new string('x', 64) + "…", truncated);
        Assert.EndsWith("…", truncated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Truncate_PassesThroughNullOrEmpty(string? slug)
    {
        Assert.Equal(slug, PositionDescriptionCatalogTypeModelBinder.Truncate(slug));
    }
}
