using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Conta;

[Authorize(Policy = "ParticipantOnly")]
public class PerfilModel : PageModel
{
    private readonly BolaoRepository _repository;

    public PerfilModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public Participant? Participant { get; private set; }
    public IReadOnlyList<AvatarOption> AvatarOptions { get; private set; } = [];
    public AvatarOption? CurrentAvatar { get; private set; }

    [BindProperty]
    public string? SelectedAvatarKey { get; set; }

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPostSaveAvatar()
    {
        if (!_repository.SaveParticipantAvatar(_repository.CurrentParticipantId, SelectedAvatarKey, out var message))
        {
            TempData["Status"] = message;
            LoadData();
            return Page();
        }

        TempData["Status"] = message;
        return RedirectToPage();
    }

    private void LoadData()
    {
        Participant = _repository.CurrentParticipant;
        AvatarOptions = AvatarCatalog.All;
        SelectedAvatarKey = Participant?.AvatarKey ?? "none";
        CurrentAvatar = AvatarCatalog.Resolve(SelectedAvatarKey);
    }
}

public sealed record AvatarOption(string Key, string Label, string Kind, string? ImageUrl = null, string? Symbol = null);

public static class AvatarCatalog
{
    public static IReadOnlyList<AvatarOption> All { get; } =
    [
        new("none", "Sem avatar", "emoji", Symbol: "👤"),
        new("flag-bra", "Brasil", "team", ImageUrl: TeamDisplay.GetFlagUrl("BRA")),
        new("emoji-ball", "Bola", "emoji", Symbol: "⚽"),
        new("emoji-trophy", "Taca", "emoji", Symbol: "🏆"),
        new("emoji-whistle", "Apito", "emoji", Symbol: "🪈"),
        new("emoji-goal", "Gol", "emoji", Symbol: "🥅"),
        new("emoji-fire", "Goleador", "emoji", Symbol: "🔥"),
        new("emoji-clap", "Torcida", "emoji", Symbol: "👏"),
        new("emoji-shield", "Escudo", "emoji", Symbol: "🛡"),
        new("emoji-star", "Craque", "emoji", Symbol: "⭐")
    ];

    public static AvatarOption? Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return All.FirstOrDefault(option => option.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
