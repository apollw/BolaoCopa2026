using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages;

public class IndexModel : PageModel
{
    private readonly BolaoRepository _repository;

    public IndexModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public DashboardStats Stats { get; private set; } = default!;

    public void OnGet()
    {
        Stats = _repository.GetDashboard();
    }
}
