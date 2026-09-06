using System.Xml.Linq;

namespace BadWolfQuiz.Web.Tests;

public sealed class PublicAccountPageRestyleRegressionTests
{
    private static readonly string[] PresentationKeys =
    [
        "HostConsole",
        "LoginKicker",
        "LoginMetaTitle",
        "LoginMetaDescription",
        "LoginMarkPrimary",
        "LoginMarkAccent",
        "LoginMarkFooter",
        "RegisterKicker",
        "RegisterMetaTitle",
        "RegisterMetaDescription",
        "RegisterMarkPrimary",
        "RegisterMarkAccent",
        "RegisterMarkFooter",
        "ForgotKicker",
        "ForgotMetaTitle",
        "RecoveryChannel",
        "ForgotMarkPrimary",
        "ForgotMarkAccent",
        "ForgotMarkFooter",
        "ChangeKicker",
        "ChangeMetaTitle",
        "ChangeMetaDescription",
        "HostSecurity",
        "ChangeMarkPrimary",
        "ChangeMarkAccent",
        "ChangeMarkFooter"
    ];

    [Theory]
    [InlineData("About.cshtml")]
    [InlineData("Faq.cshtml")]
    public void Public_information_pages_use_shared_portal_presentation(string fileName)
    {
        var markup = ReadPage(fileName);

        Assert.Contains("~/css/public-account-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-page", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-content\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("body:has(.portal-page)", markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ChangePassword.cshtml", "BadWolfQuiz.Web.Pages.Account.ChangePasswordModel")]
    [InlineData("ForgotPassword.cshtml", "BadWolfQuiz.Web.Pages.Account.ForgotPasswordModel")]
    [InlineData("Login.cshtml", "BadWolfQuiz.Web.Pages.Account.LoginModel")]
    [InlineData("Register.cshtml", "BadWolfQuiz.Web.Pages.Account.RegisterModel")]
    public void Account_pages_keep_their_correct_page_models_and_post_forms(string fileName, string pageModel)
    {
        var markup = ReadAccountPage(fileName);

        Assert.Contains($"@model {pageModel}", markup, StringComparison.Ordinal);
        Assert.Contains("IStringLocalizer<PublicAccountPagesResource>", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/public-account-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-page auth-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"auth-layout\"", markup, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_keeps_original_branded_meta_copy_through_localization()
    {
        var markup = ReadAccountPage("Login.cshtml");

        Assert.Contains("asp-for=\"Input.RememberMe\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"ForgotPassword\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"Register\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-returnUrl=\"@Model.ReturnUrl\"", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"LoginMetaTitle\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"LoginMetaDescription\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"LoginMarkPrimary\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"LoginMarkAccent\"]", markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">HOST ACCESS<", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("AUTHENTICATION GATEWAY", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_keeps_original_branded_meta_copy_through_localization()
    {
        var markup = ReadAccountPage("Register.cshtml");

        Assert.Contains("asp-for=\"Input.HostName\"", markup, StringComparison.Ordinal);
        Assert.Contains("Account_HostNameHint", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"Login\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-returnUrl=\"@Model.ReturnUrl\"", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"RegisterMetaTitle\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"RegisterMetaDescription\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"RegisterMarkPrimary\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"RegisterMarkAccent\"]", markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">NEW HOST<", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE YOUR BAD WOLF QUIZ HOST PROFILE", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_and_password_change_keep_branded_copy_through_localization()
    {
        var forgot = ReadAccountPage("ForgotPassword.cshtml");
        var change = ReadAccountPage("ChangePassword.cshtml");

        Assert.Contains("PublicAccountLocalizer[\"RecoveryChannel\"]", forgot, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"ForgotMetaTitle\"]", forgot, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"HostSecurity\"]", change, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"ChangeMetaTitle\"]", change, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"ChangeMetaDescription\"]", change, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"auth-meta-list\"", change, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PublicAccountPagesResource.resx", "HOST ACCESS", "NEW HOST", "SECURITY")]
    [InlineData("PublicAccountPagesResource.uk.resx", "ДОСТУП ХОСТА", "НОВИЙ ХОСТ", "БЕЗПЕКА")]
    [InlineData("PublicAccountPagesResource.ru.resx", "Україна", "Україна", "Україна")]
    [InlineData("PublicAccountPagesResource.it.resx", "ACCESSO HOST", "NUOVO HOST", "SICUREZZA")]
    public void Branded_account_copy_is_localized_for_supported_cultures(
        string fileName,
        string loginTitle,
        string registerTitle,
        string securityTitle)
    {
        var values = ReadResource(fileName);

        foreach (var key in PresentationKeys)
        {
            Assert.True(values.ContainsKey(key), $"Missing localization key '{key}' in {fileName}.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"Localization key '{key}' is empty in {fileName}.");
        }

        Assert.Equal(loginTitle, values["LoginMetaTitle"]);
        Assert.Equal(registerTitle, values["RegisterMetaTitle"]);
        Assert.Equal(securityTitle, values["ChangeMetaTitle"]);
    }

    [Fact]
    public void Russian_localization_intentionally_uses_only_ukraine_marker()
    {
        var localizationDirectory = Path.GetDirectoryName(
            FindWebFile("Resources", "Localization", "PublicAccountPagesResource.ru.resx"))!;
        var resourceFiles = Directory.GetFiles(
            localizationDirectory,
            "*.ru.resx",
            SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(resourceFiles);

        foreach (var resourceFile in resourceFiles)
        {
            var document = XDocument.Load(resourceFile);
            var values = document.Root!
                .Elements("data")
                .Select(element => element.Element("value")!.Value)
                .ToArray();

            Assert.NotEmpty(values);
            Assert.All(values, value => Assert.Equal("Україна", value));
        }
    }

    [Fact]
    public void Faq_keeps_all_twelve_questions_and_support_link()
    {
        var markup = ReadPage("Faq.cshtml");

        Assert.Contains("index <= 12", markup, StringComparison.Ordinal);
        Assert.Contains("<details class=\"faq-item\">", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"/AskQuestion\"", markup, StringComparison.Ordinal);
        Assert.Contains("<strong>12</strong>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Not_found_page_uses_shared_branded_presentation_and_existing_localization()
    {
        var markup = ReadPage("NotFound.cshtml");

        Assert.Contains("@model NotFoundModel", markup, StringComparison.Ordinal);
        Assert.Contains("IStringLocalizer<NotFoundResource>", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/public-account-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-page not-found-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-hero not-found-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("<strong>404</strong>", markup, StringComparison.Ordinal);
        Assert.Contains("NotFoundLocalizer[\"Heading\"]", markup, StringComparison.Ordinal);
        Assert.Contains("NotFoundLocalizer[\"Description\"]", markup, StringComparison.Ordinal);
        Assert.Contains("NotFoundLocalizer[\"BackHome\"]", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<style>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_styles_widen_faq_balance_answers_and_cover_404_responsive_accessibility_contracts()
    {
        var styles = File.ReadAllText(FindWebFile("wwwroot", "css", "public-account-pages.css"));

        Assert.StartsWith("body:has(.portal-page)", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("@page", styles, StringComparison.Ordinal);
        Assert.Contains(".portal-page.faq-page {\n    width: 100%;\n    max-width: none;", styles, StringComparison.Ordinal);
        Assert.Contains(".faq-answer {\n    padding: 18px 22px 22px;", styles, StringComparison.Ordinal);
        Assert.Contains(".not-found-page .portal-hero", styles, StringComparison.Ordinal);
        Assert.Contains(".not-found-mark strong", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 900px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadResource(string fileName)
    {
        var document = XDocument.Load(FindWebFile("Resources", "Localization", fileName));
        return document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
    }

    private static string ReadPage(string fileName)
        => File.ReadAllText(FindWebFile("Pages", fileName));

    private static string ReadAccountPage(string fileName)
        => File.ReadAllText(FindWebFile("Pages", "Account", fileName));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
