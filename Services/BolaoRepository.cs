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
            HighlightCards = highlightCards
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
                    CanEdit = !isLocked && !isFinalized
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

    public bool SaveParticipantAvatar(int participantId, string? avatarKey, out string message)
    {
        var participant = _db.Participants
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == participantId);
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
        var participant = _db.Participants
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == participantId);
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
            .OrderBy(match => match.Kickoff)
            .Select(match => (DateTimeOffset?)match.Kickoff.ToUniversalTime())
            .FirstOrDefault();

        return firstKickoff?.AddHours(-4);
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
            reason = $"Os palpites desta rodada travaram 4h antes da primeira partida oficial ({lockTime.Value.ToOffset(TimeSpan.FromHours(-3)):dd/MM HH:mm}).";
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
