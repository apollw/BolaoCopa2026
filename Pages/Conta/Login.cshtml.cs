using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa2026.Pages.Conta;

public class LoginModel : PageModel
{
    private readonly BolaoDbContext _db;
    private readonly IPasswordHasher<Participant> _passwordHasher;

    public LoginModel(BolaoDbContext db, IPasswordHasher<Participant> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var participant = await _db.Participants.SingleOrDefaultAsync(item => item.Email == email || item.Login == email);
        if (participant?.PasswordHash is null)
        {
            StatusMessage = "Email ou senha invalidos.";
            return Page();
        }

        var result = _passwordHasher.VerifyHashedPassword(participant, participant.PasswordHash, Input.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            StatusMessage = "Email ou senha invalidos.";
            return Page();
        }

        await SignInAsync(participant);
        return RedirectToPage("/Palpites/Index");
    }

    private async Task SignInAsync(Participant participant)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, participant.Id.ToString()),
            new(ClaimTypes.Name, participant.Name),
            new(ClaimTypes.Email, participant.Email),
            new(ClaimTypes.Role, "Participante")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}

public sealed class LoginInput
{
    [Required(ErrorMessage = "Informe seu email.")]
    [EmailAddress(ErrorMessage = "Informe um email valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe sua senha.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
