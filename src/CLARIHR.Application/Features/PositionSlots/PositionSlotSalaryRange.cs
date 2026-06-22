namespace CLARIHR.Application.Features.PositionSlots;

/// <summary>
/// Salary band that governs a position slot, resolved from its job profile's active salary tabulator
/// line (PositionSlot → JobProfileCompensation → SalaryTabulatorLine). Used to validate (block) an
/// employee's negotiated base salary against the plaza's range (R-3). Null bounds mean "no band".
/// </summary>
public sealed record PositionSlotSalaryRange(decimal? MinAmount, decimal? MaxAmount, string? CurrencyCode);
