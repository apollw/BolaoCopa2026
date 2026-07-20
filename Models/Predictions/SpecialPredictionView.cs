namespace BolaoCopa2026.Models;

public sealed class SpecialPredictionView
{
    public SpecialPrediction? Prediction { get; init; }
    public bool IsLocked { get; init; }
    public string? LockReason { get; init; }
    public bool CanDownloadAudit => Prediction?.IsFinal == true;
}
