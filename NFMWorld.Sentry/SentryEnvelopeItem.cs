namespace NFMWorld.Sentry;

/// <summary>
/// Base type for an item that can be placed in a Sentry envelope.
/// </summary>
public abstract class SentryEnvelopeItem
{
    /// <summary>
    /// The envelope item type string (e.g. "event", "transaction", "feedback").
    /// </summary>
    public abstract string ItemType { get; }

    /// <summary>
    /// The content type of the payload.
    /// </summary>
    public virtual string ContentType => "application/json";

    /// <summary>
    /// The event ID this item belongs to, if applicable.
    /// </summary>
    public virtual SentryEventId? EventId => null;

    /// <summary>
    /// Serialize the item payload to a JSON byte array.
    /// </summary>
    public abstract byte[] SerializePayload();
}

/// <summary>
/// An envelope item containing an error or message event.
/// </summary>
public class EventItem(SentryEvent evt) : SentryEnvelopeItem
{
    public SentryEvent Event { get; } = evt;

    public override string ItemType => "event";
    public override SentryEventId? EventId => Event.EventId;

    public override byte[] SerializePayload()
    {
        return EnvelopeSerializer.SerializeEvent(Event);
    }
}

/// <summary>
/// An envelope item containing a transaction (performance trace).
/// </summary>
public class TransactionItem(ITransaction transaction) : SentryEnvelopeItem
{
    public ITransaction Transaction { get; } = transaction;

    public override string ItemType => "transaction";
    public override SentryEventId? EventId => Transaction.EventId;

    public override byte[] SerializePayload()
    {
        return EnvelopeSerializer.SerializeTransaction(Transaction);
    }
}

/// <summary>
/// An envelope item containing user feedback.
/// </summary>
public class FeedbackItem(SentryFeedback feedback) : SentryEnvelopeItem
{
    public SentryFeedback Feedback { get; } = feedback;

    public override string ItemType => "feedback";
    public override SentryEventId? EventId => Feedback.EventId;

    public override byte[] SerializePayload()
    {
        return EnvelopeSerializer.SerializeFeedback(Feedback);
    }
}
