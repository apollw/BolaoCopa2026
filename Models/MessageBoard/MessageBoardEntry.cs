namespace BolaoCopa2026.Models;

public sealed class MessageBoardEntry
{
    public required int Id { get; init; }
    public required MessageBoardAuthor Author { get; init; }
    public required string Body { get; init; }
    public string? MoodKey { get; init; }
    public string? MoodLabel { get; init; }
    public string? MoodEmoji { get; init; }
    public bool CanDelete { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
