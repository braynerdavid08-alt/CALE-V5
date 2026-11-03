using Cale.Api.Services.Spa;

namespace Cale.UnitTests;

public class SpaHtmlSeoTests
{
    private const string Shell = """
        <!doctype html>
        <html lang="es-CO">
        <head>
          <title>Luz Verde | Plataforma y software para CEA y escuelas de conducción</title>
          <meta name="description" content="Descripcion home">
          <meta name="robots" content="index, follow">
          <link rel="canonical" href="https://www.cealuzverde.com/">
          <meta property="og:title" content="Luz Verde | Plataforma y software para CEA">
          <meta property="og:description" content="OG home">
          <meta property="og:url" content="https://www.cealuzverde.com/">
          <meta name="twitter:title" content="Luz Verde | Plataforma y software para CEA">
          <meta name="twitter:description" content="Twitter home">
        </head>
        <body><app-root></app-root></body>
        </html>
        """;

    [Fact]
    public void Home_keeps_www_canonical()
    {
        var html = SpaHtmlSeo.Rewrite(Shell, "/", SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("href=\"https://www.cealuzverde.com/\"", html);
        Assert.Contains("<title>Luz Verde | Plataforma y software para CEA y escuelas de conducción</title>", html);
        Assert.Contains("name=\"robots\" content=\"index, follow\"", html);
    }

    [Fact]
    public void Nosotros_gets_own_canonical_and_title()
    {
        var html = SpaHtmlSeo.Rewrite(Shell, "/nosotros", SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("href=\"https://www.cealuzverde.com/nosotros\"", html);
        Assert.DoesNotContain("href=\"https://www.cealuzverde.com/\">", html.Replace("og:url", "x"));
        Assert.Contains("<link rel=\"canonical\" href=\"https://www.cealuzverde.com/nosotros\">", html);
        Assert.Contains("<title>Nosotros | Luz Verde — Plataforma para CEA</title>", html);
        Assert.Contains("property=\"og:url\" content=\"https://www.cealuzverde.com/nosotros\"", html);
    }

    [Fact]
    public void Contacto_gets_own_canonical()
    {
        var html = SpaHtmlSeo.Rewrite(Shell, "/contacto", SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("<link rel=\"canonical\" href=\"https://www.cealuzverde.com/contacto\">", html);
        Assert.Contains("<title>Contacto y demo CEA | Luz Verde</title>", html);
    }

    [Fact]
    public void Private_routes_are_noindex_with_self_canonical()
    {
        var html = SpaHtmlSeo.Rewrite(Shell, "/login", SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow\"", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://www.cealuzverde.com/login\">", html);
    }

    [Fact]
    public void Legal_drafts_are_noindex()
    {
        var html = SpaHtmlSeo.Rewrite(Shell, "/legal/terms", SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow\"", html);
        Assert.Contains("href=\"https://www.cealuzverde.com/legal/terms\"", html);
    }

    [Fact]
    public void Unknown_routes_are_noindex_soft_404()
    {
        var html = SpaHtmlSeo.Rewrite(Shell, "/ruta-que-no-existe-xyz", SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow\"", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://www.cealuzverde.com/ruta-que-no-existe-xyz\">", html);
        Assert.Contains("<title>Página no encontrada | Luz Verde</title>", html);
        Assert.DoesNotContain("<title>Luz Verde | Plataforma y software para CEA y escuelas de conducción</title>", html);
    }

    [Theory]
    [InlineData("/", "https://www.cealuzverde.com/")]
    [InlineData("/nosotros", "https://www.cealuzverde.com/nosotros")]
    [InlineData("/contacto", "https://www.cealuzverde.com/contacto")]
    [InlineData("/cursos", "https://www.cealuzverde.com/cursos")]
    [InlineData("/escuelas", "https://www.cealuzverde.com/escuelas")]
    [InlineData("/instructores", "https://www.cealuzverde.com/instructores")]
    [InlineData("/tienda", "https://www.cealuzverde.com/tienda")]
    [InlineData("/tienda/catalogo", "https://www.cealuzverde.com/tienda/catalogo")]
    [InlineData("/blog", "https://www.cealuzverde.com/blog")]
    [InlineData("/apoyar", "https://www.cealuzverde.com/apoyar")]
    [InlineData("/pqr", "https://www.cealuzverde.com/pqr")]
    public void Public_routes_declare_matching_canonical(string path, string canonical)
    {
        var html = SpaHtmlSeo.Rewrite(Shell, path, SpaHtmlSeo.DefaultOrigin);
        Assert.Contains($"<link rel=\"canonical\" href=\"{canonical}\">", html);
        Assert.Contains("name=\"robots\" content=\"index, follow\"", html);
        Assert.Contains($"property=\"og:url\" content=\"{canonical}\"", html);
        Assert.DoesNotContain("name=\"robots\" content=\"noindex, nofollow\"", html);
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/school")]
    [InlineData("/school/dashboard")]
    [InlineData("/admin")]
    [InlineData("/legal/terms")]
    public void Private_routes_are_crawlable_shell_with_noindex(string path)
    {
        var html = SpaHtmlSeo.Rewrite(Shell, path, SpaHtmlSeo.DefaultOrigin);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow\"", html);
        Assert.Contains($"<link rel=\"canonical\" href=\"https://www.cealuzverde.com{path}\">", html);
    }
}

