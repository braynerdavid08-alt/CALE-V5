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

    private static async Task<(int Status, bool ReachedNext)> RunAsync(string path, bool signedIn)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuth(signedIn))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;

        var reachedNext = false;
        var middleware = new CourseVideoGateMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, reachedNext);
    }

    private sealed class FakeAuth(bool signedIn) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(signedIn
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "test")), "test"))
                : AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
