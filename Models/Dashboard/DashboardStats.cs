namespace BolaoCopa2026.Models;

public sealed class DashboardStats
{
    public required IReadOnlyList<RankingEntry> Ranking { get; init; }
    public required IReadOnlyList<PredictionRound> Rounds { get; init; }
    public required IReadOnlyList<GroupStanding> GroupStandings { get; init; }
    public required IReadOnlyList<Match> UpcomingMatches { get; init; }
    public required IReadOnlyList<Match> CompletedMatches { get; init; }
    public required IReadOnlyList<DashboardChartSlice> PredictionOutcomeSlices { get; init; }
    public int TotalMatches { get; init; }
    public int CompletedCount { get; init; }
    public int BrazilMatchesCompleted { get; init; }
    public int GoalsScored { get; init; }
    public int FinalizedPredictions { get; init; }
    public int DraftPredictions { get; init; }
    public int ExactScores { get; init; }
    public int ResultHits { get; init; }
    public int KnockoutQualifiedHits { get; init; }
    public int BrazilHits { get; init; }
    public int TotalPoints { get; init; }
    public required IReadOnlyList<DashboardHighlightCard> HighlightCards { get; init; }
    public DashboardChampionHighlight? ChampionHighlight { get; init; }
    public SpecialScoreboard? SpecialScoreboard { get; init; }
}
