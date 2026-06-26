namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Pure weighted scoring for exit-interview submissions (D-07). Only selection fields (with per-option
/// scores) and scale fields contribute; text/date/number/boolean answers do not score. Each scored answer
/// is normalized to 0–100, then the submission index is the weighted average over answered scored fields:
/// <c>Σ(weight × normalizedScore) / Σ(weight)</c>. Higher = more favorable exit experience (RQ-03).
/// </summary>
public static class ExitInterviewScoring
{
    /// <summary>Normalizes a Likert value (1..scaleMax) to 0–100; null when not scorable.</summary>
    public static decimal? NormalizeScale(decimal? value, int? scaleMax)
    {
        if (value is null || scaleMax is null || scaleMax.Value <= 1)
        {
            return null;
        }

        var clamped = Math.Clamp(value.Value, 1m, scaleMax.Value);
        return Math.Round((clamped - 1m) / (scaleMax.Value - 1m) * 100m, 2);
    }

    /// <summary>Average of the selected options' scores (each already 0–100); null when none score.</summary>
    public static decimal? NormalizeOptions(IReadOnlyCollection<decimal> selectedOptionScores) =>
        selectedOptionScores.Count == 0
            ? null
            : Math.Round(selectedOptionScores.Average(), 2);

    /// <summary>
    /// Weighted 0–100 index over the scored answers. Each entry is a field's weight (≥ 0) and its answer's
    /// normalized 0–100 score. Returns null when no scored answer has positive weight.
    /// </summary>
    public static decimal? ComputeIndex(IReadOnlyCollection<(decimal Weight, decimal NormalizedScore)> scoredAnswers)
    {
        var totalWeight = scoredAnswers.Sum(answer => answer.Weight);
        if (totalWeight <= 0m)
        {
            return null;
        }

        var weighted = scoredAnswers.Sum(answer => answer.Weight * answer.NormalizedScore);
        return Math.Round(weighted / totalWeight, 2);
    }
}
