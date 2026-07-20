namespace BolaoCopa2026.Models;

public sealed class RoundDraftUpdate
{
    public int MatchId { get; init; }
    public int? HomeGoals { get; init; }
    public int? AwayGoals { get; init; }
    public string? QualifiedTeamCode { get; init; }
}
