using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Application.Features.Leave.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the leave-configuration masters family (vacaciones e incapacidades
/// PR-1): the five per-company governed masters (medical clinics, incapacity risks with subsidy
/// tranches, incapacity types, company holidays and payroll periods — CostCenters-mirror
/// controllers with If-Match concurrency and allowedActions) plus the idempotent
/// <c>POST …/leave-configuration/load-template</c> endpoint over a tenant whose El Salvador
/// template was already applied by the company-provisioning hook.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext CreateLeaveConfigurationAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            LeaveConfigurationPermissionCodes.Admin);

    private async Task<TResponse> PostLeaveMasterAsync<TResponse>(HttpClient client, string url, object body)
    {
        var response = await client.PostJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"POST {url} failed: {(int)response.StatusCode} {response.StatusCode}. Body: {payload}");
        }

        var created = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<MedicalClinicResponse> CreateMedicalClinicAsync(
        HttpClient client,
        Guid companyId,
        string description,
        string? specialty = null,
        string? sectorCode = null) =>
        await PostLeaveMasterAsync<MedicalClinicResponse>(
            client,
            $"/api/v1/companies/{companyId}/medical-clinics",
            new { description, specialty, sectorCode });

    private async Task<IncapacityRiskResponse> CreateIncapacityRiskAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string name,
        object? parameters = null) =>
        await PostLeaveMasterAsync<IncapacityRiskResponse>(
            client,
            $"/api/v1/companies/{companyId}/incapacity-risks",
            new
            {
                code,
                name,
                countsSeventhDay = true,
                countsSaturday = true,
                countsHoliday = true,
                usesWorkSchedule = false,
                allowsIndefinite = false,
                allowsExtension = true,
                usesFund = true,
                hasSubsidy = parameters is not null,
                parameters
            });

    [Fact]
    public async Task LeaveMasters_MedicalClinics_FullFlow_ShouldCreateListGetUpdateInactivateAndActivate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        // Create against the seeded country-scoped clinic-sectors catalog (ISSS/PUBLICA/PRIVADA).
        var created = await CreateMedicalClinicAsync(
            client, scenario.TenantId, "Clinica Central", specialty: "Medicina General", sectorCode: "ISSS");
        Assert.Equal("Clinica Central", created.Description);
        Assert.Equal("ISSS", created.SectorCode);
        Assert.True(created.IsActive);

        // List with includeAllowedActions=true: the admin caller can edit/inactivate the active item.
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/medical-clinics?page=1&pageSize=20&includeAllowedActions=true");
        listResponse.EnsureSuccessStatusCode();
        using (var document = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()))
        {
            var item = document.RootElement.GetProperty("items").EnumerateArray()
                .Single(entry => entry.GetProperty("publicId").GetGuid() == created.Id);
            var allowedActions = item.GetProperty("allowedActions");
            Assert.True(allowedActions.GetProperty("canEdit").GetBoolean());
            Assert.True(allowedActions.GetProperty("canInactivate").GetBoolean());
            Assert.False(allowedActions.GetProperty("canActivate").GetBoolean());
        }

        // GetById always resolves allowedActions (no query flag needed on the detail read).
        var getResponse = await client.GetAsync($"/api/v1/medical-clinics/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<MedicalClinicResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal("Clinica Central", fetched!.Description);
        Assert.NotNull(fetched.AllowedActions);
        Assert.True(fetched.AllowedActions!.CanEdit);

        // Update rotates the concurrency token (If-Match travels via the body-token mirror helper).
        var updateResponse = await client.PutJsonAsync($"/api/v1/medical-clinics/{created.Id}", new
        {
            description = "Clinica Central Renovada",
            specialty = "Pediatria",
            sectorCode = "PRIVADA",
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<MedicalClinicResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Clinica Central Renovada", updated!.Description);
        Assert.Equal("PRIVADA", updated.SectorCode);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);

        var inactivateResponse = await client.PatchJsonAsync($"/api/v1/medical-clinics/{created.Id}/inactivate", new
        {
            concurrencyToken = updated.ConcurrencyToken
        });
        inactivateResponse.EnsureSuccessStatusCode();
        var inactive = await inactivateResponse.Content.ReadFromJsonAsync<MedicalClinicResponse>(JsonOptions);
        Assert.NotNull(inactive);
        Assert.False(inactive!.IsActive);

        var activateResponse = await client.PatchJsonAsync($"/api/v1/medical-clinics/{created.Id}/activate", new
        {
            concurrencyToken = inactive.ConcurrencyToken
        });
        activateResponse.EnsureSuccessStatusCode();
        var active = await activateResponse.Content.ReadFromJsonAsync<MedicalClinicResponse>(JsonOptions);
        Assert.NotNull(active);
        Assert.True(active!.IsActive);
    }

    [Fact]
    public async Task LeaveMasters_MedicalClinics_Create_WithDuplicateDescription_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        _ = await CreateMedicalClinicAsync(client, scenario.TenantId, "Clinica Duplicada");

        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/medical-clinics", new
        {
            description = "Clinica Duplicada",
            specialty = (string?)null,
            sectorCode = (string?)null
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "MEDICAL_CLINIC_DESCRIPTION_CONFLICT");
    }

    [Fact]
    public async Task LeaveMasters_MedicalClinics_Create_WithInvalidSectorCode_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/medical-clinics", new
        {
            description = "Clinica Sin Sector",
            specialty = (string?)null,
            sectorCode = "SECTOR_FANTASMA"
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "MEDICAL_CLINIC_SECTOR_INVALID");
    }

    [Fact]
    public async Task LeaveMasters_MedicalClinics_List_WithoutPermission_ShouldReturn403()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/medical-clinics?page=1&pageSize=20");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "LEAVE_CONFIGURATION_FORBIDDEN");
    }

    [Fact]
    public async Task LeaveMasters_IncapacityTypes_FullFlow_ShouldCreateListGetAndUpdate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        var created = await PostLeaveMasterAsync<IncapacityTypeResponse>(
            client,
            $"/api/v1/companies/{scenario.TenantId}/incapacity-types",
            new
            {
                code = "SUBSIDIO_ESPECIAL",
                name = "Subsidio especial",
                deductionTypeText = "Descuento incapacidad",
                incomeTypeText = "Subsidio incapacidad",
                appliesToWorkAccident = false
            });
        Assert.Equal("SUBSIDIO_ESPECIAL", created.Code);
        Assert.True(created.IsActive);

        var listResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/incapacity-types?page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<IncapacityTypeResponse>>(JsonOptions);
        Assert.NotNull(listPayload);
        Assert.Contains(listPayload!.Items, item => item.Id == created.Id);

        var getResponse = await client.GetAsync($"/api/v1/incapacity-types/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<IncapacityTypeResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal("Subsidio especial", fetched!.Name);

        var updateResponse = await client.PutJsonAsync($"/api/v1/incapacity-types/{created.Id}", new
        {
            code = "SUBSIDIO_ESPECIAL",
            name = "Subsidio especial actualizado",
            deductionTypeText = (string?)null,
            incomeTypeText = (string?)null,
            appliesToWorkAccident = true,
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<IncapacityTypeResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Subsidio especial actualizado", updated!.Name);
        Assert.True(updated.AppliesToWorkAccident);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
    }

    [Fact]
    public async Task LeaveMasters_IncapacityTypes_Create_WithDuplicateCode_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        _ = await PostLeaveMasterAsync<IncapacityTypeResponse>(
            client,
            $"/api/v1/companies/{scenario.TenantId}/incapacity-types",
            new
            {
                code = "TIPO_DUP",
                name = "Tipo original",
                deductionTypeText = (string?)null,
                incomeTypeText = (string?)null,
                appliesToWorkAccident = false
            });

        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/incapacity-types", new
        {
            code = "TIPO_DUP",
            name = "Tipo repetido",
            deductionTypeText = (string?)null,
            incomeTypeText = (string?)null,
            appliesToWorkAccident = true
        });

        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "INCAPACITY_TYPE_CODE_CONFLICT");
    }

    [Fact]
    public async Task LeaveMasters_IncapacityRisks_FullFlow_ShouldCreateWithTranchesAndReplaceParameters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        // Anexo A.2 shape: days 1-3 paid by the company, day 4 onwards (open-ended) by ISSS.
        var created = await CreateIncapacityRiskAsync(
            client,
            scenario.TenantId,
            "RIESGO_PRUEBA",
            "Riesgo de prueba",
            parameters: new[]
            {
                new { dayFrom = 1, dayTo = (int?)3, subsidyPercent = 75m, payerCode = "EMPRESA" },
                new { dayFrom = 4, dayTo = (int?)null, subsidyPercent = 75m, payerCode = "ISSS" }
            });
        Assert.True(created.HasSubsidy);
        Assert.Equal(2, created.Parameters.Count);

        // GetById returns the tranche set ordered by sortOrder (day 1 tranche first).
        var getResponse = await client.GetAsync($"/api/v1/incapacity-risks/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<IncapacityRiskResponse>(JsonOptions);
        Assert.NotNull(fetched);
        var orderedParameters = fetched!.Parameters.ToArray();
        Assert.Equal(2, orderedParameters.Length);
        Assert.Equal(1, orderedParameters[0].DayFrom);
        Assert.Equal(3, orderedParameters[0].DayTo);
        Assert.Equal("EMPRESA", orderedParameters[0].PayerCode);
        Assert.Equal(75m, orderedParameters[0].SubsidyPercent);
        Assert.Equal(4, orderedParameters[1].DayFrom);
        Assert.Null(orderedParameters[1].DayTo);
        Assert.Equal("ISSS", orderedParameters[1].PayerCode);
        Assert.True(orderedParameters[0].SortOrder < orderedParameters[1].SortOrder);

        // PUT …/parameters replaces the FULL tranche set in one shot (If-Match required).
        var replaceResponse = await client.PutJsonAsync($"/api/v1/incapacity-risks/{created.Id}/parameters", new
        {
            parameters = new[]
            {
                new { dayFrom = 1, dayTo = (int?)null, subsidyPercent = 100m, payerCode = "ISSS" }
            },
            concurrencyToken = fetched.ConcurrencyToken
        });
        replaceResponse.EnsureSuccessStatusCode();
        var replaced = await replaceResponse.Content.ReadFromJsonAsync<IncapacityRiskResponse>(JsonOptions);
        Assert.NotNull(replaced);
        var replacedParameter = Assert.Single(replaced!.Parameters);
        Assert.Equal(1, replacedParameter.DayFrom);
        Assert.Null(replacedParameter.DayTo);
        Assert.Equal(100m, replacedParameter.SubsidyPercent);
        Assert.Equal("ISSS", replacedParameter.PayerCode);
        Assert.NotEqual(fetched.ConcurrencyToken, replaced.ConcurrencyToken);

        // The replacement is durable: a fresh read reflects the single open-ended tranche.
        var refetchResponse = await client.GetAsync($"/api/v1/incapacity-risks/{created.Id}");
        refetchResponse.EnsureSuccessStatusCode();
        var refetched = await refetchResponse.Content.ReadFromJsonAsync<IncapacityRiskResponse>(JsonOptions);
        Assert.NotNull(refetched);
        _ = Assert.Single(refetched!.Parameters);
    }

    [Fact]
    public async Task LeaveMasters_IncapacityRisks_Create_WithNonContiguousTranches_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        // Day 4 is uncovered (1-3 then 5-open): the tranche set must be contiguous.
        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/incapacity-risks", new
        {
            code = "RIESGO_ROTO",
            name = "Riesgo con tramos rotos",
            countsSeventhDay = true,
            countsSaturday = true,
            countsHoliday = true,
            usesWorkSchedule = false,
            allowsIndefinite = false,
            allowsExtension = true,
            usesFund = true,
            hasSubsidy = true,
            parameters = new[]
            {
                new { dayFrom = 1, dayTo = (int?)3, subsidyPercent = 75m, payerCode = "EMPRESA" },
                new { dayFrom = 5, dayTo = (int?)null, subsidyPercent = 75m, payerCode = "ISSS" }
            }
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RISK_PARAMETERS_INVALID");
    }

    [Fact]
    public async Task LeaveMasters_IncapacityRisks_ReplaceParameters_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        var created = await CreateIncapacityRiskAsync(
            client,
            scenario.TenantId,
            "RIESGO_STALE",
            "Riesgo token viejo",
            parameters: new[]
            {
                new { dayFrom = 1, dayTo = (int?)null, subsidyPercent = 75m, payerCode = "ISSS" }
            });

        var response = await client.PutJsonAsync($"/api/v1/incapacity-risks/{created.Id}/parameters", new
        {
            parameters = new[]
            {
                new { dayFrom = 1, dayTo = (int?)null, subsidyPercent = 100m, payerCode = "ISSS" }
            },
            concurrencyToken = Guid.NewGuid()
        });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task LeaveMasters_CompanyHolidays_FullFlow_ShouldRejectDuplicateDateAndUpdateToNewDate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        var created = await PostLeaveMasterAsync<CompanyHolidayResponse>(
            client,
            $"/api/v1/companies/{scenario.TenantId}/company-holidays",
            new { date = "2027-08-03", description = "Fiestas Agostinas (dia local)", scopeCode = "LOCAL" });
        Assert.Equal(new DateOnly(2027, 8, 3), created.Date);
        Assert.Equal("LOCAL", created.ScopeCode);

        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/company-holidays", new
        {
            date = "2027-08-03",
            description = "Asueto repetido",
            scopeCode = "INSTITUCIONAL"
        });
        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "HOLIDAY_DUPLICATE");

        // Moving the holiday to a free date is a regular update.
        var updateResponse = await client.PutJsonAsync($"/api/v1/company-holidays/{created.Id}", new
        {
            date = "2027-08-04",
            description = "Fiestas Agostinas (dia local)",
            scopeCode = "LOCAL",
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<CompanyHolidayResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(new DateOnly(2027, 8, 4), updated!.Date);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
    }

    [Fact]
    public async Task LeaveMasters_PayrollPeriods_FullFlow_ShouldRejectDuplicateOverlapAndUnknownType()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        var created = await PostLeaveMasterAsync<PayrollPeriodResponse>(
            client,
            $"/api/v1/companies/{scenario.TenantId}/payroll-periods",
            new
            {
                payPeriodTypeCode = "MENSUAL",
                year = 2027,
                number = 1,
                label = "Enero 2027",
                startDate = "2027-01-01",
                endDate = "2027-01-31"
            });
        Assert.Equal("MENSUAL", created.PayPeriodTypeCode);
        Assert.Equal(new DateOnly(2027, 1, 1), created.StartDate);

        // Same (type, year, number) with non-overlapping dates isolates the duplicate guard.
        var duplicateResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/payroll-periods", new
        {
            payPeriodTypeCode = "MENSUAL",
            year = 2027,
            number = 1,
            label = "Enero 2027 bis",
            startDate = "2027-03-01",
            endDate = "2027-03-31"
        });
        await AssertProblemDetailsAsync(duplicateResponse, HttpStatusCode.Conflict, "PAYROLL_PERIOD_DUPLICATE");

        // A different number whose range overlaps period #1 trips the overlap guard.
        var overlapResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/payroll-periods", new
        {
            payPeriodTypeCode = "MENSUAL",
            year = 2027,
            number = 2,
            label = "Solapado 2027",
            startDate = "2027-01-15",
            endDate = "2027-02-15"
        });
        await AssertProblemDetailsAsync(overlapResponse, HttpStatusCode.UnprocessableEntity, "PAYROLL_PERIOD_OVERLAP");

        // The pay-period type must be an active code of the country-scoped pay-periods catalog.
        var unknownTypeResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/payroll-periods", new
        {
            payPeriodTypeCode = "XXX",
            year = 2027,
            number = 3,
            label = "Tipo inexistente",
            startDate = "2027-04-01",
            endDate = "2027-04-30"
        });
        await AssertProblemDetailsAsync(unknownTypeResponse, HttpStatusCode.UnprocessableEntity, "PAYROLL_PERIOD_TYPE_INVALID");
    }

    [Fact]
    public async Task LeaveConfiguration_LoadTemplate_IsIdempotent_AfterProvisioningAppliedTheTemplate()
    {
        var scenario = await factory.ResetDatabaseAsync();

        // The integration seeder creates companies directly (bypassing CompanyProvisioningService),
        // so replay the provisioning hook verbatim: apply the SV template with the current year —
        // exactly what ProvisionAsync does for a new company (5 A.2 risks + 6 types + current-year
        // holidays).
        using (var scope = factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<ILeaveTemplateSeeder>();
            var provisioning = await seeder.ApplyTemplateAsync(scenario.TenantId, DateTime.UtcNow.Year, CancellationToken.None);
            Assert.Equal(5, provisioning.RisksCreated);
            Assert.Equal(6, provisioning.TypesCreated);
            Assert.Equal(11, provisioning.HolidaysCreated);
        }

        using var client = factory.CreateClientFor(CreateLeaveConfigurationAdminContext(scenario));

        // First explicit call for a FUTURE year: risks/types already exist (skipped, never
        // overwritten); only that year's 11 Art. 190 CT holidays are created. The template always
        // yields 11 rows per year (8 fixed dates + the 3 computus-derived Semana Santa days), so the
        // assert does not couple to the movable dates.
        var templateYear = DateTime.UtcNow.Year + 2;
        var firstResponse = await client.PostAsync(
            $"/api/v1/companies/{scenario.TenantId}/leave-configuration/load-template?year={templateYear}",
            content: null);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<LeaveTemplateSeedResultResponse>(JsonOptions);
        Assert.NotNull(first);
        Assert.Equal(0, first!.RisksCreated);
        Assert.Equal(0, first.RiskParametersCreated);
        Assert.Equal(0, first.TypesCreated);
        Assert.Equal(5, first.RisksSkipped);
        Assert.Equal(6, first.TypesSkipped);
        Assert.Equal(11, first.HolidaysCreated);
        Assert.Equal(0, first.HolidaysSkipped);
        Assert.Equal(11, first.TotalCreated);
        Assert.Equal(11, first.TotalSkipped);

        // Second call for the same year: fully idempotent — everything is reported as skipped.
        var secondResponse = await client.PostAsync(
            $"/api/v1/companies/{scenario.TenantId}/leave-configuration/load-template?year={templateYear}",
            content: null);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<LeaveTemplateSeedResultResponse>(JsonOptions);
        Assert.NotNull(second);
        Assert.Equal(0, second!.TotalCreated);
        Assert.Equal(0, second.HolidaysCreated);
        Assert.Equal(11, second.HolidaysSkipped);
        Assert.Equal(5, second.RisksSkipped);
        Assert.Equal(6, second.TypesSkipped);

        // The template left the ratified A.2 risk set in place: 5 risks, and ENFERMEDAD_COMUN
        // carries the two-tranche subsidy (1-3 EMPRESA 75% then 4-open ISSS 75%).
        var risksResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/incapacity-risks?page=1&pageSize=50");
        risksResponse.EnsureSuccessStatusCode();
        var risks = await risksResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<IncapacityRiskListItemResponse>>(JsonOptions);
        Assert.NotNull(risks);
        Assert.Equal(5, risks!.TotalCount);
        var commonIllness = Assert.Single(risks.Items, item => item.Code == "ENFERMEDAD_COMUN");
        Assert.Equal(2, commonIllness.ParameterCount);

        var commonIllnessDetail = await client.GetAsync($"/api/v1/incapacity-risks/{commonIllness.Id}");
        commonIllnessDetail.EnsureSuccessStatusCode();
        var detail = await commonIllnessDetail.Content.ReadFromJsonAsync<IncapacityRiskResponse>(JsonOptions);
        Assert.NotNull(detail);
        var tranches = detail!.Parameters.ToArray();
        Assert.Equal(2, tranches.Length);
        Assert.Equal(1, tranches[0].DayFrom);
        Assert.Equal(3, tranches[0].DayTo);
        Assert.Equal("EMPRESA", tranches[0].PayerCode);
        Assert.Equal(4, tranches[1].DayFrom);
        Assert.Null(tranches[1].DayTo);
        Assert.Equal("ISSS", tranches[1].PayerCode);
    }
}
