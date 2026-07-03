using CLARIHR.Application.Features.PersonnelFiles;
using Xunit;

namespace CLARIHR.Application.UnitTests;

public sealed class CompensationConceptRulesTests
{
    [Fact]
    public void IsBaseSalaryConcept_CodeFlaggedInCatalog_ReturnsTrue()
    {
        var result = CompensationConceptRules.IsBaseSalaryConcept("SUELDO_ORDINARIO", ["SUELDO_ORDINARIO"]);

        Assert.True(result);
    }

    [Fact]
    public void IsBaseSalaryConcept_LegacyCodeWithoutFlag_ReturnsTrueByFallback()
    {
        var result = CompensationConceptRules.IsBaseSalaryConcept("SALARIO_BASE", []);

        Assert.True(result);
    }

    [Fact]
    public void IsBaseSalaryConcept_NormalizesCaseAndWhitespace()
    {
        var result = CompensationConceptRules.IsBaseSalaryConcept("  sueldo_ordinario  ", ["SUELDO_ORDINARIO"]);

        Assert.True(result);
    }

    [Fact]
    public void IsBaseSalaryConcept_UnflaggedOtherCode_ReturnsFalse()
    {
        var result = CompensationConceptRules.IsBaseSalaryConcept("BONO", ["SUELDO_ORDINARIO"]);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsBaseSalaryConcept_MissingCode_ReturnsFalse(string? conceptTypeCode)
    {
        var result = CompensationConceptRules.IsBaseSalaryConcept(conceptTypeCode, ["SUELDO_ORDINARIO"]);

        Assert.False(result);
    }

    [Fact]
    public void EvaluateBaseSalary_AnotherActiveBaseSalary_Fails()
    {
        var result = CompensationConceptRules.EvaluateBaseSalary(
            negotiatedAmount: 500m,
            anotherActiveBaseSalaryExists: true,
            range: null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EvaluateBaseSalary_WithinRange_Succeeds()
    {
        var result = CompensationConceptRules.EvaluateBaseSalary(
            negotiatedAmount: 500m,
            anotherActiveBaseSalaryExists: false,
            range: new CompensationConceptRules.SalaryRange(400m, 600m));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EvaluateBaseSalary_OutsideRange_Fails()
    {
        var result = CompensationConceptRules.EvaluateBaseSalary(
            negotiatedAmount: 700m,
            anotherActiveBaseSalaryExists: false,
            range: new CompensationConceptRules.SalaryRange(400m, 600m));

        Assert.True(result.IsFailure);
    }
}
