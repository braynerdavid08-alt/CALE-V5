using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Domain;

namespace Cale.UnitTests;

public class CurriculumTreeTests
{
    [Fact]
    public void Tree_has_seven_nuclei_and_unique_ids()
    {
        var nuclei = CurriculumTree.Nuclei;
        Assert.Equal(7, nuclei.Count);

        var ids = nuclei.Select(n => n.Id)
            .Concat(nuclei.SelectMany(n => n.Themes).Select(t => t.Id))
            .Concat(nuclei.SelectMany(n => n.Themes).SelectMany(t => t.Subtopics).Select(s => s.Id))
            .ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
    }

    [Fact]
    public void Subtopic_names_are_unique_inside_each_theme()
    {
        foreach (var theme in CurriculumTree.Nuclei.SelectMany(n => n.Themes))
        {
            var names = theme.Subtopics.Select(s => s.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void Covered_subtopics_name_a_lesson_and_gaps_do_not()
    {
        var all = CurriculumTree.Nuclei.SelectMany(n => n.Themes).SelectMany(t => t.Subtopics).ToList();
        var covered = all.Where(s => s.LessonTitle is not null).ToList();
        var gaps = all.Where(s => s.LessonTitle is null).ToList();

        Assert.NotEmpty(covered);
        Assert.NotEmpty(gaps);
        Assert.All(covered, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.CourseSlug));
            Assert.False(string.IsNullOrWhiteSpace(s.CourseTitle));
        });
        Assert.All(gaps, s => Assert.Null(s.CourseSlug));
    }

    [Fact]
    public void Classification_accepts_a_real_triple_and_rejects_a_partial_one()
    {
        CurriculumTree.EnsureClassifiable(
            "Movilidad segura y sostenible",
            "Seguridad vial y Sistema Seguro",
            "Enfoque de Sistema Seguro");
        CurriculumTree.EnsureClassifiable(null, "texto libre antiguo", null);

        var ex = Assert.Throws<DomainException>(() =>
            CurriculumTree.EnsureClassifiable("Movilidad segura y sostenible", "Seguridad vial y Sistema Seguro", null));
        Assert.Equal("invalid_curriculum", ex.ErrorCode);
    }
}
