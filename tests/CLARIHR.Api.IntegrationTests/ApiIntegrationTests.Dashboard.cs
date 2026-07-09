using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        Assert.True(personnelActions.GetProperty("active").GetBoolean()); // PR-3 activated it
        Assert.True(personnelActions.GetProperty("acceptsMonth").GetBoolean());
        Assert.Contains(sections, item => item.GetProperty("key").GetString() == "MOVEMENTS");

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("rotationFormula").GetString()));

        // The pre-existing metadata fields remain intact (additive contract).
        Assert.True(root.TryGetProperty("ageRanges", out _));
        Assert.True(root.TryGetProperty("seniorityRanges", out _));
        Assert.True(root.TryGetProperty("fileUpToDateThresholdMonths", out _));
    }

    private async Task SeedPersonnelActionAsync(
        Guid tenantId,
        Guid filePublicId,
        string actionTypeCode,
        string actionStatusCode,
        DateTime actionDateUtc,
        bool isSystemGenerated)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // The seed scope has no ambient tenant, so bypass the tenant query filter to resolve the file's internal id.
        var fileId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(file => file.PublicId == filePublicId)
            .Select(file => file.Id)
            .FirstAsync();

        var action = PersonnelFilePersonnelAction.Create(
            actionTypeCode,
            actionStatusCode,
            actionDateUtc,
            effectiveFromUtc: null,
            effectiveToUtc: null,
            description: null,
            reference: null,
            amount: null,
            currencyCode: null,
            isSystemGenerated: isSystemGenerated);
        action.BindToPersonnelFile(fileId);
        action.SetTenantId(tenantId);
        dbContext.Set<PersonnelFilePersonnelAction>().Add(action);
        await dbContext.SaveChangesAsync();
    }

    private static int BreakdownCount(JsonElement root, string property, string key) =>
        ReadBreakdown(root, property).Single(item => item.Key == key).Count;

    [Fact]
    public async Task Dashboard_PersonnelActions_ReturnsSeriesAndBreakdowns_DefaultAplicadaOnly()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var mensual = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "Mensual", "PA-1", "ana.pa@empresa.test", payrollTypeCode: "MENSUAL");
        var quincenal = await SeedDashboardEmployeeAsync(scenario.TenantId, "Bea", "Quincenal", "PA-2", "bea.pa@empresa.test", payrollTypeCode: "QUINCENAL");

        var feb = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var may = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        await SeedPersonnelActionAsync(scenario.TenantId, mensual, "BAJA", "APLICADA", feb, isSystemGenerated: true);
        await SeedPersonnelActionAsync(scenario.TenantId, mensual, "AMONESTACION", "APLICADA", feb, isSystemGenerated: false);
        await SeedPersonnelActionAsync(scenario.TenantId, quincenal, "LIQUIDACION", "APLICADA", may, isSystemGenerated: true);
        await SeedPersonnelActionAsync(scenario.TenantId, quincenal, "BAJA", "ANULADA", feb, isSystemGenerated: true); // not APLICADA

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/personnel-actions?year=2026");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(2026, root.GetProperty("year").GetInt32());
        Assert.False(root.GetProperty("includeAllStatuses").GetBoolean());

        // Series counts only the 3 APLICADA items (feb: 2, may: 1) — the ANULADA feb entry is excluded by default.
        var byMonth = root.GetProperty("series").GetProperty("byMonth").EnumerateArray().ToArray();
        Assert.Equal(12, byMonth.Length);
        Assert.Equal(2, byMonth.Single(m => m.GetProperty("month").GetInt32() == 2).GetProperty("count").GetInt32());
        Assert.Equal(1, byMonth.Single(m => m.GetProperty("month").GetInt32() == 5).GetProperty("count").GetInt32());
        Assert.Equal(0, byMonth.Single(m => m.GetProperty("month").GetInt32() == 1).GetProperty("count").GetInt32());
        Assert.Equal(3, root.GetProperty("series").GetProperty("total").GetInt32());

        // byType (items): BAJA 1 (only the APLICADA one), AMONESTACION 1, LIQUIDACION 1 — labels from the catalog.
        Assert.Equal(1, BreakdownCount(root, "byType", "BAJA"));
        Assert.Equal(1, BreakdownCount(root, "byType", "LIQUIDACION"));
        Assert.Equal("Baja / retiro definitivo", ReadBreakdown(root, "byType").Single(i => i.Key == "BAJA").Label);

        // byStatus over the FULL universe (RN-04): APLICADA 3 AND ANULADA 1, even though items exclude the annulled.
        Assert.Equal(3, BreakdownCount(root, "byStatus", "APLICADA"));
        Assert.Equal(1, BreakdownCount(root, "byStatus", "ANULADA"));

        // byOrigin (items): 2 automático (BAJA + LIQUIDACION), 1 manual (AMONESTACION).
        Assert.Equal(2, BreakdownCount(root, "byOrigin", "SYSTEM"));
        Assert.Equal(1, BreakdownCount(root, "byOrigin", "MANUAL"));

        // byDimension → payrollTypes (items): MENSUAL 2 (both of Ana's), QUINCENAL 1 (Bea's liquidación).
        var byDimension = root.GetProperty("byDimension");
        Assert.Equal(2, ReadBreakdown(byDimension, "payrollTypes").Single(i => i.Key == "MENSUAL").Count);
        Assert.Equal(1, ReadBreakdown(byDimension, "payrollTypes").Single(i => i.Key == "QUINCENAL").Count);
        Assert.Equal("Mensual", ReadBreakdown(byDimension, "payrollTypes").Single(i => i.Key == "MENSUAL").Label);

        // No-amounts contract (aclaración №8): neither `amount` nor `currency` appears anywhere in the response.
        Assert.DoesNotContain("\"amount\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"currency\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dashboard_PersonnelActions_IncludeAllStatuses_ChangesItemsButNotByStatus()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var file = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "Mensual", "PAA-1", "ana.paa@empresa.test", payrollTypeCode: "MENSUAL");
        var feb = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var may = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        await SeedPersonnelActionAsync(scenario.TenantId, file, "BAJA", "APLICADA", feb, isSystemGenerated: true);
        await SeedPersonnelActionAsync(scenario.TenantId, file, "AMONESTACION", "APLICADA", feb, isSystemGenerated: false);
        await SeedPersonnelActionAsync(scenario.TenantId, file, "LIQUIDACION", "APLICADA", may, isSystemGenerated: true);
        await SeedPersonnelActionAsync(scenario.TenantId, file, "BAJA", "ANULADA", feb, isSystemGenerated: true);

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/personnel-actions?year=2026&includeAllStatuses=true");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.GetProperty("includeAllStatuses").GetBoolean());

        // Items now include the ANULADA entry → series feb: 3, may: 1, total 4.
        var byMonth = root.GetProperty("series").GetProperty("byMonth").EnumerateArray().ToArray();
        Assert.Equal(3, byMonth.Single(m => m.GetProperty("month").GetInt32() == 2).GetProperty("count").GetInt32());
        Assert.Equal(4, root.GetProperty("series").GetProperty("total").GetInt32());

        // byType now counts BAJA 2 (APLICADA + ANULADA); byStatus is unchanged (still the full universe).
        Assert.Equal(2, BreakdownCount(root, "byType", "BAJA"));
        Assert.Equal(3, BreakdownCount(root, "byStatus", "APLICADA"));
        Assert.Equal(1, BreakdownCount(root, "byStatus", "ANULADA"));
    }

    [Fact]
    public async Task Dashboard_PersonnelActions_MonthFilterRestrictsToThatMonth()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var file = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "Mensual", "PAM-1", "ana.pam@empresa.test", payrollTypeCode: "MENSUAL");
        await SeedPersonnelActionAsync(scenario.TenantId, file, "BAJA", "APLICADA", new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), isSystemGenerated: true);
        await SeedPersonnelActionAsync(scenario.TenantId, file, "LIQUIDACION", "APLICADA", new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), isSystemGenerated: true);

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/personnel-actions?year=2026&month=5");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(5, root.GetProperty("month").GetInt32());
        Assert.Equal(1, root.GetProperty("series").GetProperty("total").GetInt32());
        Assert.Equal(1, root.GetProperty("series").GetProperty("byMonth").EnumerateArray().Single(m => m.GetProperty("month").GetInt32() == 5).GetProperty("count").GetInt32());
        Assert.Equal(1, BreakdownCount(root, "byType", "LIQUIDACION"));
        Assert.DoesNotContain(ReadBreakdown(root, "byType"), i => i.Key == "BAJA");
    }

    [Fact]
    public async Task Dashboard_PersonnelActions_MonthWithoutYear_IsBadRequest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/personnel-actions?month=3");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("DASHBOARD_MONTH_REQUIRES_YEAR", problem);
    }

    [Fact]
    public async Task Dashboard_PersonnelActions_WithoutViewReports_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/personnel-actions?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_PersonnelActions_JournalQueryUsesTenantActionDateIndex()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var file = await SeedDashboardEmployeeAsync(scenario.TenantId, "Eva", "Index", "PAX-1", "eva.pax@empresa.test", payrollTypeCode: "MENSUAL");
        for (var i = 0; i < 48; i++)
        {
            await SeedPersonnelActionAsync(
                scenario.TenantId,
                file,
                "BAJA",
                "APLICADA",
                new DateTime(2026, 1 + (i % 12), 10, 0, 0, 0, DateTimeKind.Utc),
                isSystemGenerated: true);
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var setCommand = connection.CreateCommand())
            {
                setCommand.Transaction = transaction;
                // Force the planner off seq-scan so it must use an applicable index for the (tenant, date-range)
                // predicate on the tiny seeded table; SET LOCAL is scoped to this rolled-back transaction.
                setCommand.CommandText = "SET LOCAL enable_seqscan = off;";
                await setCommand.ExecuteNonQueryAsync();
            }

            var plan = new StringBuilder();
            await using (var explainCommand = connection.CreateCommand())
            {
                explainCommand.Transaction = transaction;
                explainCommand.CommandText =
                    "EXPLAIN SELECT action_type_code, action_status_code, action_date_utc, is_system_generated, personnel_file_id " +
                    "FROM personnel_file_personnel_actions " +
                    "WHERE tenant_id = @tenant AND action_date_utc >= @start AND action_date_utc < @end;";
                AddParameter(explainCommand, "tenant", scenario.TenantId);
                AddParameter(explainCommand, "start", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                AddParameter(explainCommand, "end", new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                await using var reader = await explainCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    plan.AppendLine(reader.GetString(0));
                }
            }

            await transaction.RollbackAsync();

            Assert.Contains("ix_personnel_file_personnel_actions_tenant_action_date", plan.ToString());
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
