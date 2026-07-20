namespace BolaoCopa2026.Models;

public sealed class SpecialPrediction
{
    public int ParticipantId { get; init; }
    public required string Champion { get; set; }
    public required string RunnerUp { get; set; }
    public required string TopScorer { get; set; }
    public required string GoldenBall { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? AuditDownloadedAt { get; set; }
    public string? AuditProofHash { get; set; }
    public bool IsFinal => SubmittedAt is not null;
}
