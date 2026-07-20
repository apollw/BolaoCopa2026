namespace BolaoCopa2026.Models;

public sealed class RoundPredictionView
{
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<MatchPredictionView> Matches { get; init; }
    public DateTimeOffset? DraftLockUtc { get; init; }
    public bool IsLocked { get; init; }
    public bool IsFinalized { get; init; }
    public int DraftCount { get; init; }
    public int MissingCount { get; init; }
    public int StartedMatchCount { get; init; }
    public int StartedWithoutPredictionCount { get; init; }
    public int OpenWithoutPredictionCount { get; init; }
    public string? LockReason { get; init; }
    public bool CanSendAudit => IsFinalized;
}
