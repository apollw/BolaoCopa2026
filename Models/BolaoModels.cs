namespace BolaoCopa2026.Models;

public enum CompetitionPhase
{
    GroupStage,
    RoundOf32,
    RoundOf16,
    QuarterFinal,
    SemiFinal,
    ThirdPlace,
    Final
}

public enum MatchOutcome
{
    HomeWin,
    Draw,
    AwayWin
}

public sealed record Team(string Code, string Name, bool IsBrazil = false);

public sealed record PredictionRound(int Id, string Name, CompetitionPhase Phase, DateOnly StartsOn, DateOnly EndsOn);

public sealed class Match
{
    public int Id { get; init; }
    public int OfficialNumber { get; init; }
    public int RoundId { get; init; }
    public required Team HomeTeam { get; set; }
    public required Team AwayTeam { get; set; }
    public CompetitionPhase Phase { get; init; }
    public DateTimeOffset Kickoff { get; init; }
    public string? GroupName { get; init; }
    public string? Venue { get; init; }
    public MatchResult? Result { get; set; }
    public bool IsKnockout => Phase != CompetitionPhase.GroupStage;
    public bool IncludesBrazil => HomeTeam.IsBrazil || AwayTeam.IsBrazil;
}

public sealed class MatchResult
{
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }
    public string? QualifiedTeamCode { get; init; }
    public required string RegisteredBy { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }
}

public sealed class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Login { get; set; }
    public string? PasswordHash { get; set; }
    public string? AvatarKey { get; set; }
    public bool IsAdmin { get; set; }
}

public sealed class Prediction
{
    public int MatchId { get; init; }
    public int ParticipantId { get; init; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public string? QualifiedTeamCode { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public bool IsFinal => SubmittedAt is not null;
}

public sealed class RoundSubmission
{
    public int Id { get; init; }
    public int ParticipantId { get; init; }
    public int RoundId { get; init; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? AuditDownloadedAt { get; set; }
    public string? AuditProofHash { get; set; }
}

public sealed class SpecialPrediction
{
    public int ParticipantId { get; init; }
    public required string Champion { get; set; }
    public required string RunnerUp { get; set; }
    public required string TopScorer { get; set; }
    public required string GoldenBall { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? AuditDownloadedAt { get; set; }
    public string? AuditProofHash { get; set; }
    public bool IsFinal => SubmittedAt is not null;
}

public sealed class SpecialPredictionView
{
    public SpecialPrediction? Prediction { get; init; }
    public bool IsLocked { get; init; }
    public string? LockReason { get; init; }
    public bool CanDownloadAudit => Prediction?.IsFinal == true;
}

public sealed class RankingEntry
{
    public required Participant Participant { get; init; }
    public int Points { get; init; }
    public int ExactScores { get; init; }
    public int KnockoutQualifiedHits { get; init; }
    public int BrazilHits { get; init; }
    public int ResultHits { get; init; }
}

public sealed class DashboardStats
{
    public required IReadOnlyList<RankingEntry> Ranking { get; init; }
    public required IReadOnlyList<PredictionRound> Rounds { get; init; }
    public required IReadOnlyList<GroupStanding> GroupStandings { get; init; }
    public required IReadOnlyList<Match> UpcomingMatches { get; init; }
    public required IReadOnlyList<Match> CompletedMatches { get; init; }
    public required IReadOnlyList<DashboardChartSlice> PredictionOutcomeSlices { get; init; }
    public int TotalMatches { get; init; }
    public int CompletedCount { get; init; }
    public int BrazilMatchesCompleted { get; init; }
    public int GoalsScored { get; init; }
    public int FinalizedPredictions { get; init; }
    public int DraftPredictions { get; init; }
    public int ExactScores { get; init; }
    public int ResultHits { get; init; }
    public int KnockoutQualifiedHits { get; init; }
    public int BrazilHits { get; init; }
    public int TotalPoints { get; init; }
}

public sealed record DashboardChartSlice(string Label, int Value, string Color);

public sealed class PublicPredictionsWallView
{
    public required IReadOnlyList<PublicParticipantSummary> Participants { get; init; }
}

public sealed class PublicParticipantSummary
{
    public required PublicParticipantDto Participant { get; init; }
    public int DefinitivePredictionCount { get; init; }
    public int FinalizedRoundCount { get; init; }
    public bool HasSpecialPrediction { get; init; }
}

public sealed class PublicParticipantPredictions
{
    public required PublicParticipantDto Participant { get; init; }
    public required IReadOnlyList<PublicRoundPredictions> Rounds { get; init; }
    public PublicSpecialPredictionCard? SpecialPrediction { get; init; }
    public int DefinitivePredictionCount { get; init; }
    public int FinalizedRoundCount { get; init; }
}

public sealed class PublicRoundPredictions
{
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<PublicMatchPredictionLine> Predictions { get; init; }
    public bool IsFinalized { get; init; }
    public DateTimeOffset? FinalizedAt { get; init; }
    public int DefinitiveCount { get; init; }
    public int TotalMatches { get; init; }
}

public enum PublicPredictionOutcome
{
    NoPrediction = 0,
    PendingOfficialResult = 1,
    ExactScore = 2,
    ResultHit = 3,
    Miss = 4
}

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

public sealed class RoundPredictionView
{
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<MatchPredictionView> Matches { get; init; }
    public bool IsLocked { get; init; }
    public bool IsFinalized { get; init; }
    public int DraftCount { get; init; }
    public int MissingCount { get; init; }
    public int StartedMatchCount { get; init; }
    public int StartedWithoutPredictionCount { get; init; }
    public int OpenWithoutPredictionCount { get; init; }
    public string? LockReason { get; init; }
    public bool CanSendAudit => IsFinalized && !IsLocked;
}

public sealed class MatchPredictionView
{
    public required Match Match { get; init; }
    public Prediction? Prediction { get; init; }
    public bool HasStarted { get; init; }
    public bool CanEdit { get; init; }
}

public sealed class RoundDraftUpdate
{
    public int MatchId { get; init; }
    public int? HomeGoals { get; init; }
    public int? AwayGoals { get; init; }
    public string? QualifiedTeamCode { get; init; }
}

public sealed class ResultAudit
{
    public int Id { get; init; }
    public int MatchId { get; init; }
    public required string RegisteredBy { get; init; }
    public DateTimeOffset RegisteredAt { get; init; }
    public required string Summary { get; init; }
}

public sealed class GroupStanding
{
    public required string GroupName { get; init; }
    public required IReadOnlyList<GroupStandingEntry> Entries { get; init; }
}

public sealed class GroupStandingEntry
{
    public required Team Team { get; init; }
    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points { get; init; }
}

public sealed class AuditSnapshot
{
    public required Participant Participant { get; init; }
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<AuditPredictionLine> Predictions { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ProofHash { get; init; }
}

public sealed class AuditPredictionLine
{
    public required int OfficialNumber { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public int? HomeGoals { get; init; }
    public int? AwayGoals { get; init; }
    public string? QualifiedTeam { get; init; }
    public DateTimeOffset? SavedAt { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public bool HasPrediction => HomeGoals is not null && AwayGoals is not null;
}

public sealed class SpecialAuditSnapshot
{
    public required Participant Participant { get; init; }
    public required SpecialPrediction Prediction { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ProofHash { get; init; }
}

public sealed class PublicParticipantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
