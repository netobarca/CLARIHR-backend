using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Domain guards of the leave-configuration masters: the <see cref="IncapacityRisk"/> subsidy
/// tranche invariants (day-1 start, contiguity, single open-ended tail, payer/percent bounds,
/// subsidy-flag coupling) plus the scalar guards of <see cref="PayrollPeriodDefinition"/>,
/// <see cref="CompanyHoliday"/> and <see cref="MedicalClinic"/>.
/// </summary>
public sealed class LeaveMasterDomainTests
{
    // ------------------------------------------------------------------
    // IncapacityRisk.ReplaceParameters
    // ------------------------------------------------------------------

    [Fact]
    public void ReplaceParameters_WithContiguousTranchesAndOpenEndedTail_ShouldReplaceSet()
    {
        var risk = CreateRisk();

        risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: 3, SubsidyPercent: 75m, PayerCode: " empresa "),
            new(DayFrom: 4, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]);

        Assert.Equal(2, risk.Parameters.Count);

        var first = risk.Parameters.First();
        Assert.Equal(1, first.DayFrom);
        Assert.Equal(3, first.DayTo);
        Assert.Equal(75m, first.SubsidyPercent);
        Assert.Equal(IncapacityPayerCodes.Empresa, first.PayerCode);

        var last = risk.Parameters.Last();
        Assert.Equal(4, last.DayFrom);
        Assert.Null(last.DayTo);
        Assert.Equal(IncapacityPayerCodes.Isss, last.PayerCode);
    }

    [Fact]
    public void ReplaceParameters_ShouldAssignSequentialSortOrder()
    {
        var risk = CreateRisk();

        risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: 3, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Empresa),
            new(DayFrom: 4, DayTo: 10, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
            new(DayFrom: 11, DayTo: null, SubsidyPercent: 100m, PayerCode: IncapacityPayerCodes.Isss),
        ]);

        Assert.Equal([1, 2, 3], risk.Parameters.Select(parameter => parameter.SortOrder).ToArray());
    }

    [Fact]
    public void ReplaceParameters_WhenFirstTrancheDoesNotStartAtDayOne_ShouldThrow()
    {
        var risk = CreateRisk();

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 2, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]));

        Assert.Contains("must start at day 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceParameters_WhenTranchesAreNotContiguous_ShouldThrow()
    {
        var risk = CreateRisk();

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: 3, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Empresa),
            new(DayFrom: 5, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]));

        Assert.Contains("must be contiguous", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceParameters_WhenTrancheEndsBeforeItStarts_ShouldThrow()
    {
        var risk = CreateRisk();

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: 0, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]));

        Assert.Contains("greater than or equal to its start day", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceParameters_WhenSubsidyPercentExceedsOneHundred_ShouldThrow()
    {
        var risk = CreateRisk();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: null, SubsidyPercent: 100.01m, PayerCode: IncapacityPayerCodes.Isss),
        ]));
    }

    [Fact]
    public void ReplaceParameters_WhenPayerCodeIsUnknown_ShouldThrow()
    {
        var risk = CreateRisk();

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: null, SubsidyPercent: 75m, PayerCode: "BANCO"),
        ]));

        Assert.Contains("is not supported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceParameters_WhenOpenEndedTrancheIsNotLast_ShouldThrow()
    {
        var risk = CreateRisk();

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Empresa),
            new(DayFrom: 2, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]));

        Assert.Contains("Only the last subsidy tranche can be open-ended", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceParameters_WithoutSubsidyAndTranches_ShouldThrow()
    {
        var risk = CreateRisk(hasSubsidy: false);

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]));

        Assert.Contains("without subsidy cannot define subsidy parameters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceParameters_WithoutSubsidyAndEmptySet_ShouldClearParameters()
    {
        var risk = CreateRisk(hasSubsidy: false);
        var tokenBefore = risk.ConcurrencyToken;

        risk.ReplaceParameters([]);

        Assert.Empty(risk.Parameters);
        Assert.NotEqual(tokenBefore, risk.ConcurrencyToken);
    }

    [Fact]
    public void ReplaceParameters_WithSubsidyAndEmptySet_ShouldThrow()
    {
        var risk = CreateRisk();

        var exception = Assert.Throws<ArgumentException>(() => risk.ReplaceParameters([]));

        Assert.Contains("requires at least one subsidy parameter", exception.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // IncapacityRisk.Update
    // ------------------------------------------------------------------

    [Fact]
    public void Update_WhenTurningOffSubsidyWithLiveTranches_ShouldThrow()
    {
        var risk = CreateRisk();
        risk.ReplaceParameters(
        [
            new(DayFrom: 1, DayTo: null, SubsidyPercent: 75m, PayerCode: IncapacityPayerCodes.Isss),
        ]);

        var exception = Assert.Throws<ArgumentException>(() => risk.Update(
            "ENFERMEDAD_COMUN",
            "Enfermedad común",
            countsSeventhDay: true,
            countsSaturday: true,
            countsHoliday: true,
            usesWorkSchedule: false,
            allowsIndefinite: false,
            allowsExtension: true,
            usesFund: true,
            hasSubsidy: false));

        Assert.Contains("Cannot turn off the subsidy", exception.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // PayrollPeriodDefinition
    // ------------------------------------------------------------------

    [Fact]
    public void PayrollPeriodDefinition_WhenEndDatePrecedesStartDate_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>(() => PayrollPeriodDefinition.Create(
            "QUINCENAL",
            2026,
            1,
            "Quincena 1",
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 1)));

        Assert.Contains("End date must be greater than or equal to start date", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void PayrollPeriodDefinition_WhenYearIsOutOfRange_ShouldThrow(int year)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PayrollPeriodDefinition.Create(
            "QUINCENAL",
            year,
            1,
            "Quincena 1",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 15)));
    }

    // ------------------------------------------------------------------
    // CompanyHoliday
    // ------------------------------------------------------------------

    [Fact]
    public void CompanyHoliday_WhenScopeCodeIsUnknown_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>(() => CompanyHoliday.Create(
            new DateOnly(2026, 1, 1),
            "Año Nuevo",
            "REGIONAL"));

        Assert.Contains("is not supported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompanyHoliday_WhenDateIsDefault_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>(() => CompanyHoliday.Create(
            default,
            "Año Nuevo",
            CompanyHolidayScopes.Nacional));

        Assert.Contains("Date is required", exception.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // MedicalClinic
    // ------------------------------------------------------------------

    [Fact]
    public void MedicalClinic_WhenDescriptionIsWhitespace_ShouldThrow()
    {
        _ = Assert.Throws<ArgumentException>(() => MedicalClinic.Create("   ", specialty: null, sectorCode: null));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IncapacityRisk CreateRisk(bool hasSubsidy = true) =>
        IncapacityRisk.Create(
            "ENFERMEDAD_COMUN",
            "Enfermedad común",
            countsSeventhDay: true,
            countsSaturday: true,
            countsHoliday: true,
            usesWorkSchedule: false,
            allowsIndefinite: false,
            allowsExtension: true,
            usesFund: true,
            hasSubsidy: hasSubsidy);
}
