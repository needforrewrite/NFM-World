namespace NFMWorld.Reactor;

public interface IAnimationCallback
{
    /// <summary>
    /// Invoked on every frame, right before element or its children are rendered.
    /// </summary>
    public Action? AnimationFrameBegan { get; set; }
}