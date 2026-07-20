namespace BolaoCopa2026.Models;

public sealed class GroupStanding
{
    public required string GroupName { get; init; }
    public required IReadOnlyList<GroupStandingEntry> Entries { get; init; }
}
