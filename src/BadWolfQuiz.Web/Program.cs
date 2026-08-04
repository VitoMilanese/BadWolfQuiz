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
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

const long maximumImportRequestBodySize = 1100L * 1024 * 1024;
const long maximumQuestionEditorRequestBodySize = 128L * 1024 * 1024;
const long maximumMultipartBodySize = 1050L * 1024 * 1024;

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = maximumMultipartBodySize);

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

builder.Services.AddOptions<QuizEditorOptions>()
    .Bind(builder.Configuration.GetSection(QuizEditorOptions.SectionName))
    .Validate(options => options.IsValid, "Quiz editor limits are invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<SiteDefaultsOptions>()
    .Bind(builder.Configuration.GetSection(SiteDefaultsOptions.SectionName))
    .Validate(
        options => supportedCultures.Any(culture =>
            string.Equals(
                culture.Name,
                options.Culture,
                StringComparison.OrdinalIgnoreCase)),
        "The default site culture is not supported.")
    .Validate(
        options => SiteThemeCatalog.IsValid(options.ThemeId),
        "The default site theme is invalid.")
    .ValidateOnStart();

var defaultCulture = builder.Configuration[
    $"{SiteDefaultsOptions.SectionName}:{nameof(SiteDefaultsOptions.Culture)}"] ?? "en";

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(defaultCulture);

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
builder.Services.AddSingleton<CrashLog>();
builder.Services.AddHostedService<ActiveGamePersistenceService>();
builder.Services.AddSingleton<GameSettingsStore>();
builder.Services.AddSingleton<AvatarCatalog>();
builder.Services.AddScoped<GameSessionLauncher>();
builder.Services.AddScoped<GameHistoryStore>();
builder.Services.AddScoped<PlayerStatisticsService>();
builder.Services.AddScoped<CurrentHost>();
builder.Services.AddScoped<HostAccountService>();
builder.Services.AddScoped<PasswordResetEmailSender>();
builder.Services.AddScoped<JoinUrlBuilder>();
builder.Services.AddScoped<IPasswordHasher<HostAccount>, PasswordHasher<HostAccount>>();
builder.Services.AddScoped<QuizSeedService>();
builder.Services.AddScoped<QuizPackageService>();
builder.Services.AddScoped<QuizRatingService>();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));

var crashLog = app.Services.GetRequiredService<CrashLog>();
AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
    crashLog.Write(
        $"Unhandled process exception; terminating={eventArgs.IsTerminating}",
        eventArgs.ExceptionObject as Exception);
TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    crashLog.Write("Unobserved task exception", eventArgs.Exception);
    eventArgs.SetObserved();
};
app.Lifetime.ApplicationStopping.Register(() =>
    crashLog.Write("Application stopping"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

var localizationOptions = app.Services
    .GetRequiredService<IOptions<RequestLocalizationOptions>>()
    .Value;

app.UseRequestLocalization(localizationOptions);
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method))
    {
        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            if (context.Request.Path == "/Admin/Quizzes" &&
                string.Equals(context.Request.Query["handler"], "Import", StringComparison.OrdinalIgnoreCase))
            {
                bodySizeFeature.MaxRequestBodySize = maximumImportRequestBodySize;
            }
            else if (context.Request.Path.StartsWithSegments(
                "/Admin/Quizzes/QuestionEditor") ||
                context.Request.Path.StartsWithSegments(
                    "/Admin/Quizzes/FinalQuestionEditor"))
            {
                bodySizeFeature.MaxRequestBodySize = maximumQuestionEditorRequestBodySize;
            }
        }
    }

    await next(context);
});
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception exception)
    {
        crashLog.Write(
            $"Unhandled HTTP exception: {context.Request.Method} " +
            $"{context.Request.Path}{context.Request.QueryString}",
            exception);
        throw;
    }
});
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
