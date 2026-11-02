using Cale.Api.Services.Assistant;
using Cale.BuildingBlocks.Domain.Auth;
using Xunit;

namespace Cale.UnitTests.Assistant;

public sealed class AssistantInstantGuidanceTests
{
    [Fact]
    public void School_schedule_without_student_asks_for_name_and_guides_theory()
    {
        var user = new AssistantUser(1, Roles.School, "CEA Demo", 1);
        var reply = AssistantService.TryInstantGuidance(
            user,
            "hola kazu, me puedes agendar una clase de teoria y el examen para el dia lunes?");

        Assert.NotNull(reply);
        Assert.Contains("nombre del estudiante", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Teoría", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Greeting_returns_short_hello()
    {
        var user = new AssistantUser(2, Roles.Student, "Ana", 1);
        var reply = AssistantService.TryInstantGuidance(user, "hola");

        Assert.NotNull(reply);
        Assert.Contains("Kazu", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void School_with_student_name_does_not_block_llm()
    {
        var user = new AssistantUser(1, Roles.School, "CEA Demo", 1);
        var reply = AssistantService.TryInstantGuidance(
            user,
            "Agenda el examen de Ana Pérez el lunes a las 9:00");

        Assert.Null(reply);
    }

    [Fact]
    public void Intent_router_maps_student_next_step_without_llm()
    {
        Assert.Equal(
            "siguiente_paso",
            AssistantService.TryMapIntentToQuickKey(Roles.Student, "¿Cuál es mi siguiente paso?"));
        Assert.Equal(
            "progreso",
            AssistantService.TryMapIntentToQuickKey(Roles.Student, "cuántas horas me faltan"));
        Assert.Equal(
            "que_estudiar",
            AssistantService.TryMapIntentToQuickKey(Roles.Student, "qué debo estudiar para el examen"));
        Assert.Equal(
            "practicar",
            AssistantService.TryMapIntentToQuickKey(Roles.Student, "hazme una pregunta"));
    }

    [Fact]
    public void Intent_router_maps_school_ops_alerts()
    {
        Assert.Equal(
            "alertas",
            AssistantService.TryMapIntentToQuickKey(Roles.School, "¿Qué alertas de demanda tengo?"));
    }

    [Fact]
    public void Intent_router_ignores_open_questions()
    {
        Assert.Null(AssistantService.TryMapIntentToQuickKey(
            Roles.Student,
            "Explícame la diferencia entre una señal reglamentaria y una preventiva en una curva"));
    }
}
