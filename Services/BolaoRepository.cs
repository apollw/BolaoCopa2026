using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa2026.Services;

public sealed class BolaoRepository
{
    private const int MockParticipantId = 1;

    private readonly BolaoDbContext _db;
    private readonly ScoringService _scoringService;

    public BolaoRepository(BolaoDbContext db, ScoringService scoringService)
    {
        _db = db;
        _scoringService = scoringService;
    }

    public int CurrentParticipantId => MockParticipantId;
    public IReadOnlyList<PredictionRound> Rounds => _db.Rounds.OrderBy(round => round.Id).ToList();
    public IReadOnlyList<Match> Matches => _db.Matches.OrderBy(match => match.Kickoff).ThenBy(match => match.OfficialNumber).ToList();
    public IReadOnlyList<Participant> Participants => _db.Participants.OrderBy(participant => participant.Name).ToList();
    public IReadOnlyList<ResultAudit> Audits => _db.ResultAudits.OrderByDescending(audit => audit.RegisteredAt).ToList();

    public DashboardStats GetDashboard()
    {
        var matches = _db.Matches.OrderBy(match => match.Kickoff).ThenBy(match => match.OfficialNumber).ToList();
        var completed = matches.Where(match => match.Result is not null).ToList();

        return new DashboardStats
        {
            Ranking = GetRanking(),
            Rounds = Rounds,
            UpcomingMatches = matches.Where(match => match.Result is null).Take(8).ToList(),
            CompletedMatches = completed.OrderByDescending(match => match.Kickoff).ToList(),
            SpecialPredictions = _db.SpecialPredictions.OrderBy(prediction => prediction.ParticipantId).ToList(),
            TotalMatches = matches.Count,
            CompletedCount = completed.Count,
            BrazilMatchesCompleted = completed.Count(match => match.IncludesBrazil),
            GoalsScored = completed.Sum(match => match.Result!.HomeGoals + match.Result.AwayGoals)
        };
    }

    public RoundPredictionView GetRoundPrediction(int participantId, int? roundId)
    {
        var rounds = Rounds;
        var selectedRound = rounds.SingleOrDefault(round => round.Id == roundId) ?? rounds.First();
        var roundMatches = _db.Matches
            .Where(match => match.RoundId == selectedRound.Id)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();

        var participantPredictions = _db.Predictions
            .Where(prediction => prediction.ParticipantId == participantId)
            .ToDictionary(prediction => prediction.MatchId);

        var matches = roundMatches
            .Select(match => new MatchPredictionView
            {
                Match = match,
                Prediction = participantPredictions.GetValueOrDefault(match.Id)
            })
            .ToList();

        return new RoundPredictionView
        {
            Round = selectedRound,
            Matches = matches,
            IsFinalized = matches.Count > 0 && matches.All(item => item.Prediction?.IsFinal == true),
            DraftCount = matches.Count(item => item.Prediction is { IsFinal: false })
        };
    }

    public bool SaveDraftPrediction(int participantId, int matchId, int homeGoals, int awayGoals, string? qualifiedTeamCode, out string message)
    {
        var match = _db.Matches.SingleOrDefault(item => item.Id == matchId);
        if (match is null)
        {
            message = "Partida nao encontrada.";
            return false;
        }

        if (DateTimeOffset.UtcNow >= match.Kickoff.ToUniversalTime())
        {
            message = "O horario oficial da partida ja iniciou. O palpite nao pode mais ser alterado.";
            return false;
        }

        var existing = _db.Predictions.SingleOrDefault(item => item.ParticipantId == participantId && item.MatchId == matchId);
        if (existing?.IsFinal == true)
        {
            message = "Esta rodada ja foi finalizada. O palpite esta bloqueado.";
            return false;
        }

        if (existing is null)
        {
            _db.Predictions.Add(new Prediction
            {
                ParticipantId = participantId,
                MatchId = matchId,
                HomeGoals = homeGoals,
                AwayGoals = awayGoals,
                QualifiedTeamCode = qualifiedTeamCode,
                SavedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.HomeGoals = homeGoals;
            existing.AwayGoals = awayGoals;
            existing.QualifiedTeamCode = qualifiedTeamCode;
            existing.SavedAt = DateTimeOffset.UtcNow;
        }

        _db.SaveChanges();
        message = "Rascunho salvo no banco e associado ao perfil do participante mockado.";
        return true;
    }

    public bool FinalizeRound(int participantId, int roundId, out string message)
    {
        var roundMatchIds = _db.Matches
            .Where(match => match.RoundId == roundId)
            .Select(match => match.Id)
            .ToList();

        var predictions = _db.Predictions
            .Where(prediction => prediction.ParticipantId == participantId && roundMatchIds.Contains(prediction.MatchId))
            .ToList();

        if (predictions.Count != roundMatchIds.Count)
        {
            message = "Preencha todos os jogos da rodada antes de finalizar.";
            return false;
        }

        foreach (var prediction in predictions.Where(prediction => !prediction.IsFinal))
        {
            prediction.SubmittedAt = DateTimeOffset.UtcNow;
        }

        _db.SaveChanges();
        message = "Rodada finalizada. Agora a auditoria desta rodada pode ser enviada por email.";
        return true;
    }

    public bool CanSendPredictionAudit(int participantId, int roundId)
    {
        return GetRoundPrediction(participantId, roundId).CanSendAudit;
    }

    public IReadOnlyList<RankingEntry> GetRanking()
    {
        var participants = _db.Participants.OrderBy(participant => participant.Name).ToList();
        var matches = _db.Matches.ToDictionary(match => match.Id);
        var predictions = _db.Predictions.Where(prediction => prediction.SubmittedAt != null).ToList();

        return participants
            .Select(participant =>
            {
                var scored = predictions
                    .Where(prediction => prediction.ParticipantId == participant.Id)
                    .Select(prediction => _scoringService.Score(matches[prediction.MatchId], prediction))
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
        var match = _db.Matches.SingleOrDefault(item => item.Id == matchId);
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

        _db.ResultAudits.Add(new ResultAudit
        {
            MatchId = matchId,
            RegisteredBy = registeredBy,
            RegisteredAt = DateTimeOffset.UtcNow,
            Summary = $"{match.HomeTeam.Name} {homeGoals} x {awayGoals} {match.AwayTeam.Name}"
        });

        _db.SaveChanges();
        message = "Resultado registrado e bloqueado para edicao.";
        return true;
    }
}
