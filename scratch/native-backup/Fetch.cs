using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public enum sfetch_log_item_t
    {
        SFETCH_LOGITEM_OK,
        SFETCH_LOGITEM_MALLOC_FAILED,
        SFETCH_LOGITEM_FILE_PATH_UTF8_DECODING_FAILED,
        SFETCH_LOGITEM_SEND_QUEUE_FULL,
        SFETCH_LOGITEM_REQUEST_CHANNEL_INDEX_TOO_BIG,
        SFETCH_LOGITEM_REQUEST_PATH_IS_NULL,
        SFETCH_LOGITEM_REQUEST_PATH_TOO_LONG,
        SFETCH_LOGITEM_REQUEST_CALLBACK_MISSING,
        SFETCH_LOGITEM_REQUEST_CHUNK_SIZE_GREATER_BUFFER_SIZE,
        SFETCH_LOGITEM_REQUEST_USERDATA_PTR_IS_SET_BUT_USERDATA_SIZE_IS_NULL,
        SFETCH_LOGITEM_REQUEST_USERDATA_PTR_IS_NULL_BUT_USERDATA_SIZE_IS_NOT,
        SFETCH_LOGITEM_REQUEST_USERDATA_SIZE_TOO_BIG,
        SFETCH_LOGITEM_CLAMPING_NUM_CHANNELS_TO_MAX_CHANNELS,
        SFETCH_LOGITEM_REQUEST_POOL_EXHAUSTED,
    }

    public unsafe partial struct sfetch_logger_t
    {
        [NativeTypeName("void (*)(const char *, uint32_t, uint32_t, const char *, uint32_t, const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void> func;

        public void* user_data;
    }

    public unsafe partial struct sfetch_range_t
    {
        [NativeTypeName("const void *")]
        public void* ptr;

        [NativeTypeName("size_t")]
        public nuint size;
    }

    public unsafe partial struct sfetch_allocator_t
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public partial struct sfetch_desc_t
    {
        [NativeTypeName("uint32_t")]
        public uint max_requests;

        [NativeTypeName("uint32_t")]
        public uint num_channels;

        [NativeTypeName("uint32_t")]
        public uint num_lanes;

        public sfetch_allocator_t allocator;

        public sfetch_logger_t logger;
    }

    public partial struct sfetch_handle_t
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public enum sfetch_error_t
    {
        SFETCH_ERROR_NO_ERROR,
        SFETCH_ERROR_FILE_NOT_FOUND,
        SFETCH_ERROR_NO_BUFFER,
        SFETCH_ERROR_BUFFER_TOO_SMALL,
        SFETCH_ERROR_UNEXPECTED_EOF,
        SFETCH_ERROR_INVALID_HTTP_STATUS,
        SFETCH_ERROR_CANCELLED,
        SFETCH_ERROR_JS_OTHER,
    }

    public unsafe partial struct sfetch_response_t
    {
        public sfetch_handle_t handle;

        [NativeTypeName("_Bool")]
        public byte dispatched;

        [NativeTypeName("_Bool")]
        public byte fetched;

        [NativeTypeName("_Bool")]
        public byte paused;

        [NativeTypeName("_Bool")]
        public byte finished;

        [NativeTypeName("_Bool")]
        public byte failed;

        [NativeTypeName("_Bool")]
        public byte cancelled;

        public sfetch_error_t error_code;

        [NativeTypeName("uint32_t")]
        public uint channel;

        [NativeTypeName("uint32_t")]
        public uint lane;

        [NativeTypeName("const char *")]
        public sbyte* path;

        public void* user_data;

        [NativeTypeName("uint32_t")]
        public uint data_offset;

        public sfetch_range_t data;

        public sfetch_range_t buffer;
    }

    public unsafe partial struct sfetch_request_t
    {
        [NativeTypeName("uint32_t")]
        public uint channel;

        [NativeTypeName("const char *")]
        public sbyte* path;

        [NativeTypeName("void (*)(const sfetch_response_t *)")]
        public delegate* unmanaged[Cdecl]<sfetch_response_t*, void> callback;

        [NativeTypeName("uint32_t")]
        public uint chunk_size;

        public sfetch_range_t buffer;

        public sfetch_range_t user_data;
    }

    public static unsafe partial class Fetch
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const sfetch_desc_t *")] sfetch_desc_t* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_valid", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte valid();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_desc", ExactSpelling = true)]
        public static extern sfetch_desc_t desc();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_max_userdata_bytes", ExactSpelling = true)]
        public static extern int max_userdata_bytes();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_max_path", ExactSpelling = true)]
        public static extern int max_path();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_send", ExactSpelling = true)]
        public static extern sfetch_handle_t send([NativeTypeName("const sfetch_request_t *")] sfetch_request_t* request);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_handle_valid", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte handle_valid(sfetch_handle_t h);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_dowork", ExactSpelling = true)]
        public static extern void dowork();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_bind_buffer", ExactSpelling = true)]
        public static extern void bind_buffer(sfetch_handle_t h, sfetch_range_t buffer);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_unbind_buffer", ExactSpelling = true)]
        public static extern void* unbind_buffer(sfetch_handle_t h);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_cancel", ExactSpelling = true)]
        public static extern void cancel(sfetch_handle_t h);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_pause", ExactSpelling = true)]
        public static extern void pause(sfetch_handle_t h);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sfetch_continue", ExactSpelling = true)]
        public static extern void @continue(sfetch_handle_t h);
    }
}
