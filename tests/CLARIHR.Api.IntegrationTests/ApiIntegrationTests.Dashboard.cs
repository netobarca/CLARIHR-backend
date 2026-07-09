using System.Net;
using System.Text.Json;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// REQ-004 PR-2 (tablero de acciones de personal — dimensional layer + extended filters). Round-trips of the
/// EXISTING dashboard endpoints proving the ADDITIVE contract: <c>dashboard/overview</c> now returns the
/// <c>byPayrollType</c> breakdown (with the "Sin dato" bucket for unclassified plazas), the new
/// <c>payrollTypeCode</c>/<c>costCenterId</c> filters scope every endpoint, and <c>dashboard/metadata</c>
/// declares the three new filters, the connectable sections and the rotation formula. Also asserts the
/// no-amounts contract (aclaración №8) on the new response. The seeding writes completed active employees with
/// a chosen payroll type + cost center directly through the DbContext (the dashboard reads the active-primary
/// assignment).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime DashboardHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private async Task<Guid> SeedDashboardEmployeeAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        string? payrollTypeCode = null,
        Guid? costCenterPublicId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: institutionalEmail,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(tenantId);
        file.CompleteWithoutLinkedUser();
        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", DashboardHireDate, minimumMonthlyWage: null);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: payrollTypeCode,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: costCenterPublicId,
            startDate: DashboardHireDate,
            endDate: null,
            isPrimary: true,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(file.Id);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);
        await dbContext.SaveChangesAsync();

        return file.PublicId;
    }

    private static (string Key, string Label, int Count)[] ReadBreakdown(JsonElement root, string property) =>
        root.GetProperty(property).EnumerateArray()
            .Select(item => (
                item.GetProperty("key").GetString()!,
                item.GetProperty("label").GetString()!,
                item.GetProperty("count").GetInt32()))
            .ToArray();

    [Fact]
    public async Task Dashboard_Overview_ReturnsByPayrollTypeBreakdownWithUnassignedBucket()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "Mensual1", "DBP-1", "ana.m1@empresa.test", payrollTypeCode: "MENSUAL");
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Bea", "Mensual2", "DBP-2", "bea.m2@empresa.test", payrollTypeCode: "MENSUAL");
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Cid", "Quincenal", "DBP-3", "cid.q@empresa.test", payrollTypeCode: "QUINCENAL");
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Dan", "SinTipo", "DBP-4", "dan.s@empresa.test", payrollTypeCode: null);

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/overview");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var byPayrollType = ReadBreakdown(root, "byPayrollType");

        var mensual = Assert.Single(byPayrollType, item => item.Key == "MENSUAL");
        Assert.Equal("Mensual", mensual.Label); // label from the payroll-types catalog, never hardcoded
        Assert.Equal(2, mensual.Count);

        var quincenal = Assert.Single(byPayrollType, item => item.Key == "QUINCENAL");
        Assert.Equal("Quincenal", quincenal.Label);
        Assert.Equal(1, quincenal.Count);

        var unassigned = Assert.Single(byPayrollType, item => item.Key == "UNASSIGNED");
        Assert.Equal("Sin dato", unassigned.Label);
        Assert.Equal(1, unassigned.Count);

        // No-amounts contract (aclaración №8): the new response carries no monetary fields.
        Assert.DoesNotContain("\"amount\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"currency\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dashboard_Overview_PayrollTypeFilterReducesResults()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "Mensual1", "DBF-1", "ana.f1@empresa.test", payrollTypeCode: "MENSUAL");
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Bea", "Mensual2", "DBF-2", "bea.f2@empresa.test", payrollTypeCode: "MENSUAL");
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Cid", "Quincenal", "DBF-3", "cid.f3@empresa.test", payrollTypeCode: "QUINCENAL");

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/overview?payrollTypeCode=MENSUAL");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("headcount").GetProperty("total").GetInt32());

        var byPayrollType = ReadBreakdown(root, "byPayrollType");
        var only = Assert.Single(byPayrollType);
        Assert.Equal("MENSUAL", only.Key);
        Assert.Equal(2, only.Count);
    }

    [Fact]
    public async Task Dashboard_Overview_CostCenterFilterReducesResults()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var costCenterA = Guid.NewGuid();
        var costCenterB = Guid.NewGuid();

        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "CentroA", "DBC-1", "ana.c1@empresa.test", costCenterPublicId: costCenterA);
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Bea", "CentroB", "DBC-2", "bea.c2@empresa.test", costCenterPublicId: costCenterB);

        // Guid `costCenterId` params bind from the external `costCenterPublicId` name (PublicContract convention).
        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/overview?costCenterPublicId={costCenterA}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("headcount").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Dashboard_Metadata_DeclaresNewFiltersSectionsAndRotationFormula()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/metadata");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var filterKeys = root.GetProperty("filters").EnumerateArray()
            .Select(item => item.GetProperty("key").GetString())
            .ToArray();
        Assert.Contains("payrollTypeCode", filterKeys);
        Assert.Contains("costCenterPublicId", filterKeys);
        Assert.Contains("month", filterKeys);

        var sections = root.GetProperty("sections").EnumerateArray().ToArray();
        Assert.NotEmpty(sections);
        var personnelActions = Assert.Single(sections, item => item.GetProperty("key").GetString() == "PERSONNEL_ACTIONS");
        Assert.False(personnelActions.GetProperty("active").GetBoolean()); // PR-3 activates it
        Assert.True(personnelActions.GetProperty("acceptsMonth").GetBoolean());
        Assert.Contains(sections, item => item.GetProperty("key").GetString() == "MOVEMENTS");

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("rotationFormula").GetString()));

        // The pre-existing metadata fields remain intact (additive contract).
        Assert.True(root.TryGetProperty("ageRanges", out _));
        Assert.True(root.TryGetProperty("seniorityRanges", out _));
        Assert.True(root.TryGetProperty("fileUpToDateThresholdMonths", out _));
    }

    [Fact]
    public async Task Dashboard_Overview_WithoutViewReports_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        // A context with no personnel-file permissions cannot read the dashboard (gate EnsureCanViewReports).
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
