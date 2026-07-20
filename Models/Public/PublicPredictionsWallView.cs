namespace BolaoCopa2026.Models;

public sealed class PublicPredictionsWallView
{
    public required IReadOnlyList<PublicParticipantSummary> Participants { get; init; }
}
