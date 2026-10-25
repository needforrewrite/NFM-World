using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public enum saudio_log_item
    {
        SAUDIO_LOGITEM_OK,
        SAUDIO_LOGITEM_MALLOC_FAILED,
        SAUDIO_LOGITEM_ALSA_SND_PCM_OPEN_FAILED,
        SAUDIO_LOGITEM_ALSA_FLOAT_SAMPLES_NOT_SUPPORTED,
        SAUDIO_LOGITEM_ALSA_REQUESTED_BUFFER_SIZE_NOT_SUPPORTED,
        SAUDIO_LOGITEM_ALSA_REQUESTED_CHANNEL_COUNT_NOT_SUPPORTED,
        SAUDIO_LOGITEM_ALSA_SND_PCM_HW_PARAMS_SET_RATE_NEAR_FAILED,
        SAUDIO_LOGITEM_ALSA_SND_PCM_HW_PARAMS_FAILED,
        SAUDIO_LOGITEM_ALSA_PTHREAD_CREATE_FAILED,
        SAUDIO_LOGITEM_WASAPI_CREATE_EVENT_FAILED,
        SAUDIO_LOGITEM_WASAPI_CREATE_DEVICE_ENUMERATOR_FAILED,
        SAUDIO_LOGITEM_WASAPI_GET_DEFAULT_AUDIO_ENDPOINT_FAILED,
        SAUDIO_LOGITEM_WASAPI_DEVICE_ACTIVATE_FAILED,
        SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_INITIALIZE_FAILED,
        SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_GET_BUFFER_SIZE_FAILED,
        SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_GET_SERVICE_FAILED,
        SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_SET_EVENT_HANDLE_FAILED,
        SAUDIO_LOGITEM_WASAPI_CREATE_THREAD_FAILED,
        SAUDIO_LOGITEM_AAUDIO_STREAMBUILDER_OPEN_STREAM_FAILED,
        SAUDIO_LOGITEM_AAUDIO_PTHREAD_CREATE_FAILED,
        SAUDIO_LOGITEM_AAUDIO_RESTARTING_STREAM_AFTER_ERROR,
        SAUDIO_LOGITEM_USING_AAUDIO_BACKEND,
        SAUDIO_LOGITEM_AAUDIO_CREATE_STREAMBUILDER_FAILED,
        SAUDIO_LOGITEM_COREAUDIO_NEW_OUTPUT_FAILED,
        SAUDIO_LOGITEM_COREAUDIO_ALLOCATE_BUFFER_FAILED,
        SAUDIO_LOGITEM_COREAUDIO_START_FAILED,
        SAUDIO_LOGITEM_BACKEND_BUFFER_SIZE_ISNT_MULTIPLE_OF_PACKET_SIZE,
        SAUDIO_LOGITEM_VITA_SCEAUDIO_OPEN_FAILED,
        SAUDIO_LOGITEM_VITA_PTHREAD_CREATE_FAILED,
        SAUDIO_LOGITEM_N3DS_NDSP_OPEN_FAILED,
    }

    public unsafe partial struct saudio_logger
    {
        [NativeTypeName("void (*)(const char *, uint32_t, uint32_t, const char *, uint32_t, const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void> func;

        public void* user_data;
    }

    public unsafe partial struct saudio_allocator
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public enum saudio_n3ds_ndspinterptype
    {
        SAUDIO_N3DS_DSP_INTERP_POLYPHASE = 0,
        SAUDIO_N3DS_DSP_INTERP_LINEAR = 1,
        SAUDIO_N3DS_DSP_INTERP_NONE = 2,
    }

    public partial struct saudio_n3ds_desc
    {
        public int queue_count;

        public saudio_n3ds_ndspinterptype interpolation_type;

        public int channel_id;
    }

    public partial struct saudio_win32_desc
    {
        [NativeTypeName("_Bool")]
        public byte skip_coinitialize;
    }

    public unsafe partial struct saudio_desc
    {
        public int sample_rate;

        public int num_channels;

        public int buffer_frames;

        public int packet_frames;

        public int num_packets;

        [NativeTypeName("void (*)(float *, int, int)")]
        public delegate* unmanaged[Cdecl]<float*, int, int, void> stream_cb;

        [NativeTypeName("void (*)(float *, int, int, void *)")]
        public delegate* unmanaged[Cdecl]<float*, int, int, void*, void> stream_userdata_cb;

        public void* user_data;

        public saudio_win32_desc win32;

        public saudio_n3ds_desc n3ds;

        public saudio_allocator allocator;

        public saudio_logger logger;
    }

    public static unsafe partial class Audio
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const saudio_desc *")] saudio_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_isvalid", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte isvalid();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_userdata", ExactSpelling = true)]
        public static extern void* userdata();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_query_desc", ExactSpelling = true)]
        public static extern saudio_desc query_desc();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_sample_rate", ExactSpelling = true)]
        public static extern int sample_rate();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_buffer_frames", ExactSpelling = true)]
        public static extern int buffer_frames();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_channels", ExactSpelling = true)]
        public static extern int channels();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_suspended", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte suspended();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_expect", ExactSpelling = true)]
        public static extern int expect();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "saudio_push", ExactSpelling = true)]
        public static extern int push([NativeTypeName("const float *")] float* frames, int num_frames);
    }
}
