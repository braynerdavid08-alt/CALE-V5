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
}
