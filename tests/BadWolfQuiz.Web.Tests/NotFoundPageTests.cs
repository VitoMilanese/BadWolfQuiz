using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Tests;

public sealed class NotFoundPageTests
{
    [Fact]
    public void Page_model_preserves_404_status_and_sets_robots_header()
    {
        var httpContext = new DefaultHttpContext();
        var model = new NotFoundModel
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext
            }
        };

        model.OnGet();

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal(
            NotFoundPagePolicy.RobotsDirective,
            httpContext.Response.Headers["X-Robots-Tag"].ToString());
    }

    [Fact]
    public void Not_found_page_is_not_in_search_discovery()
    {
        Assert.DoesNotContain(
            SeoRouteCatalog.IndexablePages,
            route => string.Equals(
                route.Page,
                NotFoundPagePolicy.Path,
                StringComparison.Ordinal));
        Assert.False(
            SeoMetadataCatalog.IsIndexableRequest(
                NotFoundPagePolicy.Path,
                routeCulture: null,
                uiCulture: "en"));
    }

    [Fact]
    public void Non_indexable_pages_emit_the_required_robots_meta()
    {
        Assert.Equal(
            $"<meta name=\"robots\" content=\"{NotFoundPagePolicy.RobotsDirective}\" />",
            SeoHeadTagHelper.BuildNoIndexMarkup());
    }
}
