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

public class CadastroModel : PageModel
{
    private readonly BolaoDbContext _db;
    private readonly IPasswordHasher<Participant> _passwordHasher;

    public CadastroModel(BolaoDbContext db, IPasswordHasher<Participant> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [BindProperty]
    public CadastroInput Input { get; set; } = new();

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
        var exists = await _db.Participants.AnyAsync(participant => participant.Email == email || participant.Login == email);
        if (exists)
        {
            StatusMessage = "Ja existe um cadastro com este email.";
            return Page();
        }

        var participant = new Participant
        {
            Name = Input.Name.Trim(),
            Email = email,
            Login = email
        };
        participant.PasswordHash = _passwordHasher.HashPassword(participant, Input.Password);

        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

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

public sealed class CadastroInput
{
    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(100, ErrorMessage = "Use ate 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu email.")]
    [EmailAddress(ErrorMessage = "Informe um email valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe uma senha.")]
    [MinLength(6, ErrorMessage = "Use pelo menos 6 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a senha.")]
    [Compare(nameof(Password), ErrorMessage = "As senhas nao conferem.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
