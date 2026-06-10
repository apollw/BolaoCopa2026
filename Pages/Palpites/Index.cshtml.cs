using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Palpites;

public class IndexModel : PageModel
{
    private readonly BolaoRepository _repository;

    public IndexModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<PredictionRound> Rounds { get; private set; } = [];
    public RoundPredictionView RoundView { get; private set; } = default!;

    public void OnGet(int? roundId)
    {
        LoadData(roundId);
    }

    public IActionResult OnPostSaveDraft(int roundId, int matchId, int homeGoals, int awayGoals, string? qualifiedTeamCode)
    {
        _repository.SaveDraftPrediction(_repository.CurrentParticipantId, matchId, homeGoals, awayGoals, qualifiedTeamCode, out var message);
        TempData["Status"] = message;
        return RedirectToPage(new { roundId });
    }

    public IActionResult OnPostFinalizeRound(int roundId)
    {
        _repository.FinalizeRound(_repository.CurrentParticipantId, roundId, out var message);
        TempData["Status"] = message;
        return RedirectToPage(new { roundId });
    }

    public IActionResult OnPostSendAudit(int roundId)
    {
        TempData["Status"] = _repository.CanSendPredictionAudit(_repository.CurrentParticipantId, roundId)
            ? "Auditoria da rodada enviada por email para o participante mockado."
            : "Auditoria bloqueada: finalize a rodada antes do envio.";

        return RedirectToPage(new { roundId });
    }

    private void LoadData(int? roundId)
    {
        Rounds = _repository.Rounds;
        RoundView = _repository.GetRoundPrediction(_repository.CurrentParticipantId, roundId);
    }
}
