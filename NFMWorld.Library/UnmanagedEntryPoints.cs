using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Maxine.Extensions;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.Lua;
using NFMWorldLibrary.Radpack;
using NFMWorld.Sentry;

namespace NFMWorldLibrary;

public static class UnmanagedEntryPoints
{
    public interface IUnmanagedResult
    {
        [Description("Whether an error occurred")]
        bool HasError { get; set; }
        [Description("Error information if HasError is true")]
        NativeException Exception { get; set; }
    }

    [InlineArray(16384)]
    public struct ErrorBuffer
    {
        public byte Data;
        public Span<byte> AsSpan()
        {
            unsafe
            {
                fixed (byte* ptr = &Data)
                {
                    return new Span<byte>(ptr, 16384);
                }
            }
        }
    }
    [InlineArray(1024)]
    public struct ErrorMessageBuffer
    {
        public byte Data;
        public Span<byte> AsSpan()
        {
            unsafe
            {
                fixed (byte* ptr = &Data)
                {
                    return new Span<byte>(ptr, 1024);
                }
            }
        }
    }
        
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeException
    {
        [Description("Type name of the exception (UTF-8 bytes, null-terminated)")]
        public ErrorMessageBuffer TypeName;
        [Description("Message of the exception (UTF-8 bytes, null-terminated)")]
        public ErrorMessageBuffer Message;
        [Description("Stack trace of the exception (UTF-8 bytes, null-terminated)")]
        public ErrorBuffer StackTrace;

        public static NativeException FromException(Exception ex)
        {
            var typeNameBytes = Encoding.UTF8.GetBytes(ex.GetType().FullName ?? "UnknownException");
            var messageBytes = Encoding.UTF8.GetBytes(ex.Message);
            var stackTraceBytes = Encoding.UTF8.GetBytes(ex.StackTrace ?? "");

            var nativeEx = new NativeException();
            typeNameBytes.AsSpan(0, Math.Min(typeNameBytes.Length, 1024)).CopyTo(nativeEx.TypeName.AsSpan());
            messageBytes.AsSpan(0, Math.Min(messageBytes.Length, 1024)).CopyTo(nativeEx.Message.AsSpan());
            stackTraceBytes.AsSpan(0, Math.Min(stackTraceBytes.Length, 16384)).CopyTo(nativeEx.StackTrace.AsSpan());
                
            return nativeEx;
        }
    }

    private static T ExecuteSafely<T>(Func<T> func) where T : struct, IUnmanagedResult
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            T obj = default;
            obj.HasError = true;
            obj.Exception = NativeException.FromException(ex);
            return obj;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nfmw_validate_radpack", CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe ValidateRadpackResult ValidateRadpack(ValidateRadpackArgs* args)
    {
        return ExecuteSafely(() =>
        {
            var span = new Span<byte>(args->RadpackData, args->RadpackDataLength);
            var radpack = RadpackSerializer.Deserialize(span);

            return new ValidateRadpackResult
            {
                RadType = radpack.Metadata.Type
            };
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ValidateRadpackArgs
    {
        [Description("Pointer to radpack data")]
        public byte* RadpackData;
        [Description("Length of radpack data in bytes")]
        public int RadpackDataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ValidateRadpackResult : IUnmanagedResult
    {
        public RadpackType RadType;

        [Description("Whether an error occurred during ValidateRadpack")]
        public bool HasError { get; set; }
        [Description("Error information if HasError is true")]
        public NativeException Exception { get; set; }
    }

    [Description(
        """
        Gets information for a time trial.
        """
    )]
    [UnmanagedCallersOnly(EntryPoint = "nfmw_get_tt_info", CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe GetTTInfoResult GetTTInfo(GetTTInfoArgs* args)
    {
        return ExecuteSafely(() =>
        {
            using var timeTrialMemory =
                new UnmanagedMemoryManager<byte>(args->TimeTrialData, args->TimeTrialDataLength);
            var timeTrial = SavedTimeTrial.Load(timeTrialMemory.Memory);
            if (timeTrial == null)
            {
                SentrySdk.CaptureMessage("Failed to load time trial data", SentryLevel.Error);
                throw new InvalidOperationException("Failed to load time trial data");
            }

            return new GetTTInfoResult
            {
                CheckpointCount = timeTrial.Splits.SplitTimes.Count,
                ReplayVersion = timeTrial.Version ?? 0,
                BackendVersion = SavedTimeTrial.CURRENT_VERSION,
                TickCount = timeTrial.DemoData.Ticks.Count
            };
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GetTTInfoArgs
    {
        [Description("Pointer to time trial data")]
        public byte* TimeTrialData;
        [Description("Length of time trial data in bytes")]
        public int TimeTrialDataLength;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct GetTTInfoResult : IUnmanagedResult
    {
        [Description("Number of checkpoints in the time trial")]
        public required int CheckpointCount;
        [Description("Number of ticks in the time trial")]
        public required int TickCount;
        [Description("Version of the replay")]
        public required int ReplayVersion;
        [Description("Version of the backend")]
        public required int BackendVersion;

        [Description("Whether an error occurred during GetTTInfo")]
        public bool HasError { get; set; }
        [Description("Error information if HasError is true")]
        public NativeException Exception { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LoadResult : IUnmanagedResult
    {
        [Description("Whether an error occurred during LoadUnmanaged")]
        public bool HasError { get; set; }
        [Description("Error information if HasError is true")]
        public NativeException Exception { get; set; }
    }

    [Description(
        """
        Loads the backend. Call before calling any other functions.
        """
    )]
    [UnmanagedCallersOnly(EntryPoint = "nfmw_load", CallConvs = [typeof(CallConvStdcall)])]
    public static LoadResult LoadUnmanaged()
    {
        return ExecuteSafely(() =>
        {
            BackendGameSparker.Load();
            return new LoadResult();
        });
    }

    [Description(
        """
        Simulates a time trial to completion with a limit of 100M ticks. Returns the number of elapsed ticks, or -1 on
        timeout.
        """
    )]
    [UnmanagedCallersOnly(EntryPoint = "nfmw_simulate_tt", CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe SimulateTimeTrialResult SimulateTimeTrial(SimulateTimeTrialArgs* args)
    {
        return ExecuteSafely(() =>
        {
            using var timeTrialMemory =
                new UnmanagedMemoryManager<byte>(args->TimeTrialData, args->TimeTrialDataLength);
            var timeTrial = SavedTimeTrial.Load(timeTrialMemory.Memory)!;

            var stageName = Encoding.UTF8.GetString(args->StageName);
            var carName = Encoding.UTF8.GetString(args->Cars[0].CarName);

            var elapsedTicks = LuaTimeTrialSimulator.Run(
                stageName, timeTrial, carName, timeTrial.DemoData.Ticks.Count + 500);

            return new SimulateTimeTrialResult
            {
                ElapsedTicks = elapsedTicks ?? -1,
                ExpectedTicks = timeTrial.DemoData.Ticks.Count,
            };
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SimulateTimeTrialResult : IUnmanagedResult
    {
        [Description("The result code: number of ticks elapsed, or -1 on timeout or error")]
        public required int ElapsedTicks;
        [Description("Number of input ticks in the replay")]
        public required int ExpectedTicks;

        [Description("Whether an error occurred")]
        public bool HasError { get; set; }
        [Description("Error information")]
        public NativeException Exception { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SimulateTimeTrialArgs
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CarInfoUnmanaged
        {
            [Description("Pointer to UTF-8 encoded car name, null-terminated")]
            public byte* CarName;
        }

        [Description("Pointer to UTF-8 encoded stage name, null-terminated")]
        public byte* StageName;
        
        [Description("Pointer to array of CarInfoUnmanaged")]
        public CarInfoUnmanaged* Cars;
        [Description("Number of cars")]
        public int CarCount;
        
        [Description("Pointer to time trial data")]
        public byte* TimeTrialData;
        [Description("Length of time trial data")]
        public int TimeTrialDataLength;
    }
}