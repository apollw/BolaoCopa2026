namespace BolaoCopa2026.Models;

public sealed class PublicParticipantPredictions
{
    public required PublicParticipantDto Participant { get; init; }
    public required IReadOnlyList<PublicRoundPredictions> Rounds { get; init; }
    public PublicSpecialPredictionCard? SpecialPrediction { get; init; }
    public int DefinitivePredictionCount { get; init; }
    public int FinalizedRoundCount { get; init; }
}
