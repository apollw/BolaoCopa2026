namespace BolaoCopa2026.Models;

public sealed class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Login { get; set; }
    public string? PasswordHash { get; set; }
    public string? AvatarKey { get; set; }
    public string? AvatarImagePath { get; set; }
    public bool IsAdmin { get; set; }
}
