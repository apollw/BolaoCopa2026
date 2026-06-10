using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Conta;

[Authorize]
public class PerfilModel : PageModel
{
    private readonly BolaoRepository _repository;

    public PerfilModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public Participant? Participant { get; private set; }

    public void OnGet()
    {
        Participant = _repository.CurrentParticipant;
    }
}
