namespace Cale.Api.Extensions;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";
    public const string EmailCode = "email-code";
    public const string ClientErrors = "client-errors";
    public const string Refresh = "refresh";
    public const string ChangePassword = "change-password";
    public const string ExamStart = "exam-start";
    public const string ExamReview = "exam-review";
    public const string Uploads = "uploads";
    public const string Assistant = "assistant";
    public const string PublicJoin = "public-join";
}
