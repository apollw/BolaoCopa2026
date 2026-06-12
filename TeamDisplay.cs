using BolaoCopa2026.Models;

namespace BolaoCopa2026;

public static class TeamDisplay
{
    private static readonly IReadOnlyDictionary<string, string> Iso2ByCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ALG"] = "dz",
            ["ARG"] = "ar",
            ["AUS"] = "au",
            ["AUT"] = "at",
            ["BEL"] = "be",
            ["BIH"] = "ba",
            ["BRA"] = "br",
            ["CAN"] = "ca",
            ["COD"] = "cd",
            ["COL"] = "co",
            ["CPV"] = "cv",
            ["CIV"] = "ci",
            ["CRO"] = "hr",
            ["CUW"] = "cw",
            ["CZE"] = "cz",
            ["ECU"] = "ec",
            ["EGY"] = "eg",
            ["ENG"] = "gb",
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
            ["SCO"] = "gb",
            ["SEN"] = "sn",
            ["SUI"] = "ch",
            ["SWE"] = "se",
            ["TUN"] = "tn",
            ["TUR"] = "tr",
            ["URU"] = "uy",
            ["USA"] = "us",
            ["UZB"] = "uz"
        };

    public static string GetName(Team team)
    {
        return GetName(team.Code, team.Name);
    }

    public static string GetName(string? code, string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? code?.Trim() ?? string.Empty
            : name.Trim();
    }

    public static string? GetFlagUrl(Team team)
    {
        return GetFlagUrl(team.Code);
    }

    public static string? GetFlagUrl(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return Iso2ByCode.TryGetValue(code.Trim(), out var iso2)
            ? $"https://flagcdn.com/w40/{iso2}.png"
            : null;
    }
}
