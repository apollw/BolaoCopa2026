using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Palpites;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AuditImageService _auditImageService;
    private readonly BolaoRepository _repository;

    public IndexModel(BolaoRepository repository, AuditImageService auditImageService)
    {
        _repository = repository;
        _auditImageService = auditImageService;
    }

    public IReadOnlyList<PredictionRound> Rounds { get; private set; } = [];
    public IReadOnlySet<int> AvailableRoundIds { get; private set; } = new HashSet<int>();
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

    public IActionResult OnGetDownloadAudit(int roundId)
    {
        var snapshot = _auditImageService.BuildSnapshot(_repository.CurrentParticipantId, roundId);
        if (snapshot is null)
        {
            TempData["Status"] = "Comprovante bloqueado: finalize a rodada antes de baixar.";
            return RedirectToPage(new { roundId });
        }

        var image = _auditImageService.RenderSvg(snapshot);
        _auditImageService.MarkDownloaded(snapshot);
        var fileName = $"auditoria-rodada-{roundId}-{snapshot.Participant.Id}.svg";
        return File(image, "image/svg+xml", fileName);
    }

    private void LoadData(int? roundId)
    {
        Rounds = _repository.Rounds;
        AvailableRoundIds = Rounds
            .Where(round => _repository.IsRoundAvailable(_repository.CurrentParticipantId, round.Id, out _))
            .Select(round => round.Id)
            .ToHashSet();
        RoundView = _repository.GetRoundPrediction(_repository.CurrentParticipantId, roundId);
    }
}
