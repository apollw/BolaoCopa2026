using BolaoCopa2026.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa2026.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class DiagnosticoModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly BolaoDbContext _db;

    public DiagnosticoModel(BolaoDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public string EfProvider { get; private set; } = string.Empty;
    public string ConfiguredProvider { get; private set; } = string.Empty;
    public bool HasSupabaseUrl { get; private set; }
    public IReadOnlyList<DiagnosticCount> Counts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        EfProvider = _db.Database.ProviderName ?? "desconhecido";
        ConfiguredProvider = _configuration.GetValue<string>("Database:Provider") ?? "nao configurado";
        HasSupabaseUrl = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_DATABASE_URL"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL"));

        Counts =
        [
            new("Participantes", await _db.Participants.CountAsync()),
            new("Palpites de jogos", await _db.Predictions.CountAsync()),
            new("Rodadas finalizadas", await _db.RoundSubmissions.CountAsync()),
            new("Palpites especiais", await _db.SpecialPredictions.CountAsync()),
            new("Resultados reais auditados", await _db.ResultAudits.CountAsync()),
            new("Rodadas base", await _db.Rounds.CountAsync()),
            new("Partidas base", await _db.Matches.CountAsync())
        ];
    }
}

public sealed record DiagnosticCount(string Name, int Count);
