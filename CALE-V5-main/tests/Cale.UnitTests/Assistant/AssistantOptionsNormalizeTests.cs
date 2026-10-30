using Cale.Api.Services.Assistant;
using Xunit;

namespace Cale.UnitTests.Assistant;

public sealed class AssistantOptionsNormalizeTests
{
    [Fact]
    public void Gemini_endpoint_with_openai_model_switches_to_gemini_flash()
    {
        var o = new AssistantOptions
        {
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            Model = "openai/gpt-4o-mini"
        };

        o.NormalizeProviderPairing();

        Assert.Equal(AssistantOptions.DefaultGeminiModel, o.Model);
    }

    [Fact]
    public void PreferFastChat_remaps_heavy_gemini_3_flash()
    {
        var o = new AssistantOptions
        {
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            Model = "gemini-3.8-flash",
            PreferFastChat = true
        };

        o.NormalizeProviderPairing();

        Assert.Equal(AssistantOptions.DefaultGeminiModel, o.Model);
        Assert.Equal("none", o.ReasoningEffort);
    }

    [Fact]
    public void PreferFastChat_off_keeps_gemini_3_flash()
    {
        var o = new AssistantOptions
        {
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            Model = "gemini-3.8-flash",
            PreferFastChat = false
        };

        o.NormalizeProviderPairing();

        Assert.Equal("gemini-3.8-flash", o.Model);
    }

    [Fact]
    public void Github_models_endpoint_keeps_openai_model()
    {
        var o = new AssistantOptions
        {
            Endpoint = "https://models.github.ai/inference/chat/completions",
            Model = "openai/gpt-4o-mini"
        };

        o.NormalizeProviderPairing();

        Assert.Equal("openai/gpt-4o-mini", o.Model);
    }
}
