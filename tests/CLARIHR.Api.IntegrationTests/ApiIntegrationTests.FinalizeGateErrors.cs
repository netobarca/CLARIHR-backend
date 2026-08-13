using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-25 — a personnel file is born in `Draft`, and writing its employment information or its compensation requires
/// a FINALIZED file. The rule is fine; the problem was that nothing said so: all 154 uses of
/// `PERSONNEL_FILE_STATE_RULE_VIOLATION` answered *"The requested operation is not allowed for the current
/// personnel file state"* — without naming `Draft`, `Completed` or the `PATCH /finalize` that unlocks it. And that
/// one code covered FOUR different situations (143× "not finalized", 4× "not an employee record", 5× a caught
/// domain exception that was really a 404 or a validation, plus two more), so it could not be trusted to mean
/// anything in particular.
/// <para>
/// Now each situation has its own code, and the one that dominates carries the remedy: the current
/// `lifecycleStatus`, the transition to call, and the readiness endpoint that lists what is still missing.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>
    /// An EMPLOYEE record still in `Draft`, with the institutional email `finalize` requires. The shared
    /// `CreatePersonnelFileAsync` helper creates a `Candidate`, which trips a different gate ("not an employee") —
    /// the very ambiguity this finding is about.
    /// </summary>
    private async Task<(Guid Id, Guid ConcurrencyToken)> CreateDraftEmployeeFileAsync(
        HttpClient client,
        Guid companyId,
        string firstName,
        string lastName,
        string institutionalEmail)
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/personnel-files", new
        {
            recordType = "Employee",
            firstName,
            lastName,
            birthDate = new DateTime(1990, 3, 3),
            maritalStatusCode = "SOLTERO_A",
            professionCode = "ANALISTA_DE_DATOS",
            nationality = "SV",
            personalEmail = (string?)null,
            institutionalEmail,
            personalPhone = "+50370001000",
            institutionalPhone = (string?)null,
            birthCountryCode = "SV",
            birthDepartmentCode = "SAN_SALVADOR",
            birthMunicipalityCode = "SAN_SALVADOR_CENTRO",
            photoFilePublicId = (Guid?)null,
            orgUnitPublicId = (Guid?)null,
            customDataJson = (string?)null
        });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        return (
            document.RootElement.GetProperty("publicId").GetGuid(),
            document.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    [Fact]
    public async Task PersonnelFiles_WriteEmploymentInformationInDraft_ShouldNameTheMissingTransition()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreateDraftEmployeeFileAsync(
            client, scenario.TenantId, "Borrador", "SinFinalizar", "borrador.h25@empresa.test");

        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/personnel-files/{created.Id}/employment-information")
        {
            Content = JsonContent.Create(new
            {
                employeeCode = "EMP-H25",
                employmentStatusCode = "ACTIVO",
                hireDate = DateTime.UtcNow.Date
            })
        };
        // The `If-Match` header is mandatory on this PUT even for the first create (the binder requires it), so it
        // has to be there for the request to reach the lifecycle gate at all.
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");
        var response = await client.SendAsync(request);

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "PERSONNEL_FILE_NOT_FINALIZED");

        // The remedy travels with the error: which state it is in, what to call, and where to see what is missing.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Draft", document.RootElement.GetProperty("lifecycleStatus").GetString());
        Assert.Contains("finalize", document.RootElement.GetProperty("requiredTransition").GetString()!);
        Assert.Contains("finalize/preview", document.RootElement.GetProperty("readiness").GetString()!);
    }

    /// <summary>The readiness endpoint the error points at must actually answer, and list what is missing.</summary>
    [Fact]
    public async Task PersonnelFiles_FinalizePreview_ShouldListWhatIsMissing()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreateDraftEmployeeFileAsync(
            client, scenario.TenantId, "Previa", "Finalizar", "previa.h25@empresa.test");

        var response = await client.GetAsync($"/api/v1/personnel-files/{created.Id}/finalize/preview");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("isEligible").GetBoolean());

        var codes = document.RootElement.GetProperty("issues").EnumerateArray()
            .Select(issue => issue.GetProperty("code").GetString())
            .ToArray();
        // No plaza assigned yet, so that is what blocks finalizing.
        Assert.Contains("PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT", codes);
    }

    /// <summary>
    /// A non-employee file is a different truth from "not finalized yet", and the assignment handler used to answer
    /// the same generic state code for both.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_AssignPositionToNonEmployeeFile_ShouldSayItIsNotAnEmployee()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H25", "Direccion H25", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H25", "Perfil H25", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H25", "Plaza H25", profile.Id, maxEmployees: 1);

        // A Candidate record, not an Employee one.
        var candidate = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Candi",
            lastName = "Data",
            birthDate = new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            nationality = "SV",
            institutionalEmail = "candi.data@empresa.test"
        });
        Assert.Equal(HttpStatusCode.Created, candidate.StatusCode);
        using var candidateDocument = JsonDocument.Parse(await candidate.Content.ReadAsStringAsync());
        var candidateId = candidateDocument.RootElement.GetProperty("publicId").GetGuid();

        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{candidateId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "PERSONNEL_FILE_NOT_EMPLOYEE");
    }

    /// <summary>
    /// One of the five `catch (InvalidOperationException)` sites: the domain throws "Language with public id X was
    /// not found", which is a 404 — it used to come back as a 422 about the file's state.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_DeleteUnknownLanguage_ShouldReturn404()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Idioma", "Inexistente", "DUI", "07777777-5");

        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/personnel-files/{created.Id}/languages/{Guid.NewGuid()}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The other half of the same catch: "At least one language skill must be selected" is a validation about the
    /// payload, not about the file's lifecycle.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_AddLanguageWithoutSkills_ShouldReturnAValidationError()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Idioma", "SinHabilidad", "DUI", "07777777-6");

        var response = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/languages", new
        {
            languageCode = "ENGLISH",
            levelCode = "ADVANCED",
            speaks = false,
            writes = false,
            reads = false,
            concurrencyToken = created.ConcurrencyToken
        });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            $"Expected a validation failure, got {(int)response.StatusCode}: {payload}");

        // Whatever the status, it must NOT claim the file's state is the problem.
        Assert.DoesNotContain("PERSONNEL_FILE_STATE_RULE_VIOLATION", payload);
        Assert.DoesNotContain("PERSONNEL_FILE_NOT_FINALIZED", payload);
    }

    /// <summary>
    /// Non-regression for the order the code actually forces: plaza (in `Draft`) → finalize → employment info.
    /// If this ever needs a `422` in the middle, the documented critical path has drifted from the code.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_CriticalPathInOrder_ShouldNeverHitTheGate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H25-OK", "Direccion H25 OK", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H25-OK", "Perfil H25 OK", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H25-OK", "Plaza H25 OK", profile.Id, maxEmployees: 1);

        var created = await CreateDraftEmployeeFileAsync(
            client, scenario.TenantId, "Orden", "Correcto", "orden.h25@empresa.test");

        // 1) The plaza goes FIRST and works in Draft — finalize requires it.
        var assignment = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{created.Id}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);

        // 2) finalize — with a REFRESHED token: creating the assignment touches the parent file (that is what
        // moves its `modifiedAtUtc`), so the token obtained at create time is already stale and finalize would
        // answer 409. It is a real trap of the critical path.
        var shell = await client.GetAsync($"/api/v1/personnel-files/{created.Id}");
        shell.EnsureSuccessStatusCode();
        using var shellDocument = JsonDocument.Parse(await shell.Content.ReadAsStringAsync());
        var refreshedToken = shellDocument.RootElement.GetProperty("concurrencyToken").GetGuid();
        Assert.NotEqual(created.ConcurrencyToken, refreshedToken);

        using var finalizeRequest = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/personnel-files/{created.Id}/finalize")
        {
            Content = JsonContent.Create(new { createUserAccount = false, positionSlotPublicId = (Guid?)null })
        };
        finalizeRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{refreshedToken}\"");
        var finalize = await client.SendAsync(finalizeRequest);
        var finalizePayload = await finalize.Content.ReadAsStringAsync();
        Assert.True(finalize.IsSuccessStatusCode, $"Finalize failed: {(int)finalize.StatusCode} {finalizePayload}");

        // 3) and only now the employment information is writable. The section does not exist yet (the GET answers
        // with an empty body until the first PUT), and the `If-Match` HEADER is still mandatory even for that
        // first create — the binder requires it and the handler ignores its value when there is nothing to
        // compare against. The swagger claims the opposite; that sentence is corrected as part of H-25.
        var profileToken = Guid.NewGuid();

        using var employmentRequest = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/personnel-files/{created.Id}/employment-information")
        {
            Content = JsonContent.Create(new
            {
                employeeCode = "EMP-H25-OK",
                employmentStatusCode = "ACTIVO",
                hireDate = DateTime.UtcNow.Date
            })
        };
        employmentRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{profileToken}\"");
        var employment = await client.SendAsync(employmentRequest);
        var employmentPayload = await employment.Content.ReadAsStringAsync();
        Assert.True(
            employment.IsSuccessStatusCode,
            $"Employment information failed after finalize: {(int)employment.StatusCode} {employmentPayload}");
    }
}
