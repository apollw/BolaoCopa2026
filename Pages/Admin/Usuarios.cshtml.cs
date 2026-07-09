using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa2026.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class UsuariosModel : PageModel
{
    private readonly BolaoRepository _repository;
    private readonly BolaoDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly AuditImageService _auditImageService;

    public UsuariosModel(BolaoRepository repository, BolaoDbContext db, IWebHostEnvironment environment, AuditImageService auditImageService)
    {
        _repository = repository;
        _db = db;
        _environment = environment;
        _auditImageService = auditImageService;
    }

    public IReadOnlyList<AdminParticipantSummary> Participants { get; private set; } = [];

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPostDeleteParticipant(int participantId, string confirmName)
    {
        var participant = _db.Participants.AsNoTracking().SingleOrDefault(item => item.Id == participantId);
        if (participant is null)
        {
            TempData["Status"] = "Participante nao encontrado.";
            LoadData();
            return Page();
        }

        if (!_repository.DeleteParticipant(participantId, confirmName, out var message))
        {
            TempData["Status"] = message;
            LoadData();
            return Page();
        }

        DeleteAvatarFile(participant.AvatarImagePath);
        TempData["Status"] = $"Usuario {participant.Name} e todos os dados vinculados foram excluidos.";
        return RedirectToPage();
    }

    public IActionResult OnGetDownloadFinalCardPreview(int participantId)
    {
        var package = _auditImageService.BuildFinalCardPackage(participantId, allowPartial: true);
        if (package is null)
        {
            TempData["Status"] = "Nao foi possivel gerar o card deste participante.";
            return RedirectToPage();
        }

        var image = _auditImageService.RenderFinalCardPng(package);
        var fileName = $"card-preview-{package.Participant.Id}.png";
        return File(image, "image/png", fileName);
    }

    private void LoadData()
    {
        var participants = _repository.Participants.ToList();
        var participantIds = participants.Select(participant => participant.Id).ToList();

        var predictionCounts = _db.Predictions
            .AsNoTracking()
            .Where(prediction => participantIds.Contains(prediction.ParticipantId))
            .GroupBy(prediction => prediction.ParticipantId)
            .ToDictionary(group => group.Key, group => group.Count());

        var roundSubmissionCounts = _db.RoundSubmissions
            .AsNoTracking()
            .Where(submission => participantIds.Contains(submission.ParticipantId))
            .GroupBy(submission => submission.ParticipantId)
            .ToDictionary(group => group.Key, group => group.Count());

        var messageCounts = _db.CopaMessages
            .AsNoTracking()
            .Where(message => participantIds.Contains(message.ParticipantId))
            .GroupBy(message => message.ParticipantId)
            .ToDictionary(group => group.Key, group => group.Count());

        var specialPredictionIds = _db.SpecialPredictions
            .AsNoTracking()
            .Where(prediction => participantIds.Contains(prediction.ParticipantId))
            .Select(prediction => prediction.ParticipantId)
            .ToHashSet();

        Participants = participants
            .Select(participant => new AdminParticipantSummary
            {
                Participant = participant,
                PredictionCount = predictionCounts.GetValueOrDefault(participant.Id),
                RoundSubmissionCount = roundSubmissionCounts.GetValueOrDefault(participant.Id),
                MessageCount = messageCounts.GetValueOrDefault(participant.Id),
                HasSpecialPrediction = specialPredictionIds.Contains(participant.Id)
            })
            .OrderByDescending(item => item.Participant.IsAdmin)
            .ThenBy(item => item.Participant.Name)
            .ToList();
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

public sealed class AdminParticipantSummary
{
    public required Participant Participant { get; init; }
    public int PredictionCount { get; init; }
    public int RoundSubmissionCount { get; init; }
    public int MessageCount { get; init; }
    public bool HasSpecialPrediction { get; init; }
}
