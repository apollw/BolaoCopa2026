namespace BolaoCopa2026.Models;

public sealed class SpecialAuditSnapshot
{
    public required Participant Participant { get; init; }
    public required SpecialPrediction Prediction { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ProofHash { get; init; }
}
