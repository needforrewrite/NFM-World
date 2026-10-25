using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public partial struct sg_buffer
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public partial struct sg_image
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public partial struct sg_sampler
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public partial struct sg_shader
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public partial struct sg_pipeline
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public partial struct sg_view
    {
        [NativeTypeName("uint32_t")]
        public uint id;
    }

    public unsafe partial struct sg_range
    {
        [NativeTypeName("const void *")]
        public void* ptr;

        [NativeTypeName("size_t")]
        public nuint size;
    }

    public partial struct sg_color
    {
        public float r;

        public float g;

        public float b;

        public float a;
    }

    public enum sg_backend
    {
        SG_BACKEND_GLCORE,
        SG_BACKEND_GLES3,
        SG_BACKEND_D3D11,
        SG_BACKEND_METAL_IOS,
        SG_BACKEND_METAL_MACOS,
        SG_BACKEND_METAL_SIMULATOR,
        SG_BACKEND_WGPU,
        SG_BACKEND_VULKAN,
        SG_BACKEND_DUMMY,
    }

    public enum sg_pixel_format
    {
        _SG_PIXELFORMAT_DEFAULT,
        SG_PIXELFORMAT_NONE,
        SG_PIXELFORMAT_R8,
        SG_PIXELFORMAT_R8SN,
        SG_PIXELFORMAT_R8UI,
        SG_PIXELFORMAT_R8SI,
        SG_PIXELFORMAT_R16,
        SG_PIXELFORMAT_R16SN,
        SG_PIXELFORMAT_R16UI,
        SG_PIXELFORMAT_R16SI,
        SG_PIXELFORMAT_R16F,
        SG_PIXELFORMAT_RG8,
        SG_PIXELFORMAT_RG8SN,
        SG_PIXELFORMAT_RG8UI,
        SG_PIXELFORMAT_RG8SI,
        SG_PIXELFORMAT_R32UI,
        SG_PIXELFORMAT_R32SI,
        SG_PIXELFORMAT_R32F,
        SG_PIXELFORMAT_RG16,
        SG_PIXELFORMAT_RG16SN,
        SG_PIXELFORMAT_RG16UI,
        SG_PIXELFORMAT_RG16SI,
        SG_PIXELFORMAT_RG16F,
        SG_PIXELFORMAT_RGBA8,
        SG_PIXELFORMAT_SRGB8A8,
        SG_PIXELFORMAT_RGBA8SN,
        SG_PIXELFORMAT_RGBA8UI,
        SG_PIXELFORMAT_RGBA8SI,
        SG_PIXELFORMAT_BGRA8,
        SG_PIXELFORMAT_SBGR8A8,
        SG_PIXELFORMAT_RGB10A2,
        SG_PIXELFORMAT_RG11B10F,
        SG_PIXELFORMAT_RGB9E5,
        SG_PIXELFORMAT_RG32UI,
        SG_PIXELFORMAT_RG32SI,
        SG_PIXELFORMAT_RG32F,
        SG_PIXELFORMAT_RGBA16,
        SG_PIXELFORMAT_RGBA16SN,
        SG_PIXELFORMAT_RGBA16UI,
        SG_PIXELFORMAT_RGBA16SI,
        SG_PIXELFORMAT_RGBA16F,
        SG_PIXELFORMAT_RGBA32UI,
        SG_PIXELFORMAT_RGBA32SI,
        SG_PIXELFORMAT_RGBA32F,
        SG_PIXELFORMAT_DEPTH,
        SG_PIXELFORMAT_DEPTH_STENCIL,
        SG_PIXELFORMAT_BC1_RGBA,
        SG_PIXELFORMAT_BC2_RGBA,
        SG_PIXELFORMAT_BC3_RGBA,
        SG_PIXELFORMAT_BC3_SRGBA,
        SG_PIXELFORMAT_BC4_R,
        SG_PIXELFORMAT_BC4_RSN,
        SG_PIXELFORMAT_BC5_RG,
        SG_PIXELFORMAT_BC5_RGSN,
        SG_PIXELFORMAT_BC6H_RGBF,
        SG_PIXELFORMAT_BC6H_RGBUF,
        SG_PIXELFORMAT_BC7_RGBA,
        SG_PIXELFORMAT_BC7_SRGBA,
        SG_PIXELFORMAT_ETC2_RGB8,
        SG_PIXELFORMAT_ETC2_SRGB8,
        SG_PIXELFORMAT_ETC2_RGB8A1,
        SG_PIXELFORMAT_ETC2_RGBA8,
        SG_PIXELFORMAT_ETC2_SRGB8A8,
        SG_PIXELFORMAT_EAC_R11,
        SG_PIXELFORMAT_EAC_R11SN,
        SG_PIXELFORMAT_EAC_RG11,
        SG_PIXELFORMAT_EAC_RG11SN,
        SG_PIXELFORMAT_ASTC_4x4_RGBA,
        SG_PIXELFORMAT_ASTC_4x4_SRGBA,
    }

    public partial struct sg_pixelformat_info
    {
        [NativeTypeName("_Bool")]
        public byte sample;

        [NativeTypeName("_Bool")]
        public byte filter;

        [NativeTypeName("_Bool")]
        public byte render;

        [NativeTypeName("_Bool")]
        public byte blend;

        [NativeTypeName("_Bool")]
        public byte msaa;

        [NativeTypeName("_Bool")]
        public byte depth;

        [NativeTypeName("_Bool")]
        public byte compressed;

        [NativeTypeName("_Bool")]
        public byte read;

        [NativeTypeName("_Bool")]
        public byte write;

        public int bytes_per_pixel;
    }

    public partial struct sg_features
    {
        [NativeTypeName("_Bool")]
        public byte origin_top_left;

        [NativeTypeName("_Bool")]
        public byte image_clamp_to_border;

        [NativeTypeName("_Bool")]
        public byte mrt_independent_blend_state;

        [NativeTypeName("_Bool")]
        public byte mrt_independent_write_mask;

        [NativeTypeName("_Bool")]
        public byte compute;

        [NativeTypeName("_Bool")]
        public byte msaa_texture_bindings;

        [NativeTypeName("_Bool")]
        public byte separate_buffer_types;

        [NativeTypeName("_Bool")]
        public byte draw_base_vertex;

        [NativeTypeName("_Bool")]
        public byte draw_base_instance;

        [NativeTypeName("_Bool")]
        public byte dual_source_blending;

        [NativeTypeName("_Bool")]
        public byte vertexformat_int10_n2;

        [NativeTypeName("_Bool")]
        public byte gl_texture_views;
    }

    public partial struct sg_limits
    {
        public int max_image_size_2d;

        public int max_image_size_cube;

        public int max_image_size_3d;

        public int max_image_size_array;

        public int max_image_array_layers;

        public int max_vertex_attrs;

        public int max_color_attachments;

        public int max_texture_bindings_per_stage;

        public int max_storage_buffer_bindings_per_stage;

        public int max_storage_image_bindings_per_stage;

        public int gl_max_vertex_uniform_components;

        public int gl_max_combined_texture_image_units;

        public int d3d11_max_unordered_access_views;

        public int vk_min_uniform_buffer_offset_alignment;
    }

    public enum sg_resource_state
    {
        SG_RESOURCESTATE_INITIAL,
        SG_RESOURCESTATE_ALLOC,
        SG_RESOURCESTATE_UNSEALED,
        SG_RESOURCESTATE_VALID,
        SG_RESOURCESTATE_FAILED,
        SG_RESOURCESTATE_INVALID,
    }

    public enum sg_index_type
    {
        _SG_INDEXTYPE_DEFAULT,
        SG_INDEXTYPE_NONE,
        SG_INDEXTYPE_UINT16,
        SG_INDEXTYPE_UINT32,
    }

    public enum sg_image_type
    {
        _SG_IMAGETYPE_DEFAULT,
        SG_IMAGETYPE_2D,
        SG_IMAGETYPE_CUBE,
        SG_IMAGETYPE_3D,
        SG_IMAGETYPE_ARRAY,
    }

    public enum sg_image_sample_type
    {
        _SG_IMAGESAMPLETYPE_DEFAULT,
        SG_IMAGESAMPLETYPE_FLOAT,
        SG_IMAGESAMPLETYPE_DEPTH,
        SG_IMAGESAMPLETYPE_SINT,
        SG_IMAGESAMPLETYPE_UINT,
        SG_IMAGESAMPLETYPE_UNFILTERABLE_FLOAT,
    }

    public enum sg_sampler_type
    {
        _SG_SAMPLERTYPE_DEFAULT,
        SG_SAMPLERTYPE_FILTERING,
        SG_SAMPLERTYPE_NONFILTERING,
        SG_SAMPLERTYPE_COMPARISON,
    }

    public enum sg_primitive_type
    {
        _SG_PRIMITIVETYPE_DEFAULT,
        SG_PRIMITIVETYPE_POINTS,
        SG_PRIMITIVETYPE_LINES,
        SG_PRIMITIVETYPE_LINE_STRIP,
        SG_PRIMITIVETYPE_TRIANGLES,
        SG_PRIMITIVETYPE_TRIANGLE_STRIP,
    }

    public enum sg_filter
    {
        _SG_FILTER_DEFAULT,
        SG_FILTER_NEAREST,
        SG_FILTER_LINEAR,
    }

    public enum sg_wrap
    {
        _SG_WRAP_DEFAULT,
        SG_WRAP_REPEAT,
        SG_WRAP_CLAMP_TO_EDGE,
        SG_WRAP_CLAMP_TO_BORDER,
        SG_WRAP_MIRRORED_REPEAT,
    }

    public enum sg_border_color
    {
        _SG_BORDERCOLOR_DEFAULT,
        SG_BORDERCOLOR_TRANSPARENT_BLACK,
        SG_BORDERCOLOR_OPAQUE_BLACK,
        SG_BORDERCOLOR_OPAQUE_WHITE,
    }

    public enum sg_vertex_format
    {
        SG_VERTEXFORMAT_INVALID,
        SG_VERTEXFORMAT_FLOAT,
        SG_VERTEXFORMAT_FLOAT2,
        SG_VERTEXFORMAT_FLOAT3,
        SG_VERTEXFORMAT_FLOAT4,
        SG_VERTEXFORMAT_INT,
        SG_VERTEXFORMAT_INT2,
        SG_VERTEXFORMAT_INT3,
        SG_VERTEXFORMAT_INT4,
        SG_VERTEXFORMAT_UINT,
        SG_VERTEXFORMAT_UINT2,
        SG_VERTEXFORMAT_UINT3,
        SG_VERTEXFORMAT_UINT4,
        SG_VERTEXFORMAT_BYTE4,
        SG_VERTEXFORMAT_BYTE4N,
        SG_VERTEXFORMAT_UBYTE4,
        SG_VERTEXFORMAT_UBYTE4N,
        SG_VERTEXFORMAT_SHORT2,
        SG_VERTEXFORMAT_SHORT2N,
        SG_VERTEXFORMAT_USHORT2,
        SG_VERTEXFORMAT_USHORT2N,
        SG_VERTEXFORMAT_SHORT4,
        SG_VERTEXFORMAT_SHORT4N,
        SG_VERTEXFORMAT_USHORT4,
        SG_VERTEXFORMAT_USHORT4N,
        SG_VERTEXFORMAT_INT10_N2,
        SG_VERTEXFORMAT_UINT10_N2,
        SG_VERTEXFORMAT_HALF2,
        SG_VERTEXFORMAT_HALF4,
    }

    public enum sg_vertex_step
    {
        _SG_VERTEXSTEP_DEFAULT,
        SG_VERTEXSTEP_PER_VERTEX,
        SG_VERTEXSTEP_PER_INSTANCE,
    }

    public enum sg_uniform_type
    {
        SG_UNIFORMTYPE_INVALID,
        SG_UNIFORMTYPE_FLOAT,
        SG_UNIFORMTYPE_FLOAT2,
        SG_UNIFORMTYPE_FLOAT3,
        SG_UNIFORMTYPE_FLOAT4,
        SG_UNIFORMTYPE_INT,
        SG_UNIFORMTYPE_INT2,
        SG_UNIFORMTYPE_INT3,
        SG_UNIFORMTYPE_INT4,
        SG_UNIFORMTYPE_MAT4,
    }

    public enum sg_uniform_layout
    {
        _SG_UNIFORMLAYOUT_DEFAULT,
        SG_UNIFORMLAYOUT_NATIVE,
        SG_UNIFORMLAYOUT_STD140,
    }

    public enum sg_cull_mode
    {
        _SG_CULLMODE_DEFAULT,
        SG_CULLMODE_NONE,
        SG_CULLMODE_FRONT,
        SG_CULLMODE_BACK,
    }

    public enum sg_face_winding
    {
        _SG_FACEWINDING_DEFAULT,
        SG_FACEWINDING_CCW,
        SG_FACEWINDING_CW,
    }

    public enum sg_compare_func
    {
        _SG_COMPAREFUNC_DEFAULT,
        SG_COMPAREFUNC_NEVER,
        SG_COMPAREFUNC_LESS,
        SG_COMPAREFUNC_EQUAL,
        SG_COMPAREFUNC_LESS_EQUAL,
        SG_COMPAREFUNC_GREATER,
        SG_COMPAREFUNC_NOT_EQUAL,
        SG_COMPAREFUNC_GREATER_EQUAL,
        SG_COMPAREFUNC_ALWAYS,
    }

    public enum sg_stencil_op
    {
        _SG_STENCILOP_DEFAULT,
        SG_STENCILOP_KEEP,
        SG_STENCILOP_ZERO,
        SG_STENCILOP_REPLACE,
        SG_STENCILOP_INCR_CLAMP,
        SG_STENCILOP_DECR_CLAMP,
        SG_STENCILOP_INVERT,
        SG_STENCILOP_INCR_WRAP,
        SG_STENCILOP_DECR_WRAP,
    }

    public enum sg_blend_factor
    {
        _SG_BLENDFACTOR_DEFAULT,
        SG_BLENDFACTOR_ZERO,
        SG_BLENDFACTOR_ONE,
        SG_BLENDFACTOR_SRC_COLOR,
        SG_BLENDFACTOR_ONE_MINUS_SRC_COLOR,
        SG_BLENDFACTOR_SRC_ALPHA,
        SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
        SG_BLENDFACTOR_DST_COLOR,
        SG_BLENDFACTOR_ONE_MINUS_DST_COLOR,
        SG_BLENDFACTOR_DST_ALPHA,
        SG_BLENDFACTOR_ONE_MINUS_DST_ALPHA,
        SG_BLENDFACTOR_SRC_ALPHA_SATURATED,
        SG_BLENDFACTOR_BLEND_COLOR,
        SG_BLENDFACTOR_ONE_MINUS_BLEND_COLOR,
        SG_BLENDFACTOR_BLEND_ALPHA,
        SG_BLENDFACTOR_ONE_MINUS_BLEND_ALPHA,
        SG_BLENDFACTOR_SRC1_COLOR,
        SG_BLENDFACTOR_ONE_MINUS_SRC1_COLOR,
        SG_BLENDFACTOR_SRC1_ALPHA,
        SG_BLENDFACTOR_ONE_MINUS_SRC1_ALPHA,
    }

    public enum sg_blend_op
    {
        _SG_BLENDOP_DEFAULT,
        SG_BLENDOP_ADD,
        SG_BLENDOP_SUBTRACT,
        SG_BLENDOP_REVERSE_SUBTRACT,
        SG_BLENDOP_MIN,
        SG_BLENDOP_MAX,
    }

    public enum sg_color_mask
    {
        _SG_COLORMASK_DEFAULT = 0,
        SG_COLORMASK_NONE = 0x10,
        SG_COLORMASK_R = 0x1,
        SG_COLORMASK_G = 0x2,
        SG_COLORMASK_RG = 0x3,
        SG_COLORMASK_B = 0x4,
        SG_COLORMASK_RB = 0x5,
        SG_COLORMASK_GB = 0x6,
        SG_COLORMASK_RGB = 0x7,
        SG_COLORMASK_A = 0x8,
        SG_COLORMASK_RA = 0x9,
        SG_COLORMASK_GA = 0xA,
        SG_COLORMASK_RGA = 0xB,
        SG_COLORMASK_BA = 0xC,
        SG_COLORMASK_RBA = 0xD,
        SG_COLORMASK_GBA = 0xE,
        SG_COLORMASK_RGBA = 0xF,
    }

    public enum sg_load_action
    {
        _SG_LOADACTION_DEFAULT,
        SG_LOADACTION_CLEAR,
        SG_LOADACTION_LOAD,
        SG_LOADACTION_DONTCARE,
    }

    public enum sg_store_action
    {
        _SG_STOREACTION_DEFAULT,
        SG_STOREACTION_STORE,
        SG_STOREACTION_DONTCARE,
    }

    public partial struct sg_color_attachment_action
    {
        public sg_load_action load_action;

        public sg_store_action store_action;

        public sg_color clear_value;
    }

    public partial struct sg_depth_attachment_action
    {
        public sg_load_action load_action;

        public sg_store_action store_action;

        public float clear_value;
    }

    public partial struct sg_stencil_attachment_action
    {
        public sg_load_action load_action;

        public sg_store_action store_action;

        [NativeTypeName("uint8_t")]
        public byte clear_value;
    }

    public partial struct sg_pass_action
    {
        [NativeTypeName("sg_color_attachment_action[8]")]
        public _colors_e__FixedBuffer colors;

        public sg_depth_attachment_action depth;

        public sg_stencil_attachment_action stencil;

        [InlineArray(8)]
        public partial struct _colors_e__FixedBuffer
        {
            public sg_color_attachment_action e0;
        }
    }

    public unsafe partial struct sg_metal_swapchain
    {
        [NativeTypeName("const void *")]
        public void* current_drawable;

        [NativeTypeName("const void *")]
        public void* depth_stencil_texture;

        [NativeTypeName("const void *")]
        public void* msaa_color_texture;
    }

    public unsafe partial struct sg_d3d11_swapchain
    {
        [NativeTypeName("const void *")]
        public void* render_view;

        [NativeTypeName("const void *")]
        public void* resolve_view;

        [NativeTypeName("const void *")]
        public void* depth_stencil_view;
    }

    public unsafe partial struct sg_wgpu_swapchain
    {
        [NativeTypeName("const void *")]
        public void* render_view;

        [NativeTypeName("const void *")]
        public void* resolve_view;

        [NativeTypeName("const void *")]
        public void* depth_stencil_view;
    }

    public unsafe partial struct sg_vulkan_swapchain
    {
        [NativeTypeName("const void *")]
        public void* render_image;

        [NativeTypeName("const void *")]
        public void* render_view;

        [NativeTypeName("const void *")]
        public void* resolve_image;

        [NativeTypeName("const void *")]
        public void* resolve_view;

        [NativeTypeName("const void *")]
        public void* depth_stencil_image;

        [NativeTypeName("const void *")]
        public void* depth_stencil_view;

        [NativeTypeName("const void *")]
        public void* render_finished_semaphore;

        [NativeTypeName("const void *")]
        public void* present_complete_semaphore;
    }

    public partial struct sg_gl_swapchain
    {
        [NativeTypeName("uint32_t")]
        public uint framebuffer;
    }

    public partial struct sg_swapchain
    {
        [NativeTypeName("_Bool")]
        public byte invalid;

        public int width;

        public int height;

        public int sample_count;

        public sg_pixel_format color_format;

        public sg_pixel_format depth_format;

        public sg_metal_swapchain metal;

        public sg_d3d11_swapchain d3d11;

        public sg_wgpu_swapchain wgpu;

        public sg_vulkan_swapchain vulkan;

        public sg_gl_swapchain gl;
    }

    public partial struct sg_attachments
    {
        [NativeTypeName("sg_view[8]")]
        public _colors_e__FixedBuffer colors;

        [NativeTypeName("sg_view[8]")]
        public _resolves_e__FixedBuffer resolves;

        public sg_view depth_stencil;

        [InlineArray(8)]
        public partial struct _colors_e__FixedBuffer
        {
            public sg_view e0;
        }

        [InlineArray(8)]
        public partial struct _resolves_e__FixedBuffer
        {
            public sg_view e0;
        }
    }

    public unsafe partial struct sg_pass
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        [NativeTypeName("_Bool")]
        public byte compute;

        public sg_pass_action action;

        public sg_attachments attachments;

        public sg_swapchain swapchain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;
    }

    public partial struct sg_bindings
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        [NativeTypeName("sg_buffer[8]")]
        public _vertex_buffers_e__FixedBuffer vertex_buffers;

        [NativeTypeName("int[8]")]
        public _vertex_buffer_offsets_e__FixedBuffer vertex_buffer_offsets;

        public sg_buffer index_buffer;

        public int index_buffer_offset;

        [NativeTypeName("sg_view[32]")]
        public _views_e__FixedBuffer views;

        [NativeTypeName("sg_sampler[12]")]
        public _samplers_e__FixedBuffer samplers;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;

        [InlineArray(8)]
        public partial struct _vertex_buffers_e__FixedBuffer
        {
            public sg_buffer e0;
        }

        [InlineArray(8)]
        public partial struct _vertex_buffer_offsets_e__FixedBuffer
        {
            public int e0;
        }

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
    }

    public partial struct sg_buffer_usage
    {
        [NativeTypeName("_Bool")]
        public byte vertex_buffer;

        [NativeTypeName("_Bool")]
        public byte index_buffer;

        [NativeTypeName("_Bool")]
        public byte storage_buffer;

        [NativeTypeName("_Bool")]
        public byte immutable;

        [NativeTypeName("_Bool")]
        public byte write_unsealed;

        [NativeTypeName("_Bool")]
        public byte write_transient;

        [NativeTypeName("_Bool")]
        public byte dynamic_update;
    }

    public unsafe partial struct sg_buffer_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        [NativeTypeName("size_t")]
        public nuint size;

        public sg_buffer_usage usage;

        public sg_range data;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t[2]")]
        public _gl_buffers_e__FixedBuffer gl_buffers;

        [NativeTypeName("const void *[2]")]
        public _mtl_buffers_e__FixedBuffer mtl_buffers;

        [NativeTypeName("const void *")]
        public void* d3d11_buffer;

        [NativeTypeName("const void *")]
        public void* wgpu_buffer;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;

        [InlineArray(2)]
        public partial struct _gl_buffers_e__FixedBuffer
        {
            public uint e0;
        }

        public unsafe partial struct _mtl_buffers_e__FixedBuffer
        {
            public void* e0;
            public void* e1;

            public ref void* this[int index]
            {
                get
                {
                    fixed (void** pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public partial struct sg_image_usage
    {
        [NativeTypeName("_Bool")]
        public byte storage_image;

        [NativeTypeName("_Bool")]
        public byte color_attachment;

        [NativeTypeName("_Bool")]
        public byte resolve_attachment;

        [NativeTypeName("_Bool")]
        public byte depth_stencil_attachment;

        [NativeTypeName("_Bool")]
        public byte immutable;

        [NativeTypeName("_Bool")]
        public byte write_unsealed;

        [NativeTypeName("_Bool")]
        public byte write_transient;

        [NativeTypeName("_Bool")]
        public byte dynamic_update;
    }

    public enum sg_view_type
    {
        SG_VIEWTYPE_INVALID,
        SG_VIEWTYPE_STORAGEBUFFER,
        SG_VIEWTYPE_STORAGEIMAGE,
        SG_VIEWTYPE_TEXTURE,
        SG_VIEWTYPE_COLORATTACHMENT,
        SG_VIEWTYPE_RESOLVEATTACHMENT,
        SG_VIEWTYPE_DEPTHSTENCILATTACHMENT,
    }

    public partial struct sg_image_data
    {
        [NativeTypeName("sg_range[16]")]
        public _mip_levels_e__FixedBuffer mip_levels;

        [InlineArray(16)]
        public partial struct _mip_levels_e__FixedBuffer
        {
            public sg_range e0;
        }
    }

    public partial struct sg_image_extent
    {
        public int width;

        public int height;

        public int num_slices;
    }

    public partial struct sg_image_location
    {
        public sg_image image;

        public int mip_level;

        public int x;

        public int y;

        public int slice;
    }

    public partial struct sg_write_image_source
    {
        public sg_range data;

        [NativeTypeName("size_t")]
        public nuint offset;

        public int bytes_per_row;

        public int bytes_per_slice;
    }

    public partial struct sg_write_image_desc
    {
        public sg_write_image_source src;

        public sg_image_location dst;

        public sg_image_extent size;
    }

    public partial struct sg_buffer_location
    {
        public sg_buffer buffer;

        [NativeTypeName("size_t")]
        public nuint offset;
    }

    public partial struct sg_write_buffer_source
    {
        public sg_range data;

        [NativeTypeName("size_t")]
        public nuint offset;
    }

    public partial struct sg_write_buffer_desc
    {
        public sg_write_buffer_source src;

        public sg_buffer_location dst;

        [NativeTypeName("size_t")]
        public nuint size;
    }

    public unsafe partial struct sg_image_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        public sg_image_type type;

        public sg_image_usage usage;

        public int width;

        public int height;

        public int num_slices;

        public int num_mipmaps;

        public sg_pixel_format pixel_format;

        public int sample_count;

        public sg_image_data data;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t[2]")]
        public _gl_textures_e__FixedBuffer gl_textures;

        [NativeTypeName("uint32_t")]
        public uint gl_texture_target;

        [NativeTypeName("const void *[2]")]
        public _mtl_textures_e__FixedBuffer mtl_textures;

        [NativeTypeName("const void *")]
        public void* d3d11_texture;

        [NativeTypeName("const void *")]
        public void* wgpu_texture;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;

        [InlineArray(2)]
        public partial struct _gl_textures_e__FixedBuffer
        {
            public uint e0;
        }

        public unsafe partial struct _mtl_textures_e__FixedBuffer
        {
            public void* e0;
            public void* e1;

            public ref void* this[int index]
            {
                get
                {
                    fixed (void** pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public unsafe partial struct sg_sampler_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        public sg_filter min_filter;

        public sg_filter mag_filter;

        public sg_filter mipmap_filter;

        public sg_wrap wrap_u;

        public sg_wrap wrap_v;

        public sg_wrap wrap_w;

        public float min_lod;

        public float max_lod;

        public sg_border_color border_color;

        public sg_compare_func compare;

        [NativeTypeName("uint32_t")]
        public uint max_anisotropy;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t")]
        public uint gl_sampler;

        [NativeTypeName("const void *")]
        public void* mtl_sampler;

        [NativeTypeName("const void *")]
        public void* d3d11_sampler;

        [NativeTypeName("const void *")]
        public void* wgpu_sampler;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;
    }

    public enum sg_shader_stage
    {
        SG_SHADERSTAGE_NONE,
        SG_SHADERSTAGE_VERTEX,
        SG_SHADERSTAGE_FRAGMENT,
        SG_SHADERSTAGE_COMPUTE,
    }

    public unsafe partial struct sg_shader_function
    {
        [NativeTypeName("const char *")]
        public sbyte* source;

        public sg_range bytecode;

        [NativeTypeName("const char *")]
        public sbyte* entry;

        [NativeTypeName("const char *")]
        public sbyte* d3d11_target;

        [NativeTypeName("const char *")]
        public sbyte* d3d11_filepath;
    }

    public enum sg_shader_attr_base_type
    {
        SG_SHADERATTRBASETYPE_UNDEFINED,
        SG_SHADERATTRBASETYPE_FLOAT,
        SG_SHADERATTRBASETYPE_SINT,
        SG_SHADERATTRBASETYPE_UINT,
    }

    public unsafe partial struct sg_shader_vertex_attr
    {
        public sg_shader_attr_base_type base_type;

        [NativeTypeName("const char *")]
        public sbyte* glsl_name;

        [NativeTypeName("const char *")]
        public sbyte* hlsl_sem_name;

        [NativeTypeName("uint8_t")]
        public byte hlsl_sem_index;
    }

    public unsafe partial struct sg_glsl_shader_uniform
    {
        public sg_uniform_type type;

        [NativeTypeName("uint16_t")]
        public ushort array_count;

        [NativeTypeName("const char *")]
        public sbyte* glsl_name;
    }

    public partial struct sg_shader_uniform_block
    {
        public sg_shader_stage stage;

        [NativeTypeName("uint32_t")]
        public uint size;

        [NativeTypeName("uint8_t")]
        public byte hlsl_register_b_n;

        [NativeTypeName("uint8_t")]
        public byte msl_buffer_n;

        [NativeTypeName("uint8_t")]
        public byte wgsl_group0_binding_n;

        [NativeTypeName("uint8_t")]
        public byte spirv_set0_binding_n;

        public sg_uniform_layout layout;

        [NativeTypeName("sg_glsl_shader_uniform[16]")]
        public _glsl_uniforms_e__FixedBuffer glsl_uniforms;

        [InlineArray(16)]
        public partial struct _glsl_uniforms_e__FixedBuffer
        {
            public sg_glsl_shader_uniform e0;
        }
    }

    public partial struct sg_shader_texture_view
    {
        public sg_shader_stage stage;

        public sg_image_type image_type;

        public sg_image_sample_type sample_type;

        [NativeTypeName("_Bool")]
        public byte multisampled;

        [NativeTypeName("uint8_t")]
        public byte hlsl_register_t_n;

        [NativeTypeName("uint8_t")]
        public byte msl_texture_n;

        [NativeTypeName("uint8_t")]
        public byte wgsl_group1_binding_n;

        [NativeTypeName("uint8_t")]
        public byte spirv_set1_binding_n;
    }

    public partial struct sg_shader_storage_buffer_view
    {
        public sg_shader_stage stage;

        [NativeTypeName("_Bool")]
        public byte @readonly;

        [NativeTypeName("uint8_t")]
        public byte hlsl_register_t_n;

        [NativeTypeName("uint8_t")]
        public byte hlsl_register_u_n;

        [NativeTypeName("uint8_t")]
        public byte msl_buffer_n;

        [NativeTypeName("uint8_t")]
        public byte wgsl_group1_binding_n;

        [NativeTypeName("uint8_t")]
        public byte spirv_set1_binding_n;

        [NativeTypeName("uint8_t")]
        public byte glsl_binding_n;
    }

    public partial struct sg_shader_storage_image_view
    {
        public sg_shader_stage stage;

        public sg_image_type image_type;

        public sg_pixel_format access_format;

        [NativeTypeName("_Bool")]
        public byte writeonly;

        [NativeTypeName("uint8_t")]
        public byte hlsl_register_u_n;

        [NativeTypeName("uint8_t")]
        public byte msl_texture_n;

        [NativeTypeName("uint8_t")]
        public byte wgsl_group1_binding_n;

        [NativeTypeName("uint8_t")]
        public byte spirv_set1_binding_n;

        [NativeTypeName("uint8_t")]
        public byte glsl_binding_n;
    }

    public partial struct sg_shader_view
    {
        public sg_shader_texture_view texture;

        public sg_shader_storage_buffer_view storage_buffer;

        public sg_shader_storage_image_view storage_image;
    }

    public partial struct sg_shader_sampler
    {
        public sg_shader_stage stage;

        public sg_sampler_type sampler_type;

        [NativeTypeName("uint8_t")]
        public byte hlsl_register_s_n;

        [NativeTypeName("uint8_t")]
        public byte msl_sampler_n;

        [NativeTypeName("uint8_t")]
        public byte wgsl_group1_binding_n;

        [NativeTypeName("uint8_t")]
        public byte spirv_set1_binding_n;
    }

    public unsafe partial struct sg_shader_texture_sampler_pair
    {
        public sg_shader_stage stage;

        [NativeTypeName("uint8_t")]
        public byte view_slot;

        [NativeTypeName("uint8_t")]
        public byte sampler_slot;

        [NativeTypeName("const char *")]
        public sbyte* glsl_name;
    }

    public partial struct sg_mtl_shader_threads_per_threadgroup
    {
        public int x;

        public int y;

        public int z;
    }

    public unsafe partial struct sg_shader_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        public sg_shader_function vertex_func;

        public sg_shader_function fragment_func;

        public sg_shader_function compute_func;

        [NativeTypeName("sg_shader_vertex_attr[16]")]
        public _attrs_e__FixedBuffer attrs;

        [NativeTypeName("sg_shader_uniform_block[8]")]
        public _uniform_blocks_e__FixedBuffer uniform_blocks;

        [NativeTypeName("sg_shader_view[32]")]
        public _views_e__FixedBuffer views;

        [NativeTypeName("sg_shader_sampler[12]")]
        public _samplers_e__FixedBuffer samplers;

        [NativeTypeName("sg_shader_texture_sampler_pair[32]")]
        public _texture_sampler_pairs_e__FixedBuffer texture_sampler_pairs;

        public sg_mtl_shader_threads_per_threadgroup mtl_threads_per_threadgroup;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;

        [InlineArray(16)]
        public partial struct _attrs_e__FixedBuffer
        {
            public sg_shader_vertex_attr e0;
        }

        [InlineArray(8)]
        public partial struct _uniform_blocks_e__FixedBuffer
        {
            public sg_shader_uniform_block e0;
        }

        [InlineArray(32)]
        public partial struct _views_e__FixedBuffer
        {
            public sg_shader_view e0;
        }

        [InlineArray(12)]
        public partial struct _samplers_e__FixedBuffer
        {
            public sg_shader_sampler e0;
        }

        [InlineArray(32)]
        public partial struct _texture_sampler_pairs_e__FixedBuffer
        {
            public sg_shader_texture_sampler_pair e0;
        }
    }

    public partial struct sg_vertex_buffer_layout_state
    {
        public int stride;

        public sg_vertex_step step_func;

        public int step_rate;
    }

    public partial struct sg_vertex_attr_state
    {
        public int buffer_index;

        public int offset;

        public sg_vertex_format format;
    }

    public partial struct sg_vertex_layout_state
    {
        [NativeTypeName("sg_vertex_buffer_layout_state[8]")]
        public _buffers_e__FixedBuffer buffers;

        [NativeTypeName("sg_vertex_attr_state[16]")]
        public _attrs_e__FixedBuffer attrs;

        [InlineArray(8)]
        public partial struct _buffers_e__FixedBuffer
        {
            public sg_vertex_buffer_layout_state e0;
        }

        [InlineArray(16)]
        public partial struct _attrs_e__FixedBuffer
        {
            public sg_vertex_attr_state e0;
        }
    }

    public partial struct sg_stencil_face_state
    {
        public sg_compare_func compare;

        public sg_stencil_op fail_op;

        public sg_stencil_op depth_fail_op;

        public sg_stencil_op pass_op;
    }

    public partial struct sg_stencil_state
    {
        [NativeTypeName("_Bool")]
        public byte enabled;

        public sg_stencil_face_state front;

        public sg_stencil_face_state back;

        [NativeTypeName("uint8_t")]
        public byte read_mask;

        [NativeTypeName("uint8_t")]
        public byte write_mask;

        [NativeTypeName("uint8_t")]
        public byte @ref;
    }

    public partial struct sg_depth_state
    {
        public sg_pixel_format pixel_format;

        public sg_compare_func compare;

        [NativeTypeName("_Bool")]
        public byte write_enabled;

        public float bias;

        public float bias_slope_scale;

        public float bias_clamp;
    }

    public partial struct sg_blend_state
    {
        [NativeTypeName("_Bool")]
        public byte enabled;

        public sg_blend_factor src_factor_rgb;

        public sg_blend_factor dst_factor_rgb;

        public sg_blend_op op_rgb;

        public sg_blend_factor src_factor_alpha;

        public sg_blend_factor dst_factor_alpha;

        public sg_blend_op op_alpha;
    }

    public partial struct sg_color_target_state
    {
        public sg_pixel_format pixel_format;

        public sg_color_mask write_mask;

        public sg_blend_state blend;
    }

    public unsafe partial struct sg_pipeline_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        [NativeTypeName("_Bool")]
        public byte compute;

        public sg_shader shader;

        public sg_vertex_layout_state layout;

        public sg_depth_state depth;

        public sg_stencil_state stencil;

        public int color_count;

        [NativeTypeName("sg_color_target_state[8]")]
        public _colors_e__FixedBuffer colors;

        public sg_primitive_type primitive_type;

        public sg_index_type index_type;

        public sg_cull_mode cull_mode;

        public sg_face_winding face_winding;

        public int sample_count;

        public sg_color blend_color;

        [NativeTypeName("_Bool")]
        public byte alpha_to_coverage_enabled;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;

        [InlineArray(8)]
        public partial struct _colors_e__FixedBuffer
        {
            public sg_color_target_state e0;
        }
    }

    public partial struct sg_buffer_view_desc
    {
        public sg_buffer buffer;

        public int offset;
    }

    public partial struct sg_image_view_desc
    {
        public sg_image image;

        public int mip_level;

        public int slice;
    }

    public partial struct sg_texture_view_range
    {
        public int @base;

        public int count;
    }

    public partial struct sg_texture_view_desc
    {
        public sg_image image;

        public sg_texture_view_range mip_levels;

        public sg_texture_view_range slices;
    }

    public unsafe partial struct sg_view_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        public sg_texture_view_desc texture;

        public sg_buffer_view_desc storage_buffer;

        public sg_image_view_desc storage_image;

        public sg_image_view_desc color_attachment;

        public sg_image_view_desc resolve_attachment;

        public sg_image_view_desc depth_stencil_attachment;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;
    }

    public unsafe partial struct sg_trace_hooks
    {
        public void* user_data;

        [NativeTypeName("void (*)(void *)")]
        public delegate* unmanaged[Cdecl]<void*, void> reset_state_cache;

        [NativeTypeName("void (*)(const sg_buffer_desc *, sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer_desc*, sg_buffer, void*, void> make_buffer;

        [NativeTypeName("void (*)(const sg_image_desc *, sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image_desc*, sg_image, void*, void> make_image;

        [NativeTypeName("void (*)(const sg_sampler_desc *, sg_sampler, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler_desc*, sg_sampler, void*, void> make_sampler;

        [NativeTypeName("void (*)(const sg_shader_desc *, sg_shader, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader_desc*, sg_shader, void*, void> make_shader;

        [NativeTypeName("void (*)(const sg_pipeline_desc *, sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline_desc*, sg_pipeline, void*, void> make_pipeline;

        [NativeTypeName("void (*)(const sg_view_desc *, sg_view, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view_desc*, sg_view, void*, void> make_view;

        [NativeTypeName("void (*)(sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, void*, void> destroy_buffer;

        [NativeTypeName("void (*)(sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, void*, void> destroy_image;

        [NativeTypeName("void (*)(sg_sampler, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler, void*, void> destroy_sampler;

        [NativeTypeName("void (*)(sg_shader, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader, void*, void> destroy_shader;

        [NativeTypeName("void (*)(sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, void*, void> destroy_pipeline;

        [NativeTypeName("void (*)(sg_view, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view, void*, void> destroy_view;

        [NativeTypeName("void (*)(sg_buffer, const sg_range *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, sg_range*, void*, void> update_buffer;

        [NativeTypeName("void (*)(sg_image, const sg_image_data *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, sg_image_data*, void*, void> update_image;

        [NativeTypeName("void (*)(sg_buffer, const sg_range *, int, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, sg_range*, int, void*, void> append_buffer;

        [NativeTypeName("void (*)(const sg_write_buffer_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_write_buffer_desc*, void*, void> write_buffer_transient;

        [NativeTypeName("void (*)(const sg_write_image_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_write_image_desc*, void*, void> write_image_transient;

        [NativeTypeName("void (*)(const sg_write_buffer_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_write_buffer_desc*, void*, void> write_buffer_unsealed;

        [NativeTypeName("void (*)(const sg_write_image_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_write_image_desc*, void*, void> write_image_unsealed;

        [NativeTypeName("void (*)(sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, void*, void> seal_buffer;

        [NativeTypeName("void (*)(sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, void*, void> seal_image;

        [NativeTypeName("void (*)(const sg_pass *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pass*, void*, void> begin_pass;

        [NativeTypeName("void (*)(int, int, int, int, _Bool, void *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, int, byte, void*, void> apply_viewport;

        [NativeTypeName("void (*)(int, int, int, int, _Bool, void *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, int, byte, void*, void> apply_scissor_rect;

        [NativeTypeName("void (*)(sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, void*, void> apply_pipeline;

        [NativeTypeName("void (*)(const sg_bindings *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_bindings*, void*, void> apply_bindings;

        [NativeTypeName("void (*)(int, const sg_range *, void *)")]
        public delegate* unmanaged[Cdecl]<int, sg_range*, void*, void> apply_uniforms;

        [NativeTypeName("void (*)(int, int, int, void *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, void*, void> draw;

        [NativeTypeName("void (*)(int, int, int, int, int, void *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, void*, void> draw_ex;

        [NativeTypeName("void (*)(int, int, int, void *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, void*, void> dispatch;

        [NativeTypeName("void (*)(void *)")]
        public delegate* unmanaged[Cdecl]<void*, void> end_pass;

        [NativeTypeName("void (*)(void *)")]
        public delegate* unmanaged[Cdecl]<void*, void> commit;

        [NativeTypeName("void (*)(sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, void*, void> alloc_buffer;

        [NativeTypeName("void (*)(sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, void*, void> alloc_image;

        [NativeTypeName("void (*)(sg_sampler, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler, void*, void> alloc_sampler;

        [NativeTypeName("void (*)(sg_shader, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader, void*, void> alloc_shader;

        [NativeTypeName("void (*)(sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, void*, void> alloc_pipeline;

        [NativeTypeName("void (*)(sg_view, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view, void*, void> alloc_view;

        [NativeTypeName("void (*)(sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, void*, void> dealloc_buffer;

        [NativeTypeName("void (*)(sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, void*, void> dealloc_image;

        [NativeTypeName("void (*)(sg_sampler, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler, void*, void> dealloc_sampler;

        [NativeTypeName("void (*)(sg_shader, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader, void*, void> dealloc_shader;

        [NativeTypeName("void (*)(sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, void*, void> dealloc_pipeline;

        [NativeTypeName("void (*)(sg_view, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view, void*, void> dealloc_view;

        [NativeTypeName("void (*)(sg_buffer, const sg_buffer_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, sg_buffer_desc*, void*, void> init_buffer;

        [NativeTypeName("void (*)(sg_image, const sg_image_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, sg_image_desc*, void*, void> init_image;

        [NativeTypeName("void (*)(sg_sampler, const sg_sampler_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler, sg_sampler_desc*, void*, void> init_sampler;

        [NativeTypeName("void (*)(sg_shader, const sg_shader_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader, sg_shader_desc*, void*, void> init_shader;

        [NativeTypeName("void (*)(sg_pipeline, const sg_pipeline_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, sg_pipeline_desc*, void*, void> init_pipeline;

        [NativeTypeName("void (*)(sg_view, const sg_view_desc *, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view, sg_view_desc*, void*, void> init_view;

        [NativeTypeName("void (*)(sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, void*, void> uninit_buffer;

        [NativeTypeName("void (*)(sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, void*, void> uninit_image;

        [NativeTypeName("void (*)(sg_sampler, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler, void*, void> uninit_sampler;

        [NativeTypeName("void (*)(sg_shader, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader, void*, void> uninit_shader;

        [NativeTypeName("void (*)(sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, void*, void> uninit_pipeline;

        [NativeTypeName("void (*)(sg_view, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view, void*, void> uninit_view;

        [NativeTypeName("void (*)(sg_buffer, void *)")]
        public delegate* unmanaged[Cdecl]<sg_buffer, void*, void> fail_buffer;

        [NativeTypeName("void (*)(sg_image, void *)")]
        public delegate* unmanaged[Cdecl]<sg_image, void*, void> fail_image;

        [NativeTypeName("void (*)(sg_sampler, void *)")]
        public delegate* unmanaged[Cdecl]<sg_sampler, void*, void> fail_sampler;

        [NativeTypeName("void (*)(sg_shader, void *)")]
        public delegate* unmanaged[Cdecl]<sg_shader, void*, void> fail_shader;

        [NativeTypeName("void (*)(sg_pipeline, void *)")]
        public delegate* unmanaged[Cdecl]<sg_pipeline, void*, void> fail_pipeline;

        [NativeTypeName("void (*)(sg_view, void *)")]
        public delegate* unmanaged[Cdecl]<sg_view, void*, void> fail_view;

        [NativeTypeName("void (*)(const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, void*, void> push_debug_group;

        [NativeTypeName("void (*)(void *)")]
        public delegate* unmanaged[Cdecl]<void*, void> pop_debug_group;
    }

    public partial struct sg_slot_info
    {
        public sg_resource_state state;

        [NativeTypeName("uint32_t")]
        public uint res_id;

        [NativeTypeName("uint32_t")]
        public uint uninit_count;
    }

    public partial struct sg_buffer_info
    {
        public sg_slot_info slot;

        public int num_slots;

        public int active_slot;

        [NativeTypeName("uint32_t")]
        public uint update_frame_index;

        [NativeTypeName("uint32_t")]
        public uint append_frame_index;

        public int append_pos;

        [NativeTypeName("_Bool")]
        public byte append_overflow;
    }

    public partial struct sg_image_info
    {
        public sg_slot_info slot;

        public int num_slots;

        public int active_slot;

        [NativeTypeName("uint32_t")]
        public uint upd_frame_index;
    }

    public partial struct sg_sampler_info
    {
        public sg_slot_info slot;
    }

    public partial struct sg_shader_info
    {
        public sg_slot_info slot;
    }

    public partial struct sg_pipeline_info
    {
        public sg_slot_info slot;
    }

    public partial struct sg_view_info
    {
        public sg_slot_info slot;
    }

    public partial struct sg_frame_stats_gl
    {
        [NativeTypeName("uint32_t")]
        public uint num_bind_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_active_texture;

        [NativeTypeName("uint32_t")]
        public uint num_bind_texture;

        [NativeTypeName("uint32_t")]
        public uint num_bind_sampler;

        [NativeTypeName("uint32_t")]
        public uint num_bind_image_texture;

        [NativeTypeName("uint32_t")]
        public uint num_use_program;

        [NativeTypeName("uint32_t")]
        public uint num_render_state;

        [NativeTypeName("uint32_t")]
        public uint num_vertex_attrib_pointer;

        [NativeTypeName("uint32_t")]
        public uint num_vertex_attrib_divisor;

        [NativeTypeName("uint32_t")]
        public uint num_enable_vertex_attrib_array;

        [NativeTypeName("uint32_t")]
        public uint num_disable_vertex_attrib_array;

        [NativeTypeName("uint32_t")]
        public uint num_uniform;

        [NativeTypeName("uint32_t")]
        public uint num_memory_barriers;
    }

    public partial struct sg_frame_stats_d3d11_pass
    {
        [NativeTypeName("uint32_t")]
        public uint num_om_set_render_targets;

        [NativeTypeName("uint32_t")]
        public uint num_clear_render_target_view;

        [NativeTypeName("uint32_t")]
        public uint num_clear_depth_stencil_view;

        [NativeTypeName("uint32_t")]
        public uint num_resolve_subresource;
    }

    public partial struct sg_frame_stats_d3d11_pipeline
    {
        [NativeTypeName("uint32_t")]
        public uint num_rs_set_state;

        [NativeTypeName("uint32_t")]
        public uint num_om_set_depth_stencil_state;

        [NativeTypeName("uint32_t")]
        public uint num_om_set_blend_state;

        [NativeTypeName("uint32_t")]
        public uint num_ia_set_primitive_topology;

        [NativeTypeName("uint32_t")]
        public uint num_ia_set_input_layout;

        [NativeTypeName("uint32_t")]
        public uint num_vs_set_shader;

        [NativeTypeName("uint32_t")]
        public uint num_vs_set_constant_buffers;

        [NativeTypeName("uint32_t")]
        public uint num_ps_set_shader;

        [NativeTypeName("uint32_t")]
        public uint num_ps_set_constant_buffers;

        [NativeTypeName("uint32_t")]
        public uint num_cs_set_shader;

        [NativeTypeName("uint32_t")]
        public uint num_cs_set_constant_buffers;
    }

    public partial struct sg_frame_stats_d3d11_bindings
    {
        [NativeTypeName("uint32_t")]
        public uint num_ia_set_vertex_buffers;

        [NativeTypeName("uint32_t")]
        public uint num_ia_set_index_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_vs_set_shader_resources;

        [NativeTypeName("uint32_t")]
        public uint num_vs_set_samplers;

        [NativeTypeName("uint32_t")]
        public uint num_ps_set_shader_resources;

        [NativeTypeName("uint32_t")]
        public uint num_ps_set_samplers;

        [NativeTypeName("uint32_t")]
        public uint num_cs_set_shader_resources;

        [NativeTypeName("uint32_t")]
        public uint num_cs_set_samplers;

        [NativeTypeName("uint32_t")]
        public uint num_cs_set_unordered_access_views;
    }

    public partial struct sg_frame_stats_d3d11_uniforms
    {
        [NativeTypeName("uint32_t")]
        public uint num_update_subresource;
    }

    public partial struct sg_frame_stats_d3d11_draw
    {
        [NativeTypeName("uint32_t")]
        public uint num_draw_indexed_instanced;

        [NativeTypeName("uint32_t")]
        public uint num_draw_indexed;

        [NativeTypeName("uint32_t")]
        public uint num_draw_instanced;

        [NativeTypeName("uint32_t")]
        public uint num_draw;
    }

    public partial struct sg_frame_stats_d3d11
    {
        public sg_frame_stats_d3d11_pass pass;

        public sg_frame_stats_d3d11_pipeline pipeline;

        public sg_frame_stats_d3d11_bindings bindings;

        public sg_frame_stats_d3d11_uniforms uniforms;

        public sg_frame_stats_d3d11_draw draw;

        [NativeTypeName("uint32_t")]
        public uint num_map;

        [NativeTypeName("uint32_t")]
        public uint num_unmap;
    }

    public partial struct sg_frame_stats_metal_idpool
    {
        [NativeTypeName("uint32_t")]
        public uint num_added;

        [NativeTypeName("uint32_t")]
        public uint num_released;

        [NativeTypeName("uint32_t")]
        public uint num_garbage_collected;
    }

    public partial struct sg_frame_stats_metal_pipeline
    {
        [NativeTypeName("uint32_t")]
        public uint num_set_blend_color;

        [NativeTypeName("uint32_t")]
        public uint num_set_cull_mode;

        [NativeTypeName("uint32_t")]
        public uint num_set_front_facing_winding;

        [NativeTypeName("uint32_t")]
        public uint num_set_stencil_reference_value;

        [NativeTypeName("uint32_t")]
        public uint num_set_depth_bias;

        [NativeTypeName("uint32_t")]
        public uint num_set_render_pipeline_state;

        [NativeTypeName("uint32_t")]
        public uint num_set_depth_stencil_state;
    }

    public partial struct sg_frame_stats_metal_bindings
    {
        [NativeTypeName("uint32_t")]
        public uint num_set_vertex_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_vertex_buffer_offset;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_vertex_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_vertex_texture;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_vertex_texture;

        [NativeTypeName("uint32_t")]
        public uint num_set_vertex_sampler_state;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_vertex_sampler_state;

        [NativeTypeName("uint32_t")]
        public uint num_set_fragment_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_fragment_buffer_offset;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_fragment_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_fragment_texture;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_fragment_texture;

        [NativeTypeName("uint32_t")]
        public uint num_set_fragment_sampler_state;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_fragment_sampler_state;

        [NativeTypeName("uint32_t")]
        public uint num_set_compute_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_compute_buffer_offset;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_compute_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_compute_texture;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_compute_texture;

        [NativeTypeName("uint32_t")]
        public uint num_set_compute_sampler_state;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_compute_sampler_state;
    }

    public partial struct sg_frame_stats_metal_uniforms
    {
        [NativeTypeName("uint32_t")]
        public uint num_set_vertex_buffer_offset;

        [NativeTypeName("uint32_t")]
        public uint num_set_fragment_buffer_offset;

        [NativeTypeName("uint32_t")]
        public uint num_set_compute_buffer_offset;
    }

    public partial struct sg_frame_stats_metal
    {
        public sg_frame_stats_metal_idpool idpool;

        public sg_frame_stats_metal_pipeline pipeline;

        public sg_frame_stats_metal_bindings bindings;

        public sg_frame_stats_metal_uniforms uniforms;
    }

    public partial struct sg_frame_stats_wgpu_uniforms
    {
        [NativeTypeName("uint32_t")]
        public uint num_set_bindgroup;

        [NativeTypeName("uint32_t")]
        public uint size_write_buffer;
    }

    public partial struct sg_frame_stats_wgpu_bindings
    {
        [NativeTypeName("uint32_t")]
        public uint num_set_vertex_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_vertex_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_set_index_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_index_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_create_bindgroup;

        [NativeTypeName("uint32_t")]
        public uint num_discard_bindgroup;

        [NativeTypeName("uint32_t")]
        public uint num_set_bindgroup;

        [NativeTypeName("uint32_t")]
        public uint num_skip_redundant_bindgroup;

        [NativeTypeName("uint32_t")]
        public uint num_bindgroup_cache_hits;

        [NativeTypeName("uint32_t")]
        public uint num_bindgroup_cache_misses;

        [NativeTypeName("uint32_t")]
        public uint num_bindgroup_cache_collisions;

        [NativeTypeName("uint32_t")]
        public uint num_bindgroup_cache_invalidates;

        [NativeTypeName("uint32_t")]
        public uint num_bindgroup_cache_hash_vs_key_mismatch;
    }

    public partial struct sg_frame_stats_wgpu
    {
        public sg_frame_stats_wgpu_uniforms uniforms;

        public sg_frame_stats_wgpu_bindings bindings;
    }

    public partial struct sg_frame_stats_vk
    {
        [NativeTypeName("uint32_t")]
        public uint num_cmd_pipeline_barrier;

        [NativeTypeName("uint32_t")]
        public uint num_allocate_memory;

        [NativeTypeName("uint32_t")]
        public uint num_free_memory;

        [NativeTypeName("uint32_t")]
        public uint size_allocate_memory;

        [NativeTypeName("uint32_t")]
        public uint num_delete_queue_added;

        [NativeTypeName("uint32_t")]
        public uint num_delete_queue_collected;

        [NativeTypeName("uint32_t")]
        public uint num_cmd_copy_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_cmd_copy_buffer_to_image;

        [NativeTypeName("uint32_t")]
        public uint num_cmd_set_descriptor_buffer_offsets;

        [NativeTypeName("uint32_t")]
        public uint size_descriptor_buffer_writes;
    }

    public partial struct sg_frame_resource_stats
    {
        [NativeTypeName("uint32_t")]
        public uint allocated;

        [NativeTypeName("uint32_t")]
        public uint deallocated;

        [NativeTypeName("uint32_t")]
        public uint inited;

        [NativeTypeName("uint32_t")]
        public uint uninited;
    }

    public partial struct sg_total_resource_stats
    {
        [NativeTypeName("uint32_t")]
        public uint alive;

        [NativeTypeName("uint32_t")]
        public uint free;

        [NativeTypeName("uint32_t")]
        public uint allocated;

        [NativeTypeName("uint32_t")]
        public uint deallocated;

        [NativeTypeName("uint32_t")]
        public uint inited;

        [NativeTypeName("uint32_t")]
        public uint uninited;
    }

    public partial struct sg_total_stats
    {
        public sg_total_resource_stats buffers;

        public sg_total_resource_stats images;

        public sg_total_resource_stats samplers;

        public sg_total_resource_stats views;

        public sg_total_resource_stats shaders;

        public sg_total_resource_stats pipelines;
    }

    public partial struct sg_frame_stats
    {
        [NativeTypeName("uint32_t")]
        public uint frame_index;

        [NativeTypeName("uint32_t")]
        public uint num_passes;

        [NativeTypeName("uint32_t")]
        public uint num_apply_viewport;

        [NativeTypeName("uint32_t")]
        public uint num_apply_scissor_rect;

        [NativeTypeName("uint32_t")]
        public uint num_apply_pipeline;

        [NativeTypeName("uint32_t")]
        public uint num_apply_bindings;

        [NativeTypeName("uint32_t")]
        public uint num_apply_uniforms;

        [NativeTypeName("uint32_t")]
        public uint num_draw;

        [NativeTypeName("uint32_t")]
        public uint num_draw_ex;

        [NativeTypeName("uint32_t")]
        public uint num_dispatch;

        [NativeTypeName("uint32_t")]
        public uint num_update_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_append_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_update_image;

        [NativeTypeName("uint32_t")]
        public uint num_write_buffer_transient;

        [NativeTypeName("uint32_t")]
        public uint num_write_image_transient;

        [NativeTypeName("uint32_t")]
        public uint num_write_buffer_unsealed;

        [NativeTypeName("uint32_t")]
        public uint num_write_image_unsealed;

        [NativeTypeName("uint32_t")]
        public uint num_seal_buffer;

        [NativeTypeName("uint32_t")]
        public uint num_seal_image;

        [NativeTypeName("uint32_t")]
        public uint size_apply_uniforms;

        [NativeTypeName("uint32_t")]
        public uint size_update_buffer;

        [NativeTypeName("uint32_t")]
        public uint size_append_buffer;

        [NativeTypeName("uint32_t")]
        public uint size_update_image;

        public sg_frame_resource_stats buffers;

        public sg_frame_resource_stats images;

        public sg_frame_resource_stats samplers;

        public sg_frame_resource_stats views;

        public sg_frame_resource_stats shaders;

        public sg_frame_resource_stats pipelines;

        public sg_frame_stats_gl gl;

        public sg_frame_stats_d3d11 d3d11;

        public sg_frame_stats_metal metal;

        public sg_frame_stats_wgpu wgpu;

        public sg_frame_stats_vk vk;
    }

    public partial struct sg_stats
    {
        public sg_frame_stats prev_frame;

        public sg_frame_stats cur_frame;

        public sg_total_stats total;
    }

    public enum sg_log_item
    {
        SG_LOGITEM_OK,
        SG_LOGITEM_MALLOC_FAILED,
        SG_LOGITEM_GL_TEXTURE_FORMAT_NOT_SUPPORTED,
        SG_LOGITEM_GL_3D_TEXTURES_NOT_SUPPORTED,
        SG_LOGITEM_GL_ARRAY_TEXTURES_NOT_SUPPORTED,
        SG_LOGITEM_GL_STORAGEBUFFER_GLSL_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_GL_STORAGEIMAGE_GLSL_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_GL_SHADER_COMPILATION_FAILED,
        SG_LOGITEM_GL_SHADER_LINKING_FAILED,
        SG_LOGITEM_GL_VERTEX_ATTRIBUTE_NOT_FOUND_IN_SHADER,
        SG_LOGITEM_GL_UNIFORMBLOCK_NAME_NOT_FOUND_IN_SHADER,
        SG_LOGITEM_GL_IMAGE_SAMPLER_NAME_NOT_FOUND_IN_SHADER,
        SG_LOGITEM_GL_FRAMEBUFFER_STATUS_UNDEFINED,
        SG_LOGITEM_GL_FRAMEBUFFER_STATUS_INCOMPLETE_ATTACHMENT,
        SG_LOGITEM_GL_FRAMEBUFFER_STATUS_INCOMPLETE_MISSING_ATTACHMENT,
        SG_LOGITEM_GL_FRAMEBUFFER_STATUS_UNSUPPORTED,
        SG_LOGITEM_GL_FRAMEBUFFER_STATUS_INCOMPLETE_MULTISAMPLE,
        SG_LOGITEM_GL_FRAMEBUFFER_STATUS_UNKNOWN,
        SG_LOGITEM_D3D11_FEATURE_LEVEL_0_DETECTED,
        SG_LOGITEM_D3D11_CREATE_BUFFER_FAILED,
        SG_LOGITEM_D3D11_CREATE_BUFFER_SRV_FAILED,
        SG_LOGITEM_D3D11_CREATE_BUFFER_UAV_FAILED,
        SG_LOGITEM_D3D11_CREATE_DEPTH_TEXTURE_UNSUPPORTED_PIXEL_FORMAT,
        SG_LOGITEM_D3D11_CREATE_DEPTH_TEXTURE_FAILED,
        SG_LOGITEM_D3D11_CREATE_2D_TEXTURE_UNSUPPORTED_PIXEL_FORMAT,
        SG_LOGITEM_D3D11_CREATE_2D_TEXTURE_FAILED,
        SG_LOGITEM_D3D11_CREATE_2D_SRV_FAILED,
        SG_LOGITEM_D3D11_CREATE_3D_TEXTURE_UNSUPPORTED_PIXEL_FORMAT,
        SG_LOGITEM_D3D11_CREATE_3D_TEXTURE_FAILED,
        SG_LOGITEM_D3D11_CREATE_3D_SRV_FAILED,
        SG_LOGITEM_D3D11_CREATE_MSAA_TEXTURE_FAILED,
        SG_LOGITEM_D3D11_CREATE_SAMPLER_STATE_FAILED,
        SG_LOGITEM_D3D11_UNIFORMBLOCK_HLSL_REGISTER_B_OUT_OF_RANGE,
        SG_LOGITEM_D3D11_STORAGEBUFFER_HLSL_REGISTER_T_OUT_OF_RANGE,
        SG_LOGITEM_D3D11_STORAGEBUFFER_HLSL_REGISTER_U_OUT_OF_RANGE,
        SG_LOGITEM_D3D11_IMAGE_HLSL_REGISTER_T_OUT_OF_RANGE,
        SG_LOGITEM_D3D11_STORAGEIMAGE_HLSL_REGISTER_U_OUT_OF_RANGE,
        SG_LOGITEM_D3D11_SAMPLER_HLSL_REGISTER_S_OUT_OF_RANGE,
        SG_LOGITEM_D3D11_LOAD_D3DCOMPILER_47_DLL_FAILED,
        SG_LOGITEM_D3D11_SHADER_COMPILATION_FAILED,
        SG_LOGITEM_D3D11_SHADER_COMPILATION_OUTPUT,
        SG_LOGITEM_D3D11_CREATE_CONSTANT_BUFFER_FAILED,
        SG_LOGITEM_D3D11_CREATE_INPUT_LAYOUT_FAILED,
        SG_LOGITEM_D3D11_CREATE_RASTERIZER_STATE_FAILED,
        SG_LOGITEM_D3D11_CREATE_DEPTH_STENCIL_STATE_FAILED,
        SG_LOGITEM_D3D11_CREATE_BLEND_STATE_FAILED,
        SG_LOGITEM_D3D11_CREATE_RTV_FAILED,
        SG_LOGITEM_D3D11_CREATE_DSV_FAILED,
        SG_LOGITEM_D3D11_CREATE_UAV_FAILED,
        SG_LOGITEM_D3D11_MAP_FOR_UPDATE_BUFFER_FAILED,
        SG_LOGITEM_D3D11_MAP_FOR_APPEND_BUFFER_FAILED,
        SG_LOGITEM_D3D11_MAP_FOR_UPDATE_IMAGE_FAILED,
        SG_LOGITEM_D3D11_MAP_FOR_WRITE_BUFFER_TRANSIENT_FAILED,
        SG_LOGITEM_METAL_CREATE_BUFFER_FAILED,
        SG_LOGITEM_METAL_TEXTURE_FORMAT_NOT_SUPPORTED,
        SG_LOGITEM_METAL_CREATE_TEXTURE_FAILED,
        SG_LOGITEM_METAL_CREATE_SAMPLER_FAILED,
        SG_LOGITEM_METAL_SHADER_COMPILATION_FAILED,
        SG_LOGITEM_METAL_SHADER_CREATION_FAILED,
        SG_LOGITEM_METAL_SHADER_COMPILATION_OUTPUT,
        SG_LOGITEM_METAL_SHADER_ENTRY_NOT_FOUND,
        SG_LOGITEM_METAL_UNIFORMBLOCK_MSL_BUFFER_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_METAL_STORAGEBUFFER_MSL_BUFFER_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_METAL_STORAGEIMAGE_MSL_TEXTURE_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_METAL_IMAGE_MSL_TEXTURE_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_METAL_SAMPLER_MSL_SAMPLER_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_METAL_CREATE_CPS_FAILED,
        SG_LOGITEM_METAL_CREATE_CPS_OUTPUT,
        SG_LOGITEM_METAL_CREATE_RPS_FAILED,
        SG_LOGITEM_METAL_CREATE_RPS_OUTPUT,
        SG_LOGITEM_METAL_CREATE_DSS_FAILED,
        SG_LOGITEM_METAL_CREATE_TEXTUREVIEW_FAILED,
        SG_LOGITEM_WGPU_BINDGROUPS_POOL_EXHAUSTED,
        SG_LOGITEM_WGPU_BINDGROUPSCACHE_SIZE_GREATER_ONE,
        SG_LOGITEM_WGPU_BINDGROUPSCACHE_SIZE_POW2,
        SG_LOGITEM_WGPU_CREATEBINDGROUP_FAILED,
        SG_LOGITEM_WGPU_CREATE_BUFFER_FAILED,
        SG_LOGITEM_WGPU_CREATE_TEXTURE_FAILED,
        SG_LOGITEM_WGPU_CREATE_TEXTURE_VIEW_FAILED,
        SG_LOGITEM_WGPU_CREATE_SAMPLER_FAILED,
        SG_LOGITEM_WGPU_CREATE_SHADER_MODULE_FAILED,
        SG_LOGITEM_WGPU_SHADER_CREATE_BINDGROUP_LAYOUT_FAILED,
        SG_LOGITEM_WGPU_UNIFORMBLOCK_WGSL_GROUP0_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_WGPU_TEXTURE_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_WGPU_STORAGEBUFFER_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_WGPU_STORAGEIMAGE_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_WGPU_SAMPLER_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_WGPU_CREATE_PIPELINE_LAYOUT_FAILED,
        SG_LOGITEM_WGPU_CREATE_RENDER_PIPELINE_FAILED,
        SG_LOGITEM_WGPU_CREATE_COMPUTE_PIPELINE_FAILED,
        SG_LOGITEM_VULKAN_REQUIRED_EXTENSION_FUNCTION_MISSING,
        SG_LOGITEM_VULKAN_ALLOC_DEVICE_MEMORY_NO_SUITABLE_MEMORY_TYPE,
        SG_LOGITEM_VULKAN_ALLOCATE_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_ALLOC_BUFFER_DEVICE_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_ALLOC_IMAGE_DEVICE_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_DELETE_QUEUE_EXHAUSTED,
        SG_LOGITEM_VULKAN_STAGING_CREATE_BUFFER_FAILED,
        SG_LOGITEM_VULKAN_STAGING_ALLOCATE_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_STAGING_BIND_BUFFER_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_STAGING_TRANSIENT_BUFFER_OVERFLOW,
        SG_LOGITEM_VULKAN_STAGING_IMAGE_ROW_PITCH_GREATER_STAGING_BUFFER,
        SG_LOGITEM_VULKAN_CREATE_SHARED_BUFFER_FAILED,
        SG_LOGITEM_VULKAN_ALLOCATE_SHARED_BUFFER_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_BIND_SHARED_BUFFER_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_MAP_SHARED_BUFFER_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_CREATE_BUFFER_FAILED,
        SG_LOGITEM_VULKAN_BIND_BUFFER_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_CREATE_IMAGE_FAILED,
        SG_LOGITEM_VULKAN_BIND_IMAGE_MEMORY_FAILED,
        SG_LOGITEM_VULKAN_CREATE_SHADER_MODULE_FAILED,
        SG_LOGITEM_VULKAN_UNIFORMBLOCK_SPIRV_SET0_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_VULKAN_TEXTURE_SPIRV_SET1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_VULKAN_STORAGEBUFFER_SPIRV_SET1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_VULKAN_STORAGEIMAGE_SPIRV_SET1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_VULKAN_SAMPLER_SPIRV_SET1_BINDING_OUT_OF_RANGE,
        SG_LOGITEM_VULKAN_CREATE_DESCRIPTOR_SET_LAYOUT_FAILED,
        SG_LOGITEM_VULKAN_SHADER_UNIFORM_DESCRIPTOR_SET_SIZE_VS_CACHE_SIZE,
        SG_LOGITEM_VULKAN_CREATE_PIPELINE_LAYOUT_FAILED,
        SG_LOGITEM_VULKAN_CREATE_GRAPHICS_PIPELINE_FAILED,
        SG_LOGITEM_VULKAN_CREATE_COMPUTE_PIPELINE_FAILED,
        SG_LOGITEM_VULKAN_CREATE_IMAGE_VIEW_FAILED,
        SG_LOGITEM_VULKAN_VIEW_MAX_DESCRIPTOR_SIZE,
        SG_LOGITEM_VULKAN_CREATE_SAMPLER_FAILED,
        SG_LOGITEM_VULKAN_SAMPLER_MAX_DESCRIPTOR_SIZE,
        SG_LOGITEM_VULKAN_WAIT_FOR_FENCE_FAILED,
        SG_LOGITEM_VULKAN_UNIFORM_BUFFER_OVERFLOW,
        SG_LOGITEM_VULKAN_DESCRIPTOR_BUFFER_OVERFLOW,
        SG_LOGITEM_IDENTICAL_COMMIT_LISTENER,
        SG_LOGITEM_COMMIT_LISTENER_ARRAY_FULL,
        SG_LOGITEM_TRACE_HOOKS_NOT_ENABLED,
        SG_LOGITEM_DEALLOC_BUFFER_INVALID_STATE,
        SG_LOGITEM_DEALLOC_IMAGE_INVALID_STATE,
        SG_LOGITEM_DEALLOC_SAMPLER_INVALID_STATE,
        SG_LOGITEM_DEALLOC_SHADER_INVALID_STATE,
        SG_LOGITEM_DEALLOC_PIPELINE_INVALID_STATE,
        SG_LOGITEM_DEALLOC_VIEW_INVALID_STATE,
        SG_LOGITEM_INIT_BUFFER_INVALID_STATE,
        SG_LOGITEM_INIT_IMAGE_INVALID_STATE,
        SG_LOGITEM_INIT_SAMPLER_INVALID_STATE,
        SG_LOGITEM_INIT_SHADER_INVALID_STATE,
        SG_LOGITEM_INIT_PIPELINE_INVALID_STATE,
        SG_LOGITEM_INIT_VIEW_INVALID_STATE,
        SG_LOGITEM_UNINIT_BUFFER_INVALID_STATE,
        SG_LOGITEM_UNINIT_IMAGE_INVALID_STATE,
        SG_LOGITEM_UNINIT_SAMPLER_INVALID_STATE,
        SG_LOGITEM_UNINIT_SHADER_INVALID_STATE,
        SG_LOGITEM_UNINIT_PIPELINE_INVALID_STATE,
        SG_LOGITEM_UNINIT_VIEW_INVALID_STATE,
        SG_LOGITEM_FAIL_BUFFER_INVALID_STATE,
        SG_LOGITEM_FAIL_IMAGE_INVALID_STATE,
        SG_LOGITEM_FAIL_SAMPLER_INVALID_STATE,
        SG_LOGITEM_FAIL_SHADER_INVALID_STATE,
        SG_LOGITEM_FAIL_PIPELINE_INVALID_STATE,
        SG_LOGITEM_FAIL_VIEW_INVALID_STATE,
        SG_LOGITEM_BUFFER_POOL_EXHAUSTED,
        SG_LOGITEM_IMAGE_POOL_EXHAUSTED,
        SG_LOGITEM_SAMPLER_POOL_EXHAUSTED,
        SG_LOGITEM_SHADER_POOL_EXHAUSTED,
        SG_LOGITEM_PIPELINE_POOL_EXHAUSTED,
        SG_LOGITEM_VIEW_POOL_EXHAUSTED,
        SG_LOGITEM_BEGINPASS_TOO_MANY_COLOR_ATTACHMENTS,
        SG_LOGITEM_BEGINPASS_TOO_MANY_RESOLVE_ATTACHMENTS,
        SG_LOGITEM_BEGINPASS_ATTACHMENTS_ALIVE,
        SG_LOGITEM_DRAW_WITHOUT_BINDINGS,
        SG_LOGITEM_WRITE_BUFFER_TRANSIENT_BUFFER_ALIVE,
        SG_LOGITEM_WRITE_IMAGE_TRANSIENT_IMAGE_ALIVE,
        SG_LOGITEM_WRITE_BUFFER_UNSEALED_BUFFER_ALIVE,
        SG_LOGITEM_WRITE_IMAGE_UNSEALED_IMAGE_ALIVE,
        SG_LOGITEM_SEAL_BUFFER_ALIVE,
        SG_LOGITEM_SEAL_IMAGE_ALIVE,
        SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_TEXTURES,
        SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_TEXTURES,
        SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_TEXTURES,
        SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_STORAGEBUFFERS,
        SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_STORAGEBUFFERS,
        SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_STORAGEBUFFERS,
        SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_STORAGEIMAGES,
        SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_STORAGEIMAGES,
        SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_STORAGEIMAGES,
        SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_TEXTURESAMPLERPAIRS,
        SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_TEXTURESAMPLERPAIRS,
        SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_TEXTURESAMPLERPAIRS,
        SG_LOGITEM_VALIDATE_BUFFERDESC_CANARY,
        SG_LOGITEM_VALIDATE_BUFFERDESC_IMMUTABLE_VS_WRITABLE,
        SG_LOGITEM_VALIDATE_BUFFERDESC_UNSEALED_VS_IMMUTABLE,
        SG_LOGITEM_VALIDATE_BUFFERDESC_SEPARATE_BUFFER_TYPES,
        SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_NONZERO_SIZE,
        SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_MATCHING_DATA_SIZE,
        SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_ZERO_DATA_SIZE,
        SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_NO_DATA,
        SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_DATA,
        SG_LOGITEM_VALIDATE_BUFFERDESC_STORAGEBUFFER_SUPPORTED,
        SG_LOGITEM_VALIDATE_BUFFERDESC_STORAGEBUFFER_SIZE_MULTIPLE_4,
        SG_LOGITEM_VALIDATE_IMAGEDATA_NODATA,
        SG_LOGITEM_VALIDATE_IMAGEDATA_DATA_SIZE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_CANARY,
        SG_LOGITEM_VALIDATE_IMAGEDESC_IMMUTABLE_VS_WRITABLE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_WRITE_UNSEALED_VS_IMMUTABLE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_WRITE_UNSEALED_VS_ATTACHMENT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_WRITE_TRANSIENT_VS_ATTACHMENT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_DYNAMIC_UPDATE_VS_ATTACHMENT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_COLOR_DEPTH_STENCIL,
        SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_2D_NUMSLICES,
        SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_CUBE_NUMSLICES,
        SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_ARRAY_NUMSLICES,
        SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_3D_NUMSLICES,
        SG_LOGITEM_VALIDATE_IMAGEDESC_NUMSLICES,
        SG_LOGITEM_VALIDATE_IMAGEDESC_WIDTH,
        SG_LOGITEM_VALIDATE_IMAGEDESC_HEIGHT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_NONRT_PIXELFORMAT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_MSAA_BUT_NO_ATTACHMENT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_DEPTH_3D_IMAGE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_EXPECT_IMMUTABLE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_EXPECT_NO_DATA,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_PIXELFORMAT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_RESOLVE_EXPECT_NO_MSAA,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_NO_MSAA_SUPPORT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_NUM_MIPMAPS,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_3D_IMAGE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_CUBE_IMAGE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_ARRAY_IMAGE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_STORAGEIMAGE_EXPECT_IMMUTABLE,
        SG_LOGITEM_VALIDATE_IMAGEDESC_STORAGEIMAGE_EXPECT_NO_DATA,
        SG_LOGITEM_VALIDATE_IMAGEDESC_STORAGEIMAGE_PIXELFORMAT,
        SG_LOGITEM_VALIDATE_IMAGEDESC_STORAGEIMAGE_EXPECT_NO_MSAA,
        SG_LOGITEM_VALIDATE_IMAGEDESC_INJECTED_NO_DATA,
        SG_LOGITEM_VALIDATE_IMAGEDESC_WRITABLE_NO_DATA,
        SG_LOGITEM_VALIDATE_IMAGEDESC_COMPRESSED_IMMUTABLE,
        SG_LOGITEM_VALIDATE_SAMPLERDESC_CANARY,
        SG_LOGITEM_VALIDATE_SAMPLERDESC_ANISTROPIC_REQUIRES_LINEAR_FILTERING,
        SG_LOGITEM_VALIDATE_SHADERDESC_CANARY,
        SG_LOGITEM_VALIDATE_SHADERDESC_VERTEX_SOURCE,
        SG_LOGITEM_VALIDATE_SHADERDESC_FRAGMENT_SOURCE,
        SG_LOGITEM_VALIDATE_SHADERDESC_COMPUTE_SOURCE,
        SG_LOGITEM_VALIDATE_SHADERDESC_VERTEX_SOURCE_OR_BYTECODE,
        SG_LOGITEM_VALIDATE_SHADERDESC_FRAGMENT_SOURCE_OR_BYTECODE,
        SG_LOGITEM_VALIDATE_SHADERDESC_COMPUTE_SOURCE_OR_BYTECODE,
        SG_LOGITEM_VALIDATE_SHADERDESC_INVALID_SHADER_COMBO,
        SG_LOGITEM_VALIDATE_SHADERDESC_NO_BYTECODE_SIZE,
        SG_LOGITEM_VALIDATE_SHADERDESC_METAL_THREADS_PER_THREADGROUP_INITIALIZED,
        SG_LOGITEM_VALIDATE_SHADERDESC_METAL_THREADS_PER_THREADGROUP_MULTIPLE_32,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_NO_CONT_MEMBERS,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_SIZE_IS_ZERO,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_METAL_BUFFER_SLOT_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_HLSL_REGISTER_B_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_WGSL_GROUP0_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_SPIRV_SET0_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_NO_MEMBERS,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_UNIFORM_GLSL_NAME,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_SIZE_MISMATCH,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_ARRAY_COUNT,
        SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_STD140_ARRAY_TYPE,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_METAL_BUFFER_SLOT_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_HLSL_REGISTER_T_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_HLSL_REGISTER_U_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_GLSL_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_WGSL_GROUP1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_SPIRV_SET1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_EXPECT_COMPUTE_STAGE,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_METAL_TEXTURE_SLOT_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_HLSL_REGISTER_U_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_GLSL_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_WGSL_GROUP1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_SPIRV_SET1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_METAL_TEXTURE_SLOT_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_HLSL_REGISTER_T_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_WGSL_GROUP1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_SPIRV_SET1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_METAL_SAMPLER_SLOT_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_HLSL_REGISTER_S_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_WGSL_GROUP1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_SPIRV_SET1_BINDING_COLLISION,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_VIEW_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_SAMPLER_SLOT_OUT_OF_RANGE,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_TEXTURE_STAGE_MISMATCH,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_EXPECT_TEXTURE_VIEW,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_SAMPLER_STAGE_MISMATCH,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_GLSL_NAME,
        SG_LOGITEM_VALIDATE_SHADERDESC_NONFILTERING_SAMPLER_REQUIRED,
        SG_LOGITEM_VALIDATE_SHADERDESC_COMPARISON_SAMPLER_REQUIRED,
        SG_LOGITEM_VALIDATE_SHADERDESC_TEXVIEW_NOT_REFERENCED_BY_TEXTURE_SAMPLER_PAIRS,
        SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_NOT_REFERENCED_BY_TEXTURE_SAMPLER_PAIRS,
        SG_LOGITEM_VALIDATE_SHADERDESC_ATTR_STRING_TOO_LONG,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_CANARY,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_SHADER,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_COMPUTE_SHADER_EXPECTED,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_NO_COMPUTE_SHADER_EXPECTED,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_NO_CONT_ATTRS,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_ATTR_BASETYPE_MISMATCH,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_ATTR_VERTEXFORMAT_INT10_N2_NOT_SUPPORTED,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_LAYOUT_STRIDE4,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_ATTR_SEMANTICS,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_SHADER_READONLY_STORAGEBUFFERS,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_BLENDOP_MINMAX_REQUIRES_BLENDFACTOR_ONE,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_DUAL_SOURCE_BLENDING_NOT_SUPPORTED,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_DEPTH_FORMAT_NONE_BUT_DEPTH_WRITE_ENABLED,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_DEPTH_FORMAT_NONE_COMPARE_FUNC_MISMATCH,
        SG_LOGITEM_VALIDATE_PIPELINEDESC_COLOR_COUNT,
        SG_LOGITEM_VALIDATE_VIEWDESC_CANARY,
        SG_LOGITEM_VALIDATE_VIEWDESC_UNIQUE_VIEWTYPE,
        SG_LOGITEM_VALIDATE_VIEWDESC_ANY_VIEWTYPE,
        SG_LOGITEM_VALIDATE_VIEWDESC_RESOURCE_ALIVE,
        SG_LOGITEM_VALIDATE_VIEWDESC_RESOURCE_VALID,
        SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_VALID_UNSEALED,
        SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEBUFFER_OFFSET_VS_BUFFER_SIZE,
        SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEBUFFER_OFFSET_MULTIPLE_256,
        SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEBUFFER_USAGE,
        SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEIMAGE_USAGE,
        SG_LOGITEM_VALIDATE_VIEWDESC_COLORATTACHMENT_USAGE,
        SG_LOGITEM_VALIDATE_VIEWDESC_RESOLVEATTACHMENT_USAGE,
        SG_LOGITEM_VALIDATE_VIEWDESC_DEPTHSTENCILATTACHMENT_USAGE,
        SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_MIPLEVEL,
        SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_2D_SLICE,
        SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_CUBEMAP_SLICE,
        SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_ARRAY_SLICE,
        SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_3D_SLICE,
        SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_EXPECT_NO_MSAA,
        SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_MIPLEVELS,
        SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_2D_SLICES,
        SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_CUBEMAP_SLICES,
        SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_ARRAY_SLICES,
        SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_3D_SLICES,
        SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEIMAGE_PIXELFORMAT,
        SG_LOGITEM_VALIDATE_VIEWDESC_COLORATTACHMENT_PIXELFORMAT,
        SG_LOGITEM_VALIDATE_VIEWDESC_DEPTHSTENCILATTACHMENT_PIXELFORMAT,
        SG_LOGITEM_VALIDATE_VIEWDESC_RESOLVEATTACHMENT_SAMPLECOUNT,
        SG_LOGITEM_VALIDATE_BEGINPASS_CANARY,
        SG_LOGITEM_VALIDATE_BEGINPASS_COMPUTEPASS_EXPECT_NO_ATTACHMENTS,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_WIDTH,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_WIDTH_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_HEIGHT,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_HEIGHT_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_SAMPLECOUNT,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_SAMPLECOUNT_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_COLORFORMAT,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_COLORFORMAT_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_DEPTHFORMAT_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_CURRENTDRAWABLE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_CURRENTDRAWABLE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_DEPTHSTENCILTEXTURE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_DEPTHSTENCILTEXTURE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_MSAACOLORTEXTURE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_MSAACOLORTEXTURE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RENDERVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RENDERVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RESOLVEVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RESOLVEVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_DEPTHSTENCILVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_DEPTHSTENCILVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RENDERVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RENDERVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RESOLVEVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RESOLVEVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_DEPTHSTENCILVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_DEPTHSTENCILVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_GL_EXPECT_FRAMEBUFFER_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RENDERIMAGE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RENDERIMAGE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RENDERVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RENDERVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_DEPTHSTENCILIMAGE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_DEPTHSTENCILIMAGE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_DEPTHSTENCILVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_DEPTHSTENCILVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RESOLVEIMAGE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RESOLVEIMAGE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RESOLVEVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RESOLVEVIEW_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RENDERFINISHEDSEMAPHORE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_RENDERFINISHEDSEMAPHORE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_PRESENTCOMPLETESEMAPHORE,
        SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_VULKAN_EXPECT_PRESENTCOMPLETESEMAPHORE_NOTSET,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEWS_CONTINUOUS,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_ALIVE,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_VALID,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_TYPE,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_IMAGE_ALIVE,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_IMAGE_VALID,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_SIZES,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_SAMPLECOUNT,
        SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_SAMPLECOUNTS_EQUAL,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_NO_COLORATTACHMENTVIEW,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_ALIVE,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_VALID,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_TYPE,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_IMAGE_ALIVE,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_IMAGE_VALID,
        SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_SIZES,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEWS_CONTINUOUS,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_ALIVE,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_VALID,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_TYPE,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_IMAGE_ALIVE,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_IMAGE_VALID,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_SIZES,
        SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_SAMPLECOUNT,
        SG_LOGITEM_VALIDATE_BEGINPASS_ATTACHMENTS_EXPECTED,
        SG_LOGITEM_VALIDATE_AVP_RENDERPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_ASR_RENDERPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_APIP_PIPELINE_VALID_ID,
        SG_LOGITEM_VALIDATE_APIP_PIPELINE_EXISTS,
        SG_LOGITEM_VALIDATE_APIP_PIPELINE_VALID,
        SG_LOGITEM_VALIDATE_APIP_PASS_EXPECTED,
        SG_LOGITEM_VALIDATE_APIP_PIPELINE_SHADER_ALIVE,
        SG_LOGITEM_VALIDATE_APIP_PIPELINE_SHADER_VALID,
        SG_LOGITEM_VALIDATE_APIP_COMPUTEPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_APIP_RENDERPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_COLOR_COUNT,
        SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_COLOR_FORMAT,
        SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_DEPTH_FORMAT,
        SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_SAMPLE_COUNT,
        SG_LOGITEM_VALIDATE_APIP_ATTACHMENTS_ALIVE,
        SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_COUNT,
        SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_VIEW_VALID,
        SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_IMAGE_VALID,
        SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_FORMAT,
        SG_LOGITEM_VALIDATE_APIP_DEPTHSTENCILATTACHMENT_VIEW_VALID,
        SG_LOGITEM_VALIDATE_APIP_DEPTHSTENCILATTACHMENT_IMAGE_VALID,
        SG_LOGITEM_VALIDATE_APIP_DEPTHSTENCILATTACHMENT_FORMAT,
        SG_LOGITEM_VALIDATE_APIP_ATTACHMENT_SAMPLE_COUNT,
        SG_LOGITEM_VALIDATE_ABND_PASS_EXPECTED,
        SG_LOGITEM_VALIDATE_ABND_EMPTY_BINDINGS,
        SG_LOGITEM_VALIDATE_ABND_NO_PIPELINE,
        SG_LOGITEM_VALIDATE_ABND_PIPELINE_ALIVE,
        SG_LOGITEM_VALIDATE_ABND_PIPELINE_VALID,
        SG_LOGITEM_VALIDATE_ABND_PIPELINE_SHADER_ALIVE,
        SG_LOGITEM_VALIDATE_ABND_PIPELINE_SHADER_VALID,
        SG_LOGITEM_VALIDATE_ABND_COMPUTE_EXPECTED_NO_VBUFS,
        SG_LOGITEM_VALIDATE_ABND_COMPUTE_EXPECTED_NO_IBUF,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_VBUF,
        SG_LOGITEM_VALIDATE_ABND_VBUF_ALIVE,
        SG_LOGITEM_VALIDATE_ABND_VBUF_USAGE,
        SG_LOGITEM_VALIDATE_ABND_VBUF_OVERFLOW,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_NO_IBUF,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_IBUF,
        SG_LOGITEM_VALIDATE_ABND_IBUF_ALIVE,
        SG_LOGITEM_VALIDATE_ABND_IBUF_USAGE,
        SG_LOGITEM_VALIDATE_ABND_IBUF_OVERFLOW,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_VIEW_BINDING,
        SG_LOGITEM_VALIDATE_ABND_VIEW_ALIVE,
        SG_LOGITEM_VALIDATE_ABND_EXPECT_TEXVIEW,
        SG_LOGITEM_VALIDATE_ABND_EXPECT_SBVIEW,
        SG_LOGITEM_VALIDATE_ABND_EXPECT_SIMGVIEW,
        SG_LOGITEM_VALIDATE_ABND_TEXVIEW_IMAGETYPE_MISMATCH,
        SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_MULTISAMPLED_IMAGE,
        SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_NON_MULTISAMPLED_IMAGE,
        SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_FILTERABLE_IMAGE,
        SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_DEPTH_IMAGE,
        SG_LOGITEM_VALIDATE_ABND_SBVIEW_READWRITE_IMMUTABLE,
        SG_LOGITEM_VALIDATE_ABND_SIMGVIEW_COMPUTE_PASS_EXPECTED,
        SG_LOGITEM_VALIDATE_ABND_SIMGVIEW_IMAGETYPE_MISMATCH,
        SG_LOGITEM_VALIDATE_ABND_SIMGVIEW_ACCESSFORMAT,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_SAMPLER_BINDING,
        SG_LOGITEM_VALIDATE_ABND_UNEXPECTED_SAMPLER_COMPARE_NEVER,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_SAMPLER_COMPARE_NEVER,
        SG_LOGITEM_VALIDATE_ABND_EXPECTED_NONFILTERING_SAMPLER,
        SG_LOGITEM_VALIDATE_ABND_SAMPLER_ALIVE,
        SG_LOGITEM_VALIDATE_ABND_SAMPLER_VALID,
        SG_LOGITEM_VALIDATE_ABND_TEXTURE_BINDING_VS_DEPTHSTENCIL_ATTACHMENT,
        SG_LOGITEM_VALIDATE_ABND_TEXTURE_BINDING_VS_COLOR_ATTACHMENT,
        SG_LOGITEM_VALIDATE_ABND_TEXTURE_BINDING_VS_RESOLVE_ATTACHMENT,
        SG_LOGITEM_VALIDATE_ABND_TEXTURE_VS_STORAGEIMAGE_BINDING,
        SG_LOGITEM_VALIDATE_AU_PASS_EXPECTED,
        SG_LOGITEM_VALIDATE_AU_NO_PIPELINE,
        SG_LOGITEM_VALIDATE_AU_PIPELINE_ALIVE,
        SG_LOGITEM_VALIDATE_AU_PIPELINE_VALID,
        SG_LOGITEM_VALIDATE_AU_PIPELINE_SHADER_ALIVE,
        SG_LOGITEM_VALIDATE_AU_PIPELINE_SHADER_VALID,
        SG_LOGITEM_VALIDATE_AU_NO_UNIFORMBLOCK_AT_SLOT,
        SG_LOGITEM_VALIDATE_AU_SIZE,
        SG_LOGITEM_VALIDATE_DRAW_RENDERPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_DRAW_BASEELEMENT_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_NUMELEMENTS_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_NUMINSTANCES_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_EX_RENDERPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_DRAW_EX_BASEELEMENT_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_EX_NUMELEMENTS_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_EX_NUMINSTANCES_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_EX_BASEINSTANCE_GE_ZERO,
        SG_LOGITEM_VALIDATE_DRAW_EX_BASEVERTEX_VS_INDEXED,
        SG_LOGITEM_VALIDATE_DRAW_EX_BASEINSTANCE_VS_INSTANCED,
        SG_LOGITEM_VALIDATE_DRAW_EX_BASEVERTEX_NOT_SUPPORTED,
        SG_LOGITEM_VALIDATE_DRAW_EX_BASEINSTANCE_NOT_SUPPORTED,
        SG_LOGITEM_VALIDATE_DRAW_REQUIRED_BINDINGS_OR_UNIFORMS_MISSING,
        SG_LOGITEM_VALIDATE_DRAW_WRITE_BUFFER_TRANSIENT_MISSING,
        SG_LOGITEM_VALIDATE_DRAW_WRITE_IMAGE_TRANSIENT_MISSING,
        SG_LOGITEM_VALIDATE_DISPATCH_COMPUTEPASS_EXPECTED,
        SG_LOGITEM_VALIDATE_DISPATCH_NUMGROUPSX,
        SG_LOGITEM_VALIDATE_DISPATCH_NUMGROUPSY,
        SG_LOGITEM_VALIDATE_DISPATCH_NUMGROUPSZ,
        SG_LOGITEM_VALIDATE_DISPATCH_REQUIRED_BINDINGS_OR_UNIFORMS_MISSING,
        SG_LOGITEM_VALIDATE_DISPATCH_WRITE_BUFFER_TRANSIENT_MISSING,
        SG_LOGITEM_VALIDATE_DISPATCH_WRITE_IMAGE_TRANSIENT_MISSING,
        SG_LOGITEM_VALIDATE_UPDATEBUF_USAGE,
        SG_LOGITEM_VALIDATE_UPDATEBUF_SIZE,
        SG_LOGITEM_VALIDATE_UPDATEBUF_ONCE,
        SG_LOGITEM_VALIDATE_UPDATEBUF_APPEND,
        SG_LOGITEM_VALIDATE_APPENDBUF_USAGE,
        SG_LOGITEM_VALIDATE_APPENDBUF_SIZE,
        SG_LOGITEM_VALIDATE_APPENDBUF_UPDATE,
        SG_LOGITEM_VALIDATE_UPDIMG_USAGE,
        SG_LOGITEM_VALIDATE_UPDIMG_ONCE,
        SG_LOGITEM_VALIDATE_WRITEBUFFERUNSEALED_USAGE,
        SG_LOGITEM_VALIDATE_WRITEBUFFERUNSEALED_RESOURCESTATE,
        SG_LOGITEM_VALIDATE_WRITEBUFFERTRANSIENT_USAGE,
        SG_LOGITEM_VALIDATE_WRITEBUFFERTRANSIENT_WRITE_BEFORE_BIND,
        SG_LOGITEM_VALIDATE_WRITEBUFFERTRANSIENT_DST_OFFSET_ALIGNMENT,
        SG_LOGITEM_VALIDATE_WRITEBUFFER_SRC_DATA_POINTER,
        SG_LOGITEM_VALIDATE_WRITEBUFFER_SRC_DATA_SIZE,
        SG_LOGITEM_VALIDATE_WRITEBUFFER_SIZE,
        SG_LOGITEM_VALIDATE_WRITEBUFFER_WRITE_OVERFLOW,
        SG_LOGITEM_VALIDATE_WRITEBUFFER_READ_OVERFLOW,
        SG_LOGITEM_VALIDATE_WRITEIMAGEUNSEALED_USAGE,
        SG_LOGITEM_VALIDATE_WRITEIMAGEUNSEALED_RESOURCESTATE,
        SG_LOGITEM_VALIDATE_WRITEIMAGETRANSIENT_USAGE,
        SG_LOGITEM_VALIDATE_WRITEIMAGETRANSIENT_WRITE_BEFORE_BIND,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_SRC_DATA_POINTER,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_SRC_DATA_SIZE,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_BYTESPERROW,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_BYTESPERSLICE,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_MIPLEVEL,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_WIDTH,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_HEIGHT,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_NUMSLICES,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_READ_OVERFLOW,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_DST_X_RANGE,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_DST_Y_RANGE,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_DST_SLICE_RANGE,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_WRITE_WIDTH_OVERFLOW,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_WRITE_HEIGHT_OVERFLOW,
        SG_LOGITEM_VALIDATE_WRITEIMAGE_WRITE_NUMSLICES_OVERFLOW,
        SG_LOGITEM_VALIDATE_SEALBUFFER_RESOURCESTATE,
        SG_LOGITEM_VALIDATE_SEALIMAGE_RESOURCESTATE,
        SG_LOGITEM_VALIDATION_FAILED,
    }

    public partial struct sg_environment_defaults
    {
        public sg_pixel_format color_format;

        public sg_pixel_format depth_format;

        public int sample_count;
    }

    public unsafe partial struct sg_metal_environment
    {
        [NativeTypeName("const void *")]
        public void* device;
    }

    public unsafe partial struct sg_d3d11_environment
    {
        [NativeTypeName("const void *")]
        public void* device;

        [NativeTypeName("const void *")]
        public void* device_context;
    }

    public unsafe partial struct sg_wgpu_environment
    {
        [NativeTypeName("const void *")]
        public void* device;
    }

    public unsafe partial struct sg_vulkan_environment
    {
        [NativeTypeName("const void *")]
        public void* instance;

        [NativeTypeName("const void *")]
        public void* physical_device;

        [NativeTypeName("const void *")]
        public void* device;

        [NativeTypeName("const void *")]
        public void* queue;

        [NativeTypeName("uint32_t")]
        public uint queue_family_index;
    }

    public partial struct sg_environment
    {
        public sg_environment_defaults defaults;

        public sg_metal_environment metal;

        public sg_d3d11_environment d3d11;

        public sg_wgpu_environment wgpu;

        public sg_vulkan_environment vulkan;
    }

    public unsafe partial struct sg_commit_listener
    {
        [NativeTypeName("void (*)(void *)")]
        public delegate* unmanaged[Cdecl]<void*, void> func;

        public void* user_data;
    }

    public unsafe partial struct sg_allocator
    {
        [NativeTypeName("void *(*)(size_t, void *)")]
        public delegate* unmanaged[Cdecl]<nuint, void*, void*> alloc_fn;

        [NativeTypeName("void (*)(void *, void *)")]
        public delegate* unmanaged[Cdecl]<void*, void*, void> free_fn;

        public void* user_data;
    }

    public unsafe partial struct sg_logger
    {
        [NativeTypeName("void (*)(const char *, uint32_t, uint32_t, const char *, uint32_t, const char *, void *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void> func;

        public void* user_data;
    }

    public partial struct sg_d3d11_desc
    {
        [NativeTypeName("_Bool")]
        public byte shader_debugging;
    }

    public partial struct sg_metal_desc
    {
        [NativeTypeName("_Bool")]
        public byte force_managed_storage_mode;

        [NativeTypeName("_Bool")]
        public byte use_command_buffer_with_retained_references;
    }

    public partial struct sg_wgpu_desc
    {
        [NativeTypeName("_Bool")]
        public byte disable_bindgroups_cache;

        public int bindgroups_cache_size;
    }

    public partial struct sg_vulkan_desc
    {
        public int copy_staging_buffer_size;

        public int transient_staging_buffer_size;

        public int descriptor_buffer_size;
    }

    public partial struct sg_desc
    {
        [NativeTypeName("uint32_t")]
        public uint _start_canary;

        public int buffer_pool_size;

        public int image_pool_size;

        public int sampler_pool_size;

        public int shader_pool_size;

        public int pipeline_pool_size;

        public int view_pool_size;

        public int uniform_buffer_size;

        public int max_commit_listeners;

        [NativeTypeName("_Bool")]
        public byte disable_validation;

        [NativeTypeName("_Bool")]
        public byte enforce_portable_limits;

        public sg_d3d11_desc d3d11;

        public sg_metal_desc metal;

        public sg_wgpu_desc wgpu;

        public sg_vulkan_desc vulkan;

        public sg_allocator allocator;

        public sg_logger logger;

        public sg_environment environment;

        [NativeTypeName("uint32_t")]
        public uint _end_canary;
    }

    public unsafe partial struct sg_d3d11_buffer_info
    {
        [NativeTypeName("const void *")]
        public void* buf;
    }

    public unsafe partial struct sg_d3d11_image_info
    {
        [NativeTypeName("const void *")]
        public void* tex2d;

        [NativeTypeName("const void *")]
        public void* tex3d;

        [NativeTypeName("const void *")]
        public void* res;
    }

    public unsafe partial struct sg_d3d11_sampler_info
    {
        [NativeTypeName("const void *")]
        public void* smp;
    }

    public unsafe partial struct sg_d3d11_shader_info
    {
        [NativeTypeName("const void *[8]")]
        public _cbufs_e__FixedBuffer cbufs;

        [NativeTypeName("const void *")]
        public void* vs;

        [NativeTypeName("const void *")]
        public void* fs;

        public unsafe partial struct _cbufs_e__FixedBuffer
        {
            public void* e0;
            public void* e1;
            public void* e2;
            public void* e3;
            public void* e4;
            public void* e5;
            public void* e6;
            public void* e7;

            public ref void* this[int index]
            {
                get
                {
                    fixed (void** pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public unsafe partial struct sg_d3d11_pipeline_info
    {
        [NativeTypeName("const void *")]
        public void* il;

        [NativeTypeName("const void *")]
        public void* rs;

        [NativeTypeName("const void *")]
        public void* dss;

        [NativeTypeName("const void *")]
        public void* bs;
    }

    public unsafe partial struct sg_d3d11_view_info
    {
        [NativeTypeName("const void *")]
        public void* srv;

        [NativeTypeName("const void *")]
        public void* uav;

        [NativeTypeName("const void *")]
        public void* rtv;

        [NativeTypeName("const void *")]
        public void* dsv;
    }

    public partial struct sg_mtl_buffer_info
    {
        [NativeTypeName("const void *[2]")]
        public _buf_e__FixedBuffer buf;

        public int active_slot;

        public unsafe partial struct _buf_e__FixedBuffer
        {
            public void* e0;
            public void* e1;

            public ref void* this[int index]
            {
                get
                {
                    fixed (void** pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public partial struct sg_mtl_image_info
    {
        [NativeTypeName("const void *[2]")]
        public _tex_e__FixedBuffer tex;

        public int active_slot;

        public unsafe partial struct _tex_e__FixedBuffer
        {
            public void* e0;
            public void* e1;

            public ref void* this[int index]
            {
                get
                {
                    fixed (void** pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public unsafe partial struct sg_mtl_sampler_info
    {
        [NativeTypeName("const void *")]
        public void* smp;
    }

    public unsafe partial struct sg_mtl_shader_info
    {
        [NativeTypeName("const void *")]
        public void* vertex_lib;

        [NativeTypeName("const void *")]
        public void* fragment_lib;

        [NativeTypeName("const void *")]
        public void* vertex_func;

        [NativeTypeName("const void *")]
        public void* fragment_func;
    }

    public unsafe partial struct sg_mtl_pipeline_info
    {
        [NativeTypeName("const void *")]
        public void* rps;

        [NativeTypeName("const void *")]
        public void* dss;
    }

    public unsafe partial struct sg_wgpu_buffer_info
    {
        [NativeTypeName("const void *")]
        public void* buf;
    }

    public unsafe partial struct sg_wgpu_image_info
    {
        [NativeTypeName("const void *")]
        public void* tex;
    }

    public unsafe partial struct sg_wgpu_sampler_info
    {
        [NativeTypeName("const void *")]
        public void* smp;
    }

    public unsafe partial struct sg_wgpu_shader_info
    {
        [NativeTypeName("const void *")]
        public void* vs_mod;

        [NativeTypeName("const void *")]
        public void* fs_mod;

        [NativeTypeName("const void *")]
        public void* bgl;
    }

    public unsafe partial struct sg_wgpu_pipeline_info
    {
        [NativeTypeName("const void *")]
        public void* render_pipeline;

        [NativeTypeName("const void *")]
        public void* compute_pipeline;
    }

    public unsafe partial struct sg_wgpu_view_info
    {
        [NativeTypeName("const void *")]
        public void* view;
    }

    public partial struct sg_gl_buffer_info
    {
        [NativeTypeName("uint32_t[2]")]
        public _buf_e__FixedBuffer buf;

        public int active_slot;

        [InlineArray(2)]
        public partial struct _buf_e__FixedBuffer
        {
            public uint e0;
        }
    }

    public partial struct sg_gl_image_info
    {
        [NativeTypeName("uint32_t[2]")]
        public _tex_e__FixedBuffer tex;

        [NativeTypeName("uint32_t")]
        public uint tex_target;

        public int active_slot;

        [InlineArray(2)]
        public partial struct _tex_e__FixedBuffer
        {
            public uint e0;
        }
    }

    public partial struct sg_gl_sampler_info
    {
        [NativeTypeName("uint32_t")]
        public uint smp;
    }

    public partial struct sg_gl_shader_info
    {
        [NativeTypeName("uint32_t")]
        public uint prog;
    }

    public partial struct sg_gl_view_info
    {
        [NativeTypeName("uint32_t[2]")]
        public _tex_view_e__FixedBuffer tex_view;

        [NativeTypeName("uint32_t")]
        public uint msaa_render_buffer;

        [NativeTypeName("uint32_t")]
        public uint msaa_resolve_frame_buffer;

        [InlineArray(2)]
        public partial struct _tex_view_e__FixedBuffer
        {
            public uint e0;
        }
    }

    public static unsafe partial class Gfx
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_setup", ExactSpelling = true)]
        public static extern void setup([NativeTypeName("const sg_desc *")] sg_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_shutdown", ExactSpelling = true)]
        public static extern void shutdown();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_isvalid", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte isvalid();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_reset_state_cache", ExactSpelling = true)]
        public static extern void reset_state_cache();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_install_trace_hooks", ExactSpelling = true)]
        public static extern sg_trace_hooks install_trace_hooks([NativeTypeName("const sg_trace_hooks *")] sg_trace_hooks* trace_hooks);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_push_debug_group", ExactSpelling = true)]
        public static extern void push_debug_group([NativeTypeName("const char *")] sbyte* name);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_pop_debug_group", ExactSpelling = true)]
        public static extern void pop_debug_group();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_add_commit_listener", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte add_commit_listener(sg_commit_listener listener);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_remove_commit_listener", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte remove_commit_listener(sg_commit_listener listener);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_buffer", ExactSpelling = true)]
        public static extern sg_buffer make_buffer([NativeTypeName("const sg_buffer_desc *")] sg_buffer_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_image", ExactSpelling = true)]
        public static extern sg_image make_image([NativeTypeName("const sg_image_desc *")] sg_image_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_sampler", ExactSpelling = true)]
        public static extern sg_sampler make_sampler([NativeTypeName("const sg_sampler_desc *")] sg_sampler_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_shader", ExactSpelling = true)]
        public static extern sg_shader make_shader([NativeTypeName("const sg_shader_desc *")] sg_shader_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_pipeline", ExactSpelling = true)]
        public static extern sg_pipeline make_pipeline([NativeTypeName("const sg_pipeline_desc *")] sg_pipeline_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_view", ExactSpelling = true)]
        public static extern sg_view make_view([NativeTypeName("const sg_view_desc *")] sg_view_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_destroy_buffer", ExactSpelling = true)]
        public static extern void destroy_buffer(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_destroy_image", ExactSpelling = true)]
        public static extern void destroy_image(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_destroy_sampler", ExactSpelling = true)]
        public static extern void destroy_sampler(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_destroy_shader", ExactSpelling = true)]
        public static extern void destroy_shader(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_destroy_pipeline", ExactSpelling = true)]
        public static extern void destroy_pipeline(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_destroy_view", ExactSpelling = true)]
        public static extern void destroy_view(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_begin_pass", ExactSpelling = true)]
        public static extern void begin_pass([NativeTypeName("const sg_pass *")] sg_pass* pass);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_viewport", ExactSpelling = true)]
        public static extern void apply_viewport(int x, int y, int width, int height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_viewportf", ExactSpelling = true)]
        public static extern void apply_viewportf(float x, float y, float width, float height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_scissor_rect", ExactSpelling = true)]
        public static extern void apply_scissor_rect(int x, int y, int width, int height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_scissor_rectf", ExactSpelling = true)]
        public static extern void apply_scissor_rectf(float x, float y, float width, float height, [NativeTypeName("_Bool")] byte origin_top_left);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_pipeline", ExactSpelling = true)]
        public static extern void apply_pipeline(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_bindings", ExactSpelling = true)]
        public static extern void apply_bindings([NativeTypeName("const sg_bindings *")] sg_bindings* bindings);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_apply_uniforms", ExactSpelling = true)]
        public static extern void apply_uniforms(int ub_slot, [NativeTypeName("const sg_range *")] sg_range* data);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_draw", ExactSpelling = true)]
        public static extern void draw(int base_element, int num_elements, int num_instances);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_draw_ex", ExactSpelling = true)]
        public static extern void draw_ex(int base_element, int num_elements, int num_instances, int base_vertex, int base_instance);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dispatch", ExactSpelling = true)]
        public static extern void dispatch(int num_groups_x, int num_groups_y, int num_groups_z);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_end_pass", ExactSpelling = true)]
        public static extern void end_pass();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_commit", ExactSpelling = true)]
        public static extern void commit();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_write_buffer_transient", ExactSpelling = true)]
        public static extern void write_buffer_transient([NativeTypeName("const sg_write_buffer_desc *")] sg_write_buffer_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_write_image_transient", ExactSpelling = true)]
        public static extern void write_image_transient([NativeTypeName("const sg_write_image_desc *")] sg_write_image_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_write_buffer_unsealed", ExactSpelling = true)]
        public static extern void write_buffer_unsealed([NativeTypeName("const sg_write_buffer_desc *")] sg_write_buffer_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_write_image_unsealed", ExactSpelling = true)]
        public static extern void write_image_unsealed([NativeTypeName("const sg_write_image_desc *")] sg_write_image_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_seal_buffer", ExactSpelling = true)]
        public static extern void seal_buffer(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_seal_image", ExactSpelling = true)]
        public static extern void seal_image(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_update_buffer", ExactSpelling = true)]
        public static extern void update_buffer(sg_buffer buf, [NativeTypeName("const sg_range *")] sg_range* data);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_update_image", ExactSpelling = true)]
        public static extern void update_image(sg_image img, [NativeTypeName("const sg_image_data *")] sg_image_data* data);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_append_buffer", ExactSpelling = true)]
        public static extern int append_buffer(sg_buffer buf, [NativeTypeName("const sg_range *")] sg_range* data);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_overflow", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte query_buffer_overflow(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_will_overflow", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte query_buffer_will_overflow(sg_buffer buf, [NativeTypeName("size_t")] nuint size);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_desc", ExactSpelling = true)]
        public static extern sg_desc query_desc();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_backend", ExactSpelling = true)]
        public static extern sg_backend query_backend();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_features", ExactSpelling = true)]
        public static extern sg_features query_features();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_limits", ExactSpelling = true)]
        public static extern sg_limits query_limits();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_pixelformat", ExactSpelling = true)]
        public static extern sg_pixelformat_info query_pixelformat(sg_pixel_format fmt);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_row_pitch", ExactSpelling = true)]
        public static extern int query_row_pitch(sg_pixel_format fmt, int width, int row_align_bytes);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_surface_pitch", ExactSpelling = true)]
        public static extern int query_surface_pitch(sg_pixel_format fmt, int width, int height, int row_align_bytes);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_state", ExactSpelling = true)]
        public static extern sg_resource_state query_buffer_state(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_state", ExactSpelling = true)]
        public static extern sg_resource_state query_image_state(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_sampler_state", ExactSpelling = true)]
        public static extern sg_resource_state query_sampler_state(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_shader_state", ExactSpelling = true)]
        public static extern sg_resource_state query_shader_state(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_pipeline_state", ExactSpelling = true)]
        public static extern sg_resource_state query_pipeline_state(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_state", ExactSpelling = true)]
        public static extern sg_resource_state query_view_state(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_info", ExactSpelling = true)]
        public static extern sg_buffer_info query_buffer_info(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_info", ExactSpelling = true)]
        public static extern sg_image_info query_image_info(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_sampler_info", ExactSpelling = true)]
        public static extern sg_sampler_info query_sampler_info(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_shader_info", ExactSpelling = true)]
        public static extern sg_shader_info query_shader_info(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_pipeline_info", ExactSpelling = true)]
        public static extern sg_pipeline_info query_pipeline_info(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_info", ExactSpelling = true)]
        public static extern sg_view_info query_view_info(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_desc", ExactSpelling = true)]
        public static extern sg_buffer_desc query_buffer_desc(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_desc", ExactSpelling = true)]
        public static extern sg_image_desc query_image_desc(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_sampler_desc", ExactSpelling = true)]
        public static extern sg_sampler_desc query_sampler_desc(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_shader_desc", ExactSpelling = true)]
        public static extern sg_shader_desc query_shader_desc(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_pipeline_desc", ExactSpelling = true)]
        public static extern sg_pipeline_desc query_pipeline_desc(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_desc", ExactSpelling = true)]
        public static extern sg_view_desc query_view_desc(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_defaults", ExactSpelling = true)]
        public static extern sg_buffer_desc query_buffer_defaults([NativeTypeName("const sg_buffer_desc *")] sg_buffer_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_defaults", ExactSpelling = true)]
        public static extern sg_image_desc query_image_defaults([NativeTypeName("const sg_image_desc *")] sg_image_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_sampler_defaults", ExactSpelling = true)]
        public static extern sg_sampler_desc query_sampler_defaults([NativeTypeName("const sg_sampler_desc *")] sg_sampler_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_shader_defaults", ExactSpelling = true)]
        public static extern sg_shader_desc query_shader_defaults([NativeTypeName("const sg_shader_desc *")] sg_shader_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_pipeline_defaults", ExactSpelling = true)]
        public static extern sg_pipeline_desc query_pipeline_defaults([NativeTypeName("const sg_pipeline_desc *")] sg_pipeline_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_defaults", ExactSpelling = true)]
        public static extern sg_view_desc query_view_defaults([NativeTypeName("const sg_view_desc *")] sg_view_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_size", ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint query_buffer_size(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_buffer_usage", ExactSpelling = true)]
        public static extern sg_buffer_usage query_buffer_usage(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_type", ExactSpelling = true)]
        public static extern sg_image_type query_image_type(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_width", ExactSpelling = true)]
        public static extern int query_image_width(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_height", ExactSpelling = true)]
        public static extern int query_image_height(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_num_slices", ExactSpelling = true)]
        public static extern int query_image_num_slices(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_num_mipmaps", ExactSpelling = true)]
        public static extern int query_image_num_mipmaps(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_pixelformat", ExactSpelling = true)]
        public static extern sg_pixel_format query_image_pixelformat(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_usage", ExactSpelling = true)]
        public static extern sg_image_usage query_image_usage(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_image_sample_count", ExactSpelling = true)]
        public static extern int query_image_sample_count(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_type", ExactSpelling = true)]
        public static extern sg_view_type query_view_type(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_image", ExactSpelling = true)]
        public static extern sg_image query_view_image(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_view_buffer", ExactSpelling = true)]
        public static extern sg_buffer query_view_buffer(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_alloc_buffer", ExactSpelling = true)]
        public static extern sg_buffer alloc_buffer();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_alloc_image", ExactSpelling = true)]
        public static extern sg_image alloc_image();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_alloc_sampler", ExactSpelling = true)]
        public static extern sg_sampler alloc_sampler();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_alloc_shader", ExactSpelling = true)]
        public static extern sg_shader alloc_shader();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_alloc_pipeline", ExactSpelling = true)]
        public static extern sg_pipeline alloc_pipeline();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_alloc_view", ExactSpelling = true)]
        public static extern sg_view alloc_view();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dealloc_buffer", ExactSpelling = true)]
        public static extern void dealloc_buffer(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dealloc_image", ExactSpelling = true)]
        public static extern void dealloc_image(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dealloc_sampler", ExactSpelling = true)]
        public static extern void dealloc_sampler(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dealloc_shader", ExactSpelling = true)]
        public static extern void dealloc_shader(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dealloc_pipeline", ExactSpelling = true)]
        public static extern void dealloc_pipeline(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_dealloc_view", ExactSpelling = true)]
        public static extern void dealloc_view(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_init_buffer", ExactSpelling = true)]
        public static extern void init_buffer(sg_buffer buf, [NativeTypeName("const sg_buffer_desc *")] sg_buffer_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_init_image", ExactSpelling = true)]
        public static extern void init_image(sg_image img, [NativeTypeName("const sg_image_desc *")] sg_image_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_init_sampler", ExactSpelling = true)]
        public static extern void init_sampler(sg_sampler smg, [NativeTypeName("const sg_sampler_desc *")] sg_sampler_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_init_shader", ExactSpelling = true)]
        public static extern void init_shader(sg_shader shd, [NativeTypeName("const sg_shader_desc *")] sg_shader_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_init_pipeline", ExactSpelling = true)]
        public static extern void init_pipeline(sg_pipeline pip, [NativeTypeName("const sg_pipeline_desc *")] sg_pipeline_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_init_view", ExactSpelling = true)]
        public static extern void init_view(sg_view view, [NativeTypeName("const sg_view_desc *")] sg_view_desc* desc);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_uninit_buffer", ExactSpelling = true)]
        public static extern void uninit_buffer(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_uninit_image", ExactSpelling = true)]
        public static extern void uninit_image(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_uninit_sampler", ExactSpelling = true)]
        public static extern void uninit_sampler(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_uninit_shader", ExactSpelling = true)]
        public static extern void uninit_shader(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_uninit_pipeline", ExactSpelling = true)]
        public static extern void uninit_pipeline(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_uninit_view", ExactSpelling = true)]
        public static extern void uninit_view(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_fail_buffer", ExactSpelling = true)]
        public static extern void fail_buffer(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_fail_image", ExactSpelling = true)]
        public static extern void fail_image(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_fail_sampler", ExactSpelling = true)]
        public static extern void fail_sampler(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_fail_shader", ExactSpelling = true)]
        public static extern void fail_shader(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_fail_pipeline", ExactSpelling = true)]
        public static extern void fail_pipeline(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_fail_view", ExactSpelling = true)]
        public static extern void fail_view(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_enable_stats", ExactSpelling = true)]
        public static extern void enable_stats();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_disable_stats", ExactSpelling = true)]
        public static extern void disable_stats();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_stats_enabled", ExactSpelling = true)]
        [return: NativeTypeName("_Bool")]
        public static extern byte stats_enabled();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_query_stats", ExactSpelling = true)]
        public static extern sg_stats query_stats();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_device", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* d3d11_device();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_device_context", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* d3d11_device_context();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_query_buffer_info", ExactSpelling = true)]
        public static extern sg_d3d11_buffer_info d3d11_query_buffer_info(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_query_image_info", ExactSpelling = true)]
        public static extern sg_d3d11_image_info d3d11_query_image_info(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_query_sampler_info", ExactSpelling = true)]
        public static extern sg_d3d11_sampler_info d3d11_query_sampler_info(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_query_shader_info", ExactSpelling = true)]
        public static extern sg_d3d11_shader_info d3d11_query_shader_info(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_query_pipeline_info", ExactSpelling = true)]
        public static extern sg_d3d11_pipeline_info d3d11_query_pipeline_info(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_d3d11_query_view_info", ExactSpelling = true)]
        public static extern sg_d3d11_view_info d3d11_query_view_info(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_device", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* mtl_device();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_render_command_encoder", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* mtl_render_command_encoder();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_compute_command_encoder", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* mtl_compute_command_encoder();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_command_queue", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* mtl_command_queue();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_query_buffer_info", ExactSpelling = true)]
        public static extern sg_mtl_buffer_info mtl_query_buffer_info(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_query_image_info", ExactSpelling = true)]
        public static extern sg_mtl_image_info mtl_query_image_info(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_query_sampler_info", ExactSpelling = true)]
        public static extern sg_mtl_sampler_info mtl_query_sampler_info(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_query_shader_info", ExactSpelling = true)]
        public static extern sg_mtl_shader_info mtl_query_shader_info(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_mtl_query_pipeline_info", ExactSpelling = true)]
        public static extern sg_mtl_pipeline_info mtl_query_pipeline_info(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_device", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* wgpu_device();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_queue", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* wgpu_queue();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_command_encoder", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* wgpu_command_encoder();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_render_pass_encoder", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* wgpu_render_pass_encoder();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_compute_pass_encoder", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* wgpu_compute_pass_encoder();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_query_buffer_info", ExactSpelling = true)]
        public static extern sg_wgpu_buffer_info wgpu_query_buffer_info(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_query_image_info", ExactSpelling = true)]
        public static extern sg_wgpu_image_info wgpu_query_image_info(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_query_sampler_info", ExactSpelling = true)]
        public static extern sg_wgpu_sampler_info wgpu_query_sampler_info(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_query_shader_info", ExactSpelling = true)]
        public static extern sg_wgpu_shader_info wgpu_query_shader_info(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_query_pipeline_info", ExactSpelling = true)]
        public static extern sg_wgpu_pipeline_info wgpu_query_pipeline_info(sg_pipeline pip);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_wgpu_query_view_info", ExactSpelling = true)]
        public static extern sg_wgpu_view_info wgpu_query_view_info(sg_view view);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_gl_query_buffer_info", ExactSpelling = true)]
        public static extern sg_gl_buffer_info gl_query_buffer_info(sg_buffer buf);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_gl_query_image_info", ExactSpelling = true)]
        public static extern sg_gl_image_info gl_query_image_info(sg_image img);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_gl_query_sampler_info", ExactSpelling = true)]
        public static extern sg_gl_sampler_info gl_query_sampler_info(sg_sampler smp);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_gl_query_shader_info", ExactSpelling = true)]
        public static extern sg_gl_shader_info gl_query_shader_info(sg_shader shd);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_gl_query_view_info", ExactSpelling = true)]
        public static extern sg_gl_view_info gl_query_view_info(sg_view view);
    }
}
