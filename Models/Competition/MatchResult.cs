namespace BolaoCopa2026.Models;

public sealed class MatchResult
{
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }
    public string? QualifiedTeamCode { get; init; }
    public required string RegisteredBy { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }
}
