namespace Cale.Modules.Engagement.Domain;

/// <summary>Singleton-ish homepage CMS row (Id = 1).</summary>
public sealed class HomepageSettings
{
    public int Id { get; set; } = 1;

    public string HeroBadge { get; set; } = "PLATAFORMA #1 EN FORMACIÓN VIAL";
    public string HeroTitle { get; set; } = "Aprende a conducir de";
    public string HeroTitleHighlight { get; set; } = "manera segura y responsable";
    public string HeroDescription { get; set; } =
        "Mi CALE te acompaña en tu CEA: estudia, practica y aprueba con las mejores escuelas e instructores.";
    public string HeroCtaPrimaryLabel { get; set; } = "Comenzar ahora";
    public string HeroCtaPrimaryPath { get; set; } = "/register";
    public string HeroCtaSecondaryLabel { get; set; } = "Ver video";
    public string? HeroVideoUrl { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? HeroImageUrlMobile { get; set; }
    public string HeroImageAlt { get; set; } = "Mi CALE — formación vial";
    public bool HeroImageEnabled { get; set; } = true;
    public bool HeroVisible { get; set; } = true;

    public string BenefitsJson { get; set; } = "[]";
    public string StepsJson { get; set; } = "[]";
    public string StepsSectionTitle { get; set; } = "¿Cómo funciona Mi CALE?";
    public string StepsSectionSubtitle { get; set; } =
        "Cuatro pasos claros para completar tu formación vial.";

    public bool SchoolsSectionVisible { get; set; } = true;
    public bool InstructorsSectionVisible { get; set; } = true;
    public bool StatsSectionVisible { get; set; } = true;
    public bool BenefitsSectionVisible { get; set; } = true;
    public bool StepsSectionVisible { get; set; } = true;

    public string SeoTitle { get; set; } = "Mi CALE — tu CALE, en tu CEA";
    public string SeoDescription { get; set; } =
        "Mi CALE: tu CALE, en tu CEA. Formación vial con tu centro de enseñanza automovilística.";

    public string ContactEmail { get; set; } = "contacto@cale.local";
    public string ContactPhone { get; set; } = "";
    public string AboutHtml { get; set; } =
        "<p><strong>Mi CALE</strong> — tu CALE, en tu CEA. Formación teórica, práctica y evaluación en un solo lugar, junto a tu centro de enseñanza automovilística.</p>";
    public string BlogIntro { get; set; } =
        "Pronto publicaremos artículos sobre formación vial. Mientras tanto, explora cursos y escuelas.";

    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public sealed class HomepageStatSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string SubLabel { get; set; } = "";
    public string Icon { get; set; } = "users";
    public string Mode { get; set; } = HomepageStatModes.Auto;
    public string? ManualValue { get; set; }
    public string? LastComputedValue { get; set; }
    public string? LastComputedDisplay { get; set; }
    public bool Visible { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime? LastComputedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class HomepageStatModes
{
    public const string Auto = "Auto";
    public const string Manual = "Manual";
}

public static class HomepageStatKeys
{
    public const string Students = "students";
    public const string Schools = "schools";
    public const string Teachers = "teachers";
    public const string Rating = "rating";
}

public sealed class HomepageAudit
{
    public long Id { get; set; }
    public int ActorUserId { get; set; }
    public string Area { get; set; } = "";
    public string? StatKey { get; set; }
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class HomepageBenefitItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "book";
    public string Tone { get; set; } = "blue";
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class HomepageStepItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "users";
    public string Tone { get; set; } = "blue";
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}
