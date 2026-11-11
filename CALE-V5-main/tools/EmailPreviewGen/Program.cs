using Cale.BuildingBlocks.Infrastructure.Email.Templates;

var msg = VerificationEmailTemplate.Build("Braynner", "424242", 15, preferDataUriLogo: true);
var outDir = Path.GetFullPath(Path.Combine("docs", "email-previews"));
// When run from tools/EmailPreviewGen, go up to repo CALE-V5-main root.
if (!Directory.Exists(outDir))
{
    outDir = Path.GetFullPath(Path.Combine("..", "..", "docs", "email-previews"));
}

Directory.CreateDirectory(outDir);
var path = Path.Combine(outDir, "verification-email.preview.html");
File.WriteAllText(path, msg.Html);
Console.WriteLine(path);
Console.WriteLine(msg.Html.Contains("data:image/png;base64,", StringComparison.Ordinal)
    ? "logo:data-uri-ok"
    : "logo:missing");
