using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public enum sdtx_log_item_t
    {
        SDTX_LOGITEM_OK,
        SDTX_LOGITEM_MALLOC_FAILED,
        SDTX_LOGITEM_ADD_COMMIT_LISTENER_FAILED,
        SDTX_LOGITEM_COMMAND_BUFFER_FULL,
        SDTX_LOGITEM_CONTEXT_POOL_EXHAUSTED,
        SDTX_LOGITEM_CANNOT_DESTROY_DEFAULT_CONTEXT,
    }

    public unsafe partial struct sdtx_logger_t
    {
        [NativeTypeName("void (*)(const char *, uint32_t, uint32_t, const char *, uint32_t, const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void> func;

        public void* user_data;
    }

    public partial struct sdtx_context
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public unsafe partial struct sdtx_range
    {
        [NativeTypeName("const void *")]
        public void* ptr;

        [NativeTypeName("size_t")]
        public nuint size;
    }

    public partial struct sdtx_font_desc_t
    {
        public sdtx_range data;

        [NativeTypeName("uint8_t")]
        public byte first_char;

        [NativeTypeName("uint8_t")]
        public byte last_char;
    }

    public partial struct sdtx_context_desc_t
    {
        public int max_commands;

        public int char_buf_size;

        public float canvas_width;

        public float canvas_height;

        public int tab_width;

        public sg_pixel_format color_format;

        public sg_pixel_format depth_format;

        public int sample_count;
    }

    public unsafe partial struct sdtx_allocator_t
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public partial struct sdtx_desc_t
    {
        public int context_pool_size;

        public int printf_buf_size;

        [NativeTypeName("sdtx_font_desc_t[8]")]
        public _fonts_e__FixedBuffer fonts;

        public sdtx_context_desc_t context;

        public sdtx_allocator_t allocator;

        public sdtx_logger_t logger;

        [InlineArray(8)]
        public partial struct _fonts_e__FixedBuffer
        {
            public sdtx_font_desc_t e0;
        }
    }

    public static unsafe partial class DebugText
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const sdtx_desc_t *")] sdtx_desc_t* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font_kc853", ExactSpelling = true)]
        public static extern sdtx_font_desc_t font_kc853();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font_kc854", ExactSpelling = true)]
        public static extern sdtx_font_desc_t font_kc854();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font_z1013", ExactSpelling = true)]
        public static extern sdtx_font_desc_t font_z1013();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font_cpc", ExactSpelling = true)]
        public static extern sdtx_font_desc_t font_cpc();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font_c64", ExactSpelling = true)]
        public static extern sdtx_font_desc_t font_c64();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font_oric", ExactSpelling = true)]
        public static extern sdtx_font_desc_t font_oric();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_make_context", ExactSpelling = true)]
        public static extern sdtx_context make_context([NativeTypeName("const sdtx_context_desc_t *")] sdtx_context_desc_t* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_destroy_context", ExactSpelling = true)]
        public static extern void destroy_context(sdtx_context ctx);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_set_context", ExactSpelling = true)]
        public static extern void set_context(sdtx_context ctx);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_get_context", ExactSpelling = true)]
        public static extern sdtx_context get_context();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_default_context", ExactSpelling = true)]
        public static extern sdtx_context default_context();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_draw", ExactSpelling = true)]
        public static extern void draw();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_context_draw", ExactSpelling = true)]
        public static extern void context_draw(sdtx_context ctx);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_draw_layer", ExactSpelling = true)]
        public static extern void draw_layer(int layer_id);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_context_draw_layer", ExactSpelling = true)]
        public static extern void context_draw_layer(sdtx_context ctx, int layer_id);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_layer", ExactSpelling = true)]
        public static extern void layer(int layer_id);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_font", ExactSpelling = true)]
        public static extern void font(int font_index);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_canvas", ExactSpelling = true)]
        public static extern void canvas(float w, float h);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_origin", ExactSpelling = true)]
        public static extern void origin(float x, float y);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_home", ExactSpelling = true)]
        public static extern void home();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_pos", ExactSpelling = true)]
        public static extern void pos(float x, float y);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_pos_x", ExactSpelling = true)]
        public static extern void pos_x(float x);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_pos_y", ExactSpelling = true)]
        public static extern void pos_y(float y);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_move", ExactSpelling = true)]
        public static extern void move(float dx, float dy);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_move_x", ExactSpelling = true)]
        public static extern void move_x(float dx);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_move_y", ExactSpelling = true)]
        public static extern void move_y(float dy);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_crlf", ExactSpelling = true)]
        public static extern void crlf();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_color3b", ExactSpelling = true)]
        public static extern void color3b([NativeTypeName("uint8_t")] byte r, [NativeTypeName("uint8_t")] byte g, [NativeTypeName("uint8_t")] byte b);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_color3f", ExactSpelling = true)]
        public static extern void color3f(float r, float g, float b);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_color4b", ExactSpelling = true)]
        public static extern void color4b([NativeTypeName("uint8_t")] byte r, [NativeTypeName("uint8_t")] byte g, [NativeTypeName("uint8_t")] byte b, [NativeTypeName("uint8_t")] byte a);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_color4f", ExactSpelling = true)]
        public static extern void color4f(float r, float g, float b, float a);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_color1i", ExactSpelling = true)]
        public static extern void color1i([NativeTypeName("uint32_t")] uint rgba);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_putc", ExactSpelling = true)]
        public static extern void putc([NativeTypeName("char")] sbyte c);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_puts", ExactSpelling = true)]
        public static extern void puts([NativeTypeName("const char *")] sbyte* str);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_putr", ExactSpelling = true)]
        public static extern void putr([NativeTypeName("const char *")] sbyte* str, int len);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_printf", ExactSpelling = true)]
        public static extern int printf([NativeTypeName("const char *")] sbyte* fmt, __arglist);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_vprintf", ExactSpelling = true)]
        public static extern int vprintf([NativeTypeName("const char *")] sbyte* fmt, [NativeTypeName("va_list")] sbyte* args);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sdtx_get_cleared_fmt_buffer", ExactSpelling = true)]
        public static extern sdtx_range get_cleared_fmt_buffer();
    }
}
