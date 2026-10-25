using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public partial struct scb_cmdbuf
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public enum scb_resource_state
    {
        SCB_RESOURCESTATE_INITIAL,
        SCB_RESOURCESTATE_ALLOC,
        SCB_RESOURCESTATE_VALID,
        SCB_RESOURCESTATE_FAILED,
        SCB_RESOURCESTATE_INVALID,
    }

    public unsafe partial struct scb_cmdbuf_desc
    {
        [NativeTypeName("size_t")]
        public nuint size;

        [NativeTypeName("const char *")]
        public sbyte* label;
    }

    public partial struct scb_cmdbuf_info
    {
        [NativeTypeName("size_t")]
        public nuint size;

        [NativeTypeName("size_t")]
        public nuint remaining;

        [NativeTypeName("_Bool")]
        public byte overflown;
    }

    public enum scb_log_item
    {
        SCB_LOGITEM_OK,
        SCB_LOGITEM_MALLOC_FAILED,
        SCB_LOGITEM_CMDBUF_POOL_EXHAUSTED,
        SCB_LOGITEM_CMDBUF_OVERFLOW,
        SCB_LOGITEM_CMDBUF_NOT_VALID,
        SCB_LOGITEM_SUBMIT_CMDBUF_OVERFLOWN,
        SCB_LOGITEM_SUBMIT_INVALID_COMMAND,
    }

    public unsafe partial struct scb_logger
    {
        [NativeTypeName("void (*)(const char *, uint32_t, uint32_t, const char *, uint32_t, const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void> func;

        public void* user_data;
    }

    public unsafe partial struct scb_allocator
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public partial struct scb_desc
    {
        public int cmdbuf_pool_size;

        public scb_allocator allocator;

        public scb_logger logger;
    }

    public static unsafe partial class Cmdbuf
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const scb_desc *")] scb_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_make_cmdbuf", ExactSpelling = true)]
        public static extern scb_cmdbuf make_cmdbuf([NativeTypeName("const scb_cmdbuf_desc *")] scb_cmdbuf_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_destroy_cmdbuf", ExactSpelling = true)]
        public static extern void destroy_cmdbuf(scb_cmdbuf cb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_submit", ExactSpelling = true)]
        public static extern void submit(scb_cmdbuf cb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_reset", ExactSpelling = true)]
        public static extern void reset(scb_cmdbuf cb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_viewport", ExactSpelling = true)]
        public static extern void apply_viewport(scb_cmdbuf cb, int x, int y, int width, int height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_viewportf", ExactSpelling = true)]
        public static extern void apply_viewportf(scb_cmdbuf cb, float x, float y, float width, float height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_scissor_rect", ExactSpelling = true)]
        public static extern void apply_scissor_rect(scb_cmdbuf cb, int x, int y, int width, int height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_scissor_rectf", ExactSpelling = true)]
        public static extern void apply_scissor_rectf(scb_cmdbuf cb, float x, float y, float width, float height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_pipeline", ExactSpelling = true)]
        public static extern void apply_pipeline(scb_cmdbuf cb, sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_bindings", ExactSpelling = true)]
        public static extern void apply_bindings(scb_cmdbuf cb, [NativeTypeName("const sg_bindings *")] sg_bindings* bindings);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_apply_uniforms", ExactSpelling = true)]
        public static extern void apply_uniforms(scb_cmdbuf cb, int ub_slot, [NativeTypeName("const sg_range *")] sg_range* data);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_draw", ExactSpelling = true)]
        public static extern void draw(scb_cmdbuf cb, int base_element, int num_elements, int num_instances);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_draw_ex", ExactSpelling = true)]
        public static extern void draw_ex(scb_cmdbuf cb, int base_element, int num_elements, int num_instances, int base_vertex, int base_instance);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_dispatch", ExactSpelling = true)]
        public static extern void dispatch(scb_cmdbuf cb, int num_groups_x, int num_groups_y, int num_groups_z);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_query_cmdbuf_state", ExactSpelling = true)]
        public static extern scb_resource_state query_cmdbuf_state(scb_cmdbuf cb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "scb_query_cmdbuf_info", ExactSpelling = true)]
        public static extern scb_cmdbuf_info query_cmdbuf_info(scb_cmdbuf cb);
    }
}
