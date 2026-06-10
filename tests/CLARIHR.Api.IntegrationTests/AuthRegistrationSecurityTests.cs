using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

public sealed class AuthRegistrationSecurityTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task Register_WithPredictablePassword_ShouldReturnValidationProblem()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Ana",
            lastName = "Mendoza",
            email = "ana.mendoza@clarihr.test",
            password = "Pass2019!",
            country = "SV",
            source = "integration-tests"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("common.validation", document.RootElement.GetProperty("code").GetString());

        var errors = document.RootElement.GetProperty("errors");
        var passwordErrors = errors.TryGetProperty("Password", out var explicitPasswordErrors)
            ? explicitPasswordErrors
            : errors.GetProperty("password");

        Assert.Contains(
            passwordErrors.EnumerateArray().Select(static item => item.GetString()),
            static message => message is not null &&
                              message.Contains("at least 12 characters", StringComparison.Ordinal));
    }
}
