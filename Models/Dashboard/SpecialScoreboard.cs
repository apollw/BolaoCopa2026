namespace BolaoCopa2026.Models;

public sealed class SpecialScoreboard
{
    public required string ChampionName { get; init; }
    public required string RunnerUpName { get; init; }
    public required string TopScorerName { get; init; }
    public required IReadOnlyList<SpecialScoreboardEntry> Entries { get; init; }
}
