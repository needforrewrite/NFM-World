using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public partial struct sfb_framebuffer
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public enum sfb_resource_state
    {
        SFB_RESOURCESTATE_INITIAL,
        SFB_RESOURCESTATE_ALLOC,
        SFB_RESOURCESTATE_VALID,
        SFB_RESOURCESTATE_FAILED,
        SFB_RESOURCESTATE_INVALID,
    }

    public enum sfb_format
    {
        SFB_FORMAT_RGBA8,
        SFB_FORMAT_PALETTE8,
    }

    public partial struct sfb_rect
    {
        public int x;

        public int y;

        public int width;

        public int height;
    }

    public partial struct sfb_render_pass_desc
    {
        public sg_pixel_format color_format;

        public sg_pixel_format depth_format;

        public int sample_count;
    }

    public partial struct sfb_framebuffer_desc
    {
        public int width;

        public int height;

        public int prescale;

        public sfb_format format;

        public sfb_rect cliprect;

        [NativeTypeName("_Bool")]
        public byte rotate90;

        public sfb_render_pass_desc render_pass;
    }

    public partial struct sfb_resize_desc
    {
        public int width;

        public int height;

        public int prescale;

        public sfb_rect cliprect;
    }

    public partial struct sfb_update_desc
    {
        public sg_range pixels;

        public sg_range palette;
    }

    public partial struct sfb_render_desc
    {
        [NativeTypeName("_Bool")]
        public byte use_nearest_filter;

        public sg_pipeline pip;

        [NativeTypeName("sg_view[32]")]
        public _views_e__FixedBuffer views;

        [NativeTypeName("sg_sampler[12]")]
        public _samplers_e__FixedBuffer samplers;

        [NativeTypeName("sg_range[8]")]
        public _uniforms_e__FixedBuffer uniforms;

        [InlineArray(32)]
        public partial struct _views_e__FixedBuffer
        {
            public sg_view e0;
        }

        [InlineArray(12)]
        public partial struct _samplers_e__FixedBuffer
        {
            public sg_sampler e0;
        }

        [InlineArray(8)]
        public partial struct _uniforms_e__FixedBuffer
        {
            public sg_range e0;
        }
    }

    public partial struct sfb_texture_info
    {
        public int width;

        public int height;

        public sg_pixel_format pixel_format;

        public sg_image image;

        public sg_view tex_view;
    }

    public partial struct sfb_framebuffer_info
    {
        public sfb_texture_info update;

        public sfb_texture_info offscreen;

        public sfb_texture_info palette;

        public sg_sampler nearest_sampler;

        public sg_sampler linear_sampler;
    }

    public unsafe partial struct sfb_allocator
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public unsafe partial struct sfb_logger
    {
        [NativeTypeName("void (*)(const char *, uint32_t, uint32_t, const char *, uint32_t, const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void> func;

        public void* user_data;
    }

    public partial struct sfb_desc
    {
        public int framebuffer_pool_size;

        public sfb_allocator allocator;

        public sfb_logger logger;
    }

    public static unsafe partial class Framebuffer
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const sfb_desc *")] sfb_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_make_framebuffer", ExactSpelling = true)]
        public static extern sfb_framebuffer make_framebuffer([NativeTypeName("const sfb_framebuffer_desc *")] sfb_framebuffer_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_destroy_framebuffer", ExactSpelling = true)]
        public static extern void destroy_framebuffer(sfb_framebuffer fb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_resize", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte resize(sfb_framebuffer fb, [NativeTypeName("const sfb_resize_desc *")] sfb_resize_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_update", ExactSpelling = true)]
        public static extern void update(sfb_framebuffer fb, [NativeTypeName("const sfb_update_desc *")] sfb_update_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_render", ExactSpelling = true)]
        public static extern void render(sfb_framebuffer fb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_render_ex", ExactSpelling = true)]
        public static extern void render_ex(sfb_framebuffer fb, [NativeTypeName("const sfb_render_desc *")] sfb_render_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_query_framebuffer_state", ExactSpelling = true)]
        public static extern sfb_resource_state query_framebuffer_state(sfb_framebuffer fb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_query_framebuffer_info", ExactSpelling = true)]
        public static extern sfb_framebuffer_info query_framebuffer_info(sfb_framebuffer fb);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfb_query_framebuffer_desc", ExactSpelling = true)]
        public static extern sfb_framebuffer_desc query_framebuffer_desc(sfb_framebuffer fb);
    }
}
