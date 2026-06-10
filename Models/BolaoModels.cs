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
    public required Team HomeTeam { get; init; }
    public required Team AwayTeam { get; init; }
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
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Login { get; init; }
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

public sealed class SpecialPrediction
{
    public int ParticipantId { get; init; }
    public required string Champion { get; init; }
    public required string RunnerUp { get; init; }
    public required string TopScorer { get; init; }
    public required string GoldenBall { get; init; }
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
    public required IReadOnlyList<Match> UpcomingMatches { get; init; }
    public required IReadOnlyList<Match> CompletedMatches { get; init; }
    public required IReadOnlyList<SpecialPrediction> SpecialPredictions { get; init; }
    public int TotalMatches { get; init; }
    public int CompletedCount { get; init; }
    public int BrazilMatchesCompleted { get; init; }
    public int GoalsScored { get; init; }
}

public sealed class RoundPredictionView
{
    public required PredictionRound Round { get; init; }
    public required IReadOnlyList<MatchPredictionView> Matches { get; init; }
    public bool IsFinalized { get; init; }
    public int DraftCount { get; init; }
    public bool CanSendAudit => IsFinalized;
}

public sealed class MatchPredictionView
{
    public required Match Match { get; init; }
    public Prediction? Prediction { get; init; }
}
