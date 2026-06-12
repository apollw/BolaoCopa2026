using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages;

public class MuralModel : PageModel
{
    private readonly BolaoRepository _repository;

    public MuralModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public PublicPredictionsWallView Wall { get; private set; } = default!;

    public void OnGet()
    {
        Wall = _repository.GetPublicPredictionsWall();
    }

    public IActionResult OnGetParticipantDetails(int participantId)
    {
        var participant = _repository.GetPublicParticipantPredictions(participantId);
        return participant is null
            ? NotFound()
            : new JsonResult(participant);
    }
}
