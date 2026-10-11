using System.Security.Claims;
using Cale.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests;

public sealed class CourseVideoGateMiddlewareTests
{
    [Fact]
    public async Task Anonymous_request_for_a_course_video_is_rejected()
    {
        var (status, reachedNext) = await RunAsync("/courses/videos/clase-curva-moto.mp4", signedIn: false);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(reachedNext);
    }

    [Fact]
    public async Task Signed_in_request_for_a_course_video_is_served()
    {
        var (_, reachedNext) = await RunAsync("/courses/videos/clase-curva-moto.mp4", signedIn: true);

        Assert.True(reachedNext);
    }

    [Fact]
    public async Task Other_static_files_do_not_require_a_session()
    {
        var (_, reachedNext) = await RunAsync("/icons/icon-192.png", signedIn: false);

        Assert.True(reachedNext);
    }

    [Fact]
    public async Task Course_videos_are_cacheable_only_by_the_users_own_browser()
    {
        var (_, _, cacheControl) = await RunWithHeadersAsync("/courses/videos/clase-curva-moto.mp4", signedIn: true);

        Assert.Equal(CourseVideoGateMiddleware.VideoCacheControl, cacheControl);
        Assert.StartsWith("private,", cacheControl);
        Assert.DoesNotContain("public", cacheControl);
    }

    [Fact]
    public async Task Payment_receipts_are_never_stored()
    {
        var (_, reachedNext, cacheControl) = await RunWithHeadersAsync(
            "/uploads/receipts/0123456789abcdef0123456789abcdef.png",
            signedIn: true,
            role: "Admin");

        Assert.True(reachedNext);
        Assert.Equal("private, no-store", cacheControl);
    }

    private static async Task<(int Status, bool ReachedNext)> RunAsync(string path, bool signedIn)
    {
        var (status, reached, _) = await RunWithHeadersAsync(path, signedIn);
        return (status, reached);
    }

    private static async Task<(int Status, bool ReachedNext, string CacheControl)> RunWithHeadersAsync(
        string path,
        bool signedIn,
        string? role = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuth(signedIn, role))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var response = new StartingResponseFeature();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(response);
        context.Request.Path = path;

        var reachedNext = false;
        var middleware = new CourseVideoGateMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);
        await response.FireStartingAsync();
        return (context.Response.StatusCode, reachedNext, context.Response.Headers.CacheControl.ToString());
    }

    private sealed class StartingResponseFeature : Microsoft.AspNetCore.Http.Features.HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

        public async Task FireStartingAsync()
        {
            foreach (var (callback, state) in _starting)
            {
                await callback(state);
            }
        }
    }

    private sealed class FakeAuth(bool signedIn, string? role = null) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(signedIn
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity(
                        role is null
                            ? [new Claim(ClaimTypes.NameIdentifier, "7")]
                            : [new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, role)],
                        "test")),
                    "test"))
                : AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
