using System.Security.Cryptography;
using System.Text;
using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using SkiaSharp;

namespace BolaoCopa2026.Services;

public sealed class AuditImageService
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

    public AuditImageService(BolaoDbContext db)
    {
        _db = db;
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

            DrawSpecialRow(canvas, 310, "Campeao", snapshot.Prediction.Champion, labelPaint, valuePaint);
            DrawSpecialRow(canvas, 364, "Vice-campeao", snapshot.Prediction.RunnerUp, labelPaint, valuePaint);
            DrawSpecialRow(canvas, 418, "Artilheiro", snapshot.Prediction.TopScorer, labelPaint, valuePaint);
            DrawSpecialRow(canvas, 472, "Bola de Ouro", snapshot.Prediction.GoldenBall, labelPaint, valuePaint);

            DrawRoundedBlock(canvas, new SKRect(64, 508, 1136, 566), SubtleFill, SubtleBorder);
            canvas.DrawText("Hash SHA-256 do comprovante", 86, 532, hashLabelPaint);
            canvas.DrawText(snapshot.ProofHash, 86, 558, hashPaint);
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

    private static string BuildSpecialHash(Participant participant, SpecialPrediction prediction)
    {
        var canonical = new StringBuilder();
        canonical.AppendLine($"participant:{participant.Id}|{participant.Email}");
        canonical.AppendLine($"special:{prediction.Champion}|{prediction.RunnerUp}|{prediction.TopScorer}|{prediction.GoldenBall}");
        canonical.AppendLine($"saved:{prediction.SavedAt:O}|submitted:{prediction.SubmittedAt:O}");

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

    private static SKPaint CreateTextPaint(float size, SKColor color, bool bold = false)
    {
        return new SKPaint
        {
            IsAntialias = true,
            SubpixelText = true,
            FakeBoldText = bold,
            Typeface = SKTypeface.Default,
            TextSize = size,
            Color = color
        };
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
