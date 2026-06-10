using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BolaoCopa2026.Pages.Admin;

public class LoginModel : PageModel
{
    private const string DefaultAdminPassword = "Soyuz123";
    private readonly IConfiguration _configuration;

    public LoginModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public AdminLoginInput Input { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToPage("/Admin/Resultados");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!string.Equals(Input.Password, GetAdminPassword(), StringComparison.Ordinal))
        {
            StatusMessage = "Senha de admin invalida.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Administrador"),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToPage("/Admin/Resultados");
    }

    private string GetAdminPassword()
    {
        return _configuration["Admin:Password"] ?? DefaultAdminPassword;
    }
}

public sealed class AdminLoginInput
{
    [Required(ErrorMessage = "Informe a senha de admin.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
