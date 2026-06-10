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

    public IReadOnlyList<Match> Matches { get; private set; } = [];

    public void OnGet()
    {
        Matches = _repository.Matches.Where(match => match.Result is null).OrderBy(match => match.Kickoff).ToList();
    }

    public IActionResult OnPost()
    {
        TempData["Status"] = "Estrutura pronta: a persistencia do palpite sera ligada ao usuario autenticado na proxima etapa.";
        return RedirectToPage();
    }
}
