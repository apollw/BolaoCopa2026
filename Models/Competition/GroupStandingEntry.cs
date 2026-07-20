namespace BolaoCopa2026.Models;

public sealed class GroupStandingEntry
{
    public required Team Team { get; init; }
    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points { get; init; }
    public int FairPlayPenalty { get; init; }
    public int FifaRanking { get; init; }
}
