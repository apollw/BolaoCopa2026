namespace BolaoCopa2026.Models;

public sealed class SpecialScoreboardEntry
{
    public required Participant Participant { get; init; }
    public int TotalPoints { get; init; }
    public int ChampionPoints { get; init; }
    public int RunnerUpPoints { get; init; }
    public int TopScorerPoints { get; init; }
}
