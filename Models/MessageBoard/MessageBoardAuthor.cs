namespace BolaoCopa2026.Models;

public sealed class MessageBoardAuthor
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? AvatarKey { get; init; }
    public string? AvatarImagePath { get; init; }
}
