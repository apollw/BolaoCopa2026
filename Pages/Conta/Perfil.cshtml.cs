using BolaoCopa2026;
using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkiaSharp;
using System.IO;

namespace BolaoCopa2026.Pages.Conta;

[Authorize(Policy = "ParticipantOnly")]
public class PerfilModel : PageModel
{
    private readonly BolaoRepository _repository;
    private readonly AuditImageService _auditImageService;
    private readonly IWebHostEnvironment _environment;

    public PerfilModel(BolaoRepository repository, AuditImageService auditImageService, IWebHostEnvironment environment)
    {
        _repository = repository;
        _auditImageService = auditImageService;
        _environment = environment;
    }

    public Participant? Participant { get; private set; }
    public IReadOnlyList<AvatarOption> AvatarOptions { get; private set; } = [];
    public ParticipantAvatarVisual? CurrentAvatar { get; private set; }

    [BindProperty]
    public string? SelectedAvatarKey { get; set; }

    [BindProperty]
    public IFormFile? AvatarPhoto { get; set; }

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

    public async Task<IActionResult> OnPostUploadPhotoAsync()
    {
        if (AvatarPhoto is null || AvatarPhoto.Length == 0)
        {
            TempData["Status"] = "Selecione uma imagem antes de enviar.";
            LoadData();
            return Page();
        }

        if (AvatarPhoto.Length > 8 * 1024 * 1024)
        {
            TempData["Status"] = "A imagem precisa ter no maximo 8 MB.";
            LoadData();
            return Page();
        }

        try
        {
            var relativePath = await SaveAvatarImageAsync(AvatarPhoto);
            if (!_repository.SaveParticipantAvatarImage(_repository.CurrentParticipantId, relativePath, out var message))
            {
                TempData["Status"] = message;
                LoadData();
                return Page();
            }

            TempData["Status"] = "Foto de perfil atualizada.";
            return RedirectToPage();
        }
        catch (InvalidDataException)
        {
            TempData["Status"] = "A imagem enviada nao pode ser processada.";
            LoadData();
            return Page();
        }
    }

    public IActionResult OnPostClearPhoto()
    {
        var photoPath = _repository.CurrentParticipant?.AvatarImagePath;
        if (!_repository.SaveParticipantAvatarImage(_repository.CurrentParticipantId, null, out var message))
        {
            TempData["Status"] = message;
            LoadData();
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            DeleteAvatarFile(photoPath);
        }

        TempData["Status"] = "Foto removida do perfil.";
        return RedirectToPage();
    }

    public IActionResult OnGetDownloadFullAudit()
    {
        var package = _auditImageService.BuildFullAuditPackage(_repository.CurrentParticipantId);
        if (package is null)
        {
            TempData["Status"] = "Comprovante bloqueado: finalize ao menos uma rodada ou seus palpites especiais antes de baixar a auditoria completa.";
            return RedirectToPage();
        }

        var pdf = _auditImageService.RenderFullAuditPdf(package);
        foreach (var snapshot in package.RoundSnapshots)
        {
            _auditImageService.MarkDownloaded(snapshot);
        }

        if (package.SpecialSnapshot is not null)
        {
            _repository.MarkSpecialAuditDownloaded(package.Participant.Id, package.SpecialSnapshot.GeneratedAt, package.SpecialSnapshot.ProofHash);
        }

        var fileName = $"auditoria-completa-{package.Participant.Id}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    public IActionResult OnGetDownloadScoreAudit()
    {
        var package = _auditImageService.BuildScoreAuditPackage(_repository.CurrentParticipantId);
        if (package is null)
        {
            TempData["Status"] = "Auditoria de pontuacao indisponivel: nao ha rodadas cadastradas.";
            return RedirectToPage();
        }

        var pdf = _auditImageService.RenderScoreAuditPdf(package);
        var fileName = $"auditoria-pontuacao-{package.Participant.Id}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private void LoadData()
    {
        Participant = _repository.CurrentParticipant;
        AvatarOptions = AvatarCatalog.All;
        SelectedAvatarKey = Participant?.AvatarKey ?? "none";
        CurrentAvatar = ParticipantAvatarCatalog.Resolve(Participant);
    }

    private async Task<string> SaveAvatarImageAsync(IFormFile photo)
    {
        var avatarsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(avatarsFolder);

        var fileName = $"participant-{_repository.CurrentParticipantId}.jpg";
        var absolutePath = Path.Combine(avatarsFolder, fileName);

        using var input = photo.OpenReadStream();
        using var original = SKBitmap.Decode(input) ?? throw new InvalidDataException("Imagem invalida.");

        var cropSize = Math.Min(original.Width, original.Height);
        var cropX = (original.Width - cropSize) / 2;
        var cropY = (original.Height - cropSize) / 2;
        var cropRect = new SKRectI(cropX, cropY, cropX + cropSize, cropY + cropSize);

        using var cropped = new SKBitmap(cropSize, cropSize);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(original, cropRect, new SKRect(0, 0, cropSize, cropSize));
            canvas.Flush();
        }

        var targetSize = 512;
        using var resized = cropped.Resize(new SKImageInfo(targetSize, targetSize), SKFilterQuality.High) ?? cropped;
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 88);

        await using var output = System.IO.File.Open(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.SaveTo(output);

        return $"/uploads/avatars/{fileName}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private void DeleteAvatarFile(string? photoPath)
    {
        var path = photoPath?.Split('?', 2)[0];
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(_environment.WebRootPath, relative);
        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }
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
