namespace NFMWorld.Sentry;

/// <summary>
/// User feedback associated with a Sentry event.
/// </summary>
public class SentryFeedback
{
    /// <summary>
    /// The event ID this feedback is associated with.
    /// </summary>
    public SentryEventId EventId { get; set; }

    /// <summary>
    /// The user's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The user's feedback comments.
    /// </summary>
    public string? Comments { get; set; }
}
