using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public static unsafe partial class Time
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_setup", ExactSpelling = true)]
        public static extern void setup();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_now", ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong now();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_diff", ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong diff([NativeTypeName("uint64_t")] ulong new_ticks, [NativeTypeName("uint64_t")] ulong old_ticks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_since", ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong since([NativeTypeName("uint64_t")] ulong start_ticks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_laptime", ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong laptime([NativeTypeName("uint64_t *")] ulong* last_time);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_round_to_common_refresh_rate", ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong round_to_common_refresh_rate([NativeTypeName("uint64_t")] ulong frame_ticks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_sec", ExactSpelling = true)]
        public static extern double sec([NativeTypeName("uint64_t")] ulong ticks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_ms", ExactSpelling = true)]
        public static extern double ms([NativeTypeName("uint64_t")] ulong ticks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_us", ExactSpelling = true)]
        public static extern double us([NativeTypeName("uint64_t")] ulong ticks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "stm_ns", ExactSpelling = true)]
        public static extern double ns([NativeTypeName("uint64_t")] ulong ticks);
    }
}
