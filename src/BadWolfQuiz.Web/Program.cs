using System.Globalization;
using System.Threading.RateLimiting;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using BadWolfQuiz.Web.Models;
using Resend;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services
    .AddRazorPages(options => options.Conventions.AuthorizeFolder("/Admin"))
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource));
    });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(options =>
    options.ApiToken = builder.Configuration["Resend:ApiToken"] ?? string.Empty);
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddHttpClient<DiscordQuestionSender>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("discord-questions", httpContext =>
        !HttpMethods.IsPost(httpContext.Request.Method)
            ? RateLimitPartition.GetNoLimiter("discord-questions-read")
            : RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("uk"),
    new CultureInfo("it")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");

    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider()
    };
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 256 * 1024;
});
builder.Services.AddDbContext<QuizDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QuizDatabase")));
builder.Services.AddSingleton<BuzzCoordinator>();
builder.Services.AddSingleton<QuizSnapshotFactory>();
builder.Services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
builder.Services.AddSingleton<GameSessionRegistry>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ActiveGameStore>();
builder.Services.AddHostedService<ActiveGamePersistenceService>();
builder.Services.AddSingleton<GameSettingsStore>();
builder.Services.AddSingleton<AvatarCatalog>();
builder.Services.AddScoped<GameSessionLauncher>();
builder.Services.AddScoped<GameHistoryStore>();
builder.Services.AddScoped<CurrentHost>();
builder.Services.AddScoped<HostAccountService>();
builder.Services.AddScoped<PasswordResetEmailSender>();
builder.Services.AddScoped<JoinUrlBuilder>();
builder.Services.AddScoped<IPasswordHasher<HostAccount>, PasswordHasher<HostAccount>>();
builder.Services.AddScoped<QuizSeedService>();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

var localizationOptions = app.Services
    .GetRequiredService<IOptions<RequestLocalizationOptions>>()
    .Value;

app.UseRequestLocalization(localizationOptions);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        AvatarCatalog.ResolveRootPath(app.Environment)),
    RequestPath = "/avatars",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl =
            "no-store, no-cache, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<GameHub>("/hubs/game");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
    await DatabaseMigrationService.MigrateAsync(db);

    var seed = scope.ServiceProvider.GetRequiredService<QuizSeedService>();
    await seed.SeedAsync();
}

app.Run();
