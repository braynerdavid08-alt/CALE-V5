using System.Xml.Linq;

namespace Cale.UnitTests.Seo;

public sealed class StaticSeoAssetsTests
{
    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Nested checkout: .../CALE-V5-main/CALE-V5-main/...
            var nested = Path.Combine(new[] { dir.FullName, "CALE-V5-main" }.Concat(relativeParts).ToArray());
            if (File.Exists(nested))
            {
                return nested;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "SEO asset not found: " + string.Join('/', relativeParts));
    }

    [Fact]
    public void Robots_txt_allows_html_routes_and_blocks_only_internals()
    {
        var path = FindRepoFile("frontend", "public", "robots.txt");
        var text = File.ReadAllText(path);

        Assert.Contains("User-agent: *", text, StringComparison.Ordinal);
        Assert.Contains("Allow: /", text, StringComparison.Ordinal);
        Assert.Contains("Disallow: /api/", text, StringComparison.Ordinal);
        Assert.Contains("Disallow: /hubs/", text, StringComparison.Ordinal);
        Assert.Contains("Disallow: /uploads/", text, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://www.cealuzverde.com/sitemap.xml", text, StringComparison.Ordinal);

        // Private HTML must remain crawlable so Google can read meta noindex.
        Assert.DoesNotContain("Disallow: /login", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disallow: /contacto", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disallow: /school", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disallow: /legal", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disallow: /\n", text.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("Disallow: /\r", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sitemap_lists_exactly_the_public_www_urls()
    {
        var path = FindRepoFile("frontend", "public", "sitemap.xml");
        var doc = XDocument.Load(path);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = doc.Root!
            .Elements(ns + "url")
            .Select(u => (string?)u.Element(ns + "loc"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        string[] expected =
        [
            "https://www.cealuzverde.com/",
            "https://www.cealuzverde.com/nosotros",
            "https://www.cealuzverde.com/para-cea",
            "https://www.cealuzverde.com/estudiantes",
            "https://www.cealuzverde.com/simulacro-cale",
            "https://www.cealuzverde.com/cursos",
            "https://www.cealuzverde.com/escuelas",
            "https://www.cealuzverde.com/instructores",
            "https://www.cealuzverde.com/tienda",
            "https://www.cealuzverde.com/tienda/catalogo",
            "https://www.cealuzverde.com/blog",
            "https://www.cealuzverde.com/contacto",
            "https://www.cealuzverde.com/apoyar",
            "https://www.cealuzverde.com/pqr"
        ];

        Assert.Equal(expected.Length, locs.Count);
        Assert.Equal(expected.OrderBy(x => x), locs.OrderBy(x => x));
        Assert.All(locs, loc =>
        {
            Assert.StartsWith("https://www.cealuzverde.com", loc, StringComparison.Ordinal);
            Assert.DoesNotContain("login", loc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/school", loc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/api/", loc, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(locs.Count, locs.Distinct(StringComparer.Ordinal).Count());
    }
}
