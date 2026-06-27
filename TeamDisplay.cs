using BolaoCopa2026.Models;

namespace BolaoCopa2026;

public static class TeamDisplay
{
    private static readonly IReadOnlyDictionary<string, string> FlagCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ALG"] = "dz",
        ["ARG"] = "ar",
        ["AUS"] = "au",
        ["AUT"] = "at",
        ["BEL"] = "be",
        ["BIH"] = "ba",
        ["BRA"] = "br",
        ["CAN"] = "ca",
        ["CIV"] = "ci",
        ["COD"] = "cd",
        ["COL"] = "co",
        ["CPV"] = "cv",
        ["CRO"] = "hr",
        ["CZE"] = "cz",
        ["CUW"] = "cw",
        ["ECU"] = "ec",
        ["EGY"] = "eg",
        ["ENG"] = "gb-eng",
        ["ESP"] = "es",
        ["FRA"] = "fr",
        ["GER"] = "de",
        ["GHA"] = "gh",
        ["HAI"] = "ht",
        ["IRN"] = "ir",
        ["IRQ"] = "iq",
        ["JOR"] = "jo",
        ["JPN"] = "jp",
        ["KOR"] = "kr",
        ["KSA"] = "sa",
        ["MAR"] = "ma",
        ["MEX"] = "mx",
        ["NED"] = "nl",
        ["NOR"] = "no",
        ["NZL"] = "nz",
        ["PAN"] = "pa",
        ["PAR"] = "py",
        ["POR"] = "pt",
        ["QAT"] = "qa",
        ["RSA"] = "za",
        ["SCO"] = "gb-sct",
        ["SEN"] = "sn",
        ["SUI"] = "ch",
        ["SWE"] = "se",
        ["TUN"] = "tn",
        ["TUR"] = "tr",
        ["URU"] = "uy",
        ["USA"] = "us",
        ["UZB"] = "uz"
    };

    public static string? GetFlagUrl(string? teamCodeOrName)
    {
        if (string.IsNullOrWhiteSpace(teamCodeOrName))
        {
            return null;
        }

        var code = TeamCatalog.ResolveCode(teamCodeOrName);
        if (code is null || !FlagCodes.TryGetValue(code, out var flagCode))
        {
            return null;
        }

        return $"https://flagcdn.com/w80/{flagCode}.png";
    }

    public static string? GetFlagUrl(Team team)
    {
        return GetFlagUrl(team.Code);
    }

    public static string GetName(string? teamCodeOrName)
    {
        return TeamCatalog.ResolveName(teamCodeOrName);
    }

    public static string GetName(Team team)
    {
        return team.Name;
    }
}
