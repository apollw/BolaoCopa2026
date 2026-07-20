namespace BolaoCopa2026.Models;

public sealed class PublicSpecialPredictionCard
{
    public required string Champion { get; init; }
    public string? ChampionCode { get; init; }
    public required string RunnerUp { get; init; }
    public string? RunnerUpCode { get; init; }
    public required string TopScorer { get; init; }
    public required string GoldenBall { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
}
