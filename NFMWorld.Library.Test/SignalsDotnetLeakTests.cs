using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;
using SignalsDotnet;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards the vendored SignalsDotnet fix (<c>SignalsDotnet/</c>, branch <c>nfmw</c>): its
/// dependency tracker used to leave one observer on the ambient scope per compute, so a
/// scope that outlived the compute grew without bound and every signal read fanned out to
/// all of it. Without the fix this test grows an observer per frame and the renderer slows
/// down over the course of a race.
/// </summary>
[TestClass]
public class SignalsDotnetLeakTests
{
    private static readonly System.Reflection.FieldInfo CurrentScope =
        typeof(Signal).GetField(
            "_currentScope",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        )!;

    private static readonly System.Reflection.PropertyInfo ScopeValue =
        CurrentScope.FieldType.GetProperty("Value")!;

    private static readonly System.Reflection.BindingFlags Private =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    /// <summary>Installs a scope on the thread, as a compute that suspends mid-run does.</summary>
    private static void PinScope()
    {
        var scopeType = CurrentScope.FieldType.GetGenericArguments()[0];
        var ctor = scopeType.GetConstructor(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance,
            [scopeType]
        )!;
        ScopeValue.SetValue(CurrentScope.GetValue(null), ctor.Invoke([null]));
    }

    private static void ClearScope() => ScopeValue.SetValue(CurrentScope.GetValue(null), null);

    /// <summary>Length of the pinned scope's observer chain.</summary>
    private static int Observers()
    {
        if (ScopeValue.GetValue(CurrentScope.GetValue(null)) is not { } scope) return 0;
        if (scope.GetType().GetProperty("Subject")!.GetValue(scope) is not { } subject) return 0;
        if (subject.GetType().GetField("root", Private)?.GetValue(subject) is not { } root) return 0;

        var next = root.GetType().GetField("Next", Private)
                   ?? root.GetType().GetField("<Next>k__BackingField", Private);
        object? node = root;
        var count = 0;
        while (node is not null && count < 100_000)
        {
            count++;
            node = next?.GetValue(node);
        }

        return count;
    }

    [TestMethod]
    public void EffectsReadingASignal_DoNotOutliveTheirComputeOnTheAmbientScope()
    {
        IBackend.Backend = new DummyBackend();
        _ = UiScheduler.Instance;

        var value = new Signal<int>(0);

        // The scope exists when the effects are created: the async state machine captures
        // the ExecutionContext, so every pass restores this same scope.
        ClearScope();
        PinScope();

        var mounted = Renderer.Mount(() => Sx.View(
                style: () => Sx.Styles(width: 100.Percent()),
                children: Enumerable.Range(0, 4)
                    .Select(i => (object?)Sx.Text(text: () => $"{value.Value}-{i}"))
                    .ToArray()
            )
        );

        for (var i = 0; i < 200; i++)
        {
            value.Value++;
            Renderer.Flush();
        }

        Assert.IsLessThan(
            40,
            Observers(),
            "each compute left an observer behind on the scope, so every signal read fans out to all of them"
        );

        mounted.Dispose();
        ClearScope();
    }
}
