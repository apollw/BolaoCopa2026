using System.ComponentModel.DataAnnotations;
using BolaoCopa2026;
using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Palpites;

[Authorize(Policy = "ParticipantOnly")]
public class EspeciaisModel : PageModel
{
    private readonly AuditImageService _auditImageService;
    private readonly BolaoRepository _repository;

    public EspeciaisModel(BolaoRepository repository, AuditImageService auditImageService)
    {
        _repository = repository;
        _auditImageService = auditImageService;
    }

    [BindProperty]
    public SpecialPredictionInput Input { get; set; } = new();

    public SpecialPredictionView SpecialView { get; private set; } = default!;
    public IReadOnlyList<TeamCatalogEntry> TeamOptions => TeamCatalog.All;

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPostSaveDraft()
    {
        _repository.SaveSpecialPredictionDraft(
            _repository.CurrentParticipantId,
            Input.Champion,
            Input.RunnerUp,
            Input.TopScorer,
            Input.GoldenBall,
            out var message);

        TempData["Status"] = message;
        return RedirectToPage();
    }

    public IActionResult OnPostFinalize()
    {
        if (!_repository.SaveSpecialPredictionDraft(
            _repository.CurrentParticipantId,
            Input.Champion,
            Input.RunnerUp,
            Input.TopScorer,
            Input.GoldenBall,
            out var draftMessage))
        {
            TempData["Status"] = draftMessage;
            return RedirectToPage();
        }

        _repository.FinalizeSpecialPrediction(_repository.CurrentParticipantId, out var message);
        TempData["Status"] = message;
        return RedirectToPage();
    }

    public IActionResult OnGetDownloadAudit()
    {
        var snapshot = _auditImageService.BuildSpecialSnapshot(_repository.CurrentParticipantId);
        if (snapshot is null)
        {
            TempData["Status"] = "Comprovante bloqueado: finalize os palpites especiais antes de baixar.";
            return RedirectToPage();
        }

        var image = _auditImageService.RenderSpecialPng(snapshot);
        _repository.MarkSpecialAuditDownloaded(snapshot.Participant.Id, snapshot.GeneratedAt, snapshot.ProofHash);
        var fileName = $"auditoria-palpites-especiais-{snapshot.Participant.Id}.png";
        return File(image, "image/png", fileName);
    }

    private void LoadData()
    {
        SpecialView = _repository.GetSpecialPrediction(_repository.CurrentParticipantId);
        if (SpecialView.Prediction is null)
        {
            return;
        }

        Input = new SpecialPredictionInput
        {
            Champion = TeamCatalog.ResolveCode(SpecialView.Prediction.Champion) ?? SpecialView.Prediction.Champion,
            RunnerUp = TeamCatalog.ResolveCode(SpecialView.Prediction.RunnerUp) ?? SpecialView.Prediction.RunnerUp,
            TopScorer = SpecialView.Prediction.TopScorer,
            GoldenBall = SpecialView.Prediction.GoldenBall
        };
    }
}

public sealed class SpecialPredictionInput
{
    [Required(ErrorMessage = "Informe o campeao.")]
    [StringLength(80, ErrorMessage = "Use ate 80 caracteres.")]
    public string Champion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o vice-campeao.")]
    [StringLength(80, ErrorMessage = "Use ate 80 caracteres.")]
    public string RunnerUp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o artilheiro.")]
    [StringLength(100, ErrorMessage = "Use ate 100 caracteres.")]
    public string TopScorer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o Bola de Ouro.")]
    [StringLength(100, ErrorMessage = "Use ate 100 caracteres.")]
    public string GoldenBall { get; set; } = string.Empty;
}
