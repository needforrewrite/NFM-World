using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public unsafe partial struct sshape_range_t
    {
        [NativeTypeName("const void *")]
        public void* ptr;

        [NativeTypeName("size_t")]
        public nuint size;
    }

    public partial struct sshape_mat4_t
    {
        [NativeTypeName("float[4][4]")]
        public _m_e__FixedBuffer m;

        [InlineArray(4 * 4)]
        public partial struct _m_e__FixedBuffer
        {
            public float e0_0;
        }
    }

    public partial struct sshape_optional_components_t
    {
        [NativeTypeName("_Bool")]
        public byte normals;

        [NativeTypeName("_Bool")]
        public byte texcoords;

        [NativeTypeName("_Bool")]
        public byte colors;
    }

    public partial struct sshape_element_range_t
    {
        public int base_element;

        public int num_elements;
    }

    public partial struct sshape_sizes_item_t
    {
        [NativeTypeName("uint32_t")]
        public uint num;

        [NativeTypeName("uint32_t")]
        public uint size;
    }

    public partial struct sshape_sizes_t
    {
        public sshape_sizes_item_t vertices;

        public sshape_sizes_item_t indices;
    }

    public partial struct sshape_buffer_state_t
    {
        public sshape_range_t buffer;

        [NativeTypeName("size_t")]
        public nuint data_size;

        [NativeTypeName("size_t")]
        public nuint shape_offset;
    }

    public partial struct sshape_state_t
    {
        [NativeTypeName("_Bool")]
        public byte valid;

        public sshape_optional_components_t disable;

        public sshape_buffer_state_t vertices;

        public sshape_buffer_state_t indices;
    }

    public partial struct sshape_plane_t
    {
        public float width;

        public float depth;

        [NativeTypeName("uint16_t")]
        public ushort tiles;

        [NativeTypeName("uint32_t")]
        public uint color;

        [NativeTypeName("_Bool")]
        public byte random_colors;

        [NativeTypeName("_Bool")]
        public byte merge;

        public sshape_mat4_t transform;
    }

    public partial struct sshape_box_t
    {
        public float width;

        public float height;

        public float depth;

        [NativeTypeName("uint16_t")]
        public ushort tiles;

        [NativeTypeName("uint32_t")]
        public uint color;

        [NativeTypeName("_Bool")]
        public byte random_colors;

        [NativeTypeName("_Bool")]
        public byte merge;

        public sshape_mat4_t transform;
    }

    public partial struct sshape_sphere_t
    {
        public float radius;

        [NativeTypeName("uint16_t")]
        public ushort slices;

        [NativeTypeName("uint16_t")]
        public ushort stacks;

        [NativeTypeName("uint32_t")]
        public uint color;

        [NativeTypeName("_Bool")]
        public byte random_colors;

        [NativeTypeName("_Bool")]
        public byte merge;

        public sshape_mat4_t transform;
    }

    public partial struct sshape_cylinder_t
    {
        public float radius;

        public float height;

        [NativeTypeName("uint16_t")]
        public ushort slices;

        [NativeTypeName("uint16_t")]
        public ushort stacks;

        [NativeTypeName("uint32_t")]
        public uint color;

        [NativeTypeName("_Bool")]
        public byte random_colors;

        [NativeTypeName("_Bool")]
        public byte merge;

        public sshape_mat4_t transform;
    }

    public partial struct sshape_torus_t
    {
        public float radius;

        public float ring_radius;

        [NativeTypeName("uint16_t")]
        public ushort sides;

        [NativeTypeName("uint16_t")]
        public ushort rings;

        [NativeTypeName("uint32_t")]
        public uint color;

        [NativeTypeName("_Bool")]
        public byte random_colors;

        [NativeTypeName("_Bool")]
        public byte merge;

        public sshape_mat4_t transform;
    }

    public static unsafe partial class Shape
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_build_plane", ExactSpelling = true)]
        public static extern void build_plane(sshape_state_t* state, [NativeTypeName("const sshape_plane_t *")] sshape_plane_t* @params);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_build_box", ExactSpelling = true)]
        public static extern void build_box(sshape_state_t* state, [NativeTypeName("const sshape_box_t *")] sshape_box_t* @params);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_build_sphere", ExactSpelling = true)]
        public static extern void build_sphere(sshape_state_t* state, [NativeTypeName("const sshape_sphere_t *")] sshape_sphere_t* @params);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_build_cylinder", ExactSpelling = true)]
        public static extern void build_cylinder(sshape_state_t* state, [NativeTypeName("const sshape_cylinder_t *")] sshape_cylinder_t* @params);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_build_torus", ExactSpelling = true)]
        public static extern void build_torus(sshape_state_t* state, [NativeTypeName("const sshape_torus_t *")] sshape_torus_t* @params);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_vertex_size", ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint vertex_size([NativeTypeName("const sshape_optional_components_t *")] sshape_optional_components_t* components);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_plane_sizes", ExactSpelling = true)]
        public static extern sshape_sizes_t plane_sizes([NativeTypeName("uint32_t")] uint tiles, [NativeTypeName("size_t")] nuint vertex_size);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_box_sizes", ExactSpelling = true)]
        public static extern sshape_sizes_t box_sizes([NativeTypeName("uint32_t")] uint tiles, [NativeTypeName("size_t")] nuint vertex_size);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_sphere_sizes", ExactSpelling = true)]
        public static extern sshape_sizes_t sphere_sizes([NativeTypeName("uint32_t")] uint slices, [NativeTypeName("uint32_t")] uint stacks, [NativeTypeName("size_t")] nuint vertex_size);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_cylinder_sizes", ExactSpelling = true)]
        public static extern sshape_sizes_t cylinder_sizes([NativeTypeName("uint32_t")] uint slices, [NativeTypeName("uint32_t")] uint stacks, [NativeTypeName("size_t")] nuint vertex_size);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_torus_sizes", ExactSpelling = true)]
        public static extern sshape_sizes_t torus_sizes([NativeTypeName("uint32_t")] uint sides, [NativeTypeName("uint32_t")] uint rings, [NativeTypeName("size_t")] nuint vertex_size);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_element_range", ExactSpelling = true)]
        public static extern sshape_element_range_t element_range([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_vertex_buffer_desc", ExactSpelling = true)]
        public static extern sg_buffer_desc vertex_buffer_desc([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_index_buffer_desc", ExactSpelling = true)]
        public static extern sg_buffer_desc index_buffer_desc([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_vertex_buffer_layout_state", ExactSpelling = true)]
        public static extern sg_vertex_buffer_layout_state vertex_buffer_layout_state([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_position_vertex_attr_state", ExactSpelling = true)]
        public static extern sg_vertex_attr_state position_vertex_attr_state([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_normal_vertex_attr_state", ExactSpelling = true)]
        public static extern sg_vertex_attr_state normal_vertex_attr_state([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_texcoord_vertex_attr_state", ExactSpelling = true)]
        public static extern sg_vertex_attr_state texcoord_vertex_attr_state([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_color_vertex_attr_state", ExactSpelling = true)]
        public static extern sg_vertex_attr_state color_vertex_attr_state([NativeTypeName("const sshape_state_t *")] sshape_state_t* state);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_color_4f", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint color_4f(float r, float g, float b, float a);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_color_3f", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint color_3f(float r, float g, float b);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_color_4b", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint color_4b([NativeTypeName("uint8_t")] byte r, [NativeTypeName("uint8_t")] byte g, [NativeTypeName("uint8_t")] byte b, [NativeTypeName("uint8_t")] byte a);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_color_3b", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint color_3b([NativeTypeName("uint8_t")] byte r, [NativeTypeName("uint8_t")] byte g, [NativeTypeName("uint8_t")] byte b);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_mat4", ExactSpelling = true)]
        public static extern sshape_mat4_t mat4([NativeTypeName("const float[16]")] float* m);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sshape_mat4_transpose", ExactSpelling = true)]
        public static extern sshape_mat4_t mat4_transpose([NativeTypeName("const float[16]")] float* m);
    }
}
