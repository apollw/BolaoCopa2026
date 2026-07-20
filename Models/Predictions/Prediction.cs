namespace BolaoCopa2026.Models;

public sealed class Prediction
{
    public int MatchId { get; init; }
    public int ParticipantId { get; init; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public string? QualifiedTeamCode { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public bool IsFinal => SubmittedAt is not null;
}
