using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BolaoCopa2026.Data;
using BolaoCopa2026.Models;

namespace BolaoCopa2026.Services;

public sealed class AuditImageService
{
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
        var predictions = _db.Predictions
            .Where(prediction => prediction.ParticipantId == participantId)
            .ToDictionary(prediction => prediction.MatchId);

        if (matches.Count == 0 || matches.Any(match => !predictions.TryGetValue(match.Id, out var prediction) || !prediction.IsFinal))
        {
            return null;
        }

        var lines = matches
            .Select(match =>
            {
                var prediction = predictions[match.Id];
                return new AuditPredictionLine
                {
                    OfficialNumber = match.OfficialNumber,
                    HomeTeam = match.HomeTeam.Name,
                    AwayTeam = match.AwayTeam.Name,
                    HomeGoals = prediction.HomeGoals,
                    AwayGoals = prediction.AwayGoals,
                    QualifiedTeam = ResolveQualifiedTeam(match, prediction.QualifiedTeamCode),
                    SavedAt = prediction.SavedAt,
                    SubmittedAt = prediction.SubmittedAt!.Value
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

    public byte[] RenderSpecialSvg(SpecialAuditSnapshot snapshot)
    {
        var width = 1200;
        var height = 620;
        var generatedAt = ToBrasilia(snapshot.GeneratedAt);
        var submittedAt = snapshot.Prediction.SubmittedAt is null
            ? "-"
            : FormatBrasilia(snapshot.Prediction.SubmittedAt.Value);
        var sb = new StringBuilder();

        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        sb.AppendLine("<metadata>");
        sb.AppendLine(Escape($"participant={snapshot.Participant.Id};email={snapshot.Participant.Email};special=true;generatedAtBrasilia={generatedAt:O};timezone=America/Sao_Paulo;hash={snapshot.ProofHash}"));
        sb.AppendLine("</metadata>");
        sb.AppendLine("""<rect width="1200" height="620" fill="#f4f7f2"/>""");
        sb.AppendLine("""<rect x="32" y="32" width="1136" height="556" rx="8" fill="#ffffff" stroke="#dfe7da"/>""");
        sb.AppendLine("""<rect x="32" y="32" width="1136" height="142" rx="8" fill="#123d32"/>""");
        sb.AppendLine(Text(64, 82, "Bolao Premier AEW", 22, "#d5a928", 800));
        sb.AppendLine(Text(64, 128, "Comprovante de Auditoria de Palpites Especiais", 32, "#ffffff", 850));
        sb.AppendLine(Text(64, 210, $"Participante: {snapshot.Participant.Name} | Email: {snapshot.Participant.Email}", 20, "#233027", 700));
        sb.AppendLine(Text(64, 242, $"Finalizado: {submittedAt} | Gerado: {FormatBrasilia(snapshot.GeneratedAt)} | Horario de Brasilia", 18, "#607166", 600));

        var y = 310;
        sb.AppendLine(SpecialLine(y, "Campeao", snapshot.Prediction.Champion));
        sb.AppendLine(SpecialLine(y + 54, "Vice-campeao", snapshot.Prediction.RunnerUp));
        sb.AppendLine(SpecialLine(y + 108, "Artilheiro", snapshot.Prediction.TopScorer));
        sb.AppendLine(SpecialLine(y + 162, "Bola de Ouro", snapshot.Prediction.GoldenBall));

        sb.AppendLine("""<rect x="64" y="508" width="1072" height="58" rx="8" fill="#eef3ea" stroke="#d4dfce"/>""");
        sb.AppendLine(Text(86, 532, "Hash SHA-256 do comprovante", 15, "#607166", 800));
        sb.AppendLine(Text(86, 558, snapshot.ProofHash, 18, "#123d32", 800));
        sb.AppendLine("</svg>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] RenderSvg(AuditSnapshot snapshot)
    {
        var width = 1200;
        var rowHeight = 42;
        var headerHeight = 250;
        var footerHeight = 120;
        var height = headerHeight + snapshot.Predictions.Count * rowHeight + footerHeight;
        var sb = new StringBuilder();
        var generatedAt = ToBrasilia(snapshot.GeneratedAt);

        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        sb.AppendLine("<metadata>");
        sb.AppendLine(Escape($"participant={snapshot.Participant.Id};email={snapshot.Participant.Email};round={snapshot.Round.Id};generatedAtBrasilia={generatedAt:O};timezone=America/Sao_Paulo;hash={snapshot.ProofHash}"));
        sb.AppendLine("</metadata>");
        sb.AppendLine("""<rect width="1200" height="100%" fill="#f4f7f2"/>""");
        sb.AppendLine("""<rect x="32" y="32" width="1136" height="100%" rx="8" fill="#ffffff" stroke="#dfe7da"/>""");
        sb.AppendLine("""<rect x="32" y="32" width="1136" height="142" rx="8" fill="#123d32"/>""");
        sb.AppendLine(Text(64, 82, "Bolao Premier AEW", 22, "#d5a928", 800));
        sb.AppendLine(Text(64, 128, "Comprovante de Auditoria de Palpites", 34, "#ffffff", 850));
        sb.AppendLine(Text(64, 204, $"Participante: {snapshot.Participant.Name} | Email: {snapshot.Participant.Email}", 20, "#233027", 700));
        sb.AppendLine(Text(64, 234, $"Rodada: {snapshot.Round.Name} | Gerado: {FormatBrasilia(snapshot.GeneratedAt)} | Horario de Brasilia", 18, "#607166", 600));
        sb.AppendLine(Text(64, 270, "Jogo", 16, "#607166", 800));
        sb.AppendLine(Text(150, 270, "Palpite", 16, "#607166", 800));
        sb.AppendLine(Text(830, 270, "Salvo", 16, "#607166", 800));
        sb.AppendLine(Text(1010, 270, "Finalizado", 16, "#607166", 800));

        var y = 306;
        foreach (var line in snapshot.Predictions)
        {
            sb.AppendLine($"""<line x1="64" y1="{y - 24}" x2="1136" y2="{y - 24}" stroke="#edf2e9"/>""");
            sb.AppendLine(Text(64, y, line.OfficialNumber.ToString(), 16, "#182018", 800));
            var qualified = string.IsNullOrWhiteSpace(line.QualifiedTeam) ? string.Empty : $" | classificado: {line.QualifiedTeam}";
            sb.AppendLine(Text(150, y, $"{line.HomeTeam} {line.HomeGoals} x {line.AwayGoals} {line.AwayTeam}{qualified}", 16, "#182018", 700));
            sb.AppendLine(Text(830, y, ToBrasilia(line.SavedAt).ToString("MM-dd HH:mm"), 15, "#607166", 600));
            sb.AppendLine(Text(1010, y, ToBrasilia(line.SubmittedAt).ToString("MM-dd HH:mm"), 15, "#607166", 600));
            y += rowHeight;
        }

        var footerY = y + 20;
        sb.AppendLine($"""<rect x="64" y="{footerY}" width="1072" height="68" rx="8" fill="#eef3ea" stroke="#d4dfce"/>""");
        sb.AppendLine(Text(86, footerY + 28, "Hash SHA-256 do comprovante", 16, "#607166", 800));
        sb.AppendLine(Text(86, footerY + 54, snapshot.ProofHash, 18, "#123d32", 800));
        sb.AppendLine("</svg>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string BuildHash(Participant participant, PredictionRound round, IReadOnlyList<AuditPredictionLine> lines)
    {
        var canonical = new StringBuilder();
        canonical.AppendLine($"participant:{participant.Id}|{participant.Email}");
        canonical.AppendLine($"round:{round.Id}|{round.Name}");
        foreach (var line in lines.OrderBy(item => item.OfficialNumber))
        {
            canonical.AppendLine($"{line.OfficialNumber}|{line.HomeTeam}|{line.AwayTeam}|{line.HomeGoals}|{line.AwayGoals}|{line.QualifiedTeam}|{line.SavedAt:O}|{line.SubmittedAt:O}");
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

    private static string Text(int x, int y, string value, int size, string color, int weight)
    {
        return $"""<text x="{x}" y="{y}" font-family="Inter, Segoe UI, Arial, sans-serif" font-size="{size}" font-weight="{weight}" fill="{color}">{Escape(value)}</text>""";
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

    private static string SpecialLine(int y, string label, string value)
    {
        return $"""
            <rect x="64" y="{y - 32}" width="1072" height="44" rx="8" fill="#f8faf6" stroke="#edf2e9"/>
            {Text(88, y - 4, label, 16, "#607166", 800)}
            {Text(300, y - 4, value, 20, "#182018", 800)}
            """;
    }

    private static string Escape(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }
}
