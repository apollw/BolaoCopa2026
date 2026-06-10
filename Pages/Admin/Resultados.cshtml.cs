using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class ResultadosModel : PageModel
{
    private readonly BolaoRepository _repository;

    public ResultadosModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<Match> PendingMatches { get; private set; } = [];
    public IReadOnlyList<ResultAudit> Audits { get; private set; } = [];

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPost(int matchId, int homeGoals, int awayGoals, string? qualifiedTeamCode)
    {
        var registeredBy = User.Identity?.Name ?? "Administrador";
        _repository.TryRegisterResult(matchId, homeGoals, awayGoals, qualifiedTeamCode, registeredBy, out var message);
        TempData["Status"] = message;
        return RedirectToPage();
    }

    private void LoadData()
    {
        PendingMatches = _repository.Matches.Where(match => match.Result is null).OrderBy(match => match.Kickoff).ToList();
        Audits = _repository.Audits.OrderByDescending(audit => audit.RegisteredAt).ToList();
    }
}
