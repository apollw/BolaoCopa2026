using BolaoCopa2026;
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
    private bool _autoFinalizeSynchronized;
    private bool _autoFinalizeRunning;

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

    public Participant? CurrentParticipant => _db.Participants
        .AsNoTracking()
        .SingleOrDefault(participant => participant.Id == CurrentParticipantId);
    public IReadOnlyList<PredictionRound> Rounds => _db.Rounds
        .AsNoTracking()
        .OrderBy(round => round.Id)
        .ToList();
    public IReadOnlyList<Match> Matches => _db.Matches
        .AsNoTracking()
        .OrderBy(match => match.Kickoff)
        .ThenBy(match => match.OfficialNumber)
        .ToList();
    public IReadOnlyList<Participant> Participants => _db.Participants
        .AsNoTracking()
        .OrderBy(participant => participant.Name)
        .ToList();
    public IReadOnlyList<ResultAudit> Audits => _db.ResultAudits
        .AsNoTracking()
        .OrderByDescending(audit => audit.RegisteredAt)
        .ToList();

    public DashboardStats GetDashboard()
    {
        AutoFinalizeStartedPredictionsAndRounds();
        TryApplyKnockoutPairings();

        var rounds = _db.Rounds
            .AsNoTracking()
            .OrderBy(round => round.Id)
            .ToList();
        var participants = _db.Participants
            .AsNoTracking()
            .OrderBy(participant => participant.Name)
            .ToList();
        var matches = _db.Matches
            .AsNoTracking()
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();
        var predictions = _db.Predictions
            .AsNoTracking()
            .ToList();
        var matchesById = matches.ToDictionary(match => match.Id);
        var ranking = BuildRanking(participants, matchesById, predictions);
        var groupStandings = BuildGroupStandings(matches);
        var completed = matches.Where(match => match.Result is not null).ToList();
        var finalizedPredictions = predictions.Where(prediction => prediction.SubmittedAt != null).ToList();
        var scoredPredictions = finalizedPredictions
            .Where(prediction => matchesById[prediction.MatchId].Result is not null)
            .Select(prediction => new
            {
                Prediction = prediction,
                Score = _scoringService.Score(matchesById[prediction.MatchId], prediction)
            })
            .ToList();
        var exactScores = scoredPredictions.Count(item => item.Score.ExactScore);
        var resultHits = scoredPredictions.Count(item => item.Score.ResultHit);
        var qualifiedHits = scoredPredictions.Count(item => item.Score.QualifiedHit);
        var brazilHits = scoredPredictions.Count(item => item.Score.BrazilHit);
        var totalPoints = ranking.Sum(entry => entry.Points);
        var draftPredictions = predictions.Count(prediction => prediction.SubmittedAt is null);
        var highlightCards = BuildHighlightCards(finalizedPredictions, scoredPredictions);
        var champion = matches.Count > 0 && completed.Count == matches.Count && ranking.Count > 0
            ? ranking.First()
            : null;

        return new DashboardStats
        {
            Ranking = ranking,
            Rounds = rounds,
            GroupStandings = groupStandings,
            UpcomingMatches = matches.Where(match => match.Result is null).Take(8).ToList(),
            CompletedMatches = completed.OrderByDescending(match => match.Kickoff).ToList(),
            PredictionOutcomeSlices = BuildOutcomeSlices(exactScores, resultHits, scoredPredictions.Count - exactScores - resultHits),
            TotalMatches = matches.Count,
            CompletedCount = completed.Count,
            BrazilMatchesCompleted = completed.Count(match => match.IncludesBrazil),
            GoalsScored = completed.Sum(match => match.Result!.HomeGoals + match.Result.AwayGoals),
            FinalizedPredictions = finalizedPredictions.Count,
            DraftPredictions = draftPredictions,
            ExactScores = exactScores,
            ResultHits = resultHits,
            KnockoutQualifiedHits = qualifiedHits,
            BrazilHits = brazilHits,
            TotalPoints = totalPoints,
            HighlightCards = highlightCards,
            ChampionHighlight = champion is null
                ? null
                : new DashboardChampionHighlight(
                    champion.Participant.Name,
                    champion.Points,
                    champion.ExactScores,
                    champion.KnockoutQualifiedHits)
        };
    }

    public MessageBoardView GetMessageBoard(int page, int pageSize = 15, int? currentParticipantId = null, bool isAdmin = false)
    {
        AutoFinalizeStartedPredictionsAndRounds();

        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(page, 1);

        var totalMessages = _db.CopaMessages.AsNoTracking().Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalMessages / (double)pageSize));
        page = Math.Min(page, totalPages);

        var rows = (
            from message in _db.CopaMessages.AsNoTracking()
            join participant in _db.Participants.AsNoTracking() on message.ParticipantId equals participant.Id
            orderby message.CreatedAt descending, message.Id descending
            select new
            {
                message.Id,
                message.Body,
                message.MoodKey,
                message.CreatedAt,
                ParticipantId = participant.Id,
                ParticipantName = participant.Name,
                participant.AvatarKey,
                participant.AvatarImagePath
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new MessageBoardView
        {
            Messages = rows.Select(row =>
            {
                var mood = MessageMoodCatalog.Resolve(row.MoodKey);

                return new MessageBoardEntry
                {
                    Id = row.Id,
                    Author = new MessageBoardAuthor
                    {
                        Id = row.ParticipantId,
                        Name = row.ParticipantName,
                        AvatarKey = row.AvatarKey,
                        AvatarImagePath = row.AvatarImagePath
                    },
                    Body = row.Body,
                    MoodKey = row.MoodKey,
                    MoodLabel = mood?.Label,
                    MoodEmoji = mood?.Emoji,
                    CanDelete = isAdmin || (currentParticipantId.HasValue && row.ParticipantId == currentParticipantId.Value),
                    CreatedAt = row.CreatedAt
                };
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalMessages = totalMessages,
            TotalPages = totalPages
        };
    }

    public bool AddMessage(int participantId, string body, string? moodKey, out string message, out MessageBoardEntry? createdEntry)
    {
        createdEntry = null;
        var participant = _db.Participants
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            message = "Participante nao encontrado.";
            return false;
        }

        body = body.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            message = "Escreva uma mensagem antes de publicar.";
            return false;
        }

        if (body.Length > 200)
        {
            message = "A mensagem deve ter no maximo 200 caracteres.";
            return false;
        }

        moodKey = MessageMoodCatalog.Resolve(moodKey)?.Key;

        var now = DateTimeOffset.UtcNow;
        var entity = new CopaMessage
        {
            ParticipantId = participantId,
            Body = body,
            MoodKey = moodKey,
            CreatedAt = now
        };
        _db.CopaMessages.Add(entity);

        _db.SaveChanges();
        var mood = MessageMoodCatalog.Resolve(moodKey);
        createdEntry = new MessageBoardEntry
        {
            Id = entity.Id,
            Author = new MessageBoardAuthor
            {
                Id = participant.Id,
                Name = participant.Name,
                AvatarKey = participant.AvatarKey,
                AvatarImagePath = participant.AvatarImagePath
            },
            Body = body,
            MoodKey = moodKey,
            MoodLabel = mood?.Label,
            MoodEmoji = mood?.Emoji,
            CanDelete = true,
            CreatedAt = now
        };
        message = "Mensagem publicada no mural.";
        return true;
    }

    public bool DeleteMessage(int messageId, int? participantId, bool isAdmin, out string message, out MessageBoardView? board, int page, int pageSize = 15)
    {
        board = null;
        var entity = _db.CopaMessages.SingleOrDefault(item => item.Id == messageId);
        if (entity is null)
        {
            message = "Mensagem nao encontrada.";
            return false;
        }

        if (!isAdmin)
        {
            if (participantId is null)
            {
                message = "Entre como participante para excluir mensagens.";
                return false;
            }

            if (entity.ParticipantId != participantId.Value)
            {
                message = "Voce so pode excluir suas proprias mensagens.";
                return false;
            }
        }

        _db.CopaMessages.Remove(entity);
        _db.SaveChanges();

        board = GetMessageBoard(page, pageSize, participantId, isAdmin);
        message = "Mensagem excluida.";
        return true;
    }

    public PublicPredictionsWallView GetPublicPredictionsWall()
    {
        AutoFinalizeStartedPredictionsAndRounds();

        var participants = _db.Participants
            .AsNoTracking()
            .OrderBy(participant => participant.Name)
            .ToList();
        var matches = _db.Matches
            .AsNoTracking()
            .ToList();
        var matchesByRound = matches
            .GroupBy(match => match.RoundId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var lockedRoundIds = matchesByRound
            .Where(kvp => kvp.Value.Any(match => DateTimeOffset.UtcNow >= match.Kickoff.ToUniversalTime()))
            .Select(kvp => kvp.Key)
            .ToHashSet();
        var definitiveCounts = _db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.SubmittedAt != null)
            .AsEnumerable()
            .Where(prediction => 
            {
                var match = matches.FirstOrDefault(m => m.Id == prediction.MatchId);
                return match != null && lockedRoundIds.Contains(match.RoundId);
            })
            .GroupBy(prediction => prediction.ParticipantId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());
        var finalizedRoundCounts = _db.RoundSubmissions
            .AsNoTracking()
            .GroupBy(submission => submission.ParticipantId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());
        var specialPredictionParticipantIds = _db.SpecialPredictions
            .AsNoTracking()
            .Where(prediction => prediction.SubmittedAt != null)
            .Select(prediction => prediction.ParticipantId)
            .ToHashSet();

        var participantViews = participants
            .Select(participant =>
                new PublicParticipantSummary
                {
                    Participant = new PublicParticipantDto
                    {
                        Id = participant.Id,
                        Name = participant.Name
                    },
                    DefinitivePredictionCount = definitiveCounts.GetValueOrDefault(participant.Id),
                    FinalizedRoundCount = finalizedRoundCounts.GetValueOrDefault(participant.Id),
                    HasSpecialPrediction = specialPredictionParticipantIds.Contains(participant.Id)
                })
            .ToList();

        return new PublicPredictionsWallView
        {
            Participants = participantViews
        };
    }

    public PublicParticipantPredictions? GetPublicParticipantPredictions(int participantId)
    {
        AutoFinalizeStartedPredictionsAndRounds();

        var participant = _db.Participants
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            return null;
        }

        var rounds = Rounds;
        var matches = _db.Matches
            .AsNoTracking()
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();
        var matchesByRound = matches
            .GroupBy(match => match.RoundId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Match>)group.ToList());
        var participantPredictions = _db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.ParticipantId == participantId)
            .ToDictionary(prediction => prediction.MatchId);
        var participantSubmissions = _db.RoundSubmissions
            .AsNoTracking()
            .Where(submission => submission.ParticipantId == participantId)
            .ToDictionary(submission => submission.RoundId);
        var specialPrediction = _db.SpecialPredictions
            .AsNoTracking()
            .SingleOrDefault(prediction => prediction.ParticipantId == participantId && prediction.SubmittedAt != null);

        var roundViews = rounds
            .Select(round =>
            {
                var roundMatches = matchesByRound.GetValueOrDefault(round.Id) ?? [];
                participantSubmissions.TryGetValue(round.Id, out var submission);
                var isRoundLocked = IsRoundLocked(round.Id);

                var lines = roundMatches
                    .Select(match =>
                    {
                        participantPredictions.TryGetValue(match.Id, out var prediction);
                        var definitivePrediction = (prediction?.SubmittedAt is not null && isRoundLocked) ? prediction : null;

                        return new PublicMatchPredictionLine
                        {
                            OfficialNumber = match.OfficialNumber,
                            HomeTeamCode = match.HomeTeam.Code,
                            HomeTeamName = match.HomeTeam.Name,
                            AwayTeamCode = match.AwayTeam.Code,
                            AwayTeamName = match.AwayTeam.Name,
                            Kickoff = match.Kickoff,
                            HomeGoals = definitivePrediction?.HomeGoals,
                            AwayGoals = definitivePrediction?.AwayGoals,
                            QualifiedTeamCode = definitivePrediction?.QualifiedTeamCode,
                            QualifiedTeamName = definitivePrediction is null
                                ? null
                                : ResolveQualifiedTeam(match, definitivePrediction.QualifiedTeamCode),
                            SubmittedAt = definitivePrediction?.SubmittedAt,
                            Outcome = ResolvePublicPredictionOutcome(match, definitivePrediction)
                        };
                    })
                    .ToList();

                return new PublicRoundPredictions
                {
                    Round = round,
                    Predictions = lines,
                    IsFinalized = submission is not null,
                    FinalizedAt = submission?.SubmittedAt,
                    DefinitiveCount = lines.Count(line => line.HasDefinitivePrediction),
                    TotalMatches = lines.Count
                };
            })
            .ToList();

        return new PublicParticipantPredictions
        {
            Participant = new PublicParticipantDto
            {
                Id = participant.Id,
                Name = participant.Name
            },
            Rounds = roundViews,
            SpecialPrediction = specialPrediction is null
                ? null
                : new PublicSpecialPredictionCard
                {
                    Champion = TeamCatalog.ResolveName(specialPrediction.Champion),
                    ChampionCode = TeamCatalog.ResolveCode(specialPrediction.Champion),
                    RunnerUp = TeamCatalog.ResolveName(specialPrediction.RunnerUp),
                    RunnerUpCode = TeamCatalog.ResolveCode(specialPrediction.RunnerUp),
                    TopScorer = specialPrediction.TopScorer,
                    GoldenBall = specialPrediction.GoldenBall,
                    SubmittedAt = specialPrediction.SubmittedAt!.Value
                },
            DefinitivePredictionCount = roundViews.Sum(round => round.DefinitiveCount),
            FinalizedRoundCount = roundViews.Count(round => round.IsFinalized)
        };
    }

    public RoundPredictionView GetRoundPrediction(int participantId, int? roundId)
    {
        AutoFinalizeStartedPredictionsAndRounds();
        TryApplyKnockoutPairings();

        var rounds = Rounds;
        var selectedRound = rounds.SingleOrDefault(round => round.Id == roundId) ?? rounds.First();
        var isLocked = !IsRoundAvailable(participantId, selectedRound.Id, out var lockReason);
        var isFinalized = IsRoundFinalized(participantId, selectedRound.Id);
        var draftLockUtc = GetRoundDraftLockTime(selectedRound.Id);
        var now = DateTimeOffset.UtcNow;
        var roundMatches = _db.Matches
            .AsNoTracking()
            .Where(match => match.RoundId == selectedRound.Id)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();

        var participantPredictions = _db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.ParticipantId == participantId)
            .ToDictionary(prediction => prediction.MatchId);

        var matches = roundMatches
            .Select(match =>
            {
                var hasStarted = HasMatchStarted(match, now);
                return new MatchPredictionView
                {
                    Match = match,
                    Prediction = participantPredictions.GetValueOrDefault(match.Id),
                    HasStarted = hasStarted,
                    CanEdit = !isLocked && !isFinalized,
                    DisplayLabel = GetMatchDisplayLabel(match)
                };
            })
            .ToList();

        return new RoundPredictionView
        {
            Round = selectedRound,
            Matches = matches,
            DraftLockUtc = draftLockUtc,
            IsLocked = isLocked,
            LockReason = lockReason,
            IsFinalized = isFinalized,
            DraftCount = matches.Count(item => item.Prediction is { IsFinal: false }),
            MissingCount = matches.Count(item => item.Prediction is null),
            StartedMatchCount = matches.Count(item => item.HasStarted),
            StartedWithoutPredictionCount = matches.Count(item => item.HasStarted && item.Prediction is null),
            OpenWithoutPredictionCount = matches.Count(item => !item.HasStarted && item.Prediction is null)
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
        AutoFinalizeStartedPredictionsAndRounds();

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

        if (IsRoundFinalized(participantId, match.RoundId))
        {
            message = "Esta rodada ja foi finalizada. Nao e mais possivel adicionar ou editar palpites.";
            return false;
        }

        if (IsRoundDraftLocked(match.RoundId, out var draftLockReason))
        {
            message = draftLockReason ?? "Os palpites desta rodada estao bloqueados.";
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

    public bool SaveRoundDrafts(int participantId, int roundId, IReadOnlyList<RoundDraftUpdate> drafts, out string message)
    {
        AutoFinalizeStartedPredictionsAndRounds();

        if (!IsRoundAvailable(participantId, roundId, out var lockReason))
        {
            message = lockReason ?? "Esta rodada ainda esta bloqueada.";
            return false;
        }

        if (IsRoundFinalized(participantId, roundId))
        {
            message = "Esta rodada ja foi finalizada. Nao e mais possivel adicionar ou editar palpites.";
            return false;
        }

        if (IsRoundDraftLocked(roundId, out var draftLockReason))
        {
            message = draftLockReason ?? "Os palpites desta rodada estao bloqueados.";
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var roundMatches = _db.Matches
            .Where(match => match.RoundId == roundId)
            .ToDictionary(match => match.Id);
        var existingPredictions = _db.Predictions
            .Where(prediction => prediction.ParticipantId == participantId && roundMatches.Keys.Contains(prediction.MatchId))
            .ToDictionary(prediction => prediction.MatchId);

        var savedCount = 0;
        var removedCount = 0;
        var incompleteCount = 0;
        var lockedCount = 0;

        foreach (var draft in drafts)
        {
            if (!roundMatches.TryGetValue(draft.MatchId, out var match))
            {
                continue;
            }

            existingPredictions.TryGetValue(draft.MatchId, out var existing);
            var qualifiedTeamCode = string.IsNullOrWhiteSpace(draft.QualifiedTeamCode)
                ? null
                : draft.QualifiedTeamCode.Trim();
            var hasAnyValue = draft.HomeGoals.HasValue || draft.AwayGoals.HasValue || !string.IsNullOrWhiteSpace(qualifiedTeamCode);

            if (!hasAnyValue)
            {
                if (existing is not null && !existing.IsFinal)
                {
                    _db.Predictions.Remove(existing);
                    existingPredictions.Remove(draft.MatchId);
                    removedCount++;
                }

                continue;
            }

            if (!draft.HomeGoals.HasValue || !draft.AwayGoals.HasValue)
            {
                incompleteCount++;
                continue;
            }

            if (existing?.IsFinal == true)
            {
                lockedCount++;
                continue;
            }

            if (existing is null)
            {
                var created = new Prediction
                {
                    ParticipantId = participantId,
                    MatchId = draft.MatchId,
                    HomeGoals = draft.HomeGoals.Value,
                    AwayGoals = draft.AwayGoals.Value,
                    QualifiedTeamCode = qualifiedTeamCode,
                    SavedAt = now
                };

                _db.Predictions.Add(created);
                existingPredictions[draft.MatchId] = created;
            }
            else
            {
                existing.HomeGoals = draft.HomeGoals.Value;
                existing.AwayGoals = draft.AwayGoals.Value;
                existing.QualifiedTeamCode = qualifiedTeamCode;
                existing.SavedAt = now;
            }

            savedCount++;
        }

        _db.SaveChanges();

        message = BuildRoundDraftSaveMessage(savedCount, removedCount, incompleteCount, lockedCount);
        return true;
    }

    public bool FinalizeRound(int participantId, int roundId, out string message)
    {
        AutoFinalizeStartedPredictionsAndRounds();

        if (!IsRoundAvailable(participantId, roundId, out var lockReason))
        {
            message = lockReason ?? "Esta rodada ainda esta bloqueada.";
            return false;
        }

        if (IsRoundFinalized(participantId, roundId))
        {
            message = "Esta rodada ja foi finalizada. Nao e mais possivel adicionar ou editar palpites.";
            return false;
        }

        var roundMatchIds = _db.Matches
            .Where(match => match.RoundId == roundId)
            .Select(match => match.Id)
            .ToList();

        var predictions = _db.Predictions
            .Where(prediction => prediction.ParticipantId == participantId && roundMatchIds.Contains(prediction.MatchId))
            .ToList();
        var finalizedAt = DateTimeOffset.UtcNow;

        foreach (var prediction in predictions.Where(prediction => !prediction.IsFinal))
        {
            prediction.SubmittedAt = finalizedAt;
        }

        _db.RoundSubmissions.Add(new RoundSubmission
        {
            ParticipantId = participantId,
            RoundId = roundId,
            SubmittedAt = finalizedAt
        });

        _db.SaveChanges();
        var missingCount = roundMatchIds.Count - predictions.Select(prediction => prediction.MatchId).Distinct().Count();
        message = missingCount > 0
            ? $"Rodada finalizada com {missingCount} partida(s) sem palpite. Elas ficaram em branco e nao poderao mais ser preenchidas ou editadas. Agora o comprovante de auditoria pode ser baixado."
            : "Rodada finalizada. Nao e mais possivel adicionar ou editar palpites. Agora o comprovante de auditoria pode ser baixado.";
        return true;
    }

    public bool CanSendPredictionAudit(int participantId, int roundId)
    {
        AutoFinalizeStartedPredictionsAndRounds();
        return IsRoundFinalized(participantId, roundId);
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

    public bool SaveParticipantAvatar(int participantId, string? avatarKey, out string message)
    {
        var participant = _db.Participants.SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            message = "Participante nao encontrado.";
            return false;
        }

        participant.AvatarKey = string.IsNullOrWhiteSpace(avatarKey) ? null : avatarKey.Trim();
        _db.SaveChanges();
        message = "Avatar do perfil atualizado.";
        return true;
    }

    public bool SaveParticipantAvatarImage(int participantId, string? avatarImagePath, out string message)
    {
        var participant = _db.Participants.SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            message = "Participante nao encontrado.";
            return false;
        }

        participant.AvatarImagePath = string.IsNullOrWhiteSpace(avatarImagePath) ? null : avatarImagePath.Trim();
        _db.SaveChanges();
        message = "Foto do perfil atualizada.";
        return true;
    }

    public bool DeleteParticipant(int participantId, string confirmName, out string message)
    {
        var participant = _db.Participants.SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            message = "Participante nao encontrado.";
            return false;
        }

        confirmName = confirmName.Trim();
        if (!string.Equals(participant.Name, confirmName, StringComparison.Ordinal))
        {
            message = "Confirmacao invalida. Digite exatamente o nome do participante para excluir.";
            return false;
        }

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        try
        {
            executionStrategy.Execute(() =>
            {
                using var transaction = _db.Database.BeginTransaction();

                var messages = _db.CopaMessages.Where(item => item.ParticipantId == participantId).ToList();
                var roundSubmissions = _db.RoundSubmissions.Where(item => item.ParticipantId == participantId).ToList();
                var predictions = _db.Predictions.Where(item => item.ParticipantId == participantId).ToList();
                var specialPrediction = _db.SpecialPredictions.SingleOrDefault(item => item.ParticipantId == participantId);

                if (messages.Count > 0)
                {
                    _db.CopaMessages.RemoveRange(messages);
                }

                if (roundSubmissions.Count > 0)
                {
                    _db.RoundSubmissions.RemoveRange(roundSubmissions);
                }

                if (predictions.Count > 0)
                {
                    _db.Predictions.RemoveRange(predictions);
                }

                if (specialPrediction is not null)
                {
                    _db.SpecialPredictions.Remove(specialPrediction);
                }

                _db.Participants.Remove(participant);
                _db.SaveChanges();
                transaction.Commit();
            });

            message = "Participante e todos os dados vinculados foram excluidos.";
            return true;
        }
        catch
        {
            throw;
        }
    }

    public bool IsRoundAvailable(int participantId, int roundId, out string? reason)
    {
        AutoFinalizeStartedPredictionsAndRounds();

        reason = null;
        if (IsRoundDraftLocked(roundId, out reason))
        {
            return false;
        }

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
        AutoFinalizeStartedPredictionsAndRounds();

        var participants = _db.Participants
            .AsNoTracking()
            .OrderBy(participant => participant.Name)
            .ToList();
        var matches = _db.Matches
            .AsNoTracking()
            .ToDictionary(match => match.Id);
        var predictions = _db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.SubmittedAt != null)
            .ToList();

        return BuildRanking(participants, matches, predictions);
    }

    public bool IsRoundFinalized(int participantId, int roundId)
    {
        AutoFinalizeStartedPredictionsAndRounds();
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
        var matches = _db.Matches
            .AsNoTracking()
            .ToList();

        return BuildGroupStandings(matches);
    }

    private IReadOnlyList<RankingEntry> BuildRanking(
        IReadOnlyList<Participant> participants,
        IReadOnlyDictionary<int, Match> matchesById,
        IReadOnlyList<Prediction> predictions)
    {
        return participants
            .Select(participant =>
            {
                var scored = predictions
                    .Where(prediction => prediction.ParticipantId == participant.Id)
                    .Select(prediction => _scoringService.Score(matchesById[prediction.MatchId], prediction))
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

    private IReadOnlyList<GroupStanding> BuildGroupStandings(IReadOnlyList<Match> matches)
    {
        return matches
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

    private IReadOnlyList<GroupStandingEntry> BuildGroupEntries(IReadOnlyList<Match> matches)
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

        var ordered = entries.Values
            .Select(entry => entry.ToEntry())
            .ToList();

        return OrderGroupStandings(ordered, matches);
    }

    private bool IsSpecialPredictionLocked(SpecialPrediction? prediction, out string? reason)
    {
        reason = null;
        if (prediction?.IsFinal == true)
        {
            reason = "Palpites especiais ja finalizados. O regulamento nao permite editar depois da confirmacao.";
            return true;
        }

        var specialDeadline = GetSpecialPredictionDeadline();

        if (specialDeadline is not null && DateTimeOffset.UtcNow > specialDeadline.Value)
        {
            reason = "Palpites especiais ficam bloqueados apos o fim da 3a rodada da fase de grupos.";
            return true;
        }

        return false;
    }

    private DateTimeOffset? GetSpecialPredictionDeadline()
    {
        var thirdRound = _db.Rounds.SingleOrDefault(round => round.Id == 3);
        if (thirdRound is null)
        {
            return null;
        }

        return new DateTimeOffset(
                thirdRound.EndsOn.Year,
                thirdRound.EndsOn.Month,
                thirdRound.EndsOn.Day,
                23,
                59,
                59,
                TimeSpan.FromHours(-3))
            .ToUniversalTime();
    }

    private void AutoFinalizeStartedPredictionsAndRounds()
    {
        if (_autoFinalizeSynchronized || _autoFinalizeRunning)
        {
            return;
        }

        _autoFinalizeRunning = true;

        try
        {
        var now = DateTimeOffset.UtcNow;
        var matches = _db.Matches
            .Select(match => new
            {
                match.Id,
                match.RoundId,
                Kickoff = match.Kickoff.ToUniversalTime()
            })
            .ToList();

        if (matches.Count == 0)
        {
            return;
        }

        var startedMatches = matches
            .Where(match => now >= match.Kickoff)
            .ToList();
        var startedMatchIds = startedMatches
            .Select(match => match.Id)
            .ToHashSet();
        var startedKickoffs = startedMatches.ToDictionary(match => match.Id, match => match.Kickoff);
        var changed = false;

        if (startedMatchIds.Count > 0)
        {
            var startedDrafts = _db.Predictions
                .Where(prediction => prediction.SubmittedAt == null && startedMatchIds.Contains(prediction.MatchId))
                .ToList();

            foreach (var prediction in startedDrafts)
            {
                prediction.SubmittedAt = startedKickoffs[prediction.MatchId];
                changed = true;
            }
        }

        var fullyStartedRounds = matches
            .GroupBy(match => match.RoundId)
            .Where(group => group.All(match => now >= match.Kickoff))
            .Select(group => new
            {
                RoundId = group.Key,
                SubmittedAt = group.Max(match => match.Kickoff)
            })
            .ToList();

        if (fullyStartedRounds.Count > 0)
        {
            var participantIds = _db.Participants.Select(participant => participant.Id).ToList();
            var roundIds = fullyStartedRounds.Select(round => round.RoundId).ToHashSet();
            var existingSubmissions = _db.RoundSubmissions
                .Where(submission => roundIds.Contains(submission.RoundId))
                .ToList()
                .Select(submission => (submission.ParticipantId, submission.RoundId))
                .ToHashSet();

            foreach (var participantId in participantIds)
            {
                foreach (var round in fullyStartedRounds)
                {
                    if (existingSubmissions.Contains((participantId, round.RoundId)))
                    {
                        continue;
                    }

                    _db.RoundSubmissions.Add(new RoundSubmission
                    {
                        ParticipantId = participantId,
                        RoundId = round.RoundId,
                        SubmittedAt = round.SubmittedAt
                    });
                    existingSubmissions.Add((participantId, round.RoundId));
                    changed = true;
                }
            }
        }

        if (changed)
        {
            _db.SaveChanges();
        }

        _autoFinalizeSynchronized = true;
        }
        finally
        {
            _autoFinalizeRunning = false;
        }
    }

    private static string BuildRoundDraftSaveMessage(int savedCount, int removedCount, int incompleteCount, int lockedCount)
    {
        var parts = new List<string>();

        if (savedCount > 0)
        {
            parts.Add(savedCount == 1
                ? "1 rascunho salvo."
                : $"{savedCount} rascunhos salvos.");
        }

        if (removedCount > 0)
        {
            parts.Add(removedCount == 1
                ? "1 rascunho em branco foi removido."
                : $"{removedCount} rascunhos em branco foram removidos.");
        }

        if (incompleteCount > 0)
        {
            parts.Add(incompleteCount == 1
                ? "1 partida incompleta nao foi salva."
                : $"{incompleteCount} partidas incompletas nao foram salvas.");
        }

        if (lockedCount > 0)
        {
            parts.Add(lockedCount == 1
                ? "1 partida ja estava bloqueada pelo horario."
                : $"{lockedCount} partidas ja estavam bloqueadas pelo horario.");
        }

        return parts.Count == 0
            ? "Nenhuma alteracao para salvar."
            : string.Join(' ', parts);
    }

    private static bool HasBlankSpecialPrediction(string champion, string runnerUp, string topScorer, string goldenBall)
    {
        return string.IsNullOrWhiteSpace(champion)
            || string.IsNullOrWhiteSpace(runnerUp)
            || string.IsNullOrWhiteSpace(topScorer)
            || string.IsNullOrWhiteSpace(goldenBall);
    }

    private static bool HasMatchStarted(Match match, DateTimeOffset? now = null)
    {
        return (now ?? DateTimeOffset.UtcNow) >= match.Kickoff.ToUniversalTime();
    }

    private DateTimeOffset? GetRoundDraftLockTime(int roundId)
    {
        var firstKickoff = _db.Matches
            .Where(match => match.RoundId == roundId)
            .AsNoTracking()
            .OrderBy(match => match.Kickoff)
            .Select(match => (DateTimeOffset?)match.Kickoff.ToUniversalTime())
            .FirstOrDefault();

        return firstKickoff;
    }

    private bool IsRoundDraftLocked(int roundId, out string? reason, DateTimeOffset? now = null)
    {
        var lockTime = GetRoundDraftLockTime(roundId);
        if (lockTime is null)
        {
            reason = null;
            return false;
        }

        if ((now ?? DateTimeOffset.UtcNow) >= lockTime.Value)
        {
            reason = $"Os palpites desta rodada travaram no inicio da primeira partida ({lockTime.Value.ToOffset(TimeSpan.FromHours(-3)):dd/MM HH:mm}).";
            return true;
        }

        reason = null;
        return false;
    }

    private bool IsRoundLocked(int roundId, DateTimeOffset? now = null)
    {
        var now_utc = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var firstKickoff = _db.Matches
            .Where(match => match.RoundId == roundId)
            .OrderBy(match => match.Kickoff)
            .Select(match => match.Kickoff.ToUniversalTime())
            .FirstOrDefault();

        return firstKickoff != default && now_utc >= firstKickoff;
    }

    private static string? ResolveQualifiedTeam(Match match, string? qualifiedTeamCode)
    {
        return qualifiedTeamCode switch
        {
            null or "" => null,
            _ when qualifiedTeamCode == match.HomeTeam.Code => match.HomeTeam.Name,
            _ when qualifiedTeamCode == match.AwayTeam.Code => match.AwayTeam.Name,
            _ => qualifiedTeamCode
        };
    }

    private static string GetMatchDisplayLabel(Match match)
    {
        if (match.Phase == CompetitionPhase.GroupStage)
        {
            return match.GroupName ?? "Fase de grupos";
        }

        return match.Phase switch
        {
            CompetitionPhase.RoundOf32 => match.OfficialNumber switch
            {
                73 => "Segunda fase 1",
                74 => "Segunda fase 2",
                75 => "Segunda fase 3",
                76 => "Segunda fase 4",
                77 => "Segunda fase 5",
                78 => "Segunda fase 6",
                79 => "Segunda fase 7",
                80 => "Segunda fase 8",
                81 => "Segunda fase 9",
                82 => "Segunda fase 10",
                83 => "Segunda fase 11",
                84 => "Segunda fase 12",
                85 => "Segunda fase 13",
                86 => "Segunda fase 14",
                87 => "Segunda fase 15",
                88 => "Segunda fase 16",
                _ => match.OfficialNumber.ToString()
            },
            CompetitionPhase.RoundOf16 => match.OfficialNumber switch
            {
                89 => "Oitavas 1",
                90 => "Oitavas 2",
                93 => "Oitavas 3",
                94 => "Oitavas 4",
                91 => "Oitavas 5",
                92 => "Oitavas 6",
                95 => "Oitavas 7",
                96 => "Oitavas 8",
                _ => match.OfficialNumber.ToString()
            },
            CompetitionPhase.QuarterFinal => match.OfficialNumber switch
            {
                97 => "Venc. Oitavas 1 x Venc. Oitavas 2",
                98 => "Venc. Oitavas 3 x Venc. Oitavas 4",
                99 => "Venc. Oitavas 5 x Venc. Oitavas 6",
                100 => "Venc. Oitavas 7 x Venc. Oitavas 8",
                _ => match.OfficialNumber.ToString()
            },
            CompetitionPhase.SemiFinal => match.OfficialNumber switch
            {
                101 => "Venc. Quartas 1 x Venc. Quartas 2",
                102 => "Venc. Quartas 3 x Venc. Quartas 4",
                _ => match.OfficialNumber.ToString()
            },
            CompetitionPhase.ThirdPlace => "Perd. Semifinal 1 x Perd. Semifinal 2",
            CompetitionPhase.Final => "Venc. Semifinal 1 x Venc. Semifinal 2",
            _ => match.Phase.ToString()
        };
    }

    private static PublicPredictionOutcome ResolvePublicPredictionOutcome(Match match, Prediction? prediction)
    {
        if (prediction is null || prediction.SubmittedAt is null)
        {
            return PublicPredictionOutcome.NoPrediction;
        }

        if (match.Result is null)
        {
            return PublicPredictionOutcome.PendingOfficialResult;
        }

        if (prediction.HomeGoals == match.Result.HomeGoals && prediction.AwayGoals == match.Result.AwayGoals)
        {
            return PublicPredictionOutcome.ExactScore;
        }

        return GetOutcome(prediction.HomeGoals, prediction.AwayGoals) == GetOutcome(match.Result.HomeGoals, match.Result.AwayGoals)
            ? PublicPredictionOutcome.ResultHit
            : PublicPredictionOutcome.Miss;
    }

    private static MatchOutcome GetOutcome(int homeGoals, int awayGoals)
    {
        if (homeGoals > awayGoals)
        {
            return MatchOutcome.HomeWin;
        }

        return homeGoals == awayGoals ? MatchOutcome.Draw : MatchOutcome.AwayWin;
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

    private static IReadOnlyList<DashboardChartSlice> BuildOutcomeSlices(int exactScores, int resultHits, int misses)
    {
        return
        [
            new DashboardChartSlice("Exatos", exactScores, "#176b52"),
            new DashboardChartSlice("Resultado", resultHits, "#d5a928"),
            new DashboardChartSlice("Erros", misses, "#a33a2b")
        ];
    }

    private static IReadOnlyList<DashboardHighlightCard> BuildHighlightCards(
        IReadOnlyList<Prediction> finalizedPredictions,
        IReadOnlyList<dynamic> scoredPredictions)
    {
        var mostRegistered = finalizedPredictions
            .GroupBy(prediction => (prediction.HomeGoals, prediction.AwayGoals))
            .Select(group => new
            {
                group.Key.HomeGoals,
                group.Key.AwayGoals,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.HomeGoals + item.AwayGoals)
            .ThenBy(item => item.HomeGoals)
            .ThenBy(item => item.AwayGoals)
            .FirstOrDefault();

        var highestRegistered = finalizedPredictions
            .OrderByDescending(prediction => prediction.HomeGoals + prediction.AwayGoals)
            .ThenByDescending(prediction => prediction.HomeGoals)
            .ThenByDescending(prediction => prediction.AwayGoals)
            .FirstOrDefault();

        var mostAccurate = scoredPredictions
            .Where(item => item.Score.ExactScore)
            .GroupBy(item => (item.Prediction.HomeGoals, item.Prediction.AwayGoals))
            .Select(group => new
            {
                group.Key.HomeGoals,
                group.Key.AwayGoals,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.HomeGoals + item.AwayGoals)
            .ThenBy(item => item.HomeGoals)
            .ThenBy(item => item.AwayGoals)
            .FirstOrDefault();

        return
        [
            new DashboardHighlightCard(
                "Palpite mais registrado",
                mostRegistered is null ? "-" : $"{mostRegistered.HomeGoals} x {mostRegistered.AwayGoals}",
                mostRegistered is null ? "Sem palpites suficientes" : $"{mostRegistered.Count} registros iguais"),
            new DashboardHighlightCard(
                "Maior palpite registrado",
                highestRegistered is null ? "-" : $"{highestRegistered.HomeGoals} x {highestRegistered.AwayGoals}",
                highestRegistered is null ? "Sem palpites registrados" : $"Total de {highestRegistered.HomeGoals + highestRegistered.AwayGoals} gols previstos"),
            new DashboardHighlightCard(
                "Palpite mais acertado",
                mostAccurate is null ? "-" : $"{mostAccurate.HomeGoals} x {mostAccurate.AwayGoals}",
                mostAccurate is null ? "Ainda sem acertos exatos" : $"{mostAccurate.Count} acerto(s) exato(s)")
        ];
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
        var changed = false;
        var fixedRoundOf32Pairings = new Dictionary<int, (Team Home, Team Away)>
        {
            [73] = (ResolveCatalogTeam("RSA"), ResolveCatalogTeam("CAN")),
            [74] = (ResolveCatalogTeam("GER"), ResolveCatalogTeam("PAR")),
            [75] = (ResolveCatalogTeam("NED"), ResolveCatalogTeam("MAR")),
            [76] = (ResolveCatalogTeam("BRA"), ResolveCatalogTeam("JPN")),
            [77] = (ResolveCatalogTeam("FRA"), ResolveCatalogTeam("SWE")),
            [78] = (ResolveCatalogTeam("CIV"), ResolveCatalogTeam("NOR")),
            [79] = (ResolveCatalogTeam("MEX"), ResolveCatalogTeam("ECU")),
            [80] = (ResolveCatalogTeam("ENG"), ResolveCatalogTeam("COD")),
            [81] = (ResolveCatalogTeam("USA"), ResolveCatalogTeam("BIH")),
            [82] = (ResolveCatalogTeam("BEL"), ResolveCatalogTeam("SEN")),
            [83] = (ResolveCatalogTeam("POR"), ResolveCatalogTeam("CRO")),
            [84] = (ResolveCatalogTeam("ESP"), ResolveCatalogTeam("AUT")),
            [85] = (ResolveCatalogTeam("SUI"), ResolveCatalogTeam("ALG")),
            [86] = (ResolveCatalogTeam("ARG"), ResolveCatalogTeam("CPV")),
            [87] = (ResolveCatalogTeam("COL"), ResolveCatalogTeam("GHA")),
            [88] = (ResolveCatalogTeam("AUS"), ResolveCatalogTeam("EGY"))
        };

        foreach (var (officialNumber, pairing) in fixedRoundOf32Pairings)
        {
            var match = _db.Matches.SingleOrDefault(item => item.OfficialNumber == officialNumber);
            if (match is null || match.Result is not null)
            {
                continue;
            }

            if (match.HomeTeam.Code == pairing.Home.Code && match.AwayTeam.Code == pairing.Away.Code)
            {
                continue;
            }

            match.HomeTeam = pairing.Home;
            match.AwayTeam = pairing.Away;
            changed = true;
        }

        var fixedRoundOf16Pairings = new Dictionary<int, (Team Home, Team Away)>
        {
            [89] = (ResolveCatalogTeam("PAR"), ResolveCatalogTeam("FRA")),
            [90] = (ResolveCatalogTeam("CAN"), ResolveCatalogTeam("MAR")),
            [91] = (ResolveCatalogTeam("BRA"), ResolveCatalogTeam("NOR")),
            [92] = (ResolveCatalogTeam("MEX"), ResolveCatalogTeam("ENG")),
            [93] = (ResolveCatalogTeam("POR"), ResolveCatalogTeam("ESP")),
            [94] = (ResolveCatalogTeam("USA"), ResolveCatalogTeam("BEL")),
            [95] = (ResolveCatalogTeam("ARG"), ResolveCatalogTeam("EGY")),
            [96] = (ResolveCatalogTeam("SUI"), ResolveCatalogTeam("COL"))
        };

        foreach (var (officialNumber, pairing) in fixedRoundOf16Pairings)
        {
            var match = _db.Matches.SingleOrDefault(item => item.OfficialNumber == officialNumber);
            if (match is null || match.Result is not null)
            {
                continue;
            }

            if (match.HomeTeam.Code == pairing.Home.Code && match.AwayTeam.Code == pairing.Away.Code)
            {
                continue;
            }

            match.HomeTeam = pairing.Home;
            match.AwayTeam = pairing.Away;
            changed = true;
        }

        var matchesByOfficialNumber = _db.Matches
            .Where(match => match.OfficialNumber >= 73 && match.OfficialNumber <= 104)
            .ToDictionary(match => match.OfficialNumber);
        var knockoutPairings = new Dictionary<int, (string Home, string Away)>
        {
            [97] = ("W89", "W90"),
            [98] = ("W93", "W94"),
            [99] = ("W91", "W92"),
            [100] = ("W95", "W96"),
            [101] = ("W97", "W98"),
            [102] = ("W99", "W100"),
            [103] = ("L101", "L102"),
            [104] = ("W101", "W102")
        };

        foreach (var (officialNumber, pairing) in knockoutPairings)
        {
            if (!matchesByOfficialNumber.TryGetValue(officialNumber, out var match) || match.Result is not null)
            {
                continue;
            }

            var homeTeam = ResolveKnockoutToken(pairing.Home, matchesByOfficialNumber);
            var awayTeam = ResolveKnockoutToken(pairing.Away, matchesByOfficialNumber);
            if (homeTeam is null || awayTeam is null)
            {
                continue;
            }

            if (match.HomeTeam.Code == homeTeam.Code && match.AwayTeam.Code == awayTeam.Code)
            {
                continue;
            }

            match.HomeTeam = homeTeam;
            match.AwayTeam = awayTeam;
            changed = true;
        }

        if (changed)
        {
            _db.SaveChanges();
        }
    }

    private static Team? ResolveKnockoutToken(string token, IReadOnlyDictionary<int, Match> matchesByOfficialNumber)
    {
        if (token.Length < 2 || !int.TryParse(token[1..], out var officialNumber))
        {
            return null;
        }

        if (!matchesByOfficialNumber.TryGetValue(officialNumber, out var sourceMatch) || sourceMatch.Result is null)
        {
            return null;
        }

        var qualifiedCode = sourceMatch.Result.QualifiedTeamCode;
        var winner = qualifiedCode switch
        {
            _ when qualifiedCode == sourceMatch.HomeTeam.Code => sourceMatch.HomeTeam,
            _ when qualifiedCode == sourceMatch.AwayTeam.Code => sourceMatch.AwayTeam,
            _ => null
        };

        if (winner is null)
        {
            return null;
        }

        var resolved = token[0] switch
        {
            'W' => winner,
            'L' when winner.Code == sourceMatch.HomeTeam.Code => sourceMatch.AwayTeam,
            'L' when winner.Code == sourceMatch.AwayTeam.Code => sourceMatch.HomeTeam,
            _ => null
        };

        return resolved is null ? null : new Team(resolved.Code, resolved.Name, resolved.IsBrazil);
    }

    private static Team ResolveCatalogTeam(string teamCode)
    {
        var catalogEntry = TeamCatalog.Resolve(teamCode);
        return catalogEntry is null
            ? new Team(teamCode, teamCode)
            : new Team(catalogEntry.Code, catalogEntry.Name, catalogEntry.IsBrazil);
    }

    private sealed class MutableGroupStanding
    {
        public MutableGroupStanding(Team team)
        {
            Team = team;
            FifaRanking = TeamCatalog.ResolveFifaRanking(team.Code);
        }

        public Team Team { get; }
        public int Played { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }
        public int FairPlayPenalty { get; set; }
        public int FifaRanking { get; }
        public int GoalDifference => GoalsFor - GoalsAgainst;

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
                Points = Points,
                FairPlayPenalty = FairPlayPenalty,
                FifaRanking = FifaRanking
            };
        }
    }

    private static IReadOnlyList<GroupStandingEntry> OrderGroupStandings(
        IReadOnlyList<GroupStandingEntry> entries,
        IReadOnlyList<Match> matches)
    {
        var ordered = new List<GroupStandingEntry>();

        foreach (var pointGroup in entries
                     .OrderByDescending(entry => entry.Points)
                     .GroupBy(entry => entry.Points)
                     .OrderByDescending(group => group.Key))
        {
            var tiedEntries = pointGroup.ToList();
            if (tiedEntries.Count == 1)
            {
                ordered.Add(tiedEntries[0]);
                continue;
            }

            ordered.AddRange(OrderTiedGroup(tiedEntries, matches));
        }

        return ordered;
    }

    private static IReadOnlyList<GroupStandingEntry> OrderTiedGroup(
        IReadOnlyList<GroupStandingEntry> tiedEntries,
        IReadOnlyList<Match> matches)
    {
        var headToHeadStats = BuildHeadToHeadStats(tiedEntries, matches);
        var ordered = tiedEntries
            .OrderByDescending(entry => headToHeadStats[entry.Team.Code].Points)
            .ThenByDescending(entry => headToHeadStats[entry.Team.Code].GoalDifference)
            .ThenByDescending(entry => headToHeadStats[entry.Team.Code].GoalsFor)
            .ToList();

        var groups = ordered
            .GroupBy(entry => new
            {
                Points = headToHeadStats[entry.Team.Code].Points,
                GoalDifference = headToHeadStats[entry.Team.Code].GoalDifference,
                GoalsFor = headToHeadStats[entry.Team.Code].GoalsFor
            })
            .Select(group => group.ToList())
            .ToList();

        if (groups.Count == 1 && groups[0].Count == tiedEntries.Count)
        {
            return OrderByOverallCriteria(tiedEntries);
        }

        var resolved = new List<GroupStandingEntry>();

        foreach (var subgroup in groups)
        {
            resolved.AddRange(subgroup.Count == 1
                ? subgroup
                : OrderTiedGroup(subgroup, matches));
        }

        return resolved;
    }

    private static IReadOnlyDictionary<string, MutableGroupStanding> BuildHeadToHeadStats(
        IReadOnlyList<GroupStandingEntry> entries,
        IReadOnlyList<Match> matches)
    {
        var teamCodes = entries
            .Select(entry => entry.Team.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stats = entries.ToDictionary(entry => entry.Team.Code, entry => new MutableGroupStanding(entry.Team));

        foreach (var match in matches)
        {
            if (!teamCodes.Contains(match.HomeTeam.Code) || !teamCodes.Contains(match.AwayTeam.Code))
            {
                continue;
            }

            if (match.Result is null)
            {
                continue;
            }

            ApplyResult(stats[match.HomeTeam.Code], match.Result.HomeGoals, match.Result.AwayGoals);
            ApplyResult(stats[match.AwayTeam.Code], match.Result.AwayGoals, match.Result.HomeGoals);
        }

        return stats;
    }

    private static IReadOnlyList<GroupStandingEntry> OrderByOverallCriteria(IReadOnlyList<GroupStandingEntry> entries)
    {
        return entries
            .OrderByDescending(entry => entry.Points)
            .ThenByDescending(entry => entry.GoalDifference)
            .ThenByDescending(entry => entry.GoalsFor)
            .ThenBy(entry => entry.FairPlayPenalty)
            .ThenBy(entry => entry.FifaRanking)
            .ThenBy(entry => entry.Team.Name)
            .ToList();
    }
}
