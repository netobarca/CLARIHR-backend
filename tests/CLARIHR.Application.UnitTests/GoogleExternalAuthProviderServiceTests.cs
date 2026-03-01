using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Auth;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

public sealed class GoogleExternalAuthProviderServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenProviderIsNotGoogle_ShouldReturnValidationError()
    {
        var service = CreateService(new FakeGoogleIdTokenValidator(), clientId: "client-id");

        var result = await service.ValidateAsync(AuthProvider.Microsoft, "token", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(AuthErrors.ExternalProviderNotSupported.Code, result.Error.Code);
    }

    [Fact]
    public async Task ValidateAsync_WhenClientIdIsMissing_ShouldReturnUnexpectedError()
    {
        var service = CreateService(new FakeGoogleIdTokenValidator(), clientId: null);

        var result = await service.ValidateAsync(AuthProvider.Google, "token", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unexpected, result.Error.Type);
        Assert.Equal(AuthErrors.ExternalProviderConfigurationInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenIsInvalid_ShouldReturnUnauthorized()
    {
        var service = CreateService(new FakeGoogleIdTokenValidator());

        var result = await service.ValidateAsync(AuthProvider.Google, "token", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal(AuthErrors.ExternalTokenInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task ValidateAsync_WhenEmailIsVerifiedGmail_ShouldAllowAutoLink()
    {
        var service = CreateService(new FakeGoogleIdTokenValidator(
            new GoogleIdTokenValidationResult(
                Subject: "google-123",
                Email: " ana@gmail.com ",
                EmailVerified: true,
                GivenName: "Ana",
                FamilyName: "Mendoza",
                Name: "Ana Mendoza",
                HostedDomain: null)));

        var result = await service.ValidateAsync(AuthProvider.Google, "token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ana@gmail.com", result.Value.Email);
        Assert.Equal("Ana", result.Value.FirstName);
        Assert.Equal("Mendoza", result.Value.LastName);
        Assert.True(result.Value.CanAutoLinkByEmail);
    }

    [Fact]
    public async Task ValidateAsync_WhenEmailIsVerifiedButNotAuthoritative_ShouldDisallowAutoLink()
    {
        var service = CreateService(new FakeGoogleIdTokenValidator(
            new GoogleIdTokenValidationResult(
                Subject: "google-456",
                Email: "luisa@example.com",
                EmailVerified: true,
                GivenName: null,
                FamilyName: null,
                Name: "Luisa Martinez",
                HostedDomain: null)));

        var result = await service.ValidateAsync(AuthProvider.Google, "token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Luisa", result.Value.FirstName);
        Assert.Equal("Martinez", result.Value.LastName);
        Assert.False(result.Value.CanAutoLinkByEmail);
    }

    [Fact]
    public async Task ValidateAsync_WhenWorkspaceDomainIsPresent_ShouldAllowAutoLink()
    {
        var service = CreateService(new FakeGoogleIdTokenValidator(
            new GoogleIdTokenValidationResult(
                Subject: "google-789",
                Email: "sara@company.com",
                EmailVerified: true,
                GivenName: "Sara",
                FamilyName: null,
                Name: "Sara Company",
                HostedDomain: "company.com")));

        var result = await service.ValidateAsync(AuthProvider.Google, "token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.CanAutoLinkByEmail);
        Assert.Equal("Company", result.Value.LastName);
    }

    private static GoogleExternalAuthProviderService CreateService(
        IGoogleIdTokenValidator validator,
        string? clientId = "client-id") =>
        new(
            validator,
            Options.Create(new GoogleAuthOptions
            {
                ClientId = clientId
            }));

    private sealed class FakeGoogleIdTokenValidator(
        GoogleIdTokenValidationResult? validationResult = null) : IGoogleIdTokenValidator
    {
        public Task<GoogleIdTokenValidationResult?> ValidateAsync(
            string idToken,
            string clientId,
            CancellationToken cancellationToken) =>
            Task.FromResult(validationResult);
    }
}
