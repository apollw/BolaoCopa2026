namespace BolaoCopa2026.Models;

public sealed class PublicMatchPredictionLine
{
    public required int OfficialNumber { get; init; }
    public required string HomeTeamCode { get; init; }
    public required string HomeTeamName { get; init; }
    public required string AwayTeamCode { get; init; }
    public required string AwayTeamName { get; init; }
    public required DateTimeOffset Kickoff { get; init; }
    public int? HomeGoals { get; init; }
    public int? AwayGoals { get; init; }
    public string? QualifiedTeamCode { get; init; }
    public string? QualifiedTeamName { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public PublicPredictionOutcome Outcome { get; init; }
    public string OutcomeCssClass => Outcome.ToString().ToLowerInvariant();
    public string OutcomeLabel => Outcome switch
    {
        PublicPredictionOutcome.NoPrediction => "Sem palpite",
        PublicPredictionOutcome.ExactScore => "Exato",
        PublicPredictionOutcome.ResultHit => "Resultado",
        PublicPredictionOutcome.Miss => "Erro",
        _ => "Aguardando resultado"
    };
    public bool HasDefinitivePrediction => SubmittedAt is not null;
}
