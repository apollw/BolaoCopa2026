namespace BolaoCopa2026.Models;

public sealed class AuditPredictionLine
{
    public required int OfficialNumber { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public int? HomeGoals { get; init; }
    public int? AwayGoals { get; init; }
    public string? QualifiedTeam { get; init; }
    public DateTimeOffset? SavedAt { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public bool HasPrediction => HomeGoals is not null && AwayGoals is not null;
}
