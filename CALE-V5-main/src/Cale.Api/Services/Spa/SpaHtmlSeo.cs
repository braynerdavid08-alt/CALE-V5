using System.Text;
using System.Text.RegularExpressions;

namespace Cale.Api.Services.Spa;

/// <summary>
/// Rewrites the Angular index.html shell so crawlers see the correct
/// <c>rel=canonical</c>, title, description and robots <b>without JavaScript</b>.
/// Client-side <c>SeoService</c> still updates tags on SPA navigations.
/// </summary>
public static partial class SpaHtmlSeo
{
    public const string DefaultOrigin = "https://www.cealuzverde.com";

    private static readonly object IndexGate = new();
    private static string? CachedIndexHtml;
    private static string? CachedIndexPath;
    private static long CachedIndexTicks;

    private static readonly string[] NoIndexPrefixes =
    [
        "/login",
        "/register",
        "/register-teacher",
        "/register-school",
        "/verify-email",
        "/school",
        "/teacher",
        "/student",
        "/admin",
        "/live",
        "/game-show",
        "/autorizacion-menor",
        "/mis-pqr",
        "/solicitudes",
        "/donaciones",
        "/legal-pending",
        "/legal"
    ];

    /// <summary>Public marketing pages — keep in sync with frontend/src/app/core/seo/page-seo.ts.</summary>
    private static readonly Dictionary<string, PageMeta> PublicPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = new(
            "Luz Verde | Plataforma y software para CEA y escuelas de conducción",
            "Luz Verde es la plataforma digital para Centros de Enseñanza Automovilística en Colombia: gestión de estudiantes e instructores, agenda de clases, exámenes y formación con simulacro teórico."),
        ["/nosotros"] = new(
            "Nosotros | Luz Verde — Plataforma para CEA",
            "Conoce Luz Verde: software y plataforma tecnológica para que las escuelas de conducción (CEA) operen, formen y administren su operación en un solo lugar."),
        ["/cursos"] = new(
            "Cursos y malla vial | Luz Verde",
            "Malla de formación vial en Luz Verde (señales, normas, vía, vehículo y categorías A2, B1 y C1) para que tu CEA forme conductores dentro de la plataforma."),
        ["/escuelas"] = new(
            "Escuelas CEA en Luz Verde | Directorio",
            "Escuelas de conducción y CEA que usan Luz Verde para gestionar estudiantes, agenda y formación digital."),
        ["/instructores"] = new(
            "Instructores | Luz Verde",
            "Directorio de instructores en Luz Verde: apoyo a la formación vial y solicitud de clases particulares a través de la plataforma."),
        ["/tienda"] = new(
            "Tienda | Equipo de protección — Luz Verde",
            "Cascos y equipo de protección para conductores. Complemento de la plataforma Luz Verde para escuelas CEA y aprendices."),
        ["/tienda/catalogo"] = new(
            "Catálogo de tienda | Luz Verde",
            "Catálogo de productos de protección vial disponibles en la tienda de Luz Verde."),
        ["/blog"] = new(
            "Blog | Luz Verde — Gestión CEA y formación vial",
            "Artículos y novedades sobre plataforma para CEA, gestión de escuelas de conducción y formación vial en Colombia."),
        ["/contacto"] = new(
            "Contacto y demo CEA | Luz Verde",
            "Contacta al equipo de Luz Verde para conocer la plataforma de gestión para tu escuela de conducción o CEA en Colombia."),
        ["/apoyar"] = new(
            "Apoyar a Luz Verde | Donaciones",
            "Cómo apoyar a Luz Verde: la plataforma de formación y gestión para CEA y aprendices se sostiene con aportes voluntarios."),
        ["/pqr"] = new(
            "PQR y datos personales | Luz Verde",
            "Canal de PQR y derechos del titular en Luz Verde: peticiones, quejas, reclamos y solicitudes sobre datos personales.")
    };

    public static string ResolveOrigin(IConfiguration configuration)
    {
        var configured = configuration["PublicAppUrl"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultOrigin;
        }

        var origin = configured.Trim().TrimEnd('/');
        // Prefer www canonical even if env still points at apex or onrender during cutover.
        if (origin.Equals("https://cealuzverde.com", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultOrigin;
        }

        return origin;
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var value = path.Split('?', 2)[0].Split('#', 2)[0];
        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (value.Length > 1 && value.EndsWith('/'))
        {
            value = value.TrimEnd('/');
        }

        return string.IsNullOrEmpty(value) ? "/" : value;
    }

    public static bool IsNoIndexPath(string path) =>
        NoIndexPrefixes.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));

    public static string CanonicalUrl(string origin, string path) =>
        path == "/" ? origin + "/" : origin + path;

    public static string Rewrite(string html, string requestPath, string origin)
    {
        var path = NormalizePath(requestPath);
        var canonical = CanonicalUrl(origin, path);
        var noIndex = IsNoIndexPath(path);

        string title;
        string description;
        if (PublicPages.TryGetValue(path, out var meta))
        {
            title = meta.Title;
            description = meta.Description;
        }
        else if (path.StartsWith("/tienda/", StringComparison.OrdinalIgnoreCase))
        {
            // Public product URLs under the shop.
            title = "Tienda | Luz Verde";
            description = PublicPages["/tienda"].Description;
        }
        else if (!noIndex)
        {
            // Soft-404 SPA shell: must not look like an indexable marketing page.
            noIndex = true;
            title = "Página no encontrada | Luz Verde";
            description = "La página solicitada no forma parte del sitio público de Luz Verde.";
        }
        else
        {
            title = "Luz Verde";
            description = PublicPages["/"].Description;
        }

        var robots = noIndex ? "noindex, nofollow" : "index, follow";

        html = ReplaceTitle(html, title);
        html = ReplaceMetaByName(html, "description", description);
        html = ReplaceMetaByName(html, "robots", robots);
        html = ReplaceCanonical(html, canonical);
        html = ReplaceMetaByProperty(html, "og:title", title);
        html = ReplaceMetaByProperty(html, "og:description", description);
        html = ReplaceMetaByProperty(html, "og:url", canonical);
        html = ReplaceMetaByName(html, "twitter:title", title);
        html = ReplaceMetaByName(html, "twitter:description", description);
        return html;
    }

    public static async Task WriteIndexAsync(
        HttpContext context,
        IWebHostEnvironment env,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var origin = ResolveOrigin(configuration);
        var wwwroot = env.WebRootPath
            ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var indexPath = Path.Combine(wwwroot, "index.html");
        if (!File.Exists(indexPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("index.html not found", cancellationToken);
            return;
        }

        var raw = await LoadIndexHtmlAsync(indexPath, cancellationToken);
        var html = Rewrite(raw, context.Request.Path.Value ?? "/", origin);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Remove("ETag");
        context.Response.Headers.Remove("Last-Modified");
        await context.Response.WriteAsync(html, Encoding.UTF8, cancellationToken);
    }

    private static async Task<string> LoadIndexHtmlAsync(string indexPath, CancellationToken cancellationToken)
    {
        var ticks = new FileInfo(indexPath).LastWriteTimeUtc.Ticks;
        lock (IndexGate)
        {
            if (CachedIndexHtml is not null
                && CachedIndexPath == indexPath
                && CachedIndexTicks == ticks)
            {
                return CachedIndexHtml;
            }
        }

        var html = await File.ReadAllTextAsync(indexPath, Encoding.UTF8, cancellationToken);
        lock (IndexGate)
        {
            CachedIndexHtml = html;
            CachedIndexPath = indexPath;
            CachedIndexTicks = ticks;
        }

        return html;
    }

    private static string ReplaceTitle(string html, string title) =>
        TitleRegex().Replace(html, $"<title>{Escape(title)}</title>", 1);

    private static string ReplaceCanonical(string html, string href)
    {
        if (CanonicalRegex().IsMatch(html))
        {
            return CanonicalRegex().Replace(html, $"<link rel=\"canonical\" href=\"{Escape(href)}\">", 1);
        }

        // Ensure a canonical exists even if index.html lost it.
        return HeadCloseRegex().Replace(html, $"  <link rel=\"canonical\" href=\"{Escape(href)}\">\n</head>", 1);
    }

    private static string ReplaceMetaByName(string html, string name, string content)
    {
        var pattern = new Regex(
            $@"<meta\s+name=""{Regex.Escape(name)}""\s+content=""[^""]*""\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var replacement = $"<meta name=\"{name}\" content=\"{Escape(content)}\">";
        if (pattern.IsMatch(html))
        {
            return pattern.Replace(html, replacement, 1);
        }

        return html;
    }

    private static string ReplaceMetaByProperty(string html, string property, string content)
    {
        var pattern = new Regex(
            $@"<meta\s+property=""{Regex.Escape(property)}""\s+content=""[^""]*""\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var replacement = $"<meta property=\"{property}\" content=\"{Escape(content)}\">";
        if (pattern.IsMatch(html))
        {
            return pattern.Replace(html, replacement, 1);
        }

        return html;
    }

    private static string Escape(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private readonly record struct PageMeta(string Title, string Description);

    [GeneratedRegex(@"<title>[^<]*</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<link\s+rel=""canonical""\s+href=""[^""]*""\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex(@"</head>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadCloseRegex();
}
