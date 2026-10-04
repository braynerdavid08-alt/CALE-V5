using System.Text.Json;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Engagement.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cale.Api.Services;

public sealed class HomepageService
{
    public const string PublicCacheKey = "cale.public.homepage.v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomepageService> _logger;

    public HomepageService(
        CaleDbContext db,
        IClock clock,
        IMemoryCache cache,
        ILogger<HomepageService> logger)
    {
        _db = db;
        _clock = clock;
        _cache = cache;
        _logger = logger;
    }

    public void InvalidatePublicCache() => _cache.Remove(PublicCacheKey);

    public async Task<PublicHomeDto> GetPublicHomeAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(PublicCacheKey, out PublicHomeDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var settings = await EnsureSettingsAsync(ct);
            IReadOnlyList<ResolvedStatDto> stats;
            try
            {
                stats = await ResolveStatsAsync(persistComputed: true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Homepage stats could not be resolved.");
                stats = Array.Empty<ResolvedStatDto>();
            }

            IReadOnlyList<PublicSchoolCardDto> schools = Array.Empty<PublicSchoolCardDto>();
            IReadOnlyList<PublicInstructorCardDto> instructors = Array.Empty<PublicInstructorCardDto>();
            try
            {
                if (settings.SchoolsSectionVisible)
                {
                    schools = await ListPublicSchoolsAsync(8, ct);
                }

                if (settings.InstructorsSectionVisible)
                {
                    instructors = await ListPublicInstructorsAsync(8, ct);
                }
            }
            catch (Exception ex)
            {
                // Keep CMS content even if school/instructor cards fail.
                _logger.LogWarning(ex, "Homepage school/instructor cards could not be loaded.");
            }

            var dto = MapPublic(settings, stats, schools, instructors);
            CacheSmall(PublicCacheKey, dto, CacheTtl);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Homepage CMS unavailable; serving emergency landing.");
            // Never touch the DbContext again here — it may be poisoned after a failed SQL.
            var fallback = EmergencyHome();
            CacheSmall(PublicCacheKey, fallback, TimeSpan.FromSeconds(20));
            return fallback;
        }
    }

    // The shared cache has a SizeLimit, so every entry must declare a Size.
    private void CacheSmall<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            _cache.Set(key, value, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not cache {Key}.", key);
        }
    }

    /// <summary>Hard-coded marketing home used when CMS/DB is unavailable.</summary>
    public PublicHomeDto BuildStaticFallback() => EmergencyHome();

    /// <summary>Static fallback that does not touch instance state (safe from any catch).</summary>
    public static PublicHomeDto EmergencyHome() =>
        new(
            new PublicHeroDto(
                true,
                "PLATAFORMA #1 EN FORMACIÓN VIAL",
                "Aprende a conducir de",
                "manera segura y responsable",
                "Practica gratis el simulacro del examen CALE todas las veces que quieras y sigue tu formación con las mejores escuelas e instructores.",
                "Comenzar ahora",
                "/register",
                "Ver escuelas",
                null,
                null,
                null,
                "Luz Verde — formación vial",
                false),
            DefaultBenefits(),
            true,
            "¿Cómo funciona Luz Verde?",
            "Cuatro pasos claros para completar tu formación vial.",
            DefaultSteps(),
            Array.Empty<ResolvedStatDto>(),
            false,
            Array.Empty<PublicSchoolCardDto>(),
            false,
            Array.Empty<PublicInstructorCardDto>(),
            "Luz Verde — Formación que impulsa tu camino.",
            "Luz Verde: practica gratis el simulacro del examen CALE todas las veces que quieras y sigue tu formación vial con tu escuela.",
            "<p><strong>Luz Verde</strong> — Formación que impulsa tu camino.</p>",
            "Pronto publicaremos artículos sobre formación vial.",
            "",
            "",
            DateTime.UtcNow);

    public async Task<AdminHomepageDto> GetAdminAsync(CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var stats = await ResolveStatsAsync(persistComputed: true, ct);
        return MapAdmin(settings, stats);
    }

    public async Task<AdminHomepageDto> SaveAdminAsync(
        UpdateHomepageRequest request,
        int actorUserId,
        CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var before = JsonSerializer.Serialize(MapAdmin(settings, Array.Empty<ResolvedStatDto>()));

        settings.HeroBadge = Clip(request.HeroBadge, 120) ?? settings.HeroBadge;
        settings.HeroTitle = Clip(request.HeroTitle, 200) ?? settings.HeroTitle;
        settings.HeroTitleHighlight = Clip(request.HeroTitleHighlight, 200) ?? settings.HeroTitleHighlight;
        settings.HeroDescription = Clip(request.HeroDescription, 2000) ?? settings.HeroDescription;
        settings.HeroCtaPrimaryLabel = Clip(request.HeroCtaPrimaryLabel, 80) ?? settings.HeroCtaPrimaryLabel;
        settings.HeroCtaPrimaryPath = NormalizePath(request.HeroCtaPrimaryPath) ?? settings.HeroCtaPrimaryPath;
        settings.HeroCtaSecondaryLabel = Clip(request.HeroCtaSecondaryLabel, 80) ?? settings.HeroCtaSecondaryLabel;
        settings.HeroVideoUrl = NormalizeOptionalUrl(request.HeroVideoUrl);
        settings.HeroImageUrl = NormalizeOptionalUrl(request.HeroImageUrl);
        settings.HeroImageUrlMobile = NormalizeOptionalUrl(request.HeroImageUrlMobile);
        settings.HeroImageAlt = Clip(request.HeroImageAlt, 200) ?? settings.HeroImageAlt;
        settings.HeroImageEnabled = request.HeroImageEnabled;
        settings.HeroVisible = request.HeroVisible;
        settings.BenefitsSectionVisible = request.BenefitsSectionVisible;
        settings.StepsSectionVisible = request.StepsSectionVisible;
        settings.StatsSectionVisible = request.StatsSectionVisible;
        settings.SchoolsSectionVisible = request.SchoolsSectionVisible;
        settings.InstructorsSectionVisible = request.InstructorsSectionVisible;
        settings.StepsSectionTitle = Clip(request.StepsSectionTitle, 200) ?? settings.StepsSectionTitle;
        settings.StepsSectionSubtitle = Clip(request.StepsSectionSubtitle, 500) ?? settings.StepsSectionSubtitle;
        settings.SeoTitle = Clip(request.SeoTitle, 200) ?? settings.SeoTitle;
        settings.SeoDescription = Clip(request.SeoDescription, 500) ?? settings.SeoDescription;
        settings.ContactEmail = Clip(request.ContactEmail, 200) ?? settings.ContactEmail;
        settings.ContactPhone = Clip(request.ContactPhone, 80) ?? settings.ContactPhone;
        settings.AboutHtml = Clip(request.AboutHtml, 8000) ?? settings.AboutHtml;
        settings.BlogIntro = Clip(request.BlogIntro, 2000) ?? settings.BlogIntro;

        if (request.Benefits is not null)
        {
            settings.BenefitsJson = JsonSerializer.Serialize(
                request.Benefits.OrderBy(x => x.SortOrder).ToList(),
                JsonOpts);
        }

        if (request.Steps is not null)
        {
            settings.StepsJson = JsonSerializer.Serialize(
                request.Steps.OrderBy(x => x.SortOrder).ToList(),
                JsonOpts);
        }

        settings.UpdatedAt = _clock.UtcNow;
        settings.UpdatedByUserId = actorUserId;

        if (request.Stats is not null)
        {
            var existing = await _db.Set<HomepageStatSetting>().ToListAsync(ct);
            foreach (var incoming in request.Stats)
            {
                var row = existing.FirstOrDefault(x => x.Key == incoming.Key);
                if (row is null)
                {
                    continue;
                }

                var prevMode = row.Mode;
                var prevManual = row.ManualValue;
                var mode = string.Equals(incoming.Mode, HomepageStatModes.Manual, StringComparison.OrdinalIgnoreCase)
                    ? HomepageStatModes.Manual
                    : HomepageStatModes.Auto;

                row.Label = Clip(incoming.Label, 120) ?? row.Label;
                row.SubLabel = Clip(incoming.SubLabel, 120) ?? row.SubLabel;
                row.Icon = Clip(incoming.Icon, 40) ?? row.Icon;
                row.Mode = mode;
                row.ManualValue = mode == HomepageStatModes.Manual
                    ? Clip(incoming.ManualValue, 80)
                    : row.ManualValue;
                row.Visible = incoming.Visible;
                row.SortOrder = incoming.SortOrder;
                row.UpdatedAt = _clock.UtcNow;

                if (prevMode != row.Mode || prevManual != row.ManualValue)
                {
                    _db.Set<HomepageAudit>().Add(new HomepageAudit
                    {
                        ActorUserId = actorUserId,
                        Area = "stat",
                        StatKey = row.Key,
                        PreviousValue = $"{prevMode}:{prevManual}",
                        NewValue = $"{row.Mode}:{row.ManualValue}",
                        Note = Clip(incoming.Note, 500),
                        CreatedAt = _clock.UtcNow
                    });
                }
            }
        }

        _db.Set<HomepageAudit>().Add(new HomepageAudit
        {
            ActorUserId = actorUserId,
            Area = "homepage",
            PreviousValue = before.Length > 180 ? before[..180] : before,
            NewValue = "updated",
            Note = Clip(request.ChangeNote, 500),
            CreatedAt = _clock.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        InvalidatePublicCache();
        return await GetAdminAsync(ct);
    }

    public async Task<IReadOnlyList<PublicSchoolCardDto>> ListPublicSchoolsAsync(
        int take,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var profiles = await _db.Set<SchoolProfile>().AsNoTracking().ToListAsync(ct);
        var active = profiles
            .Where(p => IsListedSchool(p, now))
            .OrderBy(p => p.LegalName)
            .Take(Math.Clamp(take, 1, 50))
            .ToList();

        // Public cards: name + location only (plan and headcount stay private).
        return active.Select(p => new PublicSchoolCardDto(
            p.UserId,
            p.LegalName,
            PublicLocationPart(p.City),
            PublicLocationPart(p.Department),
            "/escuelas")).ToList();
    }

    private static string PublicLocationPart(string value) =>
        value == SchoolProfile.NotRegistered ? "" : value;

    private static bool IsListedSchool(SchoolProfile profile, DateTime now) =>
        profile.CanOperateProduct(now)
        && profile.SubscriptionStatus is not (
            SchoolSubscriptionStatus.Suspended
            or SchoolSubscriptionStatus.Rejected
            or SchoolSubscriptionStatus.Cancelled);

    public async Task<IReadOnlyList<PublicInstructorCardDto>> ListPublicInstructorsAsync(
        int take,
        CancellationToken ct)
    {
        List<User> teachers;
        try
        {
            teachers = await _db.Set<User>().AsNoTracking()
                .Where(u => u.IsActive
                    && u.Role == Roles.Teacher
                    && _db.Set<InstructorListing>().Any(l => l.UserId == u.Id))
                .OrderBy(u => u.Name)
                .Take(Math.Clamp(take, 1, 50))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public instructor directory unavailable.");
            return [];
        }

        var schoolIds = teachers.Where(t => t.SchoolId != null).Select(t => t.SchoolId!.Value).Distinct().ToList();
        var profiles = await _db.Set<SchoolProfile>().AsNoTracking()
            .Where(p => schoolIds.Contains(p.UserId))
            .ToListAsync(ct);
        var names = profiles.ToDictionary(p => p.UserId, p => p.LegalName);

        return teachers.Select(t => new PublicInstructorCardDto(
            t.Id,
            PublicDisplayName(t.Name),
            t.SchoolId is { } sid && names.TryGetValue(sid, out var sn) ? sn : null,
            "/instructores")).ToList();
    }

    public const string DirectoryInviteDedupeKey = "instructor-directory-invite-v1";

    public Task<bool> IsListedInDirectoryAsync(int userId, CancellationToken ct) =>
        _db.Set<InstructorListing>().AsNoTracking().AnyAsync(l => l.UserId == userId, ct);

    public async Task<bool> SetDirectoryListingAsync(int userId, bool listed, CancellationToken ct)
    {
        var row = await _db.Set<InstructorListing>().FirstOrDefaultAsync(l => l.UserId == userId, ct);
        if (listed && row is null)
        {
            _db.Add(InstructorListing.Create(userId, _clock.UtcNow));
        }
        else if (!listed && row is not null)
        {
            _db.Remove(row);
        }

        await _db.SaveChangesAsync(ct);
        InvalidatePublicCache();
        return listed;
    }

    /// <summary>
    /// One invitation per active instructor who is not listed yet. Archived notifications still count,
    /// so a deleted invitation is never sent again.
    /// </summary>
    public async Task<int> InviteInstructorsToDirectoryAsync(INotificationPublisher publisher, CancellationToken ct)
    {
        var pending = await _db.Set<User>().AsNoTracking()
            .Where(u => u.IsActive
                && u.Role == Roles.Teacher
                && !_db.Set<InstructorListing>().Any(l => l.UserId == u.Id)
                && !_db.Set<AppNotification>().Any(n => n.UserId == u.Id && n.DedupeKey == DirectoryInviteDedupeKey))
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (pending.Count == 0)
        {
            return 0;
        }

        await publisher.NotifyUsersAsync(
            pending,
            new NotificationDraft(
                "¿Quieres aparecer en el directorio de instructores?",
                "Ahora puedes elegir si tu nombre y tu escuela se muestran en el directorio público de Luz Verde. Actívalo en Perfil > Preferencias.",
                NotificationTypes.System,
                Link: "/profile?tab=preferences",
                DedupeKey: DirectoryInviteDedupeKey),
            ct);
        return pending.Count;
    }

    /// <summary>Best visible simulator reviews for the public landing (first name + initial only).</summary>
    public async Task<PublicTestimonialsDto> ListPublicTestimonialsAsync(int take, CancellationToken ct)
    {
        const string cacheKey = "cale.public.testimonials.v1";
        if (_cache.TryGetValue(cacheKey, out PublicTestimonialsDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var visible = _db.Set<AttemptRating>().AsNoTracking().Where(r => !r.Hidden);
            var count = await visible.CountAsync(ct);
            var average = count == 0 ? 0 : await visible.AverageAsync(r => (double)r.Stars, ct);

            var rows = await visible
                .Where(r => r.Stars >= 4 && r.Comment != null && r.Comment.Length >= 12)
                .OrderByDescending(r => r.Stars)
                .ThenByDescending(r => r.CreatedAt)
                .Take(Math.Clamp(take, 1, 12))
                .ToListAsync(ct);

            var userIds = rows.Select(r => r.UserId).Distinct().ToList();
            var users = await _db.Set<User>().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.SchoolId })
                .ToListAsync(ct);
            var schoolIds = users.Where(u => u.SchoolId != null).Select(u => u.SchoolId!.Value).Distinct().ToList();
            var schools = await _db.Set<SchoolProfile>().AsNoTracking()
                .Where(p => schoolIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.LegalName })
                .ToListAsync(ct);

            var items = rows.Select(r =>
            {
                var user = users.FirstOrDefault(u => u.Id == r.UserId);
                var school = user?.SchoolId is { } sid ? schools.FirstOrDefault(s => s.UserId == sid)?.LegalName : null;
                return new PublicTestimonialDto(
                    r.Id,
                    user is null ? "Estudiante" : PublicDisplayName(user.Name),
                    school,
                    r.Stars,
                    r.Comment!.Length > 280 ? r.Comment[..277] + "…" : r.Comment,
                    r.CreatedAt);
            }).ToList();

            var dto = new PublicTestimonialsDto(Math.Round(average, 1), count, items);
            CacheSmall(cacheKey, dto, TimeSpan.FromMinutes(5));
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public testimonials could not be loaded.");
            return new PublicTestimonialsDto(0, 0, []);
        }
    }

    private async Task<IReadOnlyList<ResolvedStatDto>> ResolveStatsAsync(
        bool persistComputed,
        CancellationToken ct)
    {
        await EnsureStatsAsync(ct);
        var rows = await _db.Set<HomepageStatSetting>()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var computed = await ComputeRawAsync(ct);
        var changed = false;

        foreach (var row in rows)
        {
            if (!computed.TryGetValue(row.Key, out var raw))
            {
                continue;
            }

            var display = FormatComputed(row.Key, raw);
            if (row.LastComputedValue != raw.Raw || row.LastComputedDisplay != display)
            {
                row.LastComputedValue = raw.Raw;
                row.LastComputedDisplay = display;
                row.LastComputedAt = _clock.UtcNow;
                changed = true;
            }
        }

        if (persistComputed && changed)
        {
            await _db.SaveChangesAsync(ct);
        }

        return rows.Select(row =>
        {
            var isManual = row.Mode == HomepageStatModes.Manual
                && !string.IsNullOrWhiteSpace(row.ManualValue);
            string? value;
            string source;
            if (isManual)
            {
                value = row.ManualValue!.Trim();
                source = HomepageStatModes.Manual;
            }
            else if (!string.IsNullOrWhiteSpace(row.LastComputedDisplay))
            {
                value = row.LastComputedDisplay;
                source = HomepageStatModes.Auto;
            }
            else if (computed.TryGetValue(row.Key, out var raw))
            {
                value = FormatComputed(row.Key, raw);
                source = HomepageStatModes.Auto;
            }
            else
            {
                value = null;
                source = "unavailable";
            }

            return new ResolvedStatDto(
                row.Key,
                row.Label,
                row.SubLabel,
                row.Icon,
                row.Mode,
                row.ManualValue,
                row.LastComputedValue,
                row.LastComputedDisplay,
                value,
                source,
                row.Visible,
                row.SortOrder,
                row.LastComputedAt,
                row.UpdatedAt);
        }).ToList();
    }

    private async Task<Dictionary<string, ComputedRaw>> ComputeRawAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var users = await _db.Set<User>().AsNoTracking().ToListAsync(ct);
        var students = users.Count(u => u.IsActive && Roles.Normalize(u.Role) == Roles.Student);
        var teachers = users.Count(u => u.IsActive && Roles.Normalize(u.Role) == Roles.Teacher);
        var profiles = await _db.Set<SchoolProfile>().AsNoTracking().ToListAsync(ct);
        var schools = profiles.Count(p => IsListedSchool(p, now));

        var ratings = new List<int>();
        try
        {
            ratings = await _db.Set<AttemptRating>().AsNoTracking()
                .Where(r => !r.Hidden)
                .Select(r => r.Stars)
                .ToListAsync(ct);
        }
        catch
        {
            // Valoraciones table may be missing on older DBs; rating stat becomes unavailable.
        }

        var map = new Dictionary<string, ComputedRaw>(StringComparer.OrdinalIgnoreCase)
        {
            [HomepageStatKeys.Students] = new(students.ToString(), students, ratings.Count),
            [HomepageStatKeys.Schools] = new(schools.ToString(), schools, ratings.Count),
            [HomepageStatKeys.Teachers] = new(teachers.ToString(), teachers, ratings.Count),
        };

        if (ratings.Count == 0)
        {
            map[HomepageStatKeys.Rating] = new("none", 0, 0);
        }
        else
        {
            var avg = Math.Round((decimal)ratings.Average(), 1);
            map[HomepageStatKeys.Rating] = new(avg.ToString("0.0"), avg, ratings.Count);
        }

        return map;
    }

    private static string FormatComputed(string key, ComputedRaw raw)
    {
        if (key == HomepageStatKeys.Rating)
        {
            return raw.Raw == "none" ? "Sin valoraciones" : $"{raw.Raw}/5";
        }

        return raw.Raw;
    }

    private async Task<HomepageSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var row = await _db.Set<HomepageSettings>().FirstOrDefaultAsync(ct);
        if (row is not null)
        {
            if (row.ReplaceLegacyBrandDefaults())
            {
                row.UpdatedAt = _clock.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return row;
        }

        row = CreateDefaultSettings();
        _db.Set<HomepageSettings>().Add(row);
        await EnsureStatsAsync(ct);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private async Task EnsureStatsAsync(CancellationToken ct)
    {
        var existing = await _db.Set<HomepageStatSetting>().Select(x => x.Key).ToListAsync(ct);
        var seeds = DefaultStats();
        foreach (var seed in seeds)
        {
            if (existing.Contains(seed.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            seed.UpdatedAt = _clock.UtcNow;
            _db.Set<HomepageStatSetting>().Add(seed);
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private HomepageSettings CreateDefaultSettings() =>
        new()
        {
            Id = 1,
            BenefitsJson = JsonSerializer.Serialize(DefaultBenefits(), JsonOpts),
            StepsJson = JsonSerializer.Serialize(DefaultSteps(), JsonOpts),
            UpdatedAt = _clock.UtcNow
        };

    private static List<HomepageBenefitItem> DefaultBenefits() =>
        new()
        {
            new()
            {
                Title = "Aprende a tu ritmo",
                Description = "Estudia desde donde estés con nuestros contenidos disponibles 24/7.",
                Icon = "graduate",
                Tone = "blue",
                SortOrder = 1
            },
            new()
            {
                Title = "Clases prácticas",
                Description = "Agenda tus clases prácticas con instructores certificados en tu ciudad.",
                Icon = "play",
                Tone = "green",
                SortOrder = 2
            },
            new()
            {
                Title = "Evaluaciones inteligentes",
                Description = "Prepárate y presenta tus evaluaciones teóricas y prácticas.",
                Icon = "exam",
                Tone = "purple",
                SortOrder = 3
            },
            new()
            {
                Title = "Certificación",
                Description = "Cumple con los requisitos y completa tu proceso de formación.",
                Icon = "star",
                Tone = "yellow",
                SortOrder = 4
            }
        };

    private static List<HomepageStepItem> DefaultSteps() =>
        new()
        {
            new()
            {
                Number = 1,
                Title = "Regístrate",
                Description = "Crea tu cuenta y elige la escuela de conducción que más te guste.",
                Icon = "users",
                Tone = "blue",
                SortOrder = 1
            },
            new()
            {
                Number = 2,
                Title = "Estudia",
                Description = "Accede a los contenidos teóricos y prepárate para tus evaluaciones.",
                Icon = "book",
                Tone = "green",
                SortOrder = 2
            },
            new()
            {
                Number = 3,
                Title = "Practica",
                Description = "Agenda tus clases prácticas y desarrolla tus habilidades al volante.",
                Icon = "play",
                Tone = "purple",
                SortOrder = 3
            },
            new()
            {
                Number = 4,
                Title = "Aprueba y obtén tu licencia",
                Description = "Aprueba tus evaluaciones y completa tu proceso de formación.",
                Icon = "star",
                Tone = "yellow",
                SortOrder = 4
            }
        };

    private static List<HomepageStatSetting> DefaultStats() =>
    [
        new()
        {
            Key = HomepageStatKeys.Students,
            Label = "Estudiantes activos",
            SubLabel = "Estudiantes activos",
            Icon = "graduate",
            Mode = HomepageStatModes.Auto,
            SortOrder = 1,
            Visible = true
        },
        new()
        {
            Key = HomepageStatKeys.Schools,
            Label = "Escuelas aliadas",
            SubLabel = "Escuelas aliadas",
            Icon = "building",
            Mode = HomepageStatModes.Auto,
            SortOrder = 2,
            Visible = true
        },
        new()
        {
            Key = HomepageStatKeys.Teachers,
            Label = "Instructores",
            SubLabel = "Instructores activos",
            Icon = "instructor",
            Mode = HomepageStatModes.Auto,
            SortOrder = 3,
            Visible = true
        },
        new()
        {
            Key = HomepageStatKeys.Rating,
            Label = "Valoración de usuarios",
            SubLabel = "Valoración de usuarios",
            Icon = "star",
            Mode = HomepageStatModes.Auto,
            SortOrder = 4,
            Visible = true
        }
    ];

    private PublicHomeDto MapPublic(
        HomepageSettings s,
        IReadOnlyList<ResolvedStatDto> stats,
        IReadOnlyList<PublicSchoolCardDto> schools,
        IReadOnlyList<PublicInstructorCardDto> instructors)
    {
        var benefits = DeserializeBenefits(s.BenefitsJson)
            .Where(x => x.Active)
            .OrderBy(x => x.SortOrder)
            .ToList();
        var steps = DeserializeSteps(s.StepsJson)
            .Where(x => x.Active)
            .OrderBy(x => x.SortOrder)
            .ToList();

        return new PublicHomeDto(
            new PublicHeroDto(
                s.HeroVisible,
                s.HeroBadge,
                s.HeroTitle,
                s.HeroTitleHighlight,
                s.HeroDescription,
                s.HeroCtaPrimaryLabel,
                s.HeroCtaPrimaryPath,
                s.HeroCtaSecondaryLabel,
                s.HeroVideoUrl,
                s.HeroImageEnabled ? s.HeroImageUrl : null,
                s.HeroImageEnabled ? s.HeroImageUrlMobile : null,
                s.HeroImageAlt,
                s.HeroImageEnabled),
            s.BenefitsSectionVisible ? benefits : Array.Empty<HomepageBenefitItem>(),
            s.StepsSectionVisible,
            s.StepsSectionTitle,
            s.StepsSectionSubtitle,
            s.StepsSectionVisible ? steps : Array.Empty<HomepageStepItem>(),
            s.StatsSectionVisible
                ? stats.Where(x => x.Visible).OrderBy(x => x.SortOrder).ToList()
                : Array.Empty<ResolvedStatDto>(),
            s.SchoolsSectionVisible,
            schools,
            s.InstructorsSectionVisible,
            instructors,
            s.SeoTitle,
            s.SeoDescription,
            s.AboutHtml,
            s.BlogIntro,
            s.ContactEmail,
            s.ContactPhone,
            s.UpdatedAt);
    }

    private AdminHomepageDto MapAdmin(HomepageSettings s, IReadOnlyList<ResolvedStatDto> stats) =>
        new(
            s.HeroBadge,
            s.HeroTitle,
            s.HeroTitleHighlight,
            s.HeroDescription,
            s.HeroCtaPrimaryLabel,
            s.HeroCtaPrimaryPath,
            s.HeroCtaSecondaryLabel,
            s.HeroVideoUrl,
            s.HeroImageUrl,
            s.HeroImageUrlMobile,
            s.HeroImageAlt,
            s.HeroImageEnabled,
            s.HeroVisible,
            s.BenefitsSectionVisible,
            s.StepsSectionVisible,
            s.StatsSectionVisible,
            s.SchoolsSectionVisible,
            s.InstructorsSectionVisible,
            s.StepsSectionTitle,
            s.StepsSectionSubtitle,
            DeserializeBenefits(s.BenefitsJson),
            DeserializeSteps(s.StepsJson),
            stats,
            s.SeoTitle,
            s.SeoDescription,
            s.AboutHtml,
            s.BlogIntro,
            s.ContactEmail,
            s.ContactPhone,
            s.UpdatedAt,
            s.UpdatedByUserId);

    private static List<HomepageBenefitItem> DeserializeBenefits(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<HomepageBenefitItem>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<HomepageStepItem> DeserializeSteps(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<HomepageStepItem>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string PublicDisplayName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "Instructor";
        }

        if (parts.Length == 1)
        {
            return parts[0];
        }

        return $"{parts[0]} {parts[^1][0]}.";
    }

    private static string? Clip(string? value, int max)
    {
        if (value is null)
        {
            return null;
        }

        var t = value.Trim();
        if (t.Length == 0)
        {
            return null;
        }

        return t.Length <= max ? t : t[..max];
    }

    private static string? NormalizePath(string? path)
    {
        var t = Clip(path, 200);
        if (t is null)
        {
            return null;
        }

        return t.StartsWith('/') ? t : "/" + t;
    }

    private static string? NormalizeOptionalUrl(string? url)
    {
        var t = Clip(url, 500);
        if (t is null)
        {
            return null;
        }

        if (t.StartsWith('/') || t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return t;
        }

        return "/" + t;
    }

    private record ComputedRaw(string Raw, decimal Numeric, int SampleSize);
}

public sealed record PublicHomeDto(
    PublicHeroDto Hero,
    IReadOnlyList<HomepageBenefitItem> Benefits,
    bool StepsVisible,
    string StepsTitle,
    string StepsSubtitle,
    IReadOnlyList<HomepageStepItem> Steps,
    IReadOnlyList<ResolvedStatDto> Stats,
    bool SchoolsVisible,
    IReadOnlyList<PublicSchoolCardDto> Schools,
    bool InstructorsVisible,
    IReadOnlyList<PublicInstructorCardDto> Instructors,
    string SeoTitle,
    string SeoDescription,
    string AboutHtml,
    string BlogIntro,
    string ContactEmail,
    string ContactPhone,
    DateTime UpdatedAt);

public sealed record PublicHeroDto(
    bool Visible,
    string Badge,
    string Title,
    string TitleHighlight,
    string Description,
    string CtaPrimaryLabel,
    string CtaPrimaryPath,
    string CtaSecondaryLabel,
    string? VideoUrl,
    string? ImageUrl,
    string? ImageUrlMobile,
    string ImageAlt,
    bool ImageEnabled);

public sealed record PublicSchoolCardDto(
    int Id,
    string Name,
    string City,
    string Department,
    string DetailPath);

public sealed record PublicInstructorCardDto(
    int Id,
    string DisplayName,
    string? SchoolName,
    string DetailPath);

public sealed record PublicTestimonialDto(
    int Id,
    string DisplayName,
    string? SchoolName,
    int Stars,
    string Comment,
    DateTime CreatedAt);

public sealed record PublicTestimonialsDto(
    double Average,
    int Count,
    IReadOnlyList<PublicTestimonialDto> Items);

public sealed record ResolvedStatDto(
    string Key,
    string Label,
    string SubLabel,
    string Icon,
    string Mode,
    string? ManualValue,
    string? LastComputedValue,
    string? LastComputedDisplay,
    string? DisplayValue,
    string Source,
    bool Visible,
    int SortOrder,
    DateTime? LastComputedAt,
    DateTime UpdatedAt);

public sealed record AdminHomepageDto(
    string HeroBadge,
    string HeroTitle,
    string HeroTitleHighlight,
    string HeroDescription,
    string HeroCtaPrimaryLabel,
    string HeroCtaPrimaryPath,
    string HeroCtaSecondaryLabel,
    string? HeroVideoUrl,
    string? HeroImageUrl,
    string? HeroImageUrlMobile,
    string HeroImageAlt,
    bool HeroImageEnabled,
    bool HeroVisible,
    bool BenefitsSectionVisible,
    bool StepsSectionVisible,
    bool StatsSectionVisible,
    bool SchoolsSectionVisible,
    bool InstructorsSectionVisible,
    string StepsSectionTitle,
    string StepsSectionSubtitle,
    IReadOnlyList<HomepageBenefitItem> Benefits,
    IReadOnlyList<HomepageStepItem> Steps,
    IReadOnlyList<ResolvedStatDto> Stats,
    string SeoTitle,
    string SeoDescription,
    string AboutHtml,
    string BlogIntro,
    string ContactEmail,
    string ContactPhone,
    DateTime UpdatedAt,
    int? UpdatedByUserId);

public sealed class UpdateHomepageRequest
{
    public string? HeroBadge { get; set; }
    public string? HeroTitle { get; set; }
    public string? HeroTitleHighlight { get; set; }
    public string? HeroDescription { get; set; }
    public string? HeroCtaPrimaryLabel { get; set; }
    public string? HeroCtaPrimaryPath { get; set; }
    public string? HeroCtaSecondaryLabel { get; set; }
    public string? HeroVideoUrl { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? HeroImageUrlMobile { get; set; }
    public string? HeroImageAlt { get; set; }
    public bool HeroImageEnabled { get; set; } = true;
    public bool HeroVisible { get; set; } = true;
    public bool BenefitsSectionVisible { get; set; } = true;
    public bool StepsSectionVisible { get; set; } = true;
    public bool StatsSectionVisible { get; set; } = true;
    public bool SchoolsSectionVisible { get; set; } = true;
    public bool InstructorsSectionVisible { get; set; } = true;
    public string? StepsSectionTitle { get; set; }
    public string? StepsSectionSubtitle { get; set; }
    public List<HomepageBenefitItem>? Benefits { get; set; }
    public List<HomepageStepItem>? Steps { get; set; }
    public List<UpdateHomepageStatRequest>? Stats { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? AboutHtml { get; set; }
    public string? BlogIntro { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ChangeNote { get; set; }
}

public sealed class UpdateHomepageStatRequest
{
    public string Key { get; set; } = "";
    public string? Label { get; set; }
    public string? SubLabel { get; set; }
    public string? Icon { get; set; }
    public string Mode { get; set; } = HomepageStatModes.Auto;
    public string? ManualValue { get; set; }
    public bool Visible { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Note { get; set; }
}
