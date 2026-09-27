namespace NFMWorld.Reactor;

public interface IRichTextContainer : IRichTextElement
{
    IEnumerable<IRichTextElement> Children { get; }
}