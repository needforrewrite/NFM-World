using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NFMWorldLibrary.Backend.Gamemodes;

public static class ClientServer
{
    public static bool IsRunningOnClient { get; set; } = false;

    [Obsolete("Use proper class split (PvpClientGamemode / IServerGamemode) instead of runtime gating.")]
    public static void RunIfOnClient(Action action)
    {
        if (IsRunningOnClient)
        {
            action();
        }
    }

    [Obsolete("Use proper class split (PvpClientGamemode / IServerGamemode) instead of runtime gating.")]
    public static void RunIfOnClient<T>(Action<T> action, T parameter)
    {
        if (IsRunningOnClient)
        {
            action(parameter);
        }
    }

    [DoesNotReturn]
    public static void AccidentallyCalledClientMethodOnServer([CallerMemberName] string? methodName = null)
    {
        throw new NotSupportedException($"Accidentally called a client method {methodName} on the server. This is not supported.");
    }

    [DoesNotReturn]
    public static T AccidentallyCalledClientMethodOnServer<T>([CallerMemberName] string? methodName = null)
    {
        throw new NotSupportedException($"Accidentally called a client method {methodName} on the server. This is not supported.");
    }
}