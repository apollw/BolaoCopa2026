using BolaoCopa2026.Data;
using BolaoCopa2026.Models;
using BolaoCopa2026.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<BolaoDbContext>(options =>
{
    options.UseNpgsql(GetPostgresConnectionString(builder.Configuration, builder.Environment.EnvironmentName), postgres =>
    {
        postgres.CommandTimeout(120);
        postgres.EnableRetryOnFailure();
    });
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Conta/Login";
        options.LogoutPath = "/Conta/Logout";
        options.AccessDeniedPath = "/Conta/Login";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                context.Response.Redirect("/Admin/Login");
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                context.Response.Redirect("/Admin/Login");
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ParticipantOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.HasClaim(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier)
            && !context.User.IsInRole("Admin"));
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<Participant>, PasswordHasher<Participant>>();
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<AuditImageService>();
builder.Services.AddScoped<BolaoRepository>();

var app = builder.Build();

BolaoSeedData.Initialize(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static string GetPostgresConnectionString(IConfiguration configuration, string environmentName)
{
    var configured = configuration.GetConnectionString("BolaoDb");
    var databaseUrl = Environment.GetEnvironmentVariable("SUPABASE_DATABASE_URL")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL");

    if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:BolaoDb em appsettings.Local.json para rodar localmente.");
        }

        return configured;
    }

    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        return ConvertDatabaseUrl(databaseUrl);
    }

    if (string.IsNullOrWhiteSpace(configured))
    {
        throw new InvalidOperationException("Configure SUPABASE_DATABASE_URL, DATABASE_URL ou ConnectionStrings:BolaoDb para conectar ao PostgreSQL/Supabase.");
    }
    return configured.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
        || configured.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        ? ConvertDatabaseUrl(configured)
        : configured;
}

static string ConvertDatabaseUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.TrimStart('/');

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = database,
        Username = username,
        Password = password,
        SslMode = SslMode.Require
    };

    return builder.ConnectionString;
}
