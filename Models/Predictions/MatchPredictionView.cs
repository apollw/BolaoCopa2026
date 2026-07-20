namespace BolaoCopa2026.Models;

public sealed class MatchPredictionView
{
    public required Match Match { get; init; }
    public Prediction? Prediction { get; init; }
    public bool HasStarted { get; init; }
    public bool CanEdit { get; init; }
    public required string DisplayLabel { get; init; }
}
