using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public partial struct smemtrack_info_t
    {
        public int num_allocs;

        public int num_bytes;
    }

    public static unsafe partial class Memtrack
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "smemtrack_info", ExactSpelling = true)]
        public static extern smemtrack_info_t info();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "smemtrack_alloc", ExactSpelling = true)]
        public static extern void* alloc([NativeTypeName("size_t")] nuint size, void* user_data);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "smemtrack_free", ExactSpelling = true)]
        public static extern void free(void* ptr, void* user_data);
    }
}
