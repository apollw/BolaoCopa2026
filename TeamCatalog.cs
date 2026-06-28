using BolaoCopa2026.Models;

namespace BolaoCopa2026;

public sealed record TeamCatalogEntry(string Code, string Name, bool IsBrazil = false);

public static class TeamCatalog
{
    public static IReadOnlyList<TeamCatalogEntry> All { get; } =
    [
        new("MEX", "Mexico"),
        new("RSA", "Africa do Sul"),
        new("KOR", "Coreia do Sul"),
        new("CZE", "Tchequia"),
        new("CAN", "Canada"),
        new("BIH", "Bosnia e Herzegovina"),
        new("USA", "Estados Unidos"),
        new("PAR", "Paraguai"),
        new("HAI", "Haiti"),
        new("SCO", "Escocia"),
        new("AUS", "Australia"),
        new("TUR", "Turquia"),
        new("BRA", "Brasil", true),
        new("MAR", "Marrocos"),
        new("QAT", "Catar"),
        new("SUI", "Suica"),
        new("CIV", "Costa do Marfim"),
        new("ECU", "Equador"),
        new("GER", "Alemanha"),
        new("CUW", "Curacao"),
        new("NED", "Paises Baixos"),
        new("JPN", "Japao"),
        new("SWE", "Suecia"),
        new("TUN", "Tunisia"),
        new("KSA", "Arabia Saudita"),
        new("URU", "Uruguai"),
        new("ESP", "Espanha"),
        new("CPV", "Cabo Verde"),
        new("IRN", "Ira"),
        new("NZL", "Nova Zelandia"),
        new("BEL", "Belgica"),
        new("EGY", "Egito"),
        new("FRA", "Franca"),
        new("SEN", "Senegal"),
        new("IRQ", "Iraque"),
        new("NOR", "Noruega"),
        new("ARG", "Argentina"),
        new("ALG", "Argelia"),
        new("AUT", "Austria"),
        new("JOR", "Jordania"),
        new("GHA", "Gana"),
        new("PAN", "Panama"),
        new("ENG", "Inglaterra"),
        new("CRO", "Croacia"),
        new("POR", "Portugal"),
        new("COD", "Congo DR"),
        new("UZB", "Uzbequistao"),
        new("COL", "Colombia")
    ];

    public static IReadOnlyDictionary<string, int> FifaRanking2025 { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["USA"] = 14,
            ["MEX"] = 15,
            ["CAN"] = 27,
            ["ESP"] = 1,
            ["ARG"] = 2,
            ["FRA"] = 3,
            ["ENG"] = 4,
            ["BRA"] = 5,
            ["POR"] = 6,
            ["NED"] = 7,
            ["BEL"] = 8,
            ["GER"] = 9,
            ["CRO"] = 10,
            ["MAR"] = 11,
            ["COL"] = 13,
            ["URU"] = 16,
            ["SUI"] = 17,
            ["JPN"] = 18,
            ["SEN"] = 19,
            ["IRN"] = 20,
            ["BIH"] = 21,
            ["KOR"] = 22,
            ["ECU"] = 23,
            ["AUT"] = 24,
            ["TUR"] = 25,
            ["AUS"] = 26,
            ["NOR"] = 29,
            ["PAN"] = 30,
            ["SWE"] = 33,
            ["EGY"] = 34,
            ["ALG"] = 35,
            ["SCO"] = 36,
            ["PAR"] = 39,
            ["TUN"] = 40,
            ["CIV"] = 42,
            ["CZE"] = 28,
            ["IRQ"] = 49,
            ["UZB"] = 50,
            ["QAT"] = 51,
            ["COD"] = 56,
            ["KSA"] = 60,
            ["RSA"] = 61,
            ["JOR"] = 66,
            ["CPV"] = 68,
            ["GHA"] = 72,
            ["CUW"] = 82,
            ["HAI"] = 84,
            ["NZL"] = 86
        };

    public static TeamCatalogEntry? Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return All.FirstOrDefault(team =>
            team.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || team.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveName(string? value)
    {
        return Resolve(value)?.Name ?? value?.Trim() ?? string.Empty;
    }

    public static string? ResolveCode(string? value)
    {
        return Resolve(value)?.Code;
    }

    public static string? ResolveFlagUrl(string? value)
    {
        var code = ResolveCode(value);
        return code is null ? null : TeamDisplay.GetFlagUrl(code);
    }

    public static int ResolveFifaRanking(string? value)
    {
        var code = ResolveCode(value);
        return code is not null && FifaRanking2025.TryGetValue(code, out var rank)
            ? rank
            : int.MaxValue;
    }
}
