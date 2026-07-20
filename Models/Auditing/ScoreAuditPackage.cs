namespace BolaoCopa2026.Models;

public sealed class ScoreAuditPackage
{
    public required Participant Participant { get; init; }
    public required IReadOnlyList<ScoreAuditRound> Rounds { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ProofHash { get; init; }
    public int TotalPoints => Rounds.Sum(round => round.TotalPoints);
}
