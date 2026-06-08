using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.PositionSlots.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// PS-F guardrail (defense-in-depth) for the Position Slots list endpoint; mirrors
/// <see cref="OrgUnitPaginationGuardrailsTests"/>. Every paginated <c>pageSize</c> on a Position Slots
/// controller MUST constrain its bounds at the controller boundary with
/// <c>[Range(1, PositionSlotValidationRules.MaxPageSize)]</c> — the same bounds as the handler
/// FluentValidation (<c>SearchPositionSlotsQueryValidator.InclusiveBetween(1, MaxPageSize)</c>) — instead
/// of relying solely on the handler validator. Structural pattern (namespace + name regex), not a
/// hand-maintained list, so a new family controller cannot silently regress.
/// </summary>
public sealed class PositionSlotPaginationGuardrailsTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex PositionSlotFamilyRegex =
        new(@"^PositionSlot", RegexOptions.Compiled);

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                PositionSlotFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(static method => !method.IsSpecialName)
            .ToArray();

    [Fact]
    public void PositionSlotFamily_ExposesPaginatedActions()
    {
        var pageSizeParams = FamilyActions()
            .SelectMany(static action => action.GetParameters())
            .Count(static parameter => parameter.Name == "pageSize");

        Assert.True(
            pageSizeParams > 0,
            "Expected at least one 'pageSize' parameter across the Position Slots controller family. " +
            "Zero means the namespace/name filter drifted and the guardrail would silently pass.");
    }

    [Fact]
    public void EveryPositionSlotPageSize_DeclaresRangeMatchingTheHandlerValidator()
    {
        var violations = new List<string>();

        foreach (var action in FamilyActions())
        {
            foreach (var parameter in action.GetParameters())
            {
                if (parameter.Name != "pageSize")
                {
                    continue;
                }

                var qualifiedName = $"{action.DeclaringType!.Name}.{action.Name}('{parameter.Name}')";
                var range = parameter.GetCustomAttribute<RangeAttribute>();

                if (range is null)
                {
                    violations.Add($"{qualifiedName}: missing [Range].");
                    continue;
                }

                if (Convert.ToInt32(range.Minimum) != 1)
                {
                    violations.Add($"{qualifiedName}: [Range] Minimum is {range.Minimum}, expected 1.");
                }

                if (Convert.ToInt32(range.Maximum) != PositionSlotValidationRules.MaxPageSize)
                {
                    violations.Add(
                        $"{qualifiedName}: [Range] Maximum is {range.Maximum}, expected " +
                        $"{PositionSlotValidationRules.MaxPageSize} (PositionSlotValidationRules.MaxPageSize).");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Finding PS-F (defense-in-depth): every paginated 'pageSize' on a Position Slots controller " +
            "must declare [Range(1, PositionSlotValidationRules.MaxPageSize)] at the controller boundary, " +
            "not rely solely on the handler FluentValidation rule. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}
