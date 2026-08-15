using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NFMWorldLibrary.Gamemodes;

public static class ClientServer
{
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