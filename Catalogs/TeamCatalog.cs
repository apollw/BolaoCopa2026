using BolaoCopa2026.Models;

namespace BolaoCopa2026.Catalogs;

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
}
