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

        EnsureParticipantAvatarImageColumn(db);
        db.Database.Migrate();

        if (db.Rounds.Any())
        {
            EnsureMissingMatches(db);
            EnsureOfficialRoundOf32Matches(db);
            return;
        }

        db.Rounds.AddRange(SeedRounds());
        db.Matches.AddRange(SeedMatches());

        db.SaveChanges();
    }

    private static void EnsureParticipantAvatarImageColumn(BolaoDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE "Participants"
            ADD COLUMN IF NOT EXISTS "AvatarImagePath" character varying(240);
            """);
    }

    private static void EnsureMissingMatches(BolaoDbContext db)
    {
        var existingOfficialNumbers = db.Matches.Select(match => match.OfficialNumber).ToHashSet();
        var nextId = db.Matches.Any() ? db.Matches.Max(match => match.Id) + 1 : 1;
        var missing = new List<Match>();

        foreach (var match in SeedMatches().Where(match => !existingOfficialNumbers.Contains(match.OfficialNumber)))
        {
            missing.Add(new Match
            {
                Id = nextId++,
                OfficialNumber = match.OfficialNumber,
                RoundId = match.RoundId,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                Phase = match.Phase,
                Kickoff = match.Kickoff,
                GroupName = match.GroupName,
                Venue = match.Venue
            });
        }

        if (missing.Count == 0)
        {
            return;
        }

        db.Matches.AddRange(missing);
        db.SaveChanges();
    }

    private static void EnsureOfficialRoundOf32Matches(BolaoDbContext db)
    {
        var officialMatches = SeedMatches()
            .Where(match => match.Phase == CompetitionPhase.RoundOf32)
            .ToDictionary(match => match.OfficialNumber);
        var existingMatches = db.Matches
            .Where(match => match.Phase == CompetitionPhase.RoundOf32 || (match.OfficialNumber >= 73 && match.OfficialNumber <= 88))
            .ToList();
        var changed = false;

        foreach (var match in existingMatches)
        {
            if (match.Result is not null || !officialMatches.TryGetValue(match.OfficialNumber, out var official))
            {
                continue;
            }

            if (match.HomeTeam.Code == official.HomeTeam.Code
                && match.AwayTeam.Code == official.AwayTeam.Code)
            {
                continue;
            }

            match.HomeTeam = CloneTeam(official.HomeTeam);
            match.AwayTeam = CloneTeam(official.AwayTeam);
            changed = true;
        }

        if (changed)
        {
            db.SaveChanges();
        }
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
        var matches = new List<Match>();
        var id = 1;

        AddGroupStage(matches, ref id);
        AddKnockout(matches, id);
        return matches;
    }

    private static void AddGroupStage(List<Match> matches, ref int id)
    {
        var nextId = id;
        var number = 1;
        var groups = SeedGroups().ToDictionary(group => group.Code);

        void AddMatchday1(string groupCode, int pairing, string venue, int hour)
        {
            var group = groups[groupCode];
            if (pairing == 1)
            {
                AddGroupMatch(matches, nextId++, number++, 1, group.Code, group.Team1, group.Team2, group.Matchday1Date, venue, hour);
                return;
            }

            AddGroupMatch(matches, nextId++, number++, 1, group.Code, group.Team3, group.Team4, group.Matchday1Date, venue, hour);
        }

        AddMatchday1("A", 1, "Mexico City Stadium", 16);
        AddMatchday1("A", 2, "Estadio Guadalajara", 19);
        AddMatchday1("B", 1, "Toronto Stadium", 16);
        AddMatchday1("D", 1, "Los Angeles Stadium", 19);
        AddMatchday1("C", 2, "Boston Stadium", 13);
        AddMatchday1("D", 2, "BC Place Vancouver", 16);
        AddMatchday1("C", 1, "New York New Jersey Stadium", 19);
        AddMatchday1("B", 2, "San Francisco Bay Area Stadium", 22);
        AddMatchday1("E", 1, "Philadelphia Stadium", 13);
        AddMatchday1("E", 2, "Houston Stadium", 16);
        AddMatchday1("F", 1, "Dallas Stadium", 19);
        AddMatchday1("F", 2, "Estadio Monterrey", 22);
        AddMatchday1("H", 1, "Miami Stadium", 13);
        AddMatchday1("H", 2, "Atlanta Stadium", 16);
        AddMatchday1("G", 1, "Los Angeles Stadium", 19);
        AddMatchday1("G", 2, "Seattle Stadium", 22);
        AddMatchday1("I", 1, "New York New Jersey Stadium", 13);
        AddMatchday1("I", 2, "Boston Stadium", 16);
        AddMatchday1("J", 1, "Kansas City Stadium", 19);
        AddMatchday1("J", 2, "San Francisco Bay Area Stadium", 22);
        AddMatchday1("L", 1, "Toronto Stadium", 13);
        AddMatchday1("L", 2, "Dallas Stadium", 16);
        AddMatchday1("K", 1, "Houston Stadium", 19);
        AddMatchday1("K", 2, "Mexico City Stadium", 22);

        foreach (var group in groups.Values.OrderBy(group => group.Code))
        {
            AddGroupMatch(matches, nextId++, number++, 2, group.Code, group.Team1, group.Team3, group.Matchday2Date, "A definir", 16);
            AddGroupMatch(matches, nextId++, number++, 2, group.Code, group.Team4, group.Team2, group.Matchday2Date, "A definir", 19);
        }

        foreach (var group in groups.Values.OrderBy(group => group.Code))
        {
            AddGroupMatch(matches, nextId++, number++, 3, group.Code, group.Team4, group.Team1, group.Matchday3Date, "A definir", 16);
            AddGroupMatch(matches, nextId++, number++, 3, group.Code, group.Team2, group.Team3, group.Matchday3Date, "A definir", 19);
        }

        id = nextId;
    }

    private static void AddGroupMatch(
        List<Match> matches,
        int id,
        int number,
        int roundId,
        string groupCode,
        Team home,
        Team away,
        string date,
        string venue,
        int hour)
    {
        matches.Add(new Match
        {
            Id = id,
            OfficialNumber = number,
            RoundId = roundId,
            HomeTeam = CloneTeam(home),
            AwayTeam = CloneTeam(away),
            GroupName = $"Grupo {groupCode}",
            Venue = venue,
            Phase = CompetitionPhase.GroupStage,
            Kickoff = Kickoff(date, hour)
        });
    }

    private static List<GroupSeed> SeedGroups()
    {
        var teams = SeedTeams();
        return
        [
            new("A", teams["MEX"], teams["RSA"], teams["KOR"], teams["CZE"], "2026-06-11", "2026-06-18", "2026-06-24", "Mexico City Stadium", "Estadio Guadalajara", 16, 19),
            new("B", teams["CAN"], teams["BIH"], teams["QAT"], teams["SUI"], "2026-06-12", "2026-06-18", "2026-06-24", "Toronto Stadium", "San Francisco Bay Area Stadium", 16, 22),
            new("C", teams["BRA"], teams["MAR"], teams["HAI"], teams["SCO"], "2026-06-13", "2026-06-19", "2026-06-24", "New York New Jersey Stadium", "Boston Stadium", 19, 13),
            new("D", teams["USA"], teams["PAR"], teams["AUS"], teams["TUR"], "2026-06-12", "2026-06-19", "2026-06-25", "Los Angeles Stadium", "BC Place Vancouver", 19, 16),
            new("E", teams["CIV"], teams["ECU"], teams["GER"], teams["CUW"], "2026-06-14", "2026-06-20", "2026-06-25", "Philadelphia Stadium", "Houston Stadium", 13, 16),
            new("F", teams["NED"], teams["JPN"], teams["SWE"], teams["TUN"], "2026-06-14", "2026-06-20", "2026-06-25", "Dallas Stadium", "Estadio Monterrey", 19, 22),
            new("G", teams["IRN"], teams["NZL"], teams["BEL"], teams["EGY"], "2026-06-15", "2026-06-21", "2026-06-26", "Los Angeles Stadium", "Seattle Stadium", 19, 22),
            new("H", teams["KSA"], teams["URU"], teams["ESP"], teams["CPV"], "2026-06-15", "2026-06-21", "2026-06-26", "Miami Stadium", "Atlanta Stadium", 13, 16),
            new("I", teams["FRA"], teams["SEN"], teams["IRQ"], teams["NOR"], "2026-06-16", "2026-06-22", "2026-06-26", "New York New Jersey Stadium", "Boston Stadium", 13, 16),
            new("J", teams["ARG"], teams["ALG"], teams["AUT"], teams["JOR"], "2026-06-16", "2026-06-22", "2026-06-27", "Kansas City Stadium", "San Francisco Bay Area Stadium", 19, 22),
            new("K", teams["POR"], teams["COD"], teams["UZB"], teams["COL"], "2026-06-17", "2026-06-23", "2026-06-27", "Houston Stadium", "Mexico City Stadium", 19, 22),
            new("L", teams["GHA"], teams["PAN"], teams["ENG"], teams["CRO"], "2026-06-17", "2026-06-23", "2026-06-27", "Toronto Stadium", "Dallas Stadium", 13, 16)
        ];
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
        void Add(int number, int roundId, CompetitionPhase phase, string date, string home, string away, string venue, int hour = 16, int minute = 0)
        {
            matches.Add(new Match
            {
                Id = id++,
                OfficialNumber = number,
                RoundId = roundId,
                HomeTeam = ResolveSeedTeam(home),
                AwayTeam = ResolveSeedTeam(away),
                Phase = phase,
                Venue = venue,
                Kickoff = Kickoff(date, hour, minute)
            });
        }

        Add(73, 4, CompetitionPhase.RoundOf32, "2026-06-29", "GER", "PAR", "Boston", 17, 30);
        Add(74, 4, CompetitionPhase.RoundOf32, "2026-06-30", "FRA", "SWE", "Nova Jersey", 18);
        Add(75, 4, CompetitionPhase.RoundOf32, "2026-06-28", "RSA", "CAN", "Los Angeles", 16);
        Add(76, 4, CompetitionPhase.RoundOf32, "2026-06-29", "NED", "MAR", "El Gigante de Acero", 22);
        Add(77, 4, CompetitionPhase.RoundOf32, "2026-07-02", "POR", "CRO", "Toronto Field", 20);
        Add(78, 4, CompetitionPhase.RoundOf32, "2026-07-02", "ESP", "AUT", "Los Angeles", 16);
        Add(79, 4, CompetitionPhase.RoundOf32, "2026-07-01", "USA", "BIH", "Santa Clara", 21);
        Add(80, 4, CompetitionPhase.RoundOf32, "2026-07-01", "BEL", "SEN", "Seattle Field", 17);
        Add(81, 4, CompetitionPhase.RoundOf32, "2026-06-29", "BRA", "JPN", "Houston", 14);
        Add(82, 4, CompetitionPhase.RoundOf32, "2026-06-30", "CIV", "NOR", "Dallas", 14);
        Add(83, 4, CompetitionPhase.RoundOf32, "2026-06-30", "MEX", "ECU", "Azteca", 22);
        Add(84, 4, CompetitionPhase.RoundOf32, "2026-07-01", "ENG", "COD", "Atlanta", 13);
        Add(85, 4, CompetitionPhase.RoundOf32, "2026-07-03", "ARG", "CPV", "Miami", 19);
        Add(86, 4, CompetitionPhase.RoundOf32, "2026-07-03", "AUS", "EGY", "Dallas", 15);
        Add(87, 4, CompetitionPhase.RoundOf32, "2026-07-03", "SUI", "ALG", "Vancouver Place", 0);
        Add(88, 4, CompetitionPhase.RoundOf32, "2026-07-03", "COL", "GHA", "Kansas City", 22, 30);

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

    private static DateTimeOffset Kickoff(string date, int hour, int minute = 0)
    {
        var parsed = DateOnly.Parse(date);
        return new DateTimeOffset(parsed.Year, parsed.Month, parsed.Day, hour, minute, 0, TimeSpan.FromHours(-3))
            .ToUniversalTime();
    }

    private static Team CloneTeam(Team team)
    {
        return new Team(team.Code, team.Name, team.IsBrazil);
    }

    private static Team ResolveSeedTeam(string value)
    {
        var team = TeamCatalog.Resolve(value);
        return team is null
            ? new Team(value, value)
            : new Team(team.Code, team.Name, team.IsBrazil);
    }

    private sealed record GroupSeed(
        string Code,
        Team Team1,
        Team Team2,
        Team Team3,
        Team Team4,
        string Matchday1Date,
        string Matchday2Date,
        string Matchday3Date,
        string Matchday1Venue1,
        string Matchday1Venue2,
        int Matchday1Hour1,
        int Matchday1Hour2);
}
