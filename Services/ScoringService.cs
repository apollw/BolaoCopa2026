using BolaoCopa2026.Models;

namespace BolaoCopa2026.Services;

public sealed class ScoringService
{
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
