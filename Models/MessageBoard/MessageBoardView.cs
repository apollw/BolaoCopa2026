namespace BolaoCopa2026.Models;

public sealed class MessageBoardView
{
    public required IReadOnlyList<MessageBoardEntry> Messages { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalMessages { get; init; }
    public required int TotalPages { get; init; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
