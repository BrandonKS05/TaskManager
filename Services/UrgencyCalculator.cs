namespace TaskManager.Services;

/// <summary>
/// urgency_score = (importance_weight × importance) + (complexity_weight × complexity) + (deadline_factor × days_remaining)
/// Maps the raw score to a 1–5 star rating using the min/max possible raw scores for inputs in range.
/// </summary>
public static class UrgencyCalculator
{
    public const double ImportanceWeight = 1.0;
    public const double ComplexityWeight = 1.0;
    /// <summary>Negative: more days until due date reduces raw score (less urgent).</summary>
    public const double DeadlineFactor = -0.22;

    public static int ComputeStars(int importance, int complexity, int daysRemaining)
    {
        importance = Math.Clamp(importance, 1, 5);
        complexity = Math.Clamp(complexity, 1, 5);
        daysRemaining = Math.Clamp(daysRemaining, 0, 365);

        var raw = ImportanceWeight * importance
                  + ComplexityWeight * complexity
                  + DeadlineFactor * daysRemaining;

        // If we scale against 365 days, any task due "today" ends up near the top of the range
        // and Math.Round collapses to 5 stars. Instead, scale the star mapping against a
        // realistic urgency horizon (e.g. next ~30 days).
        const int daysRemainingForStarScalingMax = 30;

        var rawMin = ImportanceWeight * 1
                    + ComplexityWeight * 1
                    + DeadlineFactor * daysRemainingForStarScalingMax;
        var rawMax = ImportanceWeight * 5
                    + ComplexityWeight * 5
                    + DeadlineFactor * 0;

        if (rawMax <= rawMin)
            return 3;

        var t = (raw - rawMin) / (rawMax - rawMin);
        t = Math.Clamp(t, 0, 1);
        var stars = (int)Math.Round(1 + t * 4);
        return Math.Clamp(stars, 1, 5);
    }

    /// <summary>Parse yyyy-MM-dd; returns neutral default days if missing or invalid.</summary>
    public static int DaysRemainingFromDueDate(string? dueDateYyyyMmDd, DateTime utcNow)
    {
        const int defaultDays = 14;
        if (string.IsNullOrWhiteSpace(dueDateYyyyMmDd))
            return defaultDays;

        if (!DateOnly.TryParse(dueDateYyyyMmDd.Trim(), out var due))
            return defaultDays;

        var today = DateOnly.FromDateTime(utcNow.ToLocalTime());
        return Math.Max(0, due.DayNumber - today.DayNumber);
    }
}
