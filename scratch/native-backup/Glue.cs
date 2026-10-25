using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public static partial class Glue
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sglue_environment", ExactSpelling = true)]
        public static extern sg_environment environment();

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sglue_swapchain", ExactSpelling = true)]
        public static extern sg_swapchain swapchain();
    }
}
