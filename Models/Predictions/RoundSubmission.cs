namespace BolaoCopa2026.Models;

public sealed class RoundSubmission
{
    public int Id { get; init; }
    public int ParticipantId { get; init; }
    public int RoundId { get; init; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? AuditDownloadedAt { get; set; }
    public string? AuditProofHash { get; set; }
}
