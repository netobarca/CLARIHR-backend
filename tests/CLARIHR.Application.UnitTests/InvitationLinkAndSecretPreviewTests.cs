using CLARIHR.Infrastructure.Companies;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Invitations used to travel as a bare token with no link at all, because "delivery" was a log line.
/// These pin the URL contract the front-end depends on, and the masking that keeps single-use
/// credentials out of the logs.
/// </summary>
public sealed class InvitationLinkAndSecretPreviewTests
{
    [Fact]
    public void Build_ShouldAppendTheTokenToTheConfiguredFrontendUrl()
    {
        var builder = CreateBuilder("https://app.clarihr.test/accept-invitation");

        Assert.Equal(
            "https://app.clarihr.test/accept-invitation?token=raw-token",
            builder.Build("raw-token"));
    }

    [Fact]
    public void Build_ShouldPreserveAnExistingQueryString()
    {
        // A deployment may point the link at a locale- or tenant-qualified landing page.
        var builder = CreateBuilder("https://app.clarihr.test/accept-invitation?lang=es");

        Assert.Equal(
            "https://app.clarihr.test/accept-invitation?lang=es&token=raw-token",
            builder.Build("raw-token"));
    }

    [Fact]
    public void Build_ShouldEncodeTokensContainingUrlReservedCharacters()
    {
        var builder = CreateBuilder("https://app.clarihr.test/accept-invitation");

        Assert.Equal(
            "https://app.clarihr.test/accept-invitation?token=a%2Bb%2Fc%3D",
            builder.Build("a+b/c="));
    }

    [Fact]
    public void Build_WhenUrlIsNotConfigured_ShouldFailWithTheConfigurationKey()
    {
        var builder = CreateBuilder("   ");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build("raw-token"));

        Assert.Contains("Authentication:Invitation", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("short", "****")]
    [InlineData("abcdefgh", "****")]
    [InlineData("abcdefghij", "abcd...ghij")]
    public void OfToken_ShouldMaskEverythingButTheEdges(string token, string expected) =>
        Assert.Equal(expected, SecretPreview.OfToken(token));

    [Fact]
    public void OfUrlToken_ShouldMaskTheTokenAndKeepTheDestinationDiagnosable()
    {
        // The host and path stay readable on purpose: a link landing on the wrong front-end is a
        // common misconfiguration and has to be visible in the logs.
        Assert.Equal(
            "https://app.clarihr.test/reset-password?token=abcd...wxyz",
            SecretPreview.OfUrlToken("https://app.clarihr.test/reset-password?token=abcdefghijklmnwxyz"));
    }

    [Fact]
    public void OfUrlToken_ShouldLeaveNonSecretParametersIntact()
    {
        Assert.Equal(
            "https://app.clarihr.test/verify-email?lang=es&token=abcd...wxyz",
            SecretPreview.OfUrlToken("https://app.clarihr.test/verify-email?lang=es&token=abcdefghijklmnwxyz"));
    }

    private static InvitationLinkBuilder CreateBuilder(string frontendAcceptUrl) =>
        new(Options.Create(new InvitationOptions { FrontendAcceptUrl = frontendAcceptUrl }));
}
