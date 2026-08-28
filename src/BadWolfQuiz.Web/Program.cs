using System.Globalization;
using System.Threading.RateLimiting;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using BadWolfQuiz.Web.Models;
using Resend;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;
using Discord;
using Discord.WebSocket;

var builder = WebApplication.CreateBuilder(args);

const long maximumImportRequestBodySize = 1100L * 1024 * 1024;
const long maximumMultipartBodySize = 1050L * 1024 * 1024;

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = maximumMultipartBodySize);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/Admin");
        options.Conventions.AuthorizePage("/Admin/MasterGames", "MasterHost");
        options.Conventions.AuthorizePage("/Admin/QuestionInbox", "MasterHost");
        options.Conventions.AuthorizePage("/Admin/Settings/QuestionBot", "MasterHost");

        foreach (var route in SeoRouteCatalog.IndexablePages)
        {
            var template = string.IsNullOrWhiteSpace(route.Path)
                ? SeoRouteCatalog.CultureRouteParameter
                : $"{SeoRouteCatalog.CultureRouteParameter}/{route.Path}";
            options.Conventions.AddPageRoute(route.Page, template);
        }
    })
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
var masterHostId = builder.Configuration["MasterHostId"]?.Trim();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MasterHost", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            !string.IsNullOrWhiteSpace(masterHostId) &&
            string.Equals(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                masterHostId,
                StringComparison.Ordinal));
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddOptions();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(options =>
    options.ApiToken = builder.Configuration["Resend:ApiToken"] ?? string.Empty);
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddScoped<DiscordQuestionSender>();
builder.Services.AddHttpClient<DiscordOAuthService>();
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
    new CultureInfo("ru"),
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
builder.Services.AddOptions<MediaProcessingOptions>()
    .Bind(builder.Configuration.GetSection(MediaProcessingOptions.SectionName))
    .Validate(options => options.IsValid, "Media processing settings are invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<PremiumHostOptions>()
    .Bind(builder.Configuration.GetSection(PremiumHostOptions.SectionName))
    .Validate(options => options.IsValid, "Premium host identifiers are invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<ActiveGameOptions>()
    .Bind(builder.Configuration.GetSection(ActiveGameOptions.SectionName))
    .Validate(options => options.IsValid, "Active game settings are invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<DiscordIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(DiscordIntegrationOptions.SectionName))
    .Validate(options => options.IsValid, "Discord integration settings are invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<DiscordQuestionBotOptions>()
    .Bind(builder.Configuration.GetSection(DiscordQuestionBotOptions.SectionName))
    .Validate(options => options.IsValid, "Discord question bot settings are invalid.")
    .ValidateOnStart();
builder.Services.Configure<FooterOptions>(
    builder.Configuration.GetSection(FooterOptions.SectionName));
builder.Services.Configure<ProjectOptions>(
    builder.Configuration.GetSection(ProjectOptions.SectionName));
builder.Services.Configure<DiscordInviteOptions>(
    builder.Configuration.GetSection(DiscordInviteOptions.SectionName));
builder.Services.AddOptions<MediaArchiveOptions>()
    .Bind(builder.Configuration.GetSection(MediaArchiveOptions.SectionName))
    .Validate(options => options.IsValid, "Media archive settings are invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<UserQuestionOptions>()
    .Bind(builder.Configuration.GetSection(UserQuestionOptions.SectionName))
    .Validate(options => options.IsValid, "User question settings are invalid.")
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
        new RouteDataRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 256 * 1024;
});
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("QuizDatabase")
    ?? "Data Source=Data/BadWolfQuiz.db";
var archiveConnection = builder.Configuration.GetConnectionString("ArchiveConnection")
    ?? "Data Source=Data/BadWolfQuiz.Archive.db";
builder.Services.AddDbContext<QuizDbContext>(options => options.UseSqlite(defaultConnection));
builder.Services.AddDbContext<ArchiveDbContext>(options => options.UseSqlite(
    archiveConnection,
    sqlite => sqlite.MigrationsAssembly(typeof(ArchiveDbContext).Assembly.FullName)
        .MigrationsHistoryTable("__ArchiveMigrationsHistory")));
builder.Services.AddSingleton<IDbContextFactory<QuizDbContext>>(_ =>
    new QuizDbContextFactory(new DbContextOptionsBuilder<QuizDbContext>()
        .UseSqlite(defaultConnection).Options));
builder.Services.AddSingleton<IDbContextFactory<ArchiveDbContext>>(_ =>
    new ArchiveDbContextFactory(new DbContextOptionsBuilder<ArchiveDbContext>()
        .UseSqlite(archiveConnection, sqlite => sqlite
            .MigrationsAssembly(typeof(ArchiveDbContext).Assembly.FullName)
            .MigrationsHistoryTable("__ArchiveMigrationsHistory")).Options));
builder.Services.AddSingleton<BuzzCoordinator>();
builder.Services.AddSingleton<QuizSnapshotFactory>();
builder.Services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
builder.Services.AddSingleton<GameSessionRegistry>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ActiveGameStore>();
builder.Services.AddSingleton<DeferredGameMediaStore>();
builder.Services.AddSingleton<ActiveGameAvailability>();
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
builder.Services.AddScoped<IQuizMediaArchiveService, QuizMediaArchiveService>();
builder.Services.AddScoped<IQuizDeletionService, QuizDeletionService>();
builder.Services.AddScoped<ISqliteVacuumService, SqliteVacuumService>();
builder.Services.AddHostedService<MediaArchiveBackgroundService>();
builder.Services.AddSingleton<MediaUploadProcessor>();
builder.Services.AddSingleton<PremiumHostAccess>();
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
    LogGatewayIntentWarnings = false
}));
builder.Services.AddSingleton<DiscordVoiceGateway>();
builder.Services.AddSingleton<IDiscordVoiceGateway>(provider =>
    provider.GetRequiredService<DiscordVoiceGateway>());
builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<DiscordVoiceGateway>());
builder.Services.AddSingleton<DiscordMuteCoordinator>();
builder.Services.AddHostedService<DiscordMuteTimeoutService>();
builder.Services.AddScoped<DiscordConnectionRepository>();
builder.Services.AddScoped<DiscordQuestionBotSettingsRepository>();
builder.Services.AddScoped<UserQuestionCleanupService>();
builder.Services.AddScoped<UserQuestionDeletionService>();
builder.Services.AddSingleton<UserQuestionHistoryService>();
builder.Services.AddSingleton<DiscordQuestionBotService>();
builder.Services.AddHostedService(
    sp => sp.GetRequiredService<DiscordQuestionBotService>());

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data"));

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

app.UseRouting();
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
                    "/Admin/Quizzes/FinalQuestionEditor") ||
                context.Request.Path.StartsWithSegments("/Admin/Settings") ||
                context.Request.Path.StartsWithSegments("/Admin/Games/Lobby"))
            {
                bodySizeFeature.MaxRequestBodySize = maximumMultipartBodySize;
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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<DeferredGameMediaMiddleware>();

app.MapRazorPages();
app.MapHub<GameHub>("/hubs/game");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
    await DatabaseMigrationService.MigrateAsync(db);

    var archiveDb = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
    await archiveDb.Database.MigrateAsync();

    var questionCleanup = scope.ServiceProvider
        .GetRequiredService<UserQuestionCleanupService>();
    await questionCleanup.CleanupAsync();

    var seed = scope.ServiceProvider.GetRequiredService<QuizSeedService>();
    await seed.SeedAsync();
}

app.Run();
