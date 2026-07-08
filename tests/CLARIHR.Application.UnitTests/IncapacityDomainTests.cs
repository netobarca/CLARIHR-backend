using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Domain guards of <see cref="PersonnelFileIncapacity"/> (vacaciones/incapacidades PR-3): origin-driven
/// initial status (RRHH → REGISTRADA, AUTOSERVICIO → EN_REVISION), date coherence (open-ended only when
/// the risk allows it), the guarded lifecycle (Confirm / CloseIndefinite / Annul), the audited computable
/// days override (RN-07), the <see cref="PersonnelFileIncapacity.ApplyCalculation"/>-only breakdown write
/// path, the R-T6 <see cref="IncapacityStatuses.CountsAsRegistered"/> predicate, the concurrency-token
/// rotation on every mutation, and the mirrored <see cref="PersonnelFileIncapacityDocument"/> guards.
/// </summary>
public sealed class IncapacityDomainTests
{
    private static readonly DateOnly Start = new(2026, 7, 1);
    private static readonly DateOnly End = new(2026, 7, 10);

    // ------------------------------------------------------------------
    // Create
    // ------------------------------------------------------------------

    [Fact]
    public void Create_WithRrhhOrigin_ShouldStartRegistrada()
    {
        var incapacity = CreateIncapacity(originCode: IncapacityOrigins.Rrhh);

        Assert.Equal(IncapacityStatuses.Registrada, incapacity.StatusCode);
        Assert.Equal(IncapacityOrigins.Rrhh, incapacity.OriginCode);
        Assert.True(incapacity.IsActive);
        Assert.NotEqual(Guid.Empty, incapacity.PublicId);
        Assert.NotEqual(Guid.Empty, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void Create_WithAutoservicioOrigin_ShouldStartEnRevision()
    {
        var incapacity = CreateIncapacity(originCode: IncapacityOrigins.Autoservicio);

        Assert.Equal(IncapacityStatuses.EnRevision, incapacity.StatusCode);
        Assert.Equal(IncapacityOrigins.Autoservicio, incapacity.OriginCode);
    }

    [Fact]
    public void Create_ShouldNormalizeOriginAndSnapshots()
    {
        var incapacity = CreateIncapacity(originCode: " rrhh ", riskCodeSnapshot: " enf-comun ");

        Assert.Equal(IncapacityOrigins.Rrhh, incapacity.OriginCode);
        Assert.Equal("ENF-COMUN", incapacity.RiskCodeSnapshot);
    }

    [Fact]
    public void Create_WithUnknownOrigin_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateIncapacity(originCode: "PORTAL"));

        Assert.Equal("originCode", exception.ParamName);
    }

    [Fact]
    public void Create_WithoutRequestedByUserId_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() => CreateIncapacity(requestedByUserId: "   "));

    [Fact]
    public void Create_OpenEndedWhenRiskDisallowsIndefinite_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateIncapacity(openEnded: true, riskAllowsIndefinite: false));

        Assert.Equal("endDate", exception.ParamName);
    }

    [Fact]
    public void Create_OpenEndedWhenRiskAllowsIndefinite_ShouldSucceed()
    {
        var incapacity = CreateIncapacity(openEnded: true, riskAllowsIndefinite: true);

        Assert.Null(incapacity.EndDate);
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() => CreateIncapacity(endDate: Start.AddDays(-1)));

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void Create_WithNonPositiveRiskId_ShouldThrow(long riskId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIncapacity(incapacityRiskId: riskId));

    [Fact]
    public void Create_WithNonPositiveTypeId_ShouldThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIncapacity(incapacityTypeId: 0));

    [Fact]
    public void Create_ShouldSnapshotRiskFlags()
    {
        var incapacity = CreateIncapacity();

        Assert.True(incapacity.RiskCountsSeventhDaySnapshot);
        Assert.False(incapacity.RiskCountsSaturdaySnapshot);
        Assert.True(incapacity.RiskCountsHolidaySnapshot);
        Assert.True(incapacity.RiskUsesFundSnapshot);
        Assert.False(incapacity.RiskHasSubsidySnapshot);
    }

    // ------------------------------------------------------------------
    // Confirm
    // ------------------------------------------------------------------

    [Fact]
    public void Confirm_FromEnRevision_ShouldTransitionToRegistradaAndRecordAudit()
    {
        var incapacity = CreateIncapacity(originCode: IncapacityOrigins.Autoservicio);
        var originalToken = incapacity.ConcurrencyToken;
        var confirmedAt = new DateTime(2026, 7, 8, 15, 0, 0, DateTimeKind.Utc);

        incapacity.Confirm("hr-user", confirmedAt);

        Assert.Equal(IncapacityStatuses.Registrada, incapacity.StatusCode);
        Assert.Equal("hr-user", incapacity.ConfirmedByUserId);
        Assert.Equal(confirmedAt, incapacity.ConfirmedAtUtc);
        Assert.NotEqual(originalToken, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void Confirm_OnRegistrada_ShouldThrow()
    {
        var incapacity = CreateIncapacity(originCode: IncapacityOrigins.Rrhh);

        Assert.Throws<InvalidOperationException>(() => incapacity.Confirm("hr-user", DateTime.UtcNow));
    }

    [Fact]
    public void Confirm_OnAnulada_ShouldThrow()
    {
        var incapacity = CreateIncapacity(originCode: IncapacityOrigins.Autoservicio);
        incapacity.Annul("captura errónea", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => incapacity.Confirm("hr-user", DateTime.UtcNow));
    }

    [Fact]
    public void Confirm_WithoutUserId_ShouldThrow()
    {
        var incapacity = CreateIncapacity(originCode: IncapacityOrigins.Autoservicio);

        Assert.Throws<ArgumentException>(() => incapacity.Confirm("  ", DateTime.UtcNow));
    }

    // ------------------------------------------------------------------
    // CloseIndefinite
    // ------------------------------------------------------------------

    [Fact]
    public void CloseIndefinite_OnOpenEndedRecord_ShouldFixEndDate()
    {
        var incapacity = CreateIncapacity(openEnded: true, riskAllowsIndefinite: true);
        var originalToken = incapacity.ConcurrencyToken;

        incapacity.CloseIndefinite(Start.AddDays(20));

        Assert.Equal(Start.AddDays(20), incapacity.EndDate);
        Assert.NotEqual(originalToken, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void CloseIndefinite_WhenEndDateAlreadySet_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<InvalidOperationException>(() => incapacity.CloseIndefinite(End.AddDays(5)));
    }

    [Fact]
    public void CloseIndefinite_WithEndDateBeforeStart_ShouldThrow()
    {
        var incapacity = CreateIncapacity(openEnded: true, riskAllowsIndefinite: true);

        Assert.Throws<ArgumentException>(() => incapacity.CloseIndefinite(Start.AddDays(-1)));
    }

    [Fact]
    public void CloseIndefinite_OnAnulada_ShouldThrow()
    {
        var incapacity = CreateIncapacity(openEnded: true, riskAllowsIndefinite: true);
        incapacity.Annul("captura errónea", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => incapacity.CloseIndefinite(Start.AddDays(3)));
    }

    // ------------------------------------------------------------------
    // Annul
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(IncapacityOrigins.Autoservicio)] // EN_REVISION
    [InlineData(IncapacityOrigins.Rrhh)] // REGISTRADA
    public void Annul_FromAnnullableState_ShouldSetAnuladaAndTurnOffIsActive(string origin)
    {
        var incapacity = CreateIncapacity(originCode: origin);
        var originalToken = incapacity.ConcurrencyToken;
        var annulledAt = new DateTime(2026, 7, 9, 9, 0, 0, DateTimeKind.Utc);

        incapacity.Annul("duplicado", annulledAt);

        Assert.Equal(IncapacityStatuses.Anulada, incapacity.StatusCode);
        Assert.Equal("duplicado", incapacity.AnnulmentReason);
        Assert.Equal(annulledAt, incapacity.AnnulledAtUtc);
        Assert.False(incapacity.IsActive);
        Assert.NotEqual(originalToken, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void Annul_WithoutReason_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<ArgumentException>(() => incapacity.Annul("  ", DateTime.UtcNow));
    }

    [Fact]
    public void Annul_OnAnulada_ShouldThrow()
    {
        var incapacity = CreateIncapacity();
        incapacity.Annul("duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => incapacity.Annul("otra vez", DateTime.UtcNow));
    }

    // ------------------------------------------------------------------
    // OverrideComputableDays (RN-07)
    // ------------------------------------------------------------------

    [Fact]
    public void OverrideComputableDays_WithNote_ShouldSetValueFlagAndNote()
    {
        var incapacity = CreateIncapacity();
        var originalToken = incapacity.ConcurrencyToken;

        incapacity.OverrideComputableDays(7, "ajuste por asueto local");

        Assert.Equal(7, incapacity.ComputableDays);
        Assert.True(incapacity.ComputableDaysOverridden);
        Assert.Equal("ajuste por asueto local", incapacity.OverrideNote);
        Assert.NotEqual(originalToken, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void OverrideComputableDays_WithoutNote_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<ArgumentException>(() => incapacity.OverrideComputableDays(7, " "));
        Assert.False(incapacity.ComputableDaysOverridden);
    }

    [Fact]
    public void OverrideComputableDays_WithNegativeValue_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<ArgumentOutOfRangeException>(() => incapacity.OverrideComputableDays(-1, "nota"));
    }

    [Fact]
    public void OverrideComputableDays_OnAnulada_ShouldThrow()
    {
        var incapacity = CreateIncapacity();
        incapacity.Annul("duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => incapacity.OverrideComputableDays(7, "nota"));
    }

    // ------------------------------------------------------------------
    // ApplyCalculation
    // ------------------------------------------------------------------

    [Fact]
    public void ApplyCalculation_ShouldAssignEveryBreakdownField()
    {
        var incapacity = CreateIncapacity();
        var originalToken = incapacity.ConcurrencyToken;
        var snapshot = new IncapacityCalculationSnapshot(
            CalendarDays: 10,
            ComputableDays: 8,
            SubsidizedDays: 5,
            DiscountDays: 3,
            EmployerDays: 0,
            MonthlyBaseSalary: 600.00m,
            DailySalary: 20.00m,
            SubsidyAmount: 75.00m,
            DiscountAmount: 60.00m,
            EmployerAmount: 0.00m,
            TrancheDetailJson: """[{"from":1,"to":3,"payer":"EMPRESA"}]""");

        incapacity.ApplyCalculation(snapshot);

        Assert.Equal(10, incapacity.CalendarDays);
        Assert.Equal(8, incapacity.ComputableDays);
        Assert.Equal(5, incapacity.SubsidizedDays);
        Assert.Equal(3, incapacity.DiscountDays);
        Assert.Equal(0, incapacity.EmployerDays);
        Assert.Equal(600.00m, incapacity.MonthlyBaseSalary);
        Assert.Equal(20.00m, incapacity.DailySalary);
        Assert.Equal(75.00m, incapacity.SubsidyAmount);
        Assert.Equal(60.00m, incapacity.DiscountAmount);
        Assert.Equal(0.00m, incapacity.EmployerAmount);
        Assert.Equal("""[{"from":1,"to":3,"payer":"EMPRESA"}]""", incapacity.TrancheDetailJson);
        Assert.NotEqual(originalToken, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void ApplyCalculation_OnAnulada_ShouldThrow()
    {
        var incapacity = CreateIncapacity();
        incapacity.Annul("duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => incapacity.ApplyCalculation(EmptySnapshot()));
    }

    [Fact]
    public void ApplyCalculation_WithNullSnapshot_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<ArgumentNullException>(() => incapacity.ApplyCalculation(null!));
    }

    // ------------------------------------------------------------------
    // UpdateDetails
    // ------------------------------------------------------------------

    [Fact]
    public void UpdateDetails_ShouldRewriteReferencesDatesAndNotes()
    {
        var incapacity = CreateIncapacity();
        var originalToken = incapacity.ConcurrencyToken;

        incapacity.UpdateDetails(
            incapacityRiskId: 22,
            riskCodeSnapshot: "acc-trabajo",
            riskCountsSeventhDaySnapshot: false,
            riskCountsSaturdaySnapshot: true,
            riskCountsHolidaySnapshot: false,
            riskUsesFundSnapshot: false,
            riskHasSubsidySnapshot: true,
            medicalClinicId: 5,
            incapacityTypeId: 33,
            assignedPositionPublicId: null,
            payrollTypeCode: "mensual",
            payrollPeriodDefinitionId: 7,
            startDate: Start.AddDays(1),
            endDate: End.AddDays(1),
            extendsIncapacityId: 99,
            notes: "actualizada",
            riskAllowsIndefinite: false);

        Assert.Equal(22, incapacity.IncapacityRiskId);
        Assert.Equal("ACC-TRABAJO", incapacity.RiskCodeSnapshot);
        Assert.False(incapacity.RiskCountsSeventhDaySnapshot);
        Assert.True(incapacity.RiskCountsSaturdaySnapshot);
        Assert.False(incapacity.RiskCountsHolidaySnapshot);
        Assert.False(incapacity.RiskUsesFundSnapshot);
        Assert.True(incapacity.RiskHasSubsidySnapshot);
        Assert.Equal(5, incapacity.MedicalClinicId);
        Assert.Equal(33, incapacity.IncapacityTypeId);
        Assert.Null(incapacity.AssignedPositionPublicId);
        Assert.Equal("MENSUAL", incapacity.PayrollTypeCode);
        Assert.Equal(7, incapacity.PayrollPeriodDefinitionId);
        Assert.Equal(Start.AddDays(1), incapacity.StartDate);
        Assert.Equal(End.AddDays(1), incapacity.EndDate);
        Assert.Equal(99, incapacity.ExtendsIncapacityId);
        Assert.Equal("actualizada", incapacity.Notes);
        Assert.NotEqual(originalToken, incapacity.ConcurrencyToken);
    }

    [Fact]
    public void UpdateDetails_OnAnulada_ShouldThrow()
    {
        var incapacity = CreateIncapacity();
        incapacity.Annul("duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => UpdateDetailsWithDates(incapacity, Start, End));
    }

    [Fact]
    public void UpdateDetails_WithEndDateBeforeStart_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<ArgumentException>(() => UpdateDetailsWithDates(incapacity, Start, Start.AddDays(-2)));
    }

    [Fact]
    public void UpdateDetails_OpenEndedWithoutRiskPermission_ShouldThrow()
    {
        var incapacity = CreateIncapacity();

        Assert.Throws<ArgumentException>(() => UpdateDetailsWithDates(incapacity, Start, endDate: null));
    }

    // ------------------------------------------------------------------
    // Statuses (R-T6)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(IncapacityStatuses.Registrada, true)]
    [InlineData(IncapacityStatuses.EnRevision, false)]
    [InlineData(IncapacityStatuses.Anulada, false)]
    public void CountsAsRegistered_OnlyRegistradaCounts(string status, bool expected) =>
        Assert.Equal(expected, IncapacityStatuses.CountsAsRegistered(status));

    [Fact]
    public void StatusSets_ShouldMatchLifecycle()
    {
        Assert.Equal([IncapacityStatuses.EnRevision], IncapacityStatuses.Confirmable);
        Assert.Equal([IncapacityStatuses.EnRevision, IncapacityStatuses.Registrada], IncapacityStatuses.Annullable);
    }

    // ------------------------------------------------------------------
    // PersonnelFileIncapacityDocument (mirror of MedicalClaimDocument)
    // ------------------------------------------------------------------

    [Fact]
    public void Document_Create_ShouldInitializeFieldsAndBind()
    {
        var publicId = Guid.NewGuid();
        var filePublicId = Guid.NewGuid();

        var document = PersonnelFileIncapacityDocument.Create(
            publicId,
            documentTypeCatalogItemId: 10,
            filePublicId,
            fileName: " constancia.pdf ",
            contentType: "application/pdf",
            sizeBytes: 1234,
            observations: "  emitida por ISSS  ");

        document.BindToIncapacity(42);

        Assert.Equal(publicId, document.PublicId);
        Assert.Equal(10, document.DocumentTypeCatalogItemId);
        Assert.Equal(filePublicId, document.FilePublicId);
        Assert.Equal("constancia.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(1234, document.SizeBytes);
        Assert.Equal("emitida por ISSS", document.Observations);
        Assert.Equal(42, document.PersonnelFileIncapacityId);
        Assert.True(document.IsActive);
    }

    [Fact]
    public void Document_Create_WithoutDocumentType_ShouldBeAllowed()
    {
        var document = CreateDocument(documentTypeCatalogItemId: null);

        Assert.Null(document.DocumentTypeCatalogItemId);
    }

    [Fact]
    public void Document_Create_WithNonPositiveDocumentType_ShouldThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDocument(documentTypeCatalogItemId: 0));

    [Fact]
    public void Document_Create_WithEmptyFilePublicId_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() => PersonnelFileIncapacityDocument.Create(
            Guid.NewGuid(),
            documentTypeCatalogItemId: 10,
            filePublicId: Guid.Empty,
            fileName: "constancia.pdf",
            contentType: "application/pdf",
            sizeBytes: 10,
            observations: null));

    [Fact]
    public void Document_ReplaceFileReference_ShouldRewriteFileFieldsAndRotateToken()
    {
        var document = CreateDocument();
        var originalToken = document.ConcurrencyToken;
        var newFileId = Guid.NewGuid();

        document.ReplaceFileReference(newFileId, "nueva.pdf", "application/pdf", 999);

        Assert.Equal(newFileId, document.FilePublicId);
        Assert.Equal("nueva.pdf", document.FileName);
        Assert.Equal(999, document.SizeBytes);
        Assert.NotEqual(originalToken, document.ConcurrencyToken);
    }

    [Fact]
    public void Document_ReplaceFileReference_WithEmptyFileId_ShouldThrow()
    {
        var document = CreateDocument();

        Assert.Throws<ArgumentException>(() =>
            document.ReplaceFileReference(Guid.Empty, "nueva.pdf", "application/pdf", 999));
    }

    [Fact]
    public void Document_UpdateMetadata_ShouldRewriteTypeAndObservations()
    {
        var document = CreateDocument();
        var originalToken = document.ConcurrencyToken;

        document.UpdateMetadata(documentTypeCatalogItemId: 55, observations: "reclasificada");

        Assert.Equal(55, document.DocumentTypeCatalogItemId);
        Assert.Equal("reclasificada", document.Observations);
        Assert.NotEqual(originalToken, document.ConcurrencyToken);
    }

    [Fact]
    public void Document_UpdateMetadata_WithNonPositiveType_ShouldThrow()
    {
        var document = CreateDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => document.UpdateMetadata(-1, null));
    }

    [Fact]
    public void Document_Inactivate_ShouldTurnOffIsActiveAndRotateToken()
    {
        var document = CreateDocument();
        var originalToken = document.ConcurrencyToken;

        document.Inactivate();

        Assert.False(document.IsActive);
        Assert.NotEqual(originalToken, document.ConcurrencyToken);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PersonnelFileIncapacity CreateIncapacity(
        string originCode = IncapacityOrigins.Rrhh,
        string requestedByUserId = "user-1",
        long incapacityRiskId = 11,
        string riskCodeSnapshot = "ENF_COMUN",
        long incapacityTypeId = 21,
        DateOnly? endDate = null,
        bool openEnded = false,
        bool riskAllowsIndefinite = false)
    {
        // Bounded record by default; `openEnded: true` forces an explicit null end date.
        var effectiveEndDate = openEnded ? null : (endDate ?? (DateOnly?)End);

        return PersonnelFileIncapacity.Create(
            requesterFilePublicId: Guid.NewGuid(),
            requesterNameSnapshot: "Empleado Uno",
            requestedByUserId: requestedByUserId,
            originCode: originCode,
            incapacityRiskId: incapacityRiskId,
            riskCodeSnapshot: riskCodeSnapshot,
            riskCountsSeventhDaySnapshot: true,
            riskCountsSaturdaySnapshot: false,
            riskCountsHolidaySnapshot: true,
            riskUsesFundSnapshot: true,
            riskHasSubsidySnapshot: false,
            medicalClinicId: null,
            incapacityTypeId: incapacityTypeId,
            assignedPositionPublicId: Guid.NewGuid(),
            payrollTypeCode: null,
            payrollPeriodDefinitionId: null,
            startDate: Start,
            endDate: effectiveEndDate,
            extendsIncapacityId: null,
            notes: null,
            riskAllowsIndefinite: riskAllowsIndefinite);
    }

    private static void UpdateDetailsWithDates(PersonnelFileIncapacity incapacity, DateOnly startDate, DateOnly? endDate) =>
        incapacity.UpdateDetails(
            incapacityRiskId: 11,
            riskCodeSnapshot: "ENF_COMUN",
            riskCountsSeventhDaySnapshot: true,
            riskCountsSaturdaySnapshot: false,
            riskCountsHolidaySnapshot: true,
            riskUsesFundSnapshot: true,
            riskHasSubsidySnapshot: false,
            medicalClinicId: null,
            incapacityTypeId: 21,
            assignedPositionPublicId: null,
            payrollTypeCode: null,
            payrollPeriodDefinitionId: null,
            startDate: startDate,
            endDate: endDate,
            extendsIncapacityId: null,
            notes: null,
            riskAllowsIndefinite: false);

    private static IncapacityCalculationSnapshot EmptySnapshot() =>
        new(0, 0, 0, 0, 0, 0m, 0m, 0m, 0m, 0m, null);

    private static PersonnelFileIncapacityDocument CreateDocument(long? documentTypeCatalogItemId = 10) =>
        PersonnelFileIncapacityDocument.Create(
            Guid.NewGuid(),
            documentTypeCatalogItemId,
            filePublicId: Guid.NewGuid(),
            fileName: "constancia.pdf",
            contentType: "application/pdf",
            sizeBytes: 100,
            observations: null);
}
