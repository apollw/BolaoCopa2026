using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;
using System.Globalization;

namespace BolaoCopa2026.Pages;

public class IndexModel : PageModel
{
    private readonly BolaoRepository _repository;

    public IndexModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public DashboardStats Stats { get; private set; } = default!;

    public ParticipantAvatarVisual GetParticipantAvatar(Participant participant)
    {
        return ParticipantAvatarCatalog.Resolve(participant);
    }

    public void OnGet()
    {
        Stats = _repository.GetDashboard();
    }

    public string BuildChartStyle(IReadOnlyList<DashboardChartSlice> slices)
    {
        var first = slices.ElementAtOrDefault(0);
        var second = slices.ElementAtOrDefault(1);
        var third = slices.ElementAtOrDefault(2);
        var total = slices.Sum(slice => Math.Max(slice.Value, 0));

        if (first is null || second is null || total <= 0)
        {
            return $"--chart-a:0%;--chart-b:0%;--chart-c:100%;--chart-a-color:{first?.Color ?? "#176b52"};--chart-b-color:{second?.Color ?? "#d5a928"};--chart-c-color:{third?.Color ?? second?.Color ?? "#dfe7da"};";
        }

        var firstPercent = Math.Round(first.Value * 100d / total, 2).ToString(CultureInfo.InvariantCulture);
        var secondPercent = Math.Round((first.Value + second.Value) * 100d / total, 2).ToString(CultureInfo.InvariantCulture);

        return $"--chart-a:{firstPercent}%;--chart-b:{secondPercent}%;--chart-c:100%;--chart-a-color:{first.Color};--chart-b-color:{second.Color};--chart-c-color:{third?.Color ?? second.Color};";
    }
}
