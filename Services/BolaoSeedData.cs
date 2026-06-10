using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa2026.Services;

public static class BolaoSeedData
{
    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BolaoDbContext>();

        Directory.CreateDirectory("Data");
        db.Database.EnsureCreated();

        if (db.Rounds.Any())
        {
            return;
        }

        db.Rounds.AddRange(SeedRounds());
        db.Matches.AddRange(SeedMatches());
        db.Participants.AddRange(
            new Participant { Id = 1, Name = "Ana", Email = "ana@example.com", Login = "ana" },
            new Participant { Id = 2, Name = "Bruno", Email = "bruno@example.com", Login = "bruno" },
            new Participant { Id = 3, Name = "Carlos", Email = "carlos@example.com", Login = "carlos" });
        db.SpecialPredictions.AddRange(
            new SpecialPrediction { ParticipantId = 1, Champion = "Brasil", RunnerUp = "Franca", TopScorer = "Vinicius Jr.", GoldenBall = "Bellingham" },
            new SpecialPrediction { ParticipantId = 2, Champion = "Argentina", RunnerUp = "Brasil", TopScorer = "Mbappe", GoldenBall = "Mbappe" },
            new SpecialPrediction { ParticipantId = 3, Champion = "Franca", RunnerUp = "Espanha", TopScorer = "Kane", GoldenBall = "Vinicius Jr." });

        db.SaveChanges();
    }

    private static List<PredictionRound> SeedRounds()
    {
        return
        [
            new PredictionRound(1, "Fase de grupos - Rodada 1", CompetitionPhase.GroupStage, new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 17)),
            new PredictionRound(2, "Fase de grupos - Rodada 2", CompetitionPhase.GroupStage, new DateOnly(2026, 6, 18), new DateOnly(2026, 6, 23)),
            new PredictionRound(3, "Fase de grupos - Rodada 3", CompetitionPhase.GroupStage, new DateOnly(2026, 6, 24), new DateOnly(2026, 6, 27)),
            new PredictionRound(4, "Rodada de 32", CompetitionPhase.RoundOf32, new DateOnly(2026, 6, 28), new DateOnly(2026, 7, 3)),
            new PredictionRound(5, "Oitavas de final", CompetitionPhase.RoundOf16, new DateOnly(2026, 7, 4), new DateOnly(2026, 7, 7)),
            new PredictionRound(6, "Quartas de final", CompetitionPhase.QuarterFinal, new DateOnly(2026, 7, 9), new DateOnly(2026, 7, 11)),
            new PredictionRound(7, "Semifinais", CompetitionPhase.SemiFinal, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 15)),
            new PredictionRound(8, "Terceiro lugar e final", CompetitionPhase.Final, new DateOnly(2026, 7, 18), new DateOnly(2026, 7, 19))
        ];
    }

    private static List<Match> SeedMatches()
    {
        var teams = SeedTeams();
        var matches = new List<Match>();
        var id = 1;

        void AddGroup(int number, string date, string home, string away, string group, string venue, int hour = 16)
        {
            matches.Add(new Match
            {
                Id = id++,
                OfficialNumber = number,
                RoundId = 1,
                HomeTeam = teams[home],
                AwayTeam = teams[away],
                GroupName = group,
                Venue = venue,
                Phase = CompetitionPhase.GroupStage,
                Kickoff = Kickoff(date, hour)
            });
        }

        AddGroup(1, "2026-06-11", "MEX", "RSA", "Grupo A", "Mexico City Stadium");
        AddGroup(2, "2026-06-11", "KOR", "CZE", "Grupo A", "Estadio Guadalajara", 19);
        AddGroup(3, "2026-06-12", "CAN", "BIH", "Grupo B", "Toronto Stadium");
        AddGroup(4, "2026-06-12", "USA", "PAR", "Grupo D", "Los Angeles Stadium", 19);
        AddGroup(5, "2026-06-13", "HAI", "SCO", "Grupo C", "Boston Stadium", 13);
        AddGroup(6, "2026-06-13", "AUS", "TUR", "Grupo D", "BC Place Vancouver", 16);
        AddGroup(7, "2026-06-13", "BRA", "MAR", "Grupo C", "New York New Jersey Stadium", 19);
        AddGroup(8, "2026-06-13", "QAT", "SUI", "Grupo B", "San Francisco Bay Area Stadium", 22);
        AddGroup(9, "2026-06-14", "CIV", "ECU", "Grupo E", "Philadelphia Stadium", 13);
        AddGroup(10, "2026-06-14", "GER", "CUW", "Grupo E", "Houston Stadium", 16);
        AddGroup(11, "2026-06-14", "NED", "JPN", "Grupo F", "Dallas Stadium", 19);
        AddGroup(12, "2026-06-14", "SWE", "TUN", "Grupo F", "Estadio Monterrey", 22);
        AddGroup(13, "2026-06-15", "KSA", "URU", "Grupo H", "Miami Stadium", 13);
        AddGroup(14, "2026-06-15", "ESP", "CPV", "Grupo H", "Atlanta Stadium", 16);
        AddGroup(15, "2026-06-15", "IRN", "NZL", "Grupo G", "Los Angeles Stadium", 19);
        AddGroup(16, "2026-06-15", "BEL", "EGY", "Grupo G", "Seattle Stadium", 22);
        AddGroup(17, "2026-06-16", "FRA", "SEN", "Grupo I", "New York New Jersey Stadium", 13);
        AddGroup(18, "2026-06-16", "IRQ", "NOR", "Grupo I", "Boston Stadium", 16);
        AddGroup(19, "2026-06-16", "ARG", "ALG", "Grupo J", "Kansas City Stadium", 19);
        AddGroup(20, "2026-06-16", "AUT", "JOR", "Grupo J", "San Francisco Bay Area Stadium", 22);
        AddGroup(21, "2026-06-17", "GHA", "PAN", "Grupo L", "Toronto Stadium", 13);
        AddGroup(22, "2026-06-17", "ENG", "CRO", "Grupo L", "Dallas Stadium", 16);
        AddGroup(23, "2026-06-17", "POR", "COD", "Grupo K", "Houston Stadium", 19);
        AddGroup(24, "2026-06-17", "UZB", "COL", "Grupo K", "Mexico City Stadium", 22);

        AddKnockout(matches, id);
        return matches;
    }

    private static Dictionary<string, Team> SeedTeams()
    {
        return new Dictionary<string, Team>
        {
            ["MEX"] = new("MEX", "Mexico"),
            ["RSA"] = new("RSA", "Africa do Sul"),
            ["KOR"] = new("KOR", "Coreia do Sul"),
            ["CZE"] = new("CZE", "Tchequia"),
            ["CAN"] = new("CAN", "Canada"),
            ["BIH"] = new("BIH", "Bosnia e Herzegovina"),
            ["USA"] = new("USA", "Estados Unidos"),
            ["PAR"] = new("PAR", "Paraguai"),
            ["HAI"] = new("HAI", "Haiti"),
            ["SCO"] = new("SCO", "Escocia"),
            ["AUS"] = new("AUS", "Australia"),
            ["TUR"] = new("TUR", "Turquia"),
            ["BRA"] = new("BRA", "Brasil", true),
            ["MAR"] = new("MAR", "Marrocos"),
            ["QAT"] = new("QAT", "Catar"),
            ["SUI"] = new("SUI", "Suica"),
            ["CIV"] = new("CIV", "Costa do Marfim"),
            ["ECU"] = new("ECU", "Equador"),
            ["GER"] = new("GER", "Alemanha"),
            ["CUW"] = new("CUW", "Curacao"),
            ["NED"] = new("NED", "Paises Baixos"),
            ["JPN"] = new("JPN", "Japao"),
            ["SWE"] = new("SWE", "Suecia"),
            ["TUN"] = new("TUN", "Tunisia"),
            ["KSA"] = new("KSA", "Arabia Saudita"),
            ["URU"] = new("URU", "Uruguai"),
            ["ESP"] = new("ESP", "Espanha"),
            ["CPV"] = new("CPV", "Cabo Verde"),
            ["IRN"] = new("IRN", "Ira"),
            ["NZL"] = new("NZL", "Nova Zelandia"),
            ["BEL"] = new("BEL", "Belgica"),
            ["EGY"] = new("EGY", "Egito"),
            ["FRA"] = new("FRA", "Franca"),
            ["SEN"] = new("SEN", "Senegal"),
            ["IRQ"] = new("IRQ", "Iraque"),
            ["NOR"] = new("NOR", "Noruega"),
            ["ARG"] = new("ARG", "Argentina"),
            ["ALG"] = new("ALG", "Argelia"),
            ["AUT"] = new("AUT", "Austria"),
            ["JOR"] = new("JOR", "Jordania"),
            ["GHA"] = new("GHA", "Gana"),
            ["PAN"] = new("PAN", "Panama"),
            ["ENG"] = new("ENG", "Inglaterra"),
            ["CRO"] = new("CRO", "Croacia"),
            ["POR"] = new("POR", "Portugal"),
            ["COD"] = new("COD", "Congo DR"),
            ["UZB"] = new("UZB", "Uzbequistao"),
            ["COL"] = new("COL", "Colombia")
        };
    }

    private static int AddKnockout(List<Match> matches, int id)
    {
        void Add(int number, int roundId, CompetitionPhase phase, string date, string home, string away, string venue, int hour = 16)
        {
            matches.Add(new Match
            {
                Id = id++,
                OfficialNumber = number,
                RoundId = roundId,
                HomeTeam = new Team(home, home),
                AwayTeam = new Team(away, away),
                Phase = phase,
                Venue = venue,
                Kickoff = Kickoff(date, hour)
            });
        }

        Add(73, 4, CompetitionPhase.RoundOf32, "2026-06-28", "2A", "2B", "SoFi Stadium", 12);
        Add(74, 4, CompetitionPhase.RoundOf32, "2026-06-29", "1E", "3A/B/C/D/F", "Gillette Stadium", 16);
        Add(75, 4, CompetitionPhase.RoundOf32, "2026-06-29", "1F", "2C", "Estadio Monterrey", 19);
        Add(76, 4, CompetitionPhase.RoundOf32, "2026-06-29", "1C", "2F", "NRG Stadium", 12);
        Add(77, 4, CompetitionPhase.RoundOf32, "2026-06-30", "1I", "3C/D/F/G/H", "New York New Jersey Stadium");
        Add(78, 4, CompetitionPhase.RoundOf32, "2026-06-30", "2E", "2I", "Dallas Stadium", 19);
        Add(79, 4, CompetitionPhase.RoundOf32, "2026-07-01", "1A", "3C/E/F/H/I", "Mexico City Stadium");
        Add(80, 4, CompetitionPhase.RoundOf32, "2026-07-01", "1L", "3E/H/I/J/K", "Atlanta Stadium", 19);
        Add(81, 4, CompetitionPhase.RoundOf32, "2026-07-01", "1D", "3B/E/F/I/J", "San Francisco Bay Area Stadium", 22);
        Add(82, 4, CompetitionPhase.RoundOf32, "2026-07-02", "1G", "3A/E/H/I/J", "Seattle Stadium");
        Add(83, 4, CompetitionPhase.RoundOf32, "2026-07-02", "2K", "2L", "Toronto Stadium", 19);
        Add(84, 4, CompetitionPhase.RoundOf32, "2026-07-02", "1H", "2J", "SoFi Stadium", 22);
        Add(85, 4, CompetitionPhase.RoundOf32, "2026-07-03", "1B", "3E/F/G/I/J", "BC Place Vancouver");
        Add(86, 4, CompetitionPhase.RoundOf32, "2026-07-03", "1J", "2H", "Miami Stadium", 19);
        Add(87, 4, CompetitionPhase.RoundOf32, "2026-07-03", "1K", "3D/E/I/J/L", "Kansas City Stadium", 22);
        Add(88, 4, CompetitionPhase.RoundOf32, "2026-07-03", "2D", "2G", "Dallas Stadium", 13);

        Add(89, 5, CompetitionPhase.RoundOf16, "2026-07-04", "W73", "W75", "Philadelphia Stadium");
        Add(90, 5, CompetitionPhase.RoundOf16, "2026-07-04", "W74", "W77", "Houston Stadium", 19);
        Add(91, 5, CompetitionPhase.RoundOf16, "2026-07-05", "W76", "W78", "New York New Jersey Stadium");
        Add(92, 5, CompetitionPhase.RoundOf16, "2026-07-05", "W79", "W80", "Mexico City Stadium", 19);
        Add(93, 5, CompetitionPhase.RoundOf16, "2026-07-06", "W83", "W84", "Dallas Stadium");
        Add(94, 5, CompetitionPhase.RoundOf16, "2026-07-06", "W81", "W82", "Seattle Stadium", 19);
        Add(95, 5, CompetitionPhase.RoundOf16, "2026-07-07", "W86", "W88", "Atlanta Stadium");
        Add(96, 5, CompetitionPhase.RoundOf16, "2026-07-07", "W85", "W87", "BC Place Vancouver", 19);
        Add(97, 6, CompetitionPhase.QuarterFinal, "2026-07-09", "W89", "W90", "Gillette Stadium");
        Add(98, 6, CompetitionPhase.QuarterFinal, "2026-07-10", "W93", "W94", "SoFi Stadium");
        Add(99, 6, CompetitionPhase.QuarterFinal, "2026-07-11", "W91", "W92", "Kansas City Stadium");
        Add(100, 6, CompetitionPhase.QuarterFinal, "2026-07-11", "W95", "W96", "Miami Stadium", 19);
        Add(101, 7, CompetitionPhase.SemiFinal, "2026-07-14", "W97", "W98", "Dallas Stadium");
        Add(102, 7, CompetitionPhase.SemiFinal, "2026-07-15", "W99", "W100", "Atlanta Stadium");
        Add(103, 8, CompetitionPhase.ThirdPlace, "2026-07-18", "L101", "L102", "Miami Stadium");
        Add(104, 8, CompetitionPhase.Final, "2026-07-19", "W101", "W102", "New York New Jersey Stadium");

        return id;
    }

    private static DateTimeOffset Kickoff(string date, int hour)
    {
        var parsed = DateOnly.Parse(date);
        return new DateTimeOffset(parsed.Year, parsed.Month, parsed.Day, hour, 0, 0, TimeSpan.FromHours(-3));
    }
}
