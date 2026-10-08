namespace MovieApp.Application.Identity;

/// <summary>
/// Lets a background rebuild call the same current-user services a request uses.
/// The HTTP current-user adapter prefers this value over the request principal.
/// </summary>
public static class CurrentUserAmbient
{
    private static readonly AsyncLocal<Guid?> AmbientUserId = new();

    public static Guid? UserId => AmbientUserId.Value;

    public static IDisposable Push(Guid userId)
    {
        var previous = AmbientUserId.Value;
        AmbientUserId.Value = userId;
        return new Restore(previous);
    }

    private sealed class Restore(Guid? previous) : IDisposable
    {
        public void Dispose() => AmbientUserId.Value = previous;
    }
}
