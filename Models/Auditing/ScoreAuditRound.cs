namespace BolaoCopa2026.Models;

public sealed class ScoreAuditRound
{
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<ScoreAuditLine> Lines { get; init; }
    public int TotalPoints => Lines.Sum(line => line.TotalPoints);
}
