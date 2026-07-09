using System.Security.Cryptography;
using System.Text;
using BolaoCopa2026;
using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace BolaoCopa2026.Services;

public sealed class AuditImageService : IDisposable
{
    private static readonly SKColor PageBackground = SKColor.Parse("#f4f7f2");
    private static readonly SKColor CardBackground = SKColors.White;
    private static readonly SKColor CardBorder = SKColor.Parse("#dfe7da");
    private static readonly SKColor HeaderBackground = SKColor.Parse("#123d32");
    private static readonly SKColor HeaderAccent = SKColor.Parse("#d5a928");
    private static readonly SKColor PrimaryText = SKColor.Parse("#182018");
    private static readonly SKColor SecondaryText = SKColor.Parse("#607166");
    private static readonly SKColor SubtleFill = SKColor.Parse("#eef3ea");
    private static readonly SKColor SubtleBorder = SKColor.Parse("#d4dfce");
    private static readonly SKColor RowFill = SKColor.Parse("#f8faf6");
    private static readonly SKColor RowBorder = SKColor.Parse("#edf2e9");

    private readonly BolaoDbContext _db;
    private readonly ScoringService _scoringService;
    private readonly SKTypeface _textTypeface;

    public AuditImageService(BolaoDbContext db, ScoringService scoringService)
    {
        _db = db;
        _scoringService = scoringService;
        _textTypeface = ResolveTypeface();
    }

    public void Dispose()
    {
        _textTypeface.Dispose();
    }

    public AuditSnapshot? BuildSnapshot(int participantId, int roundId)
    {
        var participant = _db.Participants.SingleOrDefault(item => item.Id == participantId);
        var round = _db.Rounds.SingleOrDefault(item => item.Id == roundId);
        if (participant is null || round is null)
        {
            return null;
        }

        var matches = _db.Matches
            .Where(match => match.RoundId == roundId)
            .ToList()
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();
        var submission = _db.RoundSubmissions.SingleOrDefault(item =>
            item.ParticipantId == participantId && item.RoundId == roundId);
        var predictions = _db.Predictions
            .Where(prediction => prediction.ParticipantId == participantId)
            .ToDictionary(prediction => prediction.MatchId);

        if (matches.Count == 0 || submission is null)
        {
            return null;
        }

        var lines = matches
            .Select(match =>
            {
                var prediction = predictions.GetValueOrDefault(match.Id);
                return new AuditPredictionLine
                {
                    OfficialNumber = match.OfficialNumber,
                    HomeTeam = match.HomeTeam.Name,
                    AwayTeam = match.AwayTeam.Name,
                    HomeGoals = prediction?.IsFinal == true ? prediction.HomeGoals : null,
                    AwayGoals = prediction?.IsFinal == true ? prediction.AwayGoals : null,
                    QualifiedTeam = prediction?.IsFinal == true ? ResolveQualifiedTeam(match, prediction.QualifiedTeamCode) : null,
                    SavedAt = prediction?.IsFinal == true ? prediction.SavedAt : null,
                    SubmittedAt = prediction?.SubmittedAt
                };
            })
            .ToList();

        var generatedAt = DateTimeOffset.UtcNow;
        return new AuditSnapshot
        {
            Participant = participant,
            Round = round,
            Predictions = lines,
            GeneratedAt = generatedAt,
            ProofHash = BuildHash(participant, round, lines)
        };
    }

    public void MarkDownloaded(AuditSnapshot snapshot)
    {
        var submission = _db.RoundSubmissions.SingleOrDefault(item =>
            item.ParticipantId == snapshot.Participant.Id && item.RoundId == snapshot.Round.Id);
        if (submission is null)
        {
            return;
        }

        submission.AuditDownloadedAt = snapshot.GeneratedAt;
        submission.AuditProofHash = snapshot.ProofHash;
        _db.SaveChanges();
    }

    public bool IsBolaoComplete()
    {
        var totalMatches = _db.Matches.Count();
        return totalMatches > 0 && _db.Matches.Count(match => match.Result != null) == totalMatches;
    }

    public FinalCardPackage? BuildFinalCardPackage(int participantId, bool allowPartial = false)
    {
        var participant = _db.Participants.AsNoTracking().SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            return null;
        }

        var matches = _db.Matches.AsNoTracking().ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var completedMatches = matches.Count(match => match.Result is not null);
        var isComplete = completedMatches == matches.Count;
        if (!isComplete && !allowPartial)
        {
            return null;
        }

        var participants = _db.Participants
            .AsNoTracking()
            .Where(item => !item.IsAdmin)
            .OrderBy(item => item.Name)
            .ToList();
        var predictions = _db.Predictions.AsNoTracking().ToList();
        var matchesById = matches.ToDictionary(match => match.Id);
        var ranking = BuildFinalCardRanking(participants, matchesById, predictions);
        var rankingEntry = ranking.SingleOrDefault(entry => entry.Participant.Id == participantId);
        if (rankingEntry is null)
        {
            return null;
        }

        var placement = ranking
            .Select((entry, index) => new { entry.Participant.Id, Index = index })
            .First(item => item.Id == participantId)
            .Index + 1;
        var roundsById = _db.Rounds.AsNoTracking().ToDictionary(round => round.Id);
        var bestRound = predictions
            .Where(prediction => prediction.ParticipantId == participantId && matchesById.ContainsKey(prediction.MatchId))
            .GroupBy(prediction => matchesById[prediction.MatchId].RoundId)
            .Select(group => new
            {
                RoundId = group.Key,
                Points = group.Sum(prediction => _scoringService.Score(matchesById[prediction.MatchId], prediction).Points)
            })
            .OrderByDescending(item => item.Points)
            .ThenBy(item => item.RoundId)
            .FirstOrDefault();

        var bestRoundLabel = bestRound is null || bestRound.Points == 0
            ? "Melhor rodada ainda indefinida"
            : $"{roundsById.GetValueOrDefault(bestRound.RoundId)?.Name ?? $"Rodada {bestRound.RoundId}"}: {bestRound.Points} pts";
        var curiosityLabel = rankingEntry.ExactScores > 0
            ? $"Cravou {rankingEntry.ExactScores} placar(es) exato(s)"
            : $"Acertou {rankingEntry.ResultHits} resultado(s)";

        return new FinalCardPackage
        {
            Participant = participant,
            Ranking = rankingEntry,
            StatusLabel = isComplete ? "Card final do Bolao" : "Previa parcial do card",
            BestRoundLabel = bestRoundLabel,
            CuriosityLabel = curiosityLabel,
            Placement = placement,
            ParticipantCount = ranking.Count,
            CompletedMatches = completedMatches,
            TotalMatches = matches.Count,
            IsComplete = isComplete,
            IsChampion = isComplete && placement == 1,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    public FullAuditPackage? BuildFullAuditPackage(int participantId)
    {
        var participant = _db.Participants.AsNoTracking().SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            return null;
        }

        var roundSnapshots = _db.Rounds
            .AsNoTracking()
            .OrderBy(round => round.Id)
            .AsEnumerable()
            .Select(round => BuildSnapshot(participantId, round.Id))
            .Where(snapshot => snapshot != null)
            .Select(snapshot => snapshot!)
            .ToList();
        var specialSnapshot = BuildSpecialSnapshot(participantId);

        if (!roundSnapshots.Any() && specialSnapshot is null)
        {
            return null;
        }

        return new FullAuditPackage
        {
            Participant = participant,
            RoundSnapshots = roundSnapshots,
            SpecialSnapshot = specialSnapshot
        };
    }

    public byte[] RenderFullAuditPdf(FullAuditPackage package)
    {
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);

        foreach (var snapshot in package.RoundSnapshots)
        {
            AddPdfPage(document, RenderPng(snapshot));
        }

        if (package.SpecialSnapshot is not null)
        {
            AddPdfPage(document, RenderSpecialPng(package.SpecialSnapshot));
        }

        document.Close();
        return stream.ToArray();
    }

    public ScoreAuditPackage? BuildScoreAuditPackage(int participantId)
    {
        var participant = _db.Participants.AsNoTracking().SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            return null;
        }

        var rounds = _db.Rounds
            .AsNoTracking()
            .OrderBy(round => round.Id)
            .ToList();
        var matches = _db.Matches
            .AsNoTracking()
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.OfficialNumber)
            .ToList();
        var predictions = _db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.ParticipantId == participantId)
            .ToDictionary(prediction => prediction.MatchId);

        var auditRounds = rounds
            .Select(round =>
            {
                var lines = matches
                    .Where(match => match.RoundId == round.Id)
                    .Select(match => BuildScoreAuditLine(match, predictions.GetValueOrDefault(match.Id)))
                    .ToList();

                return new ScoreAuditRound
                {
                    Round = round,
                    Lines = lines
                };
            })
            .Where(round => round.Lines.Count > 0)
            .ToList();

        if (auditRounds.Count == 0)
        {
            return null;
        }

        var generatedAt = DateTimeOffset.UtcNow;
        return new ScoreAuditPackage
        {
            Participant = participant,
            Rounds = auditRounds,
            GeneratedAt = generatedAt,
            ProofHash = BuildScoreAuditHash(participant, auditRounds, generatedAt)
        };
    }

    public byte[] RenderScoreAuditPdf(ScoreAuditPackage package)
    {
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);

        foreach (var round in package.Rounds)
        {
            AddPdfPage(document, RenderScoreRoundPng(package, round));
        }

        document.Close();
        return stream.ToArray();
    }

    public SpecialAuditSnapshot? BuildSpecialSnapshot(int participantId)
    {
        var participant = _db.Participants.SingleOrDefault(item => item.Id == participantId);
        var prediction = _db.SpecialPredictions.SingleOrDefault(item => item.ParticipantId == participantId);
        if (participant is null || prediction?.IsFinal != true)
        {
            return null;
        }

        var generatedAt = DateTimeOffset.UtcNow;
        return new SpecialAuditSnapshot
        {
            Participant = participant,
            Prediction = prediction,
            GeneratedAt = generatedAt,
            ProofHash = BuildSpecialHash(participant, prediction)
        };
    }

    public byte[] RenderFinalCardPng(FinalCardPackage package)
    {
        var width = 1200;
        var height = 675;
        var title = package.IsChampion ? "Campeao do Bolao Premier AEW" : package.StatusLabel;
        var headerColor = package.IsChampion ? SKColor.Parse("#8a5f00") : HeaderBackground;
        var accentColor = package.IsChampion ? SKColor.Parse("#ffe08a") : HeaderAccent;

        return RenderPng(width, height, canvas =>
        {
            DrawCard(canvas, width, height, 178);

            using var headerPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = headerColor
            };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(32, 32, width - 32, 210), 8, 8), headerPaint);

            using var accentPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = accentColor
            };
            canvas.DrawCircle(1040, 120, 58, accentPaint);

            using var brandPaint = CreateTextPaint(22, accentColor, true);
            using var titlePaint = CreateTextPaint(42, SKColors.White, true);
            using var subtitlePaint = CreateTextPaint(20, SKColor.Parse("#f6ecd0"));
            using var badgePaint = CreateTextPaint(38, headerColor, true);
            using var namePaint = CreateTextPaint(34, PrimaryText, true);
            using var labelPaint = CreateTextPaint(17, SecondaryText, true);
            using var valuePaint = CreateTextPaint(30, HeaderBackground, true);
            using var bodyPaint = CreateTextPaint(21, PrimaryText, true);
            using var metaPaint = CreateTextPaint(16, SecondaryText);

            canvas.DrawText("Bolao Premier AEW", 64, 82, brandPaint);
            canvas.DrawText(TruncateText(title, titlePaint, 820), 64, 132, titlePaint);
            canvas.DrawText($"{package.CompletedMatches}/{package.TotalMatches} jogos computados | {FormatBrasilia(package.GeneratedAt)}", 64, 170, subtitlePaint);
            canvas.DrawText(package.IsChampion ? "1" : $"#{package.Placement}", 1005, 136, badgePaint);

            canvas.DrawText(TruncateText(package.Participant.Name, namePaint, 940), 64, 278, namePaint);
            canvas.DrawText($"{package.Placement} de {package.ParticipantCount} participantes", 64, 316, bodyPaint);

            DrawFinalMetric(canvas, 64, 362, "Pontos", package.Ranking.Points.ToString(), labelPaint, valuePaint);
            DrawFinalMetric(canvas, 344, 362, "Exatos", package.Ranking.ExactScores.ToString(), labelPaint, valuePaint);
            DrawFinalMetric(canvas, 624, 362, "Classificados", package.Ranking.KnockoutQualifiedHits.ToString(), labelPaint, valuePaint);
            DrawFinalMetric(canvas, 904, 362, "Brasil", package.Ranking.BrazilHits.ToString(), labelPaint, valuePaint);

            DrawRoundedBlock(canvas, new SKRect(64, 508, 1136, 598), SubtleFill, SubtleBorder);
            canvas.DrawText(TruncateText($"Melhor rodada: {package.BestRoundLabel}", bodyPaint, 990), 88, 546, bodyPaint);
            canvas.DrawText(TruncateText($"Curiosidade: {package.CuriosityLabel}", metaPaint, 990), 88, 576, metaPaint);

            if (package.IsChampion)
            {
                canvas.DrawText("Campanha campea confirmada apos a conclusao oficial do bolao.", 64, 634, metaPaint);
            }
            else if (!package.IsComplete)
            {
                canvas.DrawText("Previa administrativa: valores podem mudar ate o registro de todos os resultados.", 64, 634, metaPaint);
            }
            else
            {
                canvas.DrawText("Card final confirmado apos a conclusao oficial do bolao.", 64, 634, metaPaint);
            }
        });
    }

    public byte[] RenderSpecialPng(SpecialAuditSnapshot snapshot)
    {
        var width = 1200;
        var height = 620;
        var submittedAt = snapshot.Prediction.SubmittedAt is null
            ? "-"
            : FormatBrasilia(snapshot.Prediction.SubmittedAt.Value);

        return RenderPng(width, height, canvas =>
        {
            DrawCard(canvas, width, height, 142);

            using var brandPaint = CreateTextPaint(22, HeaderAccent, true);
            using var titlePaint = CreateTextPaint(32, SKColors.White, true);
            using var bodyPaint = CreateTextPaint(20, PrimaryText, true);
            using var metaPaint = CreateTextPaint(18, SecondaryText);
            using var labelPaint = CreateTextPaint(16, SecondaryText, true);
            using var valuePaint = CreateTextPaint(20, PrimaryText, true);
            using var hashLabelPaint = CreateTextPaint(15, SecondaryText, true);
            using var hashPaint = CreateTextPaint(18, HeaderBackground, true);

            canvas.DrawText("Bolao Premier AEW", 64, 82, brandPaint);
            canvas.DrawText("Comprovante de Auditoria de Palpites Especiais", 64, 128, titlePaint);
            canvas.DrawText(TruncateText($"Participante: {snapshot.Participant.Name} | Email: {snapshot.Participant.Email}", bodyPaint, 1040), 64, 210, bodyPaint);
            canvas.DrawText(TruncateText($"Finalizado: {submittedAt} | Gerado: {FormatBrasilia(snapshot.GeneratedAt)} | Horario de Brasilia", metaPaint, 1040), 64, 242, metaPaint);

            DrawSpecialRow(canvas, 310, "Campeao", TeamCatalog.ResolveName(snapshot.Prediction.Champion), labelPaint, valuePaint);
            DrawSpecialRow(canvas, 364, "Vice-campeao", TeamCatalog.ResolveName(snapshot.Prediction.RunnerUp), labelPaint, valuePaint);
            DrawSpecialRow(canvas, 418, "Artilheiro", snapshot.Prediction.TopScorer, labelPaint, valuePaint);
            DrawSpecialRow(canvas, 472, "Bola de Ouro", snapshot.Prediction.GoldenBall, labelPaint, valuePaint);

            DrawRoundedBlock(canvas, new SKRect(64, 508, 1136, 566), SubtleFill, SubtleBorder);
            canvas.DrawText("Hash SHA-256 do comprovante", 86, 532, hashLabelPaint);
            canvas.DrawText(snapshot.ProofHash, 86, 558, hashPaint);
        });
    }

    private ScoreAuditLine BuildScoreAuditLine(Match match, Prediction? prediction)
    {
        var score = prediction is null ? MatchScore.Empty : _scoringService.Score(match, prediction);
        var hasResult = match.Result is not null;
        var status = prediction is null
            ? "Sem palpite"
            : hasResult
                ? "Pontuado"
                : "Aguardando resultado real";

        return new ScoreAuditLine
        {
            OfficialNumber = match.OfficialNumber,
            MatchLabel = $"{match.HomeTeam.Name} x {match.AwayTeam.Name}",
            Status = status,
            PredictionHomeGoals = prediction?.HomeGoals,
            PredictionAwayGoals = prediction?.AwayGoals,
            PredictionQualifiedTeam = prediction is null ? null : ResolveQualifiedTeam(match, prediction.QualifiedTeamCode),
            ResultHomeGoals = match.Result?.HomeGoals,
            ResultAwayGoals = match.Result?.AwayGoals,
            ResultQualifiedTeam = match.Result is null ? null : ResolveQualifiedTeam(match, match.Result.QualifiedTeamCode),
            ExactScorePoints = score.ExactScorePoints,
            ResultPoints = score.ResultPoints,
            QualifiedPoints = score.QualifiedPoints,
            BrazilMultiplier = match.IncludesBrazil ? 2 : 1,
            TotalPoints = score.Points
        };
    }

    private byte[] RenderScoreRoundPng(ScoreAuditPackage package, ScoreAuditRound round)
    {
        var width = 1500;
        var rowHeight = 62;
        var headerHeight = 270;
        var footerHeight = 130;
        var height = headerHeight + round.Lines.Count * rowHeight + footerHeight;

        return RenderPng(width, height, canvas =>
        {
            DrawCard(canvas, width, height, 150);

            using var brandPaint = CreateTextPaint(22, HeaderAccent, true);
            using var titlePaint = CreateTextPaint(34, SKColors.White, true);
            using var bodyPaint = CreateTextPaint(20, PrimaryText, true);
            using var metaPaint = CreateTextPaint(18, SecondaryText);
            using var tableHeaderPaint = CreateTextPaint(16, SecondaryText, true);
            using var numberPaint = CreateTextPaint(16, PrimaryText, true);
            using var rowPaint = CreateTextPaint(16, PrimaryText);
            using var rowBoldPaint = CreateTextPaint(16, PrimaryText, true);
            using var statusPaint = CreateTextPaint(14, SecondaryText);
            using var hashLabelPaint = CreateTextPaint(16, SecondaryText, true);
            using var hashPaint = CreateTextPaint(18, HeaderBackground, true);
            using var dividerPaint = new SKPaint
            {
                IsAntialias = true,
                Color = RowBorder,
                StrokeWidth = 1
            };

            canvas.DrawText("Bolao Premier AEW", 64, 82, brandPaint);
            canvas.DrawText("Auditoria de Pontuacao", 64, 128, titlePaint);
            canvas.DrawText(TruncateText($"Participante: {package.Participant.Name} | Total geral: {package.TotalPoints} pts", bodyPaint, 1360), 64, 204, bodyPaint);
            canvas.DrawText(TruncateText($"Rodada: {round.Round.Name} | Total da rodada: {round.TotalPoints} pts | Gerado: {FormatBrasilia(package.GeneratedAt)}", metaPaint, 1360), 64, 236, metaPaint);

            canvas.DrawText("Jogo", 64, 282, tableHeaderPaint);
            canvas.DrawText("Partida", 132, 282, tableHeaderPaint);
            canvas.DrawText("Palpite", 468, 282, tableHeaderPaint);
            canvas.DrawText("Resultado real", 704, 282, tableHeaderPaint);
            canvas.DrawText("Criterios aplicados", 940, 282, tableHeaderPaint);
            canvas.DrawText("Total", 1352, 282, tableHeaderPaint);

            var y = 324f;
            foreach (var line in round.Lines)
            {
                canvas.DrawLine(64, y - 28, 1436, y - 28, dividerPaint);
                canvas.DrawText(line.OfficialNumber.ToString(), 64, y, numberPaint);
                canvas.DrawText(TruncateText(line.MatchLabel, rowPaint, 305), 132, y, rowPaint);
                canvas.DrawText(TruncateText(FormatPredictionScore(line), rowPaint, 205), 468, y, rowPaint);
                canvas.DrawText(TruncateText(FormatResultScore(line), rowPaint, 205), 704, y, rowPaint);
                canvas.DrawText(TruncateText(FormatScoreCriteria(line), rowPaint, 380), 940, y, rowPaint);
                canvas.DrawText($"{line.TotalPoints} pts", 1352, y, rowBoldPaint);
                canvas.DrawText(line.Status, 132, y + 22, statusPaint);
                y += rowHeight;
            }

            var footerY = y + 18;
            DrawRoundedBlock(canvas, new SKRect(64, footerY, 1436, footerY + 72), SubtleFill, SubtleBorder);
            canvas.DrawText("Regras: grupos/32 base 5/3/3; oitavas em diante escalam por fase; jogos do Brasil dobram", 86, footerY + 28, hashLabelPaint);
            canvas.DrawText($"Hash SHA-256: {package.ProofHash}", 86, footerY + 58, hashPaint);
        });
    }

    public byte[] RenderPng(AuditSnapshot snapshot)
    {
        var width = 1200;
        var rowHeight = 42;
        var headerHeight = 250;
        var footerHeight = 120;
        var height = headerHeight + snapshot.Predictions.Count * rowHeight + footerHeight;

        return RenderPng(width, height, canvas =>
        {
            DrawCard(canvas, width, height, 142);

            using var brandPaint = CreateTextPaint(22, HeaderAccent, true);
            using var titlePaint = CreateTextPaint(34, SKColors.White, true);
            using var bodyPaint = CreateTextPaint(20, PrimaryText, true);
            using var metaPaint = CreateTextPaint(18, SecondaryText);
            using var tableHeaderPaint = CreateTextPaint(16, SecondaryText, true);
            using var numberPaint = CreateTextPaint(16, PrimaryText, true);
            using var predictionPaint = CreateTextPaint(16, PrimaryText, true);
            using var datePaint = CreateTextPaint(15, SecondaryText);
            using var hashLabelPaint = CreateTextPaint(16, SecondaryText, true);
            using var hashPaint = CreateTextPaint(18, HeaderBackground, true);
            using var dividerPaint = new SKPaint
            {
                IsAntialias = true,
                Color = RowBorder,
                StrokeWidth = 1
            };

            canvas.DrawText("Bolao Premier AEW", 64, 82, brandPaint);
            canvas.DrawText("Comprovante de Auditoria de Palpites", 64, 128, titlePaint);
            canvas.DrawText(TruncateText($"Participante: {snapshot.Participant.Name} | Email: {snapshot.Participant.Email}", bodyPaint, 1040), 64, 204, bodyPaint);
            canvas.DrawText(TruncateText($"Rodada: {snapshot.Round.Name} | Gerado: {FormatBrasilia(snapshot.GeneratedAt)} | Horario de Brasilia", metaPaint, 1040), 64, 234, metaPaint);
            canvas.DrawText("Jogo", 64, 270, tableHeaderPaint);
            canvas.DrawText("Palpite", 150, 270, tableHeaderPaint);
            canvas.DrawText("Salvo", 830, 270, tableHeaderPaint);
            canvas.DrawText("Finalizado", 1010, 270, tableHeaderPaint);

            var y = 306f;
            foreach (var line in snapshot.Predictions)
            {
                canvas.DrawLine(64, y - 24, 1136, y - 24, dividerPaint);
                canvas.DrawText(line.OfficialNumber.ToString(), 64, y, numberPaint);
                var qualified = string.IsNullOrWhiteSpace(line.QualifiedTeam) ? string.Empty : $" | classificado: {line.QualifiedTeam}";
                var prediction = line.HasPrediction
                    ? $"{line.HomeTeam} {line.HomeGoals} x {line.AwayGoals} {line.AwayTeam}{qualified}"
                    : $"{line.HomeTeam} x {line.AwayTeam} | sem palpite";
                canvas.DrawText(TruncateText(prediction, predictionPaint, 650), 150, y, predictionPaint);
                canvas.DrawText(line.SavedAt is null ? "-" : ToBrasilia(line.SavedAt.Value).ToString("MM-dd HH:mm"), 830, y, datePaint);
                canvas.DrawText(line.SubmittedAt is null ? "-" : ToBrasilia(line.SubmittedAt.Value).ToString("MM-dd HH:mm"), 1010, y, datePaint);
                y += rowHeight;
            }

            var footerY = y + 20;
            DrawRoundedBlock(canvas, new SKRect(64, footerY, 1136, footerY + 68), SubtleFill, SubtleBorder);
            canvas.DrawText("Hash SHA-256 do comprovante", 86, footerY + 28, hashLabelPaint);
            canvas.DrawText(snapshot.ProofHash, 86, footerY + 54, hashPaint);
        });
    }

    private static string BuildHash(Participant participant, PredictionRound round, IReadOnlyList<AuditPredictionLine> lines)
    {
        var canonical = new StringBuilder();
        canonical.AppendLine($"participant:{participant.Id}|{participant.Email}");
        canonical.AppendLine($"round:{round.Id}|{round.Name}");
        foreach (var line in lines.OrderBy(item => item.OfficialNumber))
        {
            canonical.AppendLine($"{line.OfficialNumber}|{line.HomeTeam}|{line.AwayTeam}|{line.HomeGoals?.ToString() ?? "-"}|{line.AwayGoals?.ToString() ?? "-"}|{line.QualifiedTeam ?? "-"}|{line.SavedAt?.ToString("O") ?? "-"}|{line.SubmittedAt?.ToString("O") ?? "-"}");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexString(bytes);
    }

    private IReadOnlyList<RankingEntry> BuildFinalCardRanking(
        IReadOnlyList<Participant> participants,
        IReadOnlyDictionary<int, Match> matchesById,
        IReadOnlyList<Prediction> predictions)
    {
        return participants
            .Select(participant =>
            {
                var scored = predictions
                    .Where(prediction => prediction.ParticipantId == participant.Id && matchesById.ContainsKey(prediction.MatchId))
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

    private static string BuildSpecialHash(Participant participant, SpecialPrediction prediction)
    {
        var canonical = new StringBuilder();
        canonical.AppendLine($"participant:{participant.Id}|{participant.Email}");
        canonical.AppendLine($"special:{prediction.Champion}|{prediction.RunnerUp}|{prediction.TopScorer}|{prediction.GoldenBall}");
        canonical.AppendLine($"saved:{prediction.SavedAt:O}|submitted:{prediction.SubmittedAt:O}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static string BuildScoreAuditHash(Participant participant, IReadOnlyList<ScoreAuditRound> rounds, DateTimeOffset generatedAt)
    {
        var canonical = new StringBuilder();
        canonical.AppendLine($"participant:{participant.Id}|{participant.Email}");
        canonical.AppendLine($"generated:{generatedAt:O}");
        foreach (var round in rounds.OrderBy(item => item.Round.Id))
        {
            canonical.AppendLine($"round:{round.Round.Id}|{round.Round.Name}|total:{round.TotalPoints}");
            foreach (var line in round.Lines.OrderBy(item => item.OfficialNumber))
            {
                canonical.AppendLine($"{line.OfficialNumber}|{line.MatchLabel}|{line.PredictionHomeGoals?.ToString() ?? "-"}|{line.PredictionAwayGoals?.ToString() ?? "-"}|{line.PredictionQualifiedTeam ?? "-"}|{line.ResultHomeGoals?.ToString() ?? "-"}|{line.ResultAwayGoals?.ToString() ?? "-"}|{line.ResultQualifiedTeam ?? "-"}|{line.ExactScorePoints}|{line.ResultPoints}|{line.QualifiedPoints}|{line.BrazilMultiplier}|{line.TotalPoints}|{line.Status}");
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexString(bytes);
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

    private static string FormatPredictionScore(ScoreAuditLine line)
    {
        if (line.PredictionHomeGoals is null || line.PredictionAwayGoals is null)
        {
            return "-";
        }

        var qualified = string.IsNullOrWhiteSpace(line.PredictionQualifiedTeam)
            ? string.Empty
            : $" | classif.: {line.PredictionQualifiedTeam}";
        return $"{line.PredictionHomeGoals} x {line.PredictionAwayGoals}{qualified}";
    }

    private static string FormatResultScore(ScoreAuditLine line)
    {
        if (line.ResultHomeGoals is null || line.ResultAwayGoals is null)
        {
            return "-";
        }

        var qualified = string.IsNullOrWhiteSpace(line.ResultQualifiedTeam)
            ? string.Empty
            : $" | classif.: {line.ResultQualifiedTeam}";
        return $"{line.ResultHomeGoals} x {line.ResultAwayGoals}{qualified}";
    }

    private static string FormatScoreCriteria(ScoreAuditLine line)
    {
        if (line.TotalPoints == 0)
        {
            return "-";
        }

        var parts = new List<string>();
        if (line.ExactScorePoints > 0)
        {
            parts.Add($"exato +{line.ExactScorePoints}");
        }

        if (line.ResultPoints > 0)
        {
            parts.Add($"resultado +{line.ResultPoints}");
        }

        if (line.QualifiedPoints > 0)
        {
            parts.Add($"classificado +{line.QualifiedPoints}");
        }

        if (line.BrazilMultiplier > 1)
        {
            parts.Add($"Brasil x{line.BrazilMultiplier}");
        }

        return string.Join(" | ", parts);
    }

    private static byte[] RenderPng(int width, int height, Action<SKCanvas> draw)
    {
        var imageInfo = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;
        canvas.Clear(PageBackground);
        draw(canvas);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void AddPdfPage(SKDocument document, byte[] imageBytes)
    {
        using var image = SKImage.FromEncodedData(imageBytes);
        if (image is null)
        {
            return;
        }

        using var canvas = document.BeginPage(image.Width, image.Height);
        canvas.DrawImage(image, 0, 0);
        document.EndPage();
    }

    private static void DrawCard(SKCanvas canvas, int width, int height, float headerHeight)
    {
        DrawRoundedBlock(canvas, new SKRect(32, 32, width - 32, height - 32), CardBackground, CardBorder);

        using var headerPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = HeaderBackground
        };

        canvas.DrawRoundRect(new SKRoundRect(new SKRect(32, 32, width - 32, 32 + headerHeight), 8, 8), headerPaint);
    }

    private static void DrawRoundedBlock(SKCanvas canvas, SKRect rect, SKColor fill, SKColor border)
    {
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = fill
        };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = border
        };

        var roundedRect = new SKRoundRect(rect, 8, 8);
        canvas.DrawRoundRect(roundedRect, fillPaint);
        canvas.DrawRoundRect(roundedRect, borderPaint);
    }

    private static void DrawSpecialRow(SKCanvas canvas, float baselineY, string label, string value, SKPaint labelPaint, SKPaint valuePaint)
    {
        DrawRoundedBlock(canvas, new SKRect(64, baselineY - 32, 1136, baselineY + 12), RowFill, RowBorder);
        canvas.DrawText(label, 88, baselineY - 4, labelPaint);
        canvas.DrawText(TruncateText(value, valuePaint, 810), 300, baselineY - 4, valuePaint);
    }

    private static void DrawFinalMetric(SKCanvas canvas, float x, float y, string label, string value, SKPaint labelPaint, SKPaint valuePaint)
    {
        DrawRoundedBlock(canvas, new SKRect(x, y, x + 232, y + 104), RowFill, RowBorder);
        canvas.DrawText(label, x + 22, y + 34, labelPaint);
        canvas.DrawText(value, x + 22, y + 78, valuePaint);
    }

    private SKPaint CreateTextPaint(float size, SKColor color, bool bold = false)
    {
        return new SKPaint
        {
            IsAntialias = true,
            LcdRenderText = true,
            SubpixelText = true,
            FakeBoldText = bold,
            Typeface = _textTypeface,
            TextSize = size,
            Color = color
        };
    }

    private SKTypeface ResolveTypeface()
    {
        var bundledFontPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "NotoSans-Regular.ttf");
        if (File.Exists(bundledFontPath))
        {
            var bundledTypeface = SKTypeface.FromFile(bundledFontPath);
            if (bundledTypeface is not null)
            {
                return bundledTypeface;
            }
        }

        foreach (var family in new[] { "Noto Sans", "DejaVu Sans", "Liberation Sans", "Segoe UI", "Arial" })
        {
            var typeface = SKTypeface.FromFamilyName(family);
            if (typeface is not null)
            {
                return typeface;
            }
        }

        return SKTypeface.Default;
    }

    private static string TruncateText(string value, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(value) <= maxWidth)
        {
            return value;
        }

        const string ellipsis = "...";
        for (var length = value.Length - 1; length > 0; length--)
        {
            var candidate = value[..length] + ellipsis;
            if (paint.MeasureText(candidate) <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    private static DateTimeOffset ToBrasilia(DateTimeOffset value)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTime(value, timeZone);
    }

    private static string FormatBrasilia(DateTimeOffset value)
    {
        return ToBrasilia(value).ToString("yyyy-MM-dd HH:mm:ss");
    }
}
