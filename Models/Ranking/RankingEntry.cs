namespace BolaoCopa2026.Models;

public sealed class RankingEntry
{
    public required Participant Participant { get; init; }
    public int Points { get; init; }
    public int MatchPoints { get; init; }
    public int SpecialPoints { get; init; }
    public int SpecialChampionPoints { get; init; }
    public int SpecialRunnerUpPoints { get; init; }
    public int SpecialTopScorerPoints { get; init; }
    public bool HasFinalizedSpecialPrediction { get; init; }
    public int ExactScores { get; init; }
    public int KnockoutQualifiedHits { get; init; }
    public int BrazilHits { get; init; }
    public int ResultHits { get; init; }
    public bool HasSpecialScore => SpecialPoints > 0;
}
