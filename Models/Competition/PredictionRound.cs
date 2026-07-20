namespace BolaoCopa2026.Models;

public sealed record PredictionRound(int Id, string Name, CompetitionPhase Phase, DateOnly StartsOn, DateOnly EndsOn);
