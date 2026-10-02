using System.Text.Json.Nodes;
using Cale.Api.Services.Assistant;
using Cale.BuildingBlocks.Domain.Auth;
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
    public void Status_is_disabled_without_api_key_or_for_other_roles()
    {
        var noKey = Create(new ScriptedLlm(), new FakeToolbox(), o => o.ApiKey = null);
        Assert.False(noKey.Status(Student).Enabled);

        var withKey = Create(new ScriptedLlm(), new FakeToolbox());
        Assert.True(withKey.Status(Student).Enabled);
        Assert.False(withKey.Status(new AssistantUser(1, Roles.Admin, "Admin", null)).Enabled);
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

    private static AssistantService Create(
        IAssistantLlm llm,
        IAssistantToolbox toolbox,
        Action<AssistantOptions>? configure = null)
    {
        var options = new AssistantOptions { ApiKey = "test" };
        configure?.Invoke(options);
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        return new AssistantService(llm, toolbox, cache, Options.Create(options), NullLogger<AssistantService>.Instance);
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

        public Task<string> DescribeAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct) =>
            Task.FromResult($"Reservar clase {args["clase_id"]}");

        public Task<string> ExecuteAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct)
        {
            Executed++;
            return Task.FromResult("Reservada");
        }
    }
}
