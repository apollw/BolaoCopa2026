namespace BolaoCopa2026.Models;

public sealed class FinalCardPackage
{
    public required Participant Participant { get; init; }
    public required RankingEntry Ranking { get; init; }
    public required string StatusLabel { get; init; }
    public required string BestRoundLabel { get; init; }
    public required string CuriosityLabel { get; init; }
    public int Placement { get; init; }
    public int ParticipantCount { get; init; }
    public int CompletedMatches { get; init; }
    public int TotalMatches { get; init; }
    public bool IsComplete { get; init; }
    public bool IsChampion { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}
