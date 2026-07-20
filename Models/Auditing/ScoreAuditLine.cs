namespace BolaoCopa2026.Models;

public sealed class ScoreAuditLine
{
    public required int OfficialNumber { get; init; }
    public CompetitionPhase Phase { get; init; }
    public required string MatchLabel { get; init; }
    public required string Status { get; init; }
    public int? PredictionHomeGoals { get; init; }
    public int? PredictionAwayGoals { get; init; }
    public string? PredictionQualifiedTeam { get; init; }
    public int? ResultHomeGoals { get; init; }
    public int? ResultAwayGoals { get; init; }
    public string? ResultQualifiedTeam { get; init; }
    public int ExactScorePoints { get; init; }
    public int ResultPoints { get; init; }
    public int QualifiedPoints { get; init; }
    public int BrazilMultiplier { get; init; } = 1;
    public int TotalPoints { get; init; }
}
