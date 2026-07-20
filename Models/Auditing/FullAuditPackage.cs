namespace BolaoCopa2026.Models;

public sealed class FullAuditPackage
{
    public required Participant Participant { get; init; }
    public required IReadOnlyList<AuditSnapshot> RoundSnapshots { get; init; }
    public SpecialAuditSnapshot? SpecialSnapshot { get; init; }
}
