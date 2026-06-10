using BolaoCopa2026.Models;

namespace BolaoCopa2026.Services;

public sealed class BolaoRepository
{
    private readonly ScoringService _scoringService;
    private readonly List<Match> _matches;
    private readonly List<Participant> _participants;
    private readonly List<Prediction> _predictions;
    private readonly List<SpecialPrediction> _specialPredictions;
    private readonly List<ResultAudit> _audits = [];

    public BolaoRepository(ScoringService scoringService)
    {
        _scoringService = scoringService;

        var brazil = new Team("BRA", "Brasil", true);
        var serbia = new Team("SRB", "Servia");
        var morocco = new Team("MAR", "Marrocos");
        var croatia = new Team("CRO", "Croacia");
        var argentina = new Team("ARG", "Argentina");
        var france = new Team("FRA", "Franca");

        _matches =
        [
            new Match { Id = 1, HomeTeam = brazil, AwayTeam = serbia, Phase = CompetitionPhase.GroupStage, Kickoff = new DateTimeOffset(2026, 6, 13, 16, 0, 0, TimeSpan.FromHours(-3)), Result = new MatchResult { HomeGoals = 2, AwayGoals = 0, RegisteredBy = "admin", RegisteredAt = DateTimeOffset.UtcNow.AddDays(-2) } },
            new Match { Id = 2, HomeTeam = morocco, AwayTeam = croatia, Phase = CompetitionPhase.GroupStage, Kickoff = new DateTimeOffset(2026, 6, 14, 13, 0, 0, TimeSpan.FromHours(-3)), Result = new MatchResult { HomeGoals = 1, AwayGoals = 1, RegisteredBy = "admin", RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1) } },
            new Match { Id = 3, HomeTeam = argentina, AwayTeam = france, Phase = CompetitionPhase.RoundOf16, Kickoff = new DateTimeOffset(2026, 7, 4, 18, 0, 0, TimeSpan.FromHours(-3)) },
            new Match { Id = 4, HomeTeam = brazil, AwayTeam = france, Phase = CompetitionPhase.QuarterFinal, Kickoff = new DateTimeOffset(2026, 7, 10, 16, 0, 0, TimeSpan.FromHours(-3)) }
        ];

        _participants =
        [
            new Participant { Id = 1, Name = "Ana", Email = "ana@example.com", Login = "ana" },
            new Participant { Id = 2, Name = "Bruno", Email = "bruno@example.com", Login = "bruno" },
            new Participant { Id = 3, Name = "Carlos", Email = "carlos@example.com", Login = "carlos" }
        ];

        _predictions =
        [
            new Prediction { MatchId = 1, ParticipantId = 1, HomeGoals = 2, AwayGoals = 0, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-4) },
            new Prediction { MatchId = 1, ParticipantId = 2, HomeGoals = 1, AwayGoals = 0, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-4) },
            new Prediction { MatchId = 1, ParticipantId = 3, HomeGoals = 1, AwayGoals = 1, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-4) },
            new Prediction { MatchId = 2, ParticipantId = 1, HomeGoals = 1, AwayGoals = 0, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-3) },
            new Prediction { MatchId = 2, ParticipantId = 2, HomeGoals = 1, AwayGoals = 1, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-3) },
            new Prediction { MatchId = 2, ParticipantId = 3, HomeGoals = 2, AwayGoals = 2, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-3) },
            new Prediction { MatchId = 3, ParticipantId = 1, HomeGoals = 2, AwayGoals = 1, QualifiedTeamCode = "ARG", SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new Prediction { MatchId = 3, ParticipantId = 2, HomeGoals = 1, AwayGoals = 2, QualifiedTeamCode = "FRA", SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1) }
        ];

        _specialPredictions =
        [
            new SpecialPrediction { ParticipantId = 1, Champion = "Brasil", RunnerUp = "Franca", TopScorer = "Vinicius Jr.", GoldenBall = "Bellingham" },
            new SpecialPrediction { ParticipantId = 2, Champion = "Argentina", RunnerUp = "Brasil", TopScorer = "Mbappe", GoldenBall = "Mbappe" },
            new SpecialPrediction { ParticipantId = 3, Champion = "Franca", RunnerUp = "Espanha", TopScorer = "Kane", GoldenBall = "Vinicius Jr." }
        ];
    }

    public IReadOnlyList<Match> Matches => _matches;
    public IReadOnlyList<Participant> Participants => _participants;
    public IReadOnlyList<ResultAudit> Audits => _audits;

    public DashboardStats GetDashboard()
    {
        var completed = _matches.Where(match => match.Result is not null).ToList();
        return new DashboardStats
        {
            Ranking = GetRanking(),
            UpcomingMatches = _matches.Where(match => match.Result is null).OrderBy(match => match.Kickoff).ToList(),
            CompletedMatches = completed.OrderByDescending(match => match.Kickoff).ToList(),
            SpecialPredictions = _specialPredictions,
            TotalMatches = _matches.Count,
            CompletedCount = completed.Count,
            BrazilMatchesCompleted = completed.Count(match => match.IncludesBrazil),
            GoalsScored = completed.Sum(match => match.Result!.HomeGoals + match.Result.AwayGoals)
        };
    }

    public IReadOnlyList<RankingEntry> GetRanking()
    {
        return _participants
            .Select(participant =>
            {
                var scored = _predictions
                    .Where(prediction => prediction.ParticipantId == participant.Id)
                    .Select(prediction => new
                    {
                        Prediction = prediction,
                        Match = _matches.Single(match => match.Id == prediction.MatchId)
                    })
                    .Select(item => _scoringService.Score(item.Match, item.Prediction))
                    .ToList();

                return new RankingEntry
                {
                    Participant = participant,
                    Points = scored.Sum(score => score.Points),
                    ExactScores = scored.Count(score => score.ExactScore),
                    KnockoutQualifiedHits = scored.Count(score => score.QualifiedHit),
                    BrazilHits = scored.Count(score => score.BrazilHit),
                    ResultHits = scored.Count(score => score.ResultHit)
                };
            })
            .OrderByDescending(entry => entry.Points)
            .ThenByDescending(entry => entry.ExactScores)
            .ThenByDescending(entry => entry.KnockoutQualifiedHits)
            .ThenByDescending(entry => entry.BrazilHits)
            .ThenByDescending(entry => entry.ResultHits)
            .ThenBy(entry => entry.Participant.Name)
            .ToList();
    }

    public bool TryRegisterResult(int matchId, int homeGoals, int awayGoals, string? qualifiedTeamCode, string registeredBy, out string message)
    {
        var match = _matches.SingleOrDefault(item => item.Id == matchId);
        if (match is null)
        {
            message = "Partida nao encontrada.";
            return false;
        }

        if (match.Result is not null)
        {
            message = "Resultado ja registrado. O regulamento nao permite editar depois da confirmacao.";
            return false;
        }

        if (match.IsKnockout && string.IsNullOrWhiteSpace(qualifiedTeamCode))
        {
            message = "Informe o classificado em jogos de mata-mata.";
            return false;
        }

        match.Result = new MatchResult
        {
            HomeGoals = homeGoals,
            AwayGoals = awayGoals,
            QualifiedTeamCode = qualifiedTeamCode,
            RegisteredBy = registeredBy,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        _audits.Add(new ResultAudit(matchId, registeredBy, DateTimeOffset.UtcNow, $"{match.HomeTeam.Name} {homeGoals} x {awayGoals} {match.AwayTeam.Name}"));
        message = "Resultado registrado e bloqueado para edicao.";
        return true;
    }
}

public sealed record ResultAudit(int MatchId, string RegisteredBy, DateTimeOffset RegisteredAt, string Summary);
