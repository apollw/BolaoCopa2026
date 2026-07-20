namespace BolaoCopa2026.Models;

public sealed class Match
{
    public int Id { get; init; }
    public int OfficialNumber { get; init; }
    public int RoundId { get; init; }
    public required Team HomeTeam { get; set; }
    public required Team AwayTeam { get; set; }
    public CompetitionPhase Phase { get; init; }
    public DateTimeOffset Kickoff { get; init; }
    public string? GroupName { get; init; }
    public string? Venue { get; init; }
    public MatchResult? Result { get; set; }
    public bool IsKnockout => Phase != CompetitionPhase.GroupStage;
    public bool IncludesBrazil => HomeTeam.IsBrazil || AwayTeam.IsBrazil;
}
