namespace BolaoCopa2026.Models;

public sealed class ResultAudit
{
    public int Id { get; init; }
    public int MatchId { get; init; }
    public required string RegisteredBy { get; init; }
    public DateTimeOffset RegisteredAt { get; init; }
    public required string Summary { get; init; }
}
