using Cale.Modules.Engagement.Domain;

namespace Cale.UnitTests.Branding;

public sealed class HomepageBrandTests
{
    [Fact]
    public void Untouched_legacy_texts_are_replaced_and_admin_texts_are_kept()
    {
        var settings = new HomepageSettings
        {
            HeroDescription = "Mi CALE te acompaña en tu CEA: estudia, practica y aprueba con las mejores escuelas e instructores.",
            StepsSectionTitle = "¿Cómo funciona Mi CALE?",
            SeoTitle = "Texto propio del administrador"
        };

        Assert.True(settings.ReplaceLegacyBrandDefaults());

        var fresh = new HomepageSettings();
        Assert.Equal(fresh.HeroDescription, settings.HeroDescription);
        Assert.Equal("¿Cómo funciona Luz Verde?", settings.StepsSectionTitle);
        Assert.Equal("Texto propio del administrador", settings.SeoTitle);
        Assert.False(settings.ReplaceLegacyBrandDefaults());
    }

    [Fact]
    public void Production_mi_cale_stub_and_old_b2c_seo_migrate_to_b2b()
    {
        var settings = new HomepageSettings
        {
            SeoTitle = "Mi CALE",
            SeoDescription =
                "Luz Verde: practica gratis el simulacro del examen CALE todas las veces que quieras y sigue tu formación vial con tu escuela.",
            AboutHtml =
                "<p><strong>Luz Verde</strong> — Formación que impulsa tu camino. Simulacros gratis e ilimitados, formación teórica, práctica y evaluación en un solo lugar, junto a tu centro de enseñanza automovilística.</p>",
            ContactEmail = "contacto@cale.local",
            BlogIntro = "Pronto publicaremos artículos sobre formación vial. Mientras tanto, explora cursos y escuelas."
        };

        Assert.True(settings.ReplaceLegacyBrandDefaults());

        var fresh = new HomepageSettings();
        Assert.Equal(fresh.SeoTitle, settings.SeoTitle);
        Assert.Equal(fresh.SeoDescription, settings.SeoDescription);
        Assert.Equal(fresh.AboutHtml, settings.AboutHtml);
        Assert.Equal(fresh.ContactEmail, settings.ContactEmail);
        Assert.Equal(fresh.BlogIntro, settings.BlogIntro);
        Assert.Equal(
            "Luz Verde | Plataforma para CEA y Escuelas de Conducción en Colombia",
            settings.SeoTitle);
        Assert.False(settings.ReplaceLegacyBrandDefaults());
    }

    [Fact]
    public void Admin_custom_seo_and_about_are_never_overwritten()
    {
        var settings = new HomepageSettings
        {
            SeoTitle = "CEA VIP — título personalizado",
            SeoDescription = "Descripción editada por el administrador de la escuela.",
            AboutHtml = "<p>Texto About personalizado</p>",
            ContactEmail = "admin@escuela.example",
            BlogIntro = "Blog intro personalizado"
        };

        Assert.False(settings.ReplaceLegacyBrandDefaults());
        Assert.Equal("CEA VIP — título personalizado", settings.SeoTitle);
        Assert.Equal("Descripción editada por el administrador de la escuela.", settings.SeoDescription);
        Assert.Equal("<p>Texto About personalizado</p>", settings.AboutHtml);
        Assert.Equal("admin@escuela.example", settings.ContactEmail);
        Assert.Equal("Blog intro personalizado", settings.BlogIntro);
    }

    [Fact]
    public void Default_constructor_uses_b2b_seo_copy()
    {
        var settings = new HomepageSettings();
        Assert.Equal(
            "Luz Verde | Plataforma para CEA y Escuelas de Conducción en Colombia",
            settings.SeoTitle);
        Assert.StartsWith(
            "Luz Verde es una plataforma tecnológica para escuelas de manejo",
            settings.SeoDescription);
        Assert.False(settings.ReplaceLegacyBrandDefaults());
    }
}
