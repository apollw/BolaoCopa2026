using BolaoCopa2026.Catalogs;
using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Globalization;
using System.Security.Claims;

namespace BolaoCopa2026.Pages;

public class IndexModel : PageModel
{
    private const int MessageBoardPageSize = 5;
    private readonly BolaoRepository _repository;

    public IndexModel(BolaoRepository repository)
    {
        _repository = repository;
    }

    public DashboardStats Stats { get; private set; } = default!;
    public MessageBoardView MessageBoard { get; private set; } = default!;

    public ParticipantAvatarVisual GetParticipantAvatar(Participant participant)
    {
        return ParticipantAvatarCatalog.Resolve(participant);
    }

    public ParticipantAvatarVisual GetParticipantAvatar(string? avatarKey)
    {
        return ParticipantAvatarCatalog.Resolve(avatarKey);
    }

    public ParticipantAvatarVisual GetParticipantAvatar(string? avatarKey, string? avatarImagePath)
    {
        if (!string.IsNullOrWhiteSpace(avatarImagePath))
        {
            return new ParticipantAvatarVisual("photo", "Foto do perfil", "photo", ImageUrl: avatarImagePath);
        }

        return ParticipantAvatarCatalog.Resolve(avatarKey);
    }

    public int? CurrentParticipantId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var participantId)
        ? participantId
        : null;

    public bool IsAdmin => User.IsInRole("Admin");

    [BindProperty(SupportsGet = true)]
    public int MessagesPage { get; set; } = 1;

    [BindProperty]
    public MessageBoardInput MessageInput { get; set; } = new();

    public void OnGet()
    {
        Stats = _repository.GetDashboard();
        MessageBoard = _repository.GetMessageBoard(MessagesPage, MessageBoardPageSize, CurrentParticipantId, IsAdmin);
    }

    public IActionResult OnGetMessageBoard(int messagesPage = 1)
    {
        var board = _repository.GetMessageBoard(messagesPage, MessageBoardPageSize, CurrentParticipantId, IsAdmin);
        return new JsonResult(new
        {
            success = true,
            board
        });
    }

    public IActionResult OnPostAddMessage()
    {
        Stats = _repository.GetDashboard();
        MessageBoard = _repository.GetMessageBoard(MessagesPage, MessageBoardPageSize, CurrentParticipantId, IsAdmin);

        if (User.Identity?.IsAuthenticated != true || User.IsInRole("Admin"))
        {
            TempData["Status"] = "Entre como participante para publicar mensagens.";
            return AjaxOrRedirectFailure();
        }

        if (!_repository.AddMessage(_repository.CurrentParticipantId, MessageInput.Message, MessageInput.MoodKey, out var message, out var createdEntry))
        {
            TempData["Status"] = message;
            MessageBoard = _repository.GetMessageBoard(MessagesPage, MessageBoardPageSize, CurrentParticipantId, IsAdmin);
            return AjaxOrRedirectFailure();
        }

        TempData["Status"] = message;
        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                message,
                board = new
                {
                    page = MessageBoard.Page,
                    pageSize = MessageBoard.PageSize,
                    totalMessages = MessageBoard.TotalMessages + 1,
                    totalPages = Math.Max(1, (int)Math.Ceiling((MessageBoard.TotalMessages + 1) / (double)MessageBoard.PageSize))
                },
                entry = createdEntry
            });
        }

        return RedirectToPage(new { messagesPage = 1 });
    }

    public IActionResult OnPostDeleteMessage(int messageId)
    {
        var page = Math.Max(MessagesPage, 1);
        var participantId = IsAdmin ? null : CurrentParticipantId;
        if (!_repository.DeleteMessage(messageId, participantId, IsAdmin, out var message, out var board, page, MessageBoardPageSize))
        {
            TempData["Status"] = message;
            return AjaxOrRedirectFailure();
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                message,
                board
            });
        }

        TempData["Status"] = message;
        return RedirectToPage(new { messagesPage = board?.Page ?? 1 });
    }

    private IActionResult AjaxOrRedirectFailure()
    {
        if (IsAjaxRequest())
        {
            return BadRequest(new { success = false, message = TempData["Status"] as string ?? "Nao foi possivel publicar a mensagem." });
        }

        return Page();
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
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

public sealed class MessageBoardInput
{
    [Required(ErrorMessage = "Escreva uma mensagem antes de publicar.")]
    [StringLength(200, ErrorMessage = "Use ate 200 caracteres.")]
    public string Message { get; set; } = string.Empty;

    [StringLength(24, ErrorMessage = "Use uma opcao valida de humor.")]
    public string? MoodKey { get; set; }
}
