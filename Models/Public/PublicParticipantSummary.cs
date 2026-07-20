namespace BolaoCopa2026.Models;

public sealed class PublicParticipantSummary
{
    public required PublicParticipantDto Participant { get; init; }
    public int DefinitivePredictionCount { get; init; }
    public int FinalizedRoundCount { get; init; }
    public bool HasSpecialPrediction { get; init; }
}
