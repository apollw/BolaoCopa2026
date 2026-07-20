namespace BolaoCopa2026.Models;

public sealed class PublicRoundPredictions
{
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<PublicMatchPredictionLine> Predictions { get; init; }
    public bool IsFinalized { get; init; }
    public DateTimeOffset? FinalizedAt { get; init; }
    public int DefinitiveCount { get; init; }
    public int TotalMatches { get; init; }
}
