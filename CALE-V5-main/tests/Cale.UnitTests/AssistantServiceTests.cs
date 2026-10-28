using System.Text.Json.Nodes;
using Cale.Api.Services.Assistant;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.TheoreticalTraining.Domain;
using Cale.UnitTests.Assistant;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests;

public sealed class AssistantServiceTests
{
    private static readonly AssistantUser Student = new(10, Roles.Student, "Ana", 1);
    private static readonly AssistantUser OtherStudent = new(11, Roles.Student, "Juan", 1);

    [Fact]
    public async Task Write_tool_is_only_prepared_and_runs_once_after_owner_confirms()
    {
        var llm = new ScriptedLlm(
            new LlmReply(null, [new LlmToolCall("c1", "reservar_clase_teorica", "{\"clase_id\":5}")]),
            new LlmReply("Revisa y confirma la reserva.", []));
        var toolbox = new FakeToolbox();
        var service = Create(llm, toolbox);

        var result = await service.ChatAsync(Student, [new("user", "Resérvame la clase 5")], default);

        Assert.NotNull(result.Action);
        Assert.Equal("Reservar clase 5", result.Action!.Title);
        Assert.Equal(0, toolbox.Executed);
        Assert.Contains("PREPARADA", llm.LastToolResult);

        var stranger = await service.ConfirmAsync(OtherStudent, result.Action.Id, default);
        Assert.False(stranger.Ok);
        Assert.Equal(0, toolbox.Executed);

        var ok = await service.ConfirmAsync(Student, result.Action.Id, default);
        Assert.True(ok.Ok);
        Assert.Equal(1, toolbox.Executed);

        var again = await service.ConfirmAsync(Student, result.Action.Id, default);
        Assert.False(again.Ok);
        Assert.Equal(1, toolbox.Executed);
    }

    [Fact]
    public async Task Daily_user_limit_stops_before_calling_the_provider()
    {
        var llm = new ScriptedLlm(new LlmReply("Hola", []));
        var service = Create(llm, new FakeToolbox(), o => o.DailyMessagesPerUser = 1);

        var first = await service.ChatAsync(Student, [new("user", "Hola")], default);
        var second = await service.ChatAsync(Student, [new("user", "Hola otra vez")], default);

        Assert.Equal("Hola", first.Reply);
        Assert.Equal(0, second.RemainingToday);
        Assert.Equal(1, llm.Calls);
    }

    [Fact]
    public async Task Status_is_disabled_without_api_key_or_for_other_roles()
    {
        var noKey = Create(new ScriptedLlm(), new FakeToolbox(), o => o.ApiKey = null);
        Assert.False((await noKey.StatusAsync(Student, default)).Enabled);

        var withKey = Create(new ScriptedLlm(), new FakeToolbox());
        Assert.True((await withKey.StatusAsync(Student, default)).Enabled);
        Assert.False((await withKey.StatusAsync(new AssistantUser(1, Roles.Admin, "Admin", null), default)).Enabled);
    }

    [Fact]
    public void Students_and_schools_get_separate_tools()
    {
        var toolbox = new AssistantToolbox(null!, null!, null!, null!, null!, null!, null!);
        var student = toolbox.ToolsFor(Roles.Student).Select(t => t.Name).ToList();
        var school = toolbox.ToolsFor(Roles.School).Select(t => t.Name).ToList();

        Assert.Contains("reservar_clase_teorica", student);
        Assert.DoesNotContain("autorizar_examen", student);
        Assert.Contains("autorizar_manejo", school);
        Assert.DoesNotContain("reservar_clase_manejo", school);
        Assert.Empty(toolbox.ToolsFor(Roles.Admin));
    }

    [Fact]
    public void Parses_tool_calls_from_openai_compatible_response()
    {
        const string json = """
            {"choices":[{"message":{"role":"assistant","content":null,
              "tool_calls":[{"id":"call_1","type":"function","function":{"name":"mi_progreso","arguments":"{}"}}]}}]}
            """;

        var reply = AssistantLlmClient.Parse(json);

        Assert.Null(reply.Content);
        var call = Assert.Single(reply.ToolCalls);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("mi_progreso", call.Name);
    }

    [Fact]
    public async Task Gemini_thought_signature_is_sent_back_with_the_tool_call()
    {
        const string json = """
            {"choices":[{"message":{"role":"assistant","content":null,
              "tool_calls":[{"id":"call_1","type":"function","function":{"name":"mi_progreso","arguments":"{}"},
                "extra_content":{"google":{"thought_signature":"sig123"}}}]}}]}
            """;
        var llm = new ScriptedLlm(AssistantLlmClient.Parse(json), new LlmReply("Te faltan 10 horas.", []));
        var service = Create(llm, new FakeToolbox());

        var result = await service.ChatAsync(Student, [new("user", "¿Cuántas horas me faltan?")], default);

        Assert.Equal("Te faltan 10 horas.", result.Reply);
        var echoed = llm.LastMessages!
            .Select(m => m?["tool_calls"]?[0])
            .First(c => c is not null);
        Assert.Equal("sig123", echoed!["extra_content"]!["google"]!["thought_signature"]!.GetValue<string>());
    }

    [Fact]
    public async Task Quick_questions_skip_the_provider_and_spend_no_messages()
    {
        var llm = new ScriptedLlm();
        var service = Create(llm, new FakeToolbox(), o => o.DailyMessagesPerUser = 5);

        var result = await service.QuickAsync(Student, "progreso", default);

        Assert.StartsWith("Horas 10/20", result.Reply);
        Assert.Equal(5, result.RemainingToday);
        Assert.Equal(0, llm.Calls);
        await Assert.ThrowsAsync<Cale.BuildingBlocks.Domain.Exceptions.DomainException>(
            () => service.QuickAsync(Student, "resumen", default));
    }

    [Fact]
    public void Practical_requirements_match_license_categories()
    {
        Assert.Equal(15, PracticalLessonRequirements.GetRequired("A2"));
        Assert.Equal(14, PracticalLessonRequirements.GetPracticeRequired("A2"));
        Assert.Equal(20, PracticalLessonRequirements.GetRequired("B1"));
        Assert.Equal(19, PracticalLessonRequirements.GetPracticeRequired("B1"));
        Assert.Equal(30, PracticalLessonRequirements.GetRequired("C1"));
        Assert.Equal(29, PracticalLessonRequirements.GetPracticeRequired("C1"));
    }

    [Fact]
    public void Theory_week_ui_exposes_bookable_session_ids()
    {
        var week = new Cale.Modules.TheoreticalTraining.Application.DTOs.TheoryWeekScheduleDto(
            new DateOnly(2026, 10, 27),
            new DateOnly(2026, 11, 2),
            [
                new(
                    11,
                    new DateOnly(2026, 10, 28),
                    "08:00",
                    "09:59",
                    1,
                    "Normas",
                    "#0f0",
                    1,
                    "Aula 1",
                    20,
                    0,
                    20,
                    "Scheduled",
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddHours(2),
                    "can_reserve",
                    "20 cupos disponibles",
                    null,
                    null)
            ],
            [],
            "Weekday");

        var ui = AssistantToolbox.BuildTheoryWeekUi(week, fullList: true);

        Assert.Equal("theory_week", ui.Type);
        Assert.True(ui.Selectable);
        Assert.Equal(11, ui.Days[0].AvailableSlots[0].SessionId);
        Assert.Equal("reservar_clase_teorica", ui.Days[0].AvailableSlots[0].BookTool);
    }

    [Fact]
    public void Student_exam_ui_lists_only_slots_with_capacity()
    {
        var dto = new Cale.Modules.TheoreticalTraining.Application.DTOs.StudentExamAvailabilityDto(
            From: "2026-10-20",
            To: "2026-10-27",
            CanBook: true,
            BlockReason: null,
            MyBooking: null,
            MinCancelHours: 24,
            Days:
            [
                new("2026-10-22",
                [
                    new("2026-10-22", "09:00", 2, "Available"),
                    new("2026-10-22", "10:00", 0, "Full")
                ]),
                new("2026-10-23",
                [
                    new("2026-10-23", "11:00", 0, "Full")
                ])
            ]);

        var ui = AssistantToolbox.BuildStudentExamUi(dto);

        Assert.Equal("exam_availability", ui.Type);
        Assert.True(ui.Selectable);
        Assert.Single(ui.Days);
        Assert.Equal("2026-10-22", ui.Days[0].Date);
        Assert.Single(ui.Days[0].AvailableSlots);
        Assert.Equal("09:00", ui.Days[0].AvailableSlots[0].Time);
        Assert.Equal(2, ui.Days[0].AvailableSlots[0].AvailableCapacity);
    }

    [Fact]
    public void Clean_for_user_hides_internal_ids()
    {
        const string text = """
            clase_id=5 | lunes 2026-10-05 07:00-09:00 | Normas | cupos 3/20 | YA RESERVADA reserva_id=9
            Listos para examen: Ana (estudiante_id=12), Juan (estudiante_id=13)
            Examen teórico: autorizado=sí, aprobado=no.
            Cita de examen cita_id=4: 2026-10-05 11:00.
            """;

        var clean = AssistantService.CleanForUser(text);

        Assert.DoesNotContain("_id", clean);
        Assert.Contains("lunes 2026-10-05 07:00-09:00 | Normas | cupos 3/20", clean);
        Assert.Contains("ya la tienes reservada", clean);
        Assert.Contains("Listos para examen: Ana, Juan", clean);
        Assert.Contains("autorizado: sí, aprobado: no", clean);
        Assert.Contains("Cita de examen: 2026-10-05 11:00.", clean);
    }

    [Fact]
    public void Finalize_reply_strips_ids_from_formatted_lists()
    {
        const string text = """
            **Agenda**
            - **18:00 – 20:00** · Clase teórica
              - Instructor: sin asignar
              - (clase_id=12)
            """;

        var clean = AssistantService.FinalizeReply(text);

        Assert.Contains("Clase teórica", clean);
        Assert.DoesNotContain("clase_id", clean);
        Assert.DoesNotContain("_id", clean);
    }

    private static AssistantService Create(
        IAssistantLlm llm,
        IAssistantToolbox toolbox,
        Action<AssistantOptions>? configure = null)
    {
        var options = new AssistantOptions { ApiKey = "test" };
        configure?.Invoke(options);
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        return new AssistantService(
            llm,
            toolbox,
            new InMemoryAssistantUsageStore(),
            cache,
            Options.Create(options),
            NullLogger<AssistantService>.Instance);
    }

    private sealed class ScriptedLlm : IAssistantLlm
    {
        private readonly Queue<LlmReply> _replies;

        public ScriptedLlm(params LlmReply[] replies) => _replies = new Queue<LlmReply>(replies);

        public int Calls { get; private set; }
        public string LastToolResult { get; private set; } = "";
        public JsonArray? LastMessages { get; private set; }

        public Task<LlmReply> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct)
        {
            Calls++;
            LastMessages = (JsonArray)messages.DeepClone();
            var last = messages[^1];
            if (last?["role"]?.GetValue<string>() == "tool")
            {
                LastToolResult = last["content"]!.GetValue<string>();
            }

            return Task.FromResult(_replies.Count > 0 ? _replies.Dequeue() : new LlmReply("Listo", []));
        }
    }

    private sealed class FakeToolbox : IAssistantToolbox
    {
        public int Executed { get; private set; }

        public IReadOnlyList<AssistantToolDefinition> ToolsFor(string role) =>
        [
            new("mi_progreso", "progreso", new JsonObject(), false),
            new("reservar_clase_teorica", "reservar", new JsonObject(), true)
        ];

        public Task<string> ReadAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct) =>
            Task.FromResult("Horas 10/20");

        public Task<(string Text, AssistantExamAvailabilityUi? ExamAvailability)> ReadWithUiAsync(
            AssistantUser user, string tool, JsonObject args, CancellationToken ct) =>
            Task.FromResult<(string, AssistantExamAvailabilityUi?)>(("Horas 10/20", null));

        public Task<string> DescribeAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct) =>
            Task.FromResult($"Reservar clase {args["clase_id"]}");

        public Task<string> ExecuteAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct)
        {
            Executed++;
            return Task.FromResult("Reservada");
        }
    }
}
