using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public unsafe partial struct sargs_allocator
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public unsafe partial struct sargs_desc
    {
        public int argc;

        [NativeTypeName("char **")]
        public sbyte** argv;

        public int max_args;

        public int buf_size;

        public sargs_allocator allocator;
    }

    public static unsafe partial class Args
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const sargs_desc *")] sargs_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_isvalid", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte isvalid();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_exists", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte exists([NativeTypeName("const char *")] sbyte* key);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_value", ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* value([NativeTypeName("const char *")] sbyte* key);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_value_def", ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* value_def([NativeTypeName("const char *")] sbyte* key, [NativeTypeName("const char *")] sbyte* def);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_equals", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte equals([NativeTypeName("const char *")] sbyte* key, [NativeTypeName("const char *")] sbyte* val);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_boolean", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte boolean([NativeTypeName("const char *")] sbyte* key);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_find", ExactSpelling = true)]
        public static extern int find([NativeTypeName("const char *")] sbyte* key);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_num_args", ExactSpelling = true)]
        public static extern int num_args();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_key_at", ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* key_at(int index);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sargs_value_at", ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* value_at(int index);
    }
}
