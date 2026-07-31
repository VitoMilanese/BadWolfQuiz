using System.Globalization;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services
    .AddRazorPages()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource));
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

builder.Services.AddSignalR();
builder.Services.AddDbContext<QuizDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QuizDatabase")));
builder.Services.AddSingleton<BuzzCoordinator>();
builder.Services.AddSingleton<QuizSnapshotFactory>();
builder.Services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
builder.Services.AddSingleton<GameSessionRegistry>();
builder.Services.AddSingleton<GameSettingsStore>();
builder.Services.AddScoped<GameSessionLauncher>();
builder.Services.AddScoped<GameHistoryStore>();
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
app.UseRouting();
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
