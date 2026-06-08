using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Application.Features.SalaryTabulator.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// ST-D: unit coverage for the pure <see cref="SalaryTabulatorCommandSupport"/> helpers — the brittle
/// domain-exception → application-error mapping (string-matched, precedence-sensitive: a message reword
/// could silently reclassify an error) and the request-number format/uniqueness invariant. Fast
/// regression independent of the integration suite that exercises the full apply-on-approve path.
/// </summary>
public sealed class SalaryTabulatorCommandSupportTests
{
    [Theory]
    [InlineData("Base amount must be greater than zero.", "SALARY_TABULATOR_AMOUNT_RULE_VIOLATION")]
    [InlineData("MinAmount cannot exceed MaxAmount.", "SALARY_TABULATOR_AMOUNT_RULE_VIOLATION")]
    [InlineData("The effective date range is invalid.", "SALARY_TABULATOR_EFFECTIVE_DATES_INVALID")]
    [InlineData("EffectiveFromUtc is required.", "SALARY_TABULATOR_EFFECTIVE_DATES_INVALID")]
    [InlineData("The requester cannot approve their own request.", "SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION")]
    [InlineData("Cannot approve from this state.", "SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION")]
    [InlineData("Only a draft request can be submitted.", "SALARY_TABULATOR_REQUEST_STATE_CONFLICT")]
    [InlineData("The request is already finalized.", "SALARY_TABULATOR_REQUEST_STATE_CONFLICT")]
    [InlineData("Something entirely unrelated went wrong.", "SALARY_TABULATOR_REQUEST_STATE_CONFLICT")]
    public void MapDomainValidation_MapsMessageToCanonicalError(string message, string expectedCode)
    {
        var error = SalaryTabulatorCommandSupport.MapDomainValidation(new InvalidOperationException(message));
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void MapDomainValidation_AmountTakesPrecedenceOverDate()
    {
        // The mapper checks the amount keywords before the date keywords; a message mentioning both
        // must classify as the amount rule (locks in the precedence the integration tests don't pin down).
        var error = SalaryTabulatorCommandSupport.MapDomainValidation(
            new InvalidOperationException("The base amount is invalid for the effective date."));

        Assert.Equal(SalaryTabulatorErrors.AmountRuleViolation.Code, error.Code);
    }

    [Fact]
    public void MapDomainValidation_ApproveTakesPrecedenceOverState()
    {
        // "approve" is checked before the draft/submitted/state bucket, so an approval-policy message that
        // also mentions "state" must still classify as the approval-policy violation, not a state conflict.
        var error = SalaryTabulatorCommandSupport.MapDomainValidation(
            new InvalidOperationException("Cannot approve a request in this state."));

        Assert.Equal(SalaryTabulatorErrors.ApprovalPolicyViolation.Code, error.Code);
    }

    [Fact]
    public void GenerateRequestNumber_ProducesPrefixedUppercase32CharToken()
    {
        var requestNumber = SalaryTabulatorCommandSupport.GenerateRequestNumber(
            new DateTime(2026, 6, 8, 13, 45, 30, 123, DateTimeKind.Utc));

        Assert.StartsWith("STR-", requestNumber);
        Assert.Equal(32, requestNumber.Length);
        Assert.Equal(requestNumber.ToUpperInvariant(), requestNumber);
    }

    [Fact]
    public void GenerateRequestNumber_IsUniqueEvenWithinTheSameMillisecond()
    {
        // Uniqueness comes from the GUID suffix; this also pins that the GUID survives the [..32]
        // truncation (if the format ever shifted the GUID past char 32, same-timestamp tokens would collide).
        var timestamp = new DateTime(2026, 6, 8, 13, 45, 30, 123, DateTimeKind.Utc);

        var first = SalaryTabulatorCommandSupport.GenerateRequestNumber(timestamp);
        var second = SalaryTabulatorCommandSupport.GenerateRequestNumber(timestamp);

        Assert.NotEqual(first, second);
    }
}
