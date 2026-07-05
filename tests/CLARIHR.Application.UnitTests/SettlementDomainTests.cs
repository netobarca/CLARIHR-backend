using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the settlement ("liquidación") domain (PR-3): entity guards of
/// <see cref="PersonnelFileSettlement"/> (kind rules, editability, MarkIssued/Annul/SetActive transitions,
/// parameter validation) and of <see cref="PersonnelFileSettlementLine"/> (override with mandatory reason
/// D-14, value-0 rule RN-008.4, manual lines, final-amount refresh).
/// </summary>
public sealed class SettlementDomainTests
{
    private static readonly DateTime AsOf = new(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PlazaStart = new(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PersonnelFileSettlement NewSettlement() =>
        PersonnelFileSettlement.CreateSettlement(
            retirementRequestPublicId: Guid.NewGuid(),
            assignedPositionPublicId: Guid.NewGuid(),
            positionNameSnapshot: "Analista de RRHH",
            plazaStartDate: PlazaStart,
            costCenterPublicId: Guid.NewGuid(),
            costCenterNameSnapshot: "CC-RRHH",
            retirementDate: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            retirementCategoryCode: "voluntaria",
            retirementCategoryNameSnapshot: "Renuncia voluntaria",
            retirementReasonCode: "motivos_personales",
            retirementReasonNameSnapshot: "Motivos personales",
            requesterFilePublicId: Guid.NewGuid(),
            requesterNameSnapshot: "Gestora de RRHH",
            requestDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            notes: "Liquidación de prueba",
            requestedByUserId: Guid.NewGuid(),
            currencyCode: "USD");

    private static PersonnelFileSettlement NewScenario() =>
        PersonnelFileSettlement.CreateScenario(
            assignedPositionPublicId: Guid.NewGuid(),
            positionNameSnapshot: "Analista de RRHH",
            plazaStartDate: PlazaStart,
            costCenterPublicId: null,
            costCenterNameSnapshot: null,
            estimatedRetirementDate: new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc),
            retirementCategoryCode: "VOLUNTARIA",
            retirementCategoryNameSnapshot: "Renuncia voluntaria",
            retirementReasonCode: "ESTUDIOS",
            retirementReasonNameSnapshot: "Estudios",
            requesterFilePublicId: Guid.NewGuid(),
            requesterNameSnapshot: "Gestora de RRHH",
            requestDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            notes: null,
            requestedByUserId: Guid.NewGuid(),
            currencyCode: "USD");

    private static PersonnelFileSettlementLine IncomeLine(string code = "SALARIO", decimal amount = 100m)
    {
        var line = PersonnelFileSettlementLine.Create(
            SettlementConceptClass.Ingreso, code, "Salario pendiente", description: null, isSystemCalculated: true, sortOrder: 10);
        line.ApplyComputation(
            calculationBase: 600m,
            unitsOrDays: 5m,
            calculatedAmount: amount,
            exemptAmount: 0m,
            taxableExcessAmount: amount,
            calculationDetail: "5 × $20.00",
            isZeroByLaw: false,
            zeroReasonCode: null);
        return line;
    }

    // ── Creation and kind rules ──────────────────────────────────────────────────

    [Fact]
    public void CreateSettlement_StartsAsBorrador_WithNormalizedCodes()
    {
        var settlement = NewSettlement();

        Assert.Equal(SettlementKind.Liquidacion, settlement.Kind);
        Assert.Equal(SettlementStatuses.Borrador, settlement.StatusCode);
        Assert.Equal("VOLUNTARIA", settlement.RetirementCategoryCode);
        Assert.Equal("MOTIVOS_PERSONALES", settlement.RetirementReasonCode);
        Assert.True(settlement.IsEditable);
        Assert.True(settlement.IsActive);
    }

    [Fact]
    public void CreateScenario_HasNoLifecycle_AndIsAlwaysEditable()
    {
        var scenario = NewScenario();

        Assert.Equal(SettlementKind.Escenario, scenario.Kind);
        Assert.Null(scenario.StatusCode);
        Assert.True(scenario.IsEditable);
    }

    [Fact]
    public void CreateSettlement_RetirementDateBeforePlazaStart_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            PersonnelFileSettlement.CreateSettlement(
                Guid.NewGuid(), Guid.NewGuid(), null, PlazaStart, null, null,
                retirementDate: PlazaStart.AddDays(-1),
                "VOLUNTARIA", null, "ESTUDIOS", null,
                Guid.NewGuid(), "RRHH", AsOf, null, Guid.NewGuid(), "USD"));

    // ── Scenario assumptions and header edits ───────────────────────────────────

    [Fact]
    public void UpdateScenarioAssumptions_OnRealSettlement_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            NewSettlement().UpdateScenarioAssumptions(AsOf, "INVOLUNTARIA", null, "BAJO_DESEMPENO", null));

    [Fact]
    public void UpdateScenarioAssumptions_OnScenario_RecomputesFacts()
    {
        var scenario = NewScenario();
        scenario.UpdateScenarioAssumptions(
            new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc), "INVOLUNTARIA", "Despido", "BAJO_DESEMPENO", "Bajo desempeño");

        Assert.Equal("INVOLUNTARIA", scenario.RetirementCategoryCode);
        Assert.Equal(new DateTime(2027, 1, 31), scenario.RetirementDate.Date);
    }

    // ── Parameters ───────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateParameters_ValidValues_SnapshotsThem()
    {
        var settlement = NewSettlement();
        settlement.UpdateParameters(
            minimumMonthlyWage: 365.00m,
            indemnityCapMultiplier: 4m,
            resignationCapMultiplier: 2m,
            vacationDays: 15m,
            vacationPremiumPercent: 30m,
            aguinaldoDays: 19m,
            resignationBenefitDays: 15m,
            resignationMinimumServiceYears: 2,
            aguinaldoExemptionMultiplier: 2m,
            monthDivisorDays: 30,
            yearDivisorDays: 365);

        Assert.Equal(365.00m, settlement.MinimumMonthlyWage);
        Assert.Equal(4m, settlement.IndemnityCapMultiplier);
        Assert.Equal(19m, settlement.AguinaldoDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void UpdateParameters_NonPositiveMinimumWage_Throws(decimal minimumWage) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewSettlement().UpdateParameters(minimumWage, 4m, 2m, 15m, 30m, 15m, 15m, 2, 2m, 30, 365));

    // ── Issue (MarkIssued) ───────────────────────────────────────────────────────

    [Fact]
    public void MarkIssued_WithIncludedIncome_TransitionsToEmitida()
    {
        var settlement = NewSettlement();
        settlement.AddLine(IncomeLine());
        settlement.ApplyCalculation(600m, 4, 121, 600m, 600m, 100m, 10m, 90m, 60m, 160m);

        settlement.MarkIssued(Guid.NewGuid(), AsOf, confirmNegativeNet: false);

        Assert.Equal(SettlementStatuses.Emitida, settlement.StatusCode);
        Assert.False(settlement.IsEditable);
        Assert.NotNull(settlement.IssuedAtUtc);
    }

    [Fact]
    public void MarkIssued_WithoutIncludedIncome_Throws()
    {
        var settlement = NewSettlement();
        var line = IncomeLine();
        line.SetIncluded(false);
        settlement.AddLine(line);

        Assert.Throws<InvalidOperationException>(() => settlement.MarkIssued(Guid.NewGuid(), AsOf, false));
    }

    [Fact]
    public void MarkIssued_NegativeNetWithoutConfirmation_Throws()
    {
        var settlement = NewSettlement();
        settlement.AddLine(IncomeLine());
        settlement.ApplyCalculation(600m, 4, 121, 600m, 600m, 100m, 150m, -50m, 60m, 160m);

        Assert.Throws<InvalidOperationException>(() => settlement.MarkIssued(Guid.NewGuid(), AsOf, confirmNegativeNet: false));
        settlement.MarkIssued(Guid.NewGuid(), AsOf, confirmNegativeNet: true);
        Assert.Equal(SettlementStatuses.Emitida, settlement.StatusCode);
    }

    [Fact]
    public void MarkIssued_OnScenario_Throws() =>
        Assert.Throws<InvalidOperationException>(() => NewScenario().MarkIssued(Guid.NewGuid(), AsOf, false));

    [Fact]
    public void MarkIssued_Twice_Throws()
    {
        var settlement = NewSettlement();
        settlement.AddLine(IncomeLine());
        settlement.MarkIssued(Guid.NewGuid(), AsOf, false);

        Assert.Throws<InvalidOperationException>(() => settlement.MarkIssued(Guid.NewGuid(), AsOf, false));
    }

    // ── Annul ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Annul_FromBorrador_ReasonOptional()
    {
        var settlement = NewSettlement();
        settlement.Annul(Guid.NewGuid(), AsOf, reason: null);

        Assert.Equal(SettlementStatuses.Anulada, settlement.StatusCode);
        Assert.False(settlement.IsEditable);
    }

    [Fact]
    public void Annul_FromEmitida_RequiresReason()
    {
        var settlement = NewSettlement();
        settlement.AddLine(IncomeLine());
        settlement.MarkIssued(Guid.NewGuid(), AsOf, false);

        Assert.Throws<InvalidOperationException>(() => settlement.Annul(Guid.NewGuid(), AsOf, reason: "  "));
        settlement.Annul(Guid.NewGuid(), AsOf, reason: "Error en el cálculo");
        Assert.Equal(SettlementStatuses.Anulada, settlement.StatusCode);
        Assert.Equal("Error en el cálculo", settlement.AnnulmentReason);
    }

    [Fact]
    public void Annul_Anulada_Throws()
    {
        var settlement = NewSettlement();
        settlement.Annul(Guid.NewGuid(), AsOf, null);

        Assert.Throws<InvalidOperationException>(() => settlement.Annul(Guid.NewGuid(), AsOf, "otra vez"));
    }

    [Fact]
    public void Annul_OnScenario_Throws() =>
        Assert.Throws<InvalidOperationException>(() => NewScenario().Annul(Guid.NewGuid(), AsOf, null));

    // ── Soft delete (scenario only) ─────────────────────────────────────────────

    [Fact]
    public void SetActive_OnScenario_SoftDeletes()
    {
        var scenario = NewScenario();
        scenario.SetActive(false);
        Assert.False(scenario.IsActive);
    }

    [Fact]
    public void SetActive_OnRealSettlement_Throws() =>
        Assert.Throws<InvalidOperationException>(() => NewSettlement().SetActive(false));

    // ── Editability guards ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateHeader_AfterIssue_Throws()
    {
        var settlement = NewSettlement();
        settlement.AddLine(IncomeLine());
        settlement.MarkIssued(Guid.NewGuid(), AsOf, false);

        Assert.Throws<InvalidOperationException>(() =>
            settlement.UpdateHeader(Guid.NewGuid(), "Otra gestora", AsOf, null));
        Assert.Throws<InvalidOperationException>(() => settlement.AddLine(IncomeLine("OTRO_INGRESO")));
        Assert.Throws<InvalidOperationException>(() => settlement.ClearLines());
    }

    // ── Lines ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Line_SetOverride_RequiresReason_AndKeepsCalculatedVisible()
    {
        var line = IncomeLine(amount: 100m);

        Assert.Throws<InvalidOperationException>(() => line.SetOverride(120m, "  "));
        line.SetOverride(120m, "Ajuste acordado con el contador");

        Assert.Equal(100m, line.CalculatedAmount);
        Assert.Equal(120m, line.FinalAmount);

        line.ClearOverride();
        Assert.Equal(100m, line.FinalAmount);
    }

    [Fact]
    public void Line_OverrideSurvivesRecalculation()
    {
        var line = IncomeLine(amount: 100m);
        line.SetOverride(120m, "Ajuste");

        line.ApplyComputation(600m, 6m, 110m, 0m, 110m, "6 × $18.33", false, null);

        Assert.Equal(110m, line.CalculatedAmount);
        Assert.Equal(120m, line.FinalAmount);
    }

    [Fact]
    public void Line_ZeroByLaw_RecordsReason()
    {
        var line = PersonnelFileSettlementLine.Create(
            SettlementConceptClass.Ingreso, "RENUNCIA_VOLUNTARIA", "Compensación por renuncia", null, true, 50);
        line.ApplyComputation(0m, null, 0m, 0m, 0m, null, isZeroByLaw: true, zeroReasonCode: "SERVICIO_MINIMO_NO_CUMPLIDO");

        Assert.True(line.IsZeroByLaw);
        Assert.Equal("SERVICIO_MINIMO_NO_CUMPLIDO", line.ZeroReasonCode);
        Assert.Equal(0m, line.FinalAmount);
    }

    [Fact]
    public void Line_UpdateManual_OnEngineLine_Throws()
    {
        var line = IncomeLine();
        Assert.Throws<InvalidOperationException>(() => line.UpdateManual("Otro", 50m));
    }

    [Fact]
    public void Line_UpdateManual_SetsAmount()
    {
        var line = PersonnelFileSettlementLine.Create(
            SettlementConceptClass.Ingreso, "OTRO_INGRESO", "Otro ingreso", "Bono de despedida", isSystemCalculated: false, 90);
        line.UpdateManual("Bono de despedida", 75.50m);

        Assert.Equal(75.50m, line.FinalAmount);
        Assert.Throws<ArgumentOutOfRangeException>(() => line.UpdateManual("Negativo", -1m));
    }

    [Fact]
    public void Line_SetUnitsOrDays_Negative_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => IncomeLine().SetUnitsOrDays(-1m));
}
