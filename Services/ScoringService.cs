using System.Globalization;
using System.Text;
using BolaoCopa2026;
using BolaoCopa2026.Models;

namespace BolaoCopa2026.Services;

public sealed class ScoringService
{
    public const string ActualTopScorer = "Mbappe";

    public MatchScore Score(Match match, Prediction prediction)
    {
        if (match.Result is null)
        {
            return MatchScore.Empty;
        }

        var result = match.Result;
        var exactScore = prediction.HomeGoals == result.HomeGoals && prediction.AwayGoals == result.AwayGoals;
        var resultHit = GetOutcome(prediction.HomeGoals, prediction.AwayGoals) == GetOutcome(result.HomeGoals, result.AwayGoals);
        var qualifiedHit = match.IsKnockout
            && !string.IsNullOrWhiteSpace(prediction.QualifiedTeamCode)
            && prediction.QualifiedTeamCode == result.QualifiedTeamCode;

        var exactScorePoints = 0;
        var resultPoints = 0;
        var qualifiedPoints = 0;
        if (exactScore)
        {
            exactScorePoints = GetExactScorePoints(match.Phase);
        }
        else if (resultHit)
        {
            resultPoints = GetResultPoints(match.Phase);
        }

        if (qualifiedHit)
        {
            qualifiedPoints = GetQualifiedPoints(match.Phase);
        }

        var basePoints = exactScorePoints + resultPoints + qualifiedPoints;
        var points = basePoints;
        if (match.IncludesBrazil)
        {
            points *= 2;
        }

        return new MatchScore(
            points,
            exactScore,
            resultHit,
            qualifiedHit,
            match.IncludesBrazil && basePoints > 0,
            exactScorePoints,
            resultPoints,
            qualifiedPoints);
    }

    public SpecialScore ScoreSpecial(SpecialPrediction? prediction, string? championCode, string? runnerUpCode)
    {
        if (prediction?.IsFinal != true)
        {
            return SpecialScore.Empty;
        }

        var predictionChampionCode = ResolveTeamCode(prediction.Champion);
        var predictionRunnerUpCode = ResolveTeamCode(prediction.RunnerUp);
        var championHit = !string.IsNullOrWhiteSpace(championCode)
            && string.Equals(predictionChampionCode, championCode, StringComparison.OrdinalIgnoreCase);
        var runnerUpHit = !string.IsNullOrWhiteSpace(runnerUpCode)
            && string.Equals(predictionRunnerUpCode, runnerUpCode, StringComparison.OrdinalIgnoreCase);
        var topScorerHit = IsTopScorerHit(prediction.TopScorer);
        var championPoints = championHit ? 20 : 0;
        var runnerUpPoints = runnerUpHit ? 10 : 0;
        var topScorerPoints = topScorerHit ? 10 : 0;

        return new SpecialScore(
            championPoints + runnerUpPoints + topScorerPoints,
            championHit,
            runnerUpHit,
            topScorerHit,
            championPoints,
            runnerUpPoints,
            topScorerPoints);
    }

    private static string? ResolveTeamCode(string? value)
    {
        var directCode = TeamCatalog.ResolveCode(value);
        if (!string.IsNullOrWhiteSpace(directCode))
        {
            return directCode;
        }

        var normalized = NormalizeText(value);
        return normalized switch
        {
            "mexico" => "MEX",
            "south africa" or "africa do sul" => "RSA",
            "south korea" or "coreia do sul" => "KOR",
            "czechia" or "czech republic" or "tchequia" => "CZE",
            "canada" => "CAN",
            "bosnia and herzegovina" or "bosnia e herzegovina" or "bosnia" => "BIH",
            "united states" or "usa" or "estados unidos" => "USA",
            "paraguay" or "paraguai" => "PAR",
            "haiti" => "HAI",
            "scotland" or "escocia" => "SCO",
            "australia" => "AUS",
            "turkey" or "turkiye" or "turquia" => "TUR",
            "brazil" or "brasil" => "BRA",
            "morocco" or "marrocos" => "MAR",
            "qatar" or "catar" => "QAT",
            "switzerland" or "suica" => "SUI",
            "ivory coast" or "cote divoire" or "costa do marfim" => "CIV",
            "ecuador" or "equador" => "ECU",
            "germany" or "alemanha" => "GER",
            "curacao" => "CUW",
            "netherlands" or "holland" or "paises baixos" or "holanda" => "NED",
            "japan" or "japao" => "JPN",
            "sweden" or "suecia" => "SWE",
            "tunisia" => "TUN",
            "saudi arabia" or "arabia saudita" => "KSA",
            "uruguay" or "uruguai" => "URU",
            "spain" or "espanha" => "ESP",
            "cape verde" or "cabo verde" => "CPV",
            "iran" or "ira" => "IRN",
            "new zealand" or "nova zelandia" => "NZL",
            "belgium" or "belgica" => "BEL",
            "egypt" or "egito" => "EGY",
            "france" or "franca" => "FRA",
            "senegal" => "SEN",
            "iraq" or "iraque" => "IRQ",
            "norway" or "noruega" => "NOR",
            "argentina" => "ARG",
            "algeria" or "argelia" => "ALG",
            "austria" => "AUT",
            "jordan" or "jordania" => "JOR",
            "ghana" or "gana" => "GHA",
            "panama" => "PAN",
            "england" or "inglaterra" => "ENG",
            "croatia" or "croacia" => "CRO",
            "portugal" => "POR",
            "dr congo" or "congo dr" or "rd congo" or "congo" => "COD",
            "uzbekistan" or "uzbequistao" => "UZB",
            "colombia" => "COL",
            _ => null
        };
    }

    private static bool IsTopScorerHit(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized == "mbappe"
            || normalized == "kylian mbappe"
            || normalized.Contains("mbappe", StringComparison.Ordinal);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length + 2);
        builder.Append(' ');
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        builder.Append(' ');
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static int GetExactScorePoints(CompetitionPhase phase)
    {
        return phase switch
        {
            CompetitionPhase.RoundOf16 => 7,
            CompetitionPhase.QuarterFinal => 9,
            CompetitionPhase.SemiFinal => 11,
            CompetitionPhase.ThirdPlace => 13,
            CompetitionPhase.Final => 15,
            _ => 5
        };
    }

    private static int GetResultPoints(CompetitionPhase phase)
    {
        return phase switch
        {
            CompetitionPhase.RoundOf16 => 4,
            CompetitionPhase.QuarterFinal => 5,
            CompetitionPhase.SemiFinal => 6,
            CompetitionPhase.ThirdPlace => 8,
            CompetitionPhase.Final => 9,
            _ => 3
        };
    }

    private static int GetQualifiedPoints(CompetitionPhase phase)
    {
        return phase switch
        {
            CompetitionPhase.RoundOf16 => 4,
            CompetitionPhase.QuarterFinal => 5,
            CompetitionPhase.SemiFinal => 6,
            CompetitionPhase.ThirdPlace => 3,
            CompetitionPhase.Final => 5,
            _ => 3
        };
    }

    private static MatchOutcome GetOutcome(int homeGoals, int awayGoals)
    {
        if (homeGoals > awayGoals)
        {
            return MatchOutcome.HomeWin;
        }

        return homeGoals == awayGoals ? MatchOutcome.Draw : MatchOutcome.AwayWin;
    }
}

public sealed record MatchScore(
    int Points,
    bool ExactScore,
    bool ResultHit,
    bool QualifiedHit,
    bool BrazilHit,
    int ExactScorePoints,
    int ResultPoints,
    int QualifiedPoints)
{
    public static MatchScore Empty { get; } = new(0, false, false, false, false, 0, 0, 0);
}

public sealed record SpecialScore(
    int Points,
    bool ChampionHit,
    bool RunnerUpHit,
    bool TopScorerHit,
    int ChampionPoints,
    int RunnerUpPoints,
    int TopScorerPoints)
{
    public static SpecialScore Empty { get; } = new(0, false, false, false, 0, 0, 0);
}
