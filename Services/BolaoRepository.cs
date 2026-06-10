using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BolaoCopa2026.Services;

public sealed class BolaoRepository
{
    private readonly BolaoDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ScoringService _scoringService;

    public BolaoRepository(BolaoDbContext db, IHttpContextAccessor httpContextAccessor, ScoringService scoringService)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _scoringService = scoringService;
    }

    public int CurrentParticipantId
    {
        get
        {
            var id = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(id, out var participantId))
            {
                return participantId;
            }

            throw new InvalidOperationException("Usuario autenticado sem identificador de participante.");
        }
    }

    public Participant? CurrentParticipant => _db.Participants.SingleOrDefault(participant => participant.Id == CurrentParticipantId);
    public IReadOnlyList<PredictionRound> Rounds => _db.Rounds.OrderBy(round => round.Id).ToList();
    public IReadOnlyList<Match> Matches => _db.Matches
        .ToList()
        .OrderBy(match => match.Kickoff)
        .ThenBy(match => match.OfficialNumber)
        .ToList();
    public IReadOnlyList<Participant> Participants => _db.Participants.OrderBy(participant => participant.Name).ToList();
    public IReadOnlyList<ResultAudit> Audits => _db.ResultAudits
        .ToList()
        .OrderByDescending(audit => audit.RegisteredAt)
        .ToList();

    public DashboardStats GetDashboard()
    {
        var matches = _db.Matches
            .ToList()
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();
        var completed = matches.Where(match => match.Result is not null).ToList();

        return new DashboardStats
        {
            Ranking = GetRanking(),
            Rounds = Rounds,
            GroupStandings = GetGroupStandings(),
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
        var isLocked = !IsRoundAvailable(participantId, selectedRound.Id, out var lockReason);
        var roundMatches = _db.Matches
            .Where(match => match.RoundId == selectedRound.Id)
            .ToList()
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
            IsLocked = isLocked,
            LockReason = lockReason,
            IsFinalized = IsRoundFinalized(participantId, selectedRound.Id),
            DraftCount = matches.Count(item => item.Prediction is { IsFinal: false })
        };
    }

    public SpecialPredictionView GetSpecialPrediction(int participantId)
    {
        var prediction = _db.SpecialPredictions.SingleOrDefault(item => item.ParticipantId == participantId);
        var isLocked = IsSpecialPredictionLocked(prediction, out var lockReason);

        return new SpecialPredictionView
        {
            Prediction = prediction,
            IsLocked = isLocked,
            LockReason = lockReason
        };
    }

    public bool SaveSpecialPredictionDraft(
        int participantId,
        string champion,
        string runnerUp,
        string topScorer,
        string goldenBall,
        out string message)
    {
        var existing = _db.SpecialPredictions.SingleOrDefault(item => item.ParticipantId == participantId);
        if (IsSpecialPredictionLocked(existing, out var lockReason))
        {
            message = lockReason ?? "Palpites especiais bloqueados.";
            return false;
        }

        champion = champion.Trim();
        runnerUp = runnerUp.Trim();
        topScorer = topScorer.Trim();
        goldenBall = goldenBall.Trim();

        if (HasBlankSpecialPrediction(champion, runnerUp, topScorer, goldenBall))
        {
            message = "Preencha todos os palpites especiais antes de salvar.";
            return false;
        }

        if (existing is null)
        {
            _db.SpecialPredictions.Add(new SpecialPrediction
            {
                ParticipantId = participantId,
                Champion = champion,
                RunnerUp = runnerUp,
                TopScorer = topScorer,
                GoldenBall = goldenBall,
                SavedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Champion = champion;
            existing.RunnerUp = runnerUp;
            existing.TopScorer = topScorer;
            existing.GoldenBall = goldenBall;
            existing.SavedAt = DateTimeOffset.UtcNow;
        }

        _db.SaveChanges();
        message = "Palpites especiais salvos como rascunho.";
        return true;
    }

    public bool FinalizeSpecialPrediction(int participantId, out string message)
    {
        var prediction = _db.SpecialPredictions.SingleOrDefault(item => item.ParticipantId == participantId);
        if (prediction is null)
        {
            message = "Salve seus palpites especiais antes de finalizar.";
            return false;
        }

        if (IsSpecialPredictionLocked(prediction, out var lockReason))
        {
            message = lockReason ?? "Palpites especiais bloqueados.";
            return false;
        }

        if (HasBlankSpecialPrediction(prediction.Champion, prediction.RunnerUp, prediction.TopScorer, prediction.GoldenBall))
        {
            message = "Preencha todos os palpites especiais antes de finalizar.";
            return false;
        }

        prediction.SubmittedAt = DateTimeOffset.UtcNow;
        _db.SaveChanges();
        message = "Palpites especiais finalizados. Agora o comprovante de auditoria pode ser baixado.";
        return true;
    }

    public bool SaveDraftPrediction(int participantId, int matchId, int homeGoals, int awayGoals, string? qualifiedTeamCode, out string message)
    {
        var match = _db.Matches.SingleOrDefault(item => item.Id == matchId);
        if (match is null)
        {
            message = "Partida nao encontrada.";
            return false;
        }

        if (!IsRoundAvailable(participantId, match.RoundId, out var lockReason))
        {
            message = lockReason ?? "Esta rodada ainda esta bloqueada.";
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
        message = "Rascunho salvo no banco e associado ao seu perfil.";
        return true;
    }

    public bool FinalizeRound(int participantId, int roundId, out string message)
    {
        if (!IsRoundAvailable(participantId, roundId, out var lockReason))
        {
            message = lockReason ?? "Esta rodada ainda esta bloqueada.";
            return false;
        }

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

        var submission = _db.RoundSubmissions.SingleOrDefault(item => item.ParticipantId == participantId && item.RoundId == roundId);
        if (submission is null)
        {
            _db.RoundSubmissions.Add(new RoundSubmission
            {
                ParticipantId = participantId,
                RoundId = roundId,
                SubmittedAt = DateTimeOffset.UtcNow
            });
        }

        _db.SaveChanges();
        message = "Rodada finalizada. Agora o comprovante de auditoria pode ser baixado.";
        return true;
    }

    public bool CanSendPredictionAudit(int participantId, int roundId)
    {
        return GetRoundPrediction(participantId, roundId).CanSendAudit;
    }

    public void MarkSpecialAuditDownloaded(int participantId, DateTimeOffset generatedAt, string proofHash)
    {
        var prediction = _db.SpecialPredictions.SingleOrDefault(item => item.ParticipantId == participantId);
        if (prediction is null)
        {
            return;
        }

        prediction.AuditDownloadedAt = generatedAt;
        prediction.AuditProofHash = proofHash;
        _db.SaveChanges();
    }

    public bool IsRoundAvailable(int participantId, int roundId, out string? reason)
    {
        reason = null;
        if (roundId <= 1)
        {
            return true;
        }

        if (roundId is 2 or 3)
        {
            var previousRoundId = roundId - 1;
            if (IsRoundFinalized(participantId, previousRoundId))
            {
                return true;
            }

            reason = $"Finalize a rodada {previousRoundId} antes de preencher esta rodada.";
            return false;
        }

        if (roundId == 4)
        {
            if (AreAllGroupStageResultsRegistered())
            {
                return true;
            }

            reason = "O mata-mata sera liberado quando o admin registrar todos os resultados reais da fase de grupos.";
            return false;
        }

        var previousKnockoutRoundId = roundId - 1;
        var previousMatches = _db.Matches.Where(match => match.RoundId == previousKnockoutRoundId).ToList();
        if (previousMatches.Count > 0 && previousMatches.All(match => match.Result is not null))
        {
            return true;
        }

        reason = "Esta fase sera liberada apos o fechamento da fase anterior.";
        return false;
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

    public bool IsRoundFinalized(int participantId, int roundId)
    {
        return _db.RoundSubmissions.Any(item => item.ParticipantId == participantId && item.RoundId == roundId);
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
        TryApplyKnockoutPairings();
        message = "Resultado registrado e bloqueado para edicao.";
        return true;
    }

    public IReadOnlyList<GroupStanding> GetGroupStandings()
    {
        return _db.Matches
            .ToList()
            .Where(match => match.Phase == CompetitionPhase.GroupStage && !string.IsNullOrWhiteSpace(match.GroupName))
            .GroupBy(match => match.GroupName!)
            .OrderBy(group => group.Key)
            .Select(group => new GroupStanding
            {
                GroupName = group.Key,
                Entries = BuildGroupEntries(group.ToList())
            })
            .ToList();
    }

    private static IReadOnlyList<GroupStandingEntry> BuildGroupEntries(IReadOnlyList<Match> matches)
    {
        var entries = new Dictionary<string, MutableGroupStanding>();

        foreach (var match in matches)
        {
            EnsureTeam(entries, match.HomeTeam);
            EnsureTeam(entries, match.AwayTeam);

            if (match.Result is null)
            {
                continue;
            }

            ApplyResult(entries[match.HomeTeam.Code], match.Result.HomeGoals, match.Result.AwayGoals);
            ApplyResult(entries[match.AwayTeam.Code], match.Result.AwayGoals, match.Result.HomeGoals);
        }

        return entries.Values
            .Select(entry => entry.ToEntry())
            .OrderByDescending(entry => entry.Points)
            .ThenByDescending(entry => entry.GoalDifference)
            .ThenByDescending(entry => entry.GoalsFor)
            .ThenBy(entry => entry.Team.Name)
            .ToList();
    }

    private bool IsSpecialPredictionLocked(SpecialPrediction? prediction, out string? reason)
    {
        reason = null;
        if (prediction?.IsFinal == true)
        {
            reason = "Palpites especiais ja finalizados. O regulamento nao permite editar depois da confirmacao.";
            return true;
        }

        var openingKickoff = _db.Matches
            .ToList()
            .OrderBy(match => match.Kickoff)
            .Select(match => match.Kickoff.ToUniversalTime())
            .FirstOrDefault();

        if (openingKickoff != default && DateTimeOffset.UtcNow >= openingKickoff)
        {
            reason = "Palpites especiais ficam bloqueados apos o inicio da Copa.";
            return true;
        }

        return false;
    }

    private static bool HasBlankSpecialPrediction(string champion, string runnerUp, string topScorer, string goldenBall)
    {
        return string.IsNullOrWhiteSpace(champion)
            || string.IsNullOrWhiteSpace(runnerUp)
            || string.IsNullOrWhiteSpace(topScorer)
            || string.IsNullOrWhiteSpace(goldenBall);
    }

    private static void EnsureTeam(Dictionary<string, MutableGroupStanding> entries, Team team)
    {
        entries.TryAdd(team.Code, new MutableGroupStanding(team));
    }

    private static void ApplyResult(MutableGroupStanding entry, int goalsFor, int goalsAgainst)
    {
        entry.Played++;
        entry.GoalsFor += goalsFor;
        entry.GoalsAgainst += goalsAgainst;

        if (goalsFor > goalsAgainst)
        {
            entry.Wins++;
            entry.Points += 3;
            return;
        }

        if (goalsFor == goalsAgainst)
        {
            entry.Draws++;
            entry.Points += 1;
            return;
        }

        entry.Losses++;
    }

    private bool AreAllGroupStageResultsRegistered()
    {
        return _db.Matches
            .Where(match => match.Phase == CompetitionPhase.GroupStage)
            .ToList()
            .All(match => match.Result is not null);
    }

    private void TryApplyKnockoutPairings()
    {
        if (!AreAllGroupStageResultsRegistered())
        {
            return;
        }

        var standings = GetGroupStandings()
            .ToDictionary(standing => standing.GroupName.Replace("Grupo ", string.Empty), standing => standing.Entries);
        var thirdPlaces = standings
            .Select(item => new { Group = item.Key, Entry = item.Value.ElementAt(2) })
            .OrderByDescending(item => item.Entry.Points)
            .ThenByDescending(item => item.Entry.GoalDifference)
            .ThenByDescending(item => item.Entry.GoalsFor)
            .Take(8)
            .ToList();
        var usedThirdGroups = new HashSet<string>();

        Team Resolve(string token)
        {
            if (token.StartsWith('1') || token.StartsWith('2'))
            {
                var index = token[0] == '1' ? 0 : 1;
                return standings[token[1].ToString()][index].Team;
            }

            if (token.StartsWith('3'))
            {
                var allowedGroups = token[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
                var third = thirdPlaces.FirstOrDefault(item => allowedGroups.Contains(item.Group) && usedThirdGroups.Add(item.Group));
                return third?.Entry.Team ?? new Team(token, token);
            }

            return new Team(token, token);
        }

        var pairings = new Dictionary<int, (string Home, string Away)>
        {
            [73] = ("2A", "2B"),
            [74] = ("1E", "3A/B/C/D/F"),
            [75] = ("1F", "2C"),
            [76] = ("1C", "2F"),
            [77] = ("1I", "3C/D/F/G/H"),
            [78] = ("2E", "2I"),
            [79] = ("1A", "3C/E/F/H/I"),
            [80] = ("1L", "3E/H/I/J/K"),
            [81] = ("1D", "3B/E/F/I/J"),
            [82] = ("1G", "3A/E/H/I/J"),
            [83] = ("2K", "2L"),
            [84] = ("1H", "2J"),
            [85] = ("1B", "3E/F/G/I/J"),
            [86] = ("1J", "2H"),
            [87] = ("1K", "3D/E/I/J/L"),
            [88] = ("2D", "2G")
        };

        foreach (var (officialNumber, pairing) in pairings)
        {
            var match = _db.Matches.SingleOrDefault(item => item.OfficialNumber == officialNumber);
            if (match is null || match.Result is not null)
            {
                continue;
            }

            match.HomeTeam = Resolve(pairing.Home);
            match.AwayTeam = Resolve(pairing.Away);
        }

        _db.SaveChanges();
    }

    private sealed class MutableGroupStanding
    {
        public MutableGroupStanding(Team team)
        {
            Team = team;
        }

        public Team Team { get; }
        public int Played { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }

        public GroupStandingEntry ToEntry()
        {
            return new GroupStandingEntry
            {
                Team = Team,
                Played = Played,
                Wins = Wins,
                Draws = Draws,
                Losses = Losses,
                GoalsFor = GoalsFor,
                GoalsAgainst = GoalsAgainst,
                Points = Points
            };
        }
    }
}
