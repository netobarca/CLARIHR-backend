using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-19 + H-20 — integration coverage for the overtime record against the employee's CONTRACTED DAY. Before this,
/// the module never read the work schedule and never compared the time range with anything: 09:00–11:00 on a
/// Mon–Fri 08:00–17:00 shift was accepted and paid on top of the salary that already covered it, and two records
/// of 14:00–18:00 and 16:00–20:00 paid the overlapping two hours twice while summing 8 h against the daily cap.
/// <para>
/// The guards: a range inside the shift → 422; a position marked <c>generatesOvertime = false</c> → 422; a range
/// overlapping another record of the day → 422; a range straddling the legal 06:00/19:00 boundary → 422 (one
/// record carries ONE factor); a zero-length range → 422. A day absent from the schedule is a free day and every
/// hour is overtime; an employee with no schedule at all only WARNS (configuration gap, not fraud).
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private const string OvertimeShiftScheduleName = "Jornada comercial";

    /// <summary>
    /// Attaches a contracted day to an already-seeded candidate: a Mon–Fri 08:00–17:00 schedule (meal 12:00–13:00,
    /// nominal — see <c>OvertimeScheduleRules.OverlapsShift</c>) and a plaza, wired through the primary assignment,
    /// which is where the overtime record reads both from. Saturday and Sunday deliberately have NO row.
    /// </summary>
    private async Task AttachOvertimeShiftAsync(
        IntegrationTestScenario scenario,
        Guid filePublicId,
        string codeSuffix,
        bool withSchedule = true,
        bool generatesOvertime = true)
    {
        var tenantId = scenario.TenantId;
        string? workdayCode = null;
        if (withSchedule)
        {
            // Through the API on purpose: the schedule's child days are tenant-scoped too, and a direct
            // `dbContext.Add` leaves them without a tenant ("Tenant-scoped writes require a tenant context").
            workdayCode = $"J-{codeSuffix}";
            using var scheduleClient = factory.CreateClientFor(PayrollConfigurationManagerContext(scenario));
            var scheduleResponse = await scheduleClient.PostAsJsonAsync(
                $"/api/v1/companies/{tenantId}/work-schedules",
                new
                {
                    code = workdayCode,
                    name = OvertimeShiftScheduleName,
                    scheduleLabel = (string?)null,
                    attendanceDateAnchor = "ENTRADA",
                    scheduleClass = "ORDINARIA",
                    totalWeeklyHours = (decimal?)null,
                    days = Enumerable.Range((int)DayOfWeek.Monday, 5)
                        .Select(day => new
                        {
                            dayOfWeek = day,
                            startTime = "08:00:00",
                            endTime = "17:00:00",
                            mealStart = (string?)"12:00:00",
                            mealEnd = (string?)"13:00:00"
                        })
                        .ToArray()
                });
            var schedulePayload = await scheduleResponse.Content.ReadAsStringAsync();
            Assert.True(
                scheduleResponse.IsSuccessStatusCode,
                $"Work-schedule seed failed: {(int)scheduleResponse.StatusCode} {schedulePayload}");
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // A plaza needs a job profile, which needs an org unit (FK + EnsurePositiveId), which needs its type.
        var orgUnitType = OrgUnitTypeCatalogItem.Create($"OUT-{codeSuffix}", $"Tipo {codeSuffix}", description: null, sortOrder: 10);
        orgUnitType.SetTenantId(tenantId);
        dbContext.OrgUnitTypeCatalogItems.Add(orgUnitType);
        await dbContext.SaveChangesAsync();

        var orgUnit = OrgUnit.Create(
            $"OU-{codeSuffix}",
            $"Unidad {codeSuffix}",
            orgUnitType.Id,
            functionalAreaCatalogItemId: null,
            parentId: null,
            sortOrder: 1,
            description: null,
            costCenterCode: null,
            managerEmployeeId: null);
        orgUnit.SetTenantId(tenantId);
        dbContext.OrgUnits.Add(orgUnit);
        await dbContext.SaveChangesAsync();

        var profile = JobProfile.Create($"JP-{codeSuffix}", $"Perfil {codeSuffix}");
        profile.SetTenantId(tenantId);
        profile.UpdateCore(
            $"JP-{codeSuffix}",
            $"Perfil {codeSuffix}",
            objective: null,
            orgUnitId: orgUnit.Id,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);
        dbContext.JobProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var slot = PositionSlot.Create(
            $"PL-{codeSuffix}",
            $"Plaza {codeSuffix}",
            profile.Id,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            PositionSlotStatus.Occupied,
            maxEmployees: 1,
            isFixedTerm: false,
            generatesOvertime,
            effectiveFromUtc: DateTime.UtcNow.Date.AddYears(-1),
            effectiveToUtc: null,
            notes: null);
        slot.SetTenantId(tenantId);
        dbContext.Set<PositionSlot>().Add(slot);
        await dbContext.SaveChangesAsync();

        var assignment = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == tenantId
                && item.PersonnelFile.PublicId == filePublicId
                && item.IsPrimary);
        assignment.Update(
            assignment.AssignmentTypeCode,
            assignment.ContractTypeCode,
            workdayCode,
            assignment.PayrollTypeCode,
            slot.PublicId,
            assignment.OrgUnitPublicId,
            assignment.WorkCenterPublicId,
            assignment.CostCenterPublicId,
            assignment.StartDate,
            assignment.EndDate,
            assignment.IsPrimary,
            assignment.Notes);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>The most recent past date falling on <paramref name="dayOfWeek"/> — the work date may be in the past.</summary>
    private static DateOnly LastPastDateOn(DayOfWeek dayOfWeek)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    /// <summary>An overtime body with an explicit work date and an explicit time RANGE (the duration is derived).</summary>
    private static object ShiftOvertimeBody(
        Guid typeId,
        Guid justificationId,
        Guid requesterFilePublicId,
        DateOnly workDate,
        TimeOnly startTime,
        TimeOnly endTime) =>
        new
        {
            workDate = workDate.ToString("yyyy-MM-dd"),
            overtimeTypePublicId = typeId,
            factorApplied = (decimal?)null,
            factorOverrideNote = (string?)null,
            startTime = startTime.ToString("HH:mm:ss"),
            endTime = endTime.ToString("HH:mm:ss"),
            justificationTypePublicId = justificationId,
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId,
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            payrollPeriodEndDate = (string?)null
        };

    /// <summary>H-20 — the finding's scenario: hours already covered by the salary cannot be billed as overtime.</summary>
    [Fact]
    public async Task Overtime_CreateInsideScheduledShift_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Jimena", "Jornada", "EMP-OTJ-A", "jimena.otj.a@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicitante", "EMP-OTJ-A2", "rodrigo.otj.a@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJA");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Tuesday), new TimeOnly(9, 0), new TimeOnly(11, 0)));

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "OVERTIME_WITHIN_SCHEDULED_SHIFT");
    }

    /// <summary>The legitimate case the guard must not touch: 17:00–19:00 starts exactly where the shift ends.</summary>
    [Fact]
    public async Task Overtime_CreateOutsideScheduledShift_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Otilia", "Fuera", "EMP-OTJ-B", "otilia.otj.b@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rosa", "Solicitante", "EMP-OTJ-B2", "rosa.otj.b@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJB");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Tuesday), new TimeOnly(17, 0), new TimeOnly(19, 0)));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        Assert.Equal(2.00m, doc.RootElement.GetProperty("durationDecimalHours").GetDecimal());

        // The schedule was found, so nothing was left unchecked and no warning is raised.
        Assert.Empty(doc.RootElement.GetProperty("warnings").EnumerateArray());
    }

    /// <summary>
    /// D3 — a weekday absent from the schedule is a FREE day, so every hour worked is overtime. This is also what
    /// makes a custom day work with no extra modelling: 06:00–18:00 Mon–Thu simply leaves Friday out.
    /// </summary>
    [Fact]
    public async Task Overtime_CreateOnDayWithoutScheduledShift_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Dominga", "Libre", "EMP-OTJ-C", "dominga.otj.c@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Raquel", "Solicitante", "EMP-OTJ-C2", "raquel.otj.c@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJC");

        // Sunday has no row in the Mon-Fri schedule: the same 09:00-11:00 that is rejected on Tuesday is valid here.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Sunday), new TimeOnly(9, 0), new TimeOnly(11, 0)));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
    }

    /// <summary>D4 — the exemption lives on the PLAZA, so it survives a change of incumbent.</summary>
    [Fact]
    public async Task Overtime_CreateOnExemptPosition_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Damián", "Dirección", "EMP-OTJ-D", "damian.otj.d@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Renata", "Solicitante", "EMP-OTJ-D2", "renata.otj.d@empresa.test");
        await AttachOvertimeShiftAsync(
            scenario, fileId, "OTJD", withSchedule: false, generatesOvertime: false);

        // No schedule either — the exemption must decide, not the missing shift, so the 422 wins over the warning.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Tuesday), new TimeOnly(20, 0), new TimeOnly(22, 0)));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_POSITION_EXEMPT");
    }

    /// <summary>T5 — a configuration gap warns and lets the record through; only the plaza's flag blocks.</summary>
    [Fact]
    public async Task Overtime_CreateWithoutWorkSchedule_SucceedsWithWarning()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Hueco", "SinJornada", "EMP-OTJ-E", "hueco.otj.e@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ramón", "Solicitante", "EMP-OTJ-E2", "ramon.otj.e@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJE", withSchedule: false);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Tuesday), new TimeOnly(9, 0), new TimeOnly(11, 0)));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        var warning = Assert.Single(doc.RootElement.GetProperty("warnings").EnumerateArray().ToArray());
        Assert.Equal("OVERTIME_WARNING_MISSING_WORK_SCHEDULE", warning.GetProperty("code").GetString());
    }

    /// <summary>H-20 — the two hours that used to be paid twice, invisibly.</summary>
    [Fact]
    public async Task Overtime_CreateOverlappingAnotherRecord_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Solapa", "Doble", "EMP-OTJ-F", "solapa.otj.f@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rubén", "Solicitante", "EMP-OTJ-F2", "ruben.otj.f@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJF");

        var workDate = LastPastDateOn(DayOfWeek.Tuesday);
        _ = await CreateOvertimeAsync(client, fileId,
            ShiftOvertimeBody(typeId, justId, requesterId, workDate, new TimeOnly(20, 0), new TimeOnly(21, 0)));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(typeId, justId, requesterId, workDate, new TimeOnly(20, 30), new TimeOnly(21, 30)));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_RECORDS_OVERLAP");
    }

    /// <summary>
    /// Art. 161 — 18:00–21:00 is 1 h daytime (×2.00) + 2 h night (×2.50). One record carries one factor, so it is
    /// rejected instead of being silently mispriced in either direction.
    /// </summary>
    [Fact]
    public async Task Overtime_CreateCrossingLegalBoundary_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Cruce", "Legal", "EMP-OTJ-G", "cruce.otj.g@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-OTJ-G2", "rita.otj.g@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJG");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Sunday), new TimeOnly(18, 0), new TimeOnly(21, 0)));

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "OVERTIME_CROSSES_LEGAL_BOUNDARY");
    }

    /// <summary>
    /// D1 — the duration is DERIVED from the range and can no longer disagree with it: what the authorizer reads is
    /// what the payroll engine multiplies by the factor.
    /// </summary>
    [Fact]
    public async Task Overtime_CreateDerivesDurationFromRange()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Deriva", "Duración", "EMP-OTJ-H", "deriva.otj.h@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ricardo", "Solicitante", "EMP-OTJ-H2", "ricardo.otj.h@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJH");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Tuesday), new TimeOnly(20, 0), new TimeOnly(21, 30)));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        Assert.Equal(1, doc.RootElement.GetProperty("durationHours").GetInt32());
        Assert.Equal(30, doc.RootElement.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(1.50m, doc.RootElement.GetProperty("durationDecimalHours").GetDecimal());
    }

    /// <summary>A range whose end precedes its start crosses midnight — legitimate, and 22:00–02:00 is 4 h of night.</summary>
    [Fact]
    public async Task Overtime_CreateCrossingMidnight_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Medianoche", "Nocturna", "EMP-OTJ-I", "medianoche.otj.i@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Roberto", "Solicitante", "EMP-OTJ-I2", "roberto.otj.i@empresa.test");
        await AttachOvertimeShiftAsync(scenario, fileId, "OTJI");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{fileId}/overtime-records",
            ShiftOvertimeBody(
                typeId, justId, requesterId, LastPastDateOn(DayOfWeek.Tuesday), new TimeOnly(22, 0), new TimeOnly(2, 0)));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        Assert.Equal(4.00m, doc.RootElement.GetProperty("durationDecimalHours").GetDecimal());
    }
}
