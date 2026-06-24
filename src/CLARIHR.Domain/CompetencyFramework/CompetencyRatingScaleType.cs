namespace CLARIHR.Domain.CompetencyFramework;

/// <summary>
/// Shape of a company's competency rating scale (decision D-04). A <see cref="Numeric"/> scale is a
/// continuous range (e.g. 0–100, 1–5, 0–10); a <see cref="Discrete"/> scale is an ordered set of labelled
/// levels each carrying an ordinal <c>Value</c> (e.g. A=5 … F=0). Both expose a comparable numeric value so
/// the gap (expected − achieved) can always be computed.
/// </summary>
public enum CompetencyRatingScaleType
{
    Numeric = 1,
    Discrete = 2
}
