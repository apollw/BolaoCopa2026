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

        var points = 0;
        if (exactScore)
        {
            points += 5;
        }
        else if (resultHit)
        {
            points += 3;
        }

        if (qualifiedHit)
        {
            points += 2;
        }

        if (match.IsKnockout)
        {
            points = Math.Min(points, 7);
        }

        if (match.IncludesBrazil)
        {
            points *= 2;
        }

        return new MatchScore(points, exactScore, resultHit, qualifiedHit, match.IncludesBrazil && (exactScore || resultHit || qualifiedHit));
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
    bool BrazilHit)
{
    public static MatchScore Empty { get; } = new(0, false, false, false, false);
}
