namespace BolaoCopa2026.Models;

public sealed class AuditSnapshot
{
    public required Participant Participant { get; init; }
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<AuditPredictionLine> Predictions { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ProofHash { get; init; }
}
