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
        Guid? costCenterPublicId = null,
        DateTime? hireDate = null)
    {
        var effectiveHireDate = hireDate ?? DashboardHireDate;
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

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", effectiveHireDate, minimumMonthlyWage: null);
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
            startDate: effectiveHireDate,
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

    // ---- REQ-004 PR-4: movements section (bajas/altas/neto/rotación/cobertura/liquidaciones) ----

    private async Task RetireDashboardEmployeeAsync(
        Guid tenantId,
        Guid filePublicId,
        string categoryCode,
        string reasonCode,
        DateTime retirementDate)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var profile = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .IgnoreQueryFilters()
            .Where(item => item.PersonnelFile.PublicId == filePublicId)
            .FirstAsync();
        profile.ApplyRetirement(categoryCode, reasonCode, retirementNotes: null, retirementDate);
        await dbContext.SaveChangesAsync();
    }

    private async Task ReverseDashboardRetirementAsync(Guid tenantId, Guid filePublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var profile = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .IgnoreQueryFilters()
            .Where(item => item.PersonnelFile.PublicId == filePublicId)
            .FirstAsync();
        // A reversal clears RetirementDate → the baja leaves the movements series/ratios (aclaración №4).
        profile.ClearRetirement("ACTIVO");
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedSettlementAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid filePublicId,
        string? statusCode,
        DateTime retirementDate,
        bool scenario = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fileId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(file => file.PublicId == filePublicId)
            .Select(file => file.Id)
            .FirstAsync();

        var plazaStart = retirementDate.AddYears(-1);
        var settlement = scenario
            ? PersonnelFileSettlement.CreateScenario(
                Guid.NewGuid(), "Plaza", plazaStart, null, null, retirementDate,
                "VOLUNTARIA", "Renuncia voluntaria", "MEJOR_OFERTA_SALARIAL", "Mejor oferta salarial",
                filePublicId, "Solicitante", retirementDate, null, actorUserId, "USD")
            : PersonnelFileSettlement.CreateSettlement(
                Guid.NewGuid(), Guid.NewGuid(), "Plaza", plazaStart, null, null, retirementDate,
                "VOLUNTARIA", "Renuncia voluntaria", "MEJOR_OFERTA_SALARIAL", "Mejor oferta salarial",
                filePublicId, "Solicitante", retirementDate, null, actorUserId, "USD");

        if (!scenario && statusCode == SettlementStatuses.Anulada)
        {
            settlement.Annul(actorUserId, DateTime.UtcNow, "annul for test");
        }

        settlement.BindToPersonnelFile(fileId);
        settlement.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileSettlement>().Add(settlement);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedCompletedExitInterviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid filePublicId,
        string reasonCode,
        string categoryCode)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fileId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(file => file.PublicId == filePublicId)
            .Select(file => file.Id)
            .FirstAsync();

        var form = ExitInterviewForm.Create("Movements coverage form", null, isAnonymous: false);
        form.SetTenantId(tenantId);
        dbContext.Set<ExitInterviewForm>().Add(form);
        await dbContext.SaveChangesAsync();

        var submission = ExitInterviewSubmission.Create(
            form.Id, form.Version, isAnonymous: false, fileId, actorUserId,
            reasonCode, categoryCode, separationType: null, positionSlotPublicId: null, plazaSnapshot: null, period: "2026-05");
        // The "completed" state is ExitInterviewSubmissionStatus.Submitted (verified: Draft/Submitted/Archived).
        submission.MarkSubmitted(DateTime.UtcNow, totalScore: 80m);
        submission.SetTenantId(tenantId);
        dbContext.Set<ExitInterviewSubmission>().Add(submission);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Dashboard_Movements_ReturnsSeriesRotationCoverageAndSettlements()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        // 2 hires in Feb 2026.
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Ana", "Alta1", "MOV-A", "ana.mov@empresa.test", hireDate: new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        _ = await SeedDashboardEmployeeAsync(scenario.TenantId, "Bea", "Alta2", "MOV-B", "bea.mov@empresa.test", hireDate: new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));

        // 2 separations in May 2026 (hired long before), 1 reverted retirement (must NOT count as a baja).
        var cid = await SeedDashboardEmployeeAsync(scenario.TenantId, "Cid", "Baja1", "MOV-C", "cid.mov@empresa.test");
        var dan = await SeedDashboardEmployeeAsync(scenario.TenantId, "Dan", "Baja2", "MOV-D", "dan.mov@empresa.test");
        var eva = await SeedDashboardEmployeeAsync(scenario.TenantId, "Eva", "Revertida", "MOV-E", "eva.mov@empresa.test");

        var mayC = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var mayD = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);
        await RetireDashboardEmployeeAsync(scenario.TenantId, cid, "VOLUNTARIA", "MEJOR_OFERTA_SALARIAL", mayC);
        await RetireDashboardEmployeeAsync(scenario.TenantId, dan, "INVOLUNTARIA", "BAJO_DESEMPENO", mayD);
        await RetireDashboardEmployeeAsync(scenario.TenantId, eva, "VOLUNTARIA", "MOTIVOS_PERSONALES", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        await ReverseDashboardRetirementAsync(scenario.TenantId, eva); // reversal → RetirementDate cleared

        // Coverage: Cid has a completed exit interview; Dan does not → 1/2 = 50 %.
        await SeedCompletedExitInterviewAsync(scenario.TenantId, scenario.ActorUserId, cid, "MEJOR_OFERTA_SALARIAL", "VOLUNTARIA");

        // Settlements in the period: 1 BORRADOR + 1 ANULADA + 1 scenario (excluded — null status).
        await SeedSettlementAsync(scenario.TenantId, scenario.ActorUserId, cid, SettlementStatuses.Borrador, mayC);
        await SeedSettlementAsync(scenario.TenantId, scenario.ActorUserId, dan, SettlementStatuses.Anulada, mayD);
        await SeedSettlementAsync(scenario.TenantId, scenario.ActorUserId, cid, statusCode: null, mayC, scenario: true);

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/movements?year=2026");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(2026, root.GetProperty("year").GetInt32());

        // hires: Feb 2026 → 2, total 2.
        var hires = root.GetProperty("hires");
        Assert.Equal(2, hires.GetProperty("total").GetInt32());
        Assert.Equal(2, hires.GetProperty("byMonth").EnumerateArray().Single(m => m.GetProperty("month").GetInt32() == 2).GetProperty("count").GetInt32());

        // separations: May 2026 → 2 (the reverted baja is excluded), byCategory/byReason labelled from the catalogs.
        var separations = root.GetProperty("separations");
        Assert.Equal(2, separations.GetProperty("series").GetProperty("total").GetInt32());
        Assert.Equal(2, separations.GetProperty("series").GetProperty("byMonth").EnumerateArray().Single(m => m.GetProperty("month").GetInt32() == 5).GetProperty("count").GetInt32());
        Assert.Equal(1, ReadBreakdown(separations, "byCategory").Single(i => i.Key == "VOLUNTARIA").Count);
        Assert.Equal("Renuncia voluntaria", ReadBreakdown(separations, "byCategory").Single(i => i.Key == "VOLUNTARIA").Label);
        Assert.Equal(1, ReadBreakdown(separations, "byCategory").Single(i => i.Key == "INVOLUNTARIA").Count);
        Assert.Equal(1, ReadBreakdown(separations, "byReason").Single(i => i.Key == "MEJOR_OFERTA_SALARIAL").Count);
        Assert.Equal("Mejor oferta salarial", ReadBreakdown(separations, "byReason").Single(i => i.Key == "MEJOR_OFERTA_SALARIAL").Label);
        // The reverted employee's reason (MOTIVOS_PERSONALES) never appears — the reversal removed it from the series.
        Assert.DoesNotContain(ReadBreakdown(separations, "byReason"), i => i.Key == "MOTIVOS_PERSONALES");

        // net: Feb +2, May −2, total 0.
        var net = root.GetProperty("net");
        Assert.Equal(2, net.GetProperty("byMonth").EnumerateArray().Single(m => m.GetProperty("month").GetInt32() == 2).GetProperty("count").GetInt32());
        Assert.Equal(-2, net.GetProperty("byMonth").EnumerateArray().Single(m => m.GetProperty("month").GetInt32() == 5).GetProperty("count").GetInt32());
        Assert.Equal(0, net.GetProperty("total").GetInt32());

        // rotation: 2 separations / avg headcount 3 ((3 start + 3 end)/2) = 66.67 %.
        var rotation = root.GetProperty("rotation");
        Assert.Equal(2, rotation.GetProperty("separations").GetInt32());
        Assert.Equal(3m, rotation.GetProperty("averageHeadcount").GetDecimal());
        Assert.Equal(66.67m, rotation.GetProperty("ratePercent").GetDecimal());

        // exit-interview coverage: 1 completed of 2 separations → 50 %.
        var coverage = root.GetProperty("exitInterviewCoverage");
        Assert.Equal(2, coverage.GetProperty("separations").GetInt32());
        Assert.Equal(1, coverage.GetProperty("completed").GetInt32());
        Assert.Equal(50.0m, coverage.GetProperty("coveragePercent").GetDecimal());

        // settlementsByStatus: BORRADOR 1 + ANULADA 1 (the scenario is excluded — it carries a null status).
        var settlements = ReadBreakdown(root, "settlementsByStatus");
        Assert.Equal(2, settlements.Length);
        Assert.Equal(1, settlements.Single(i => i.Key == "BORRADOR").Count);
        Assert.Equal("Borrador", settlements.Single(i => i.Key == "BORRADOR").Label); // label from settlement-statuses
        Assert.Equal(1, settlements.Single(i => i.Key == "ANULADA").Count);

        // No-amounts contract (aclaración №8): even with real settlements seeded, no monetary field is exposed.
        Assert.DoesNotContain("\"amount\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"currency\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dashboard_Movements_MonthWithoutYear_IsBadRequest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/movements?month=3");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DASHBOARD_MONTH_REQUIRES_YEAR", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dashboard_Movements_WithoutViewReports_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-files/dashboard/movements?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- REQ-004 PR-5: company-wide personnel-actions bandeja + exports (drill of the journal) ----

    private async Task<(Guid Mensual, Guid Quincenal)> SeedBandejaFixtureAsync(Guid tenantId, string tag)
    {
        var mensual = await SeedDashboardEmployeeAsync(tenantId, "Ana", "Mensual", $"BJ{tag}-1", $"ana.bj{tag}@empresa.test", payrollTypeCode: "MENSUAL");
        var quincenal = await SeedDashboardEmployeeAsync(tenantId, "Bea", "Quincenal", $"BJ{tag}-2", $"bea.bj{tag}@empresa.test", payrollTypeCode: "QUINCENAL");

        var feb = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var may = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        await SeedPersonnelActionAsync(tenantId, mensual, "BAJA", "APLICADA", feb, isSystemGenerated: true);
        await SeedPersonnelActionAsync(tenantId, mensual, "AMONESTACION", "APLICADA", feb, isSystemGenerated: false);
        await SeedPersonnelActionAsync(tenantId, quincenal, "LIQUIDACION", "APLICADA", may, isSystemGenerated: true);
        await SeedPersonnelActionAsync(tenantId, quincenal, "BAJA", "ANULADA", feb, isSystemGenerated: true);

        return (mensual, quincenal);
    }

    private static int StatusCount(JsonElement root, string status)
    {
        var counts = root.GetProperty("statusCounts");
        return counts.TryGetProperty(status, out var value) ? value.GetInt32() : 0;
    }

    [Fact]
    public async Task PersonnelActionsBandeja_ReturnsItemsStatusCountsAndFilters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await SeedBandejaFixtureAsync(scenario.TenantId, "A");

        // Full year: 4 entries; statusCounts span every status (APLICADA 3, ANULADA 1) regardless of any filter.
        var allResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/query",
            new { year = 2026, pageSize = 25 });
        allResponse.EnsureSuccessStatusCode();

        var allBody = await allResponse.Content.ReadAsStringAsync();
        using (var document = JsonDocument.Parse(allBody))
        {
            var root = document.RootElement;
            Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(4, root.GetProperty("items").GetArrayLength());
            Assert.Equal(3, StatusCount(root, "APLICADA"));
            Assert.Equal(1, StatusCount(root, "ANULADA"));

            // Rows carry the documentary facts (empleado, código, tipo, estado, origen) — and NO monetary field.
            var first = root.GetProperty("items").EnumerateArray().First();
            Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("employeeFullName").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("actionTypeCode").GetString()));
            Assert.True(first.TryGetProperty("originCode", out var origin));
            Assert.Contains(origin.GetString(), new[] { "MANUAL", "SYSTEM" });

            // No-amounts contract (aclaración №8): neither `amount` nor `currency` appears anywhere in the bandeja.
            Assert.DoesNotContain("\"amount\"", allBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"currency\"", allBody, StringComparison.OrdinalIgnoreCase);
        }

        // Type filter = BAJA → the 2 BAJA entries (APLICADA + ANULADA); statusCounts still span both statuses.
        var typeResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/query",
            new { year = 2026, actionTypeCode = "BAJA" });
        typeResponse.EnsureSuccessStatusCode();
        using (var document = JsonDocument.Parse(await typeResponse.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, StatusCount(root, "APLICADA"));
            Assert.Equal(1, StatusCount(root, "ANULADA"));
        }

        // Status filter = APLICADA → 3 items, but statusCounts IGNORE the status filter (still APLICADA 3 + ANULADA 1).
        var statusResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/query",
            new { year = 2026, actionStatusCode = "APLICADA" });
        statusResponse.EnsureSuccessStatusCode();
        using (var document = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
            Assert.All(root.GetProperty("items").EnumerateArray(), item => Assert.Equal("APLICADA", item.GetProperty("actionStatusCode").GetString()));
            Assert.Equal(3, StatusCount(root, "APLICADA"));
            Assert.Equal(1, StatusCount(root, "ANULADA")); // counts span all statuses even with the status filter applied
        }

        // Origin filter = manual (isSystemGenerated=false) → only the AMONESTACION (manual) entry.
        var originResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/query",
            new { year = 2026, isSystemGenerated = false });
        originResponse.EnsureSuccessStatusCode();
        using (var document = JsonDocument.Parse(await originResponse.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            var only = root.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("AMONESTACION", only.GetProperty("actionTypeCode").GetString());
            Assert.Equal("MANUAL", only.GetProperty("originCode").GetString());
        }
    }

    [Fact]
    public async Task PersonnelActionsBandeja_Pagination_ReturnsRequestedPage()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await SeedBandejaFixtureAsync(scenario.TenantId, "P");

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/query",
            new { year = 2026, pageNumber = 2, pageSize = 3 });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(3, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("items").GetArrayLength()); // 4 total, page 2 of size 3 → 1 row
    }

    [Fact]
    public async Task PersonnelActionsBandeja_Export_ReturnsFileWithoutAmounts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        _ = await SeedBandejaFixtureAsync(scenario.TenantId, "X");

        var xlsxResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/export?format=xlsx&year=2026");
        xlsxResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxResponse.Content.Headers.ContentType?.MediaType);
        Assert.True((await xlsxResponse.Content.ReadAsByteArrayAsync()).Length > 0);

        // CSV headers are the Spanish property names; assert the export carries NO monetary column (aclaración №8).
        var csvResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/export?format=csv&year=2026");
        csvResponse.EnsureSuccessStatusCode();
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("Empleado", csv);
        Assert.Contains("Tipo", csv);
        Assert.DoesNotContain("amount", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currency", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("monto", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonnelActionsBandeja_WithoutViewReports_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var queryResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/query",
            new { year = 2026 });
        Assert.Equal(HttpStatusCode.Forbidden, queryResponse.StatusCode);

        var exportResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/export?year=2026");
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }
}
