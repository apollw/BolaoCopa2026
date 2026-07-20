namespace BolaoCopa2026.Models;

public sealed class CopaMessage
{
    public int Id { get; init; }
    public int ParticipantId { get; init; }
    public required string Body { get; set; }
    public string? MoodKey { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}
