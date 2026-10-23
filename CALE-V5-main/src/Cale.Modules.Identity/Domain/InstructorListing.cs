namespace Cale.Modules.Identity.Domain;

/// <summary>
/// An instructor's consent to appear in the public instructor directory. No row means hidden.
/// </summary>
public sealed class InstructorListing
{
    public int UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>When true, students can request private classes from the public directory.</summary>
    public bool AcceptsPrivateClasses { get; private set; }

    private InstructorListing()
    {
    }

    public static InstructorListing Create(int userId, DateTime utcNow) =>
        new() { UserId = userId, CreatedAt = utcNow, AcceptsPrivateClasses = false };

    public void SetAcceptsPrivateClasses(bool accepts) => AcceptsPrivateClasses = accepts;
}
