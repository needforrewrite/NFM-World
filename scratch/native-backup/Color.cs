using System.Runtime.InteropServices;

namespace SharpSokol.Native
{
    public static unsafe partial class Color
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_color_4b", ExactSpelling = true)]
        public static extern sg_color make_color_4b([NativeTypeName("uint8_t")] byte r, [NativeTypeName("uint8_t")] byte g, [NativeTypeName("uint8_t")] byte b, [NativeTypeName("uint8_t")] byte a);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_make_color_1i", ExactSpelling = true)]
        public static extern sg_color make_color_1i([NativeTypeName("uint32_t")] uint rgba);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_color_lerp", ExactSpelling = true)]
        public static extern sg_color color_lerp([NativeTypeName("const sg_color *")] sg_color* color_a, [NativeTypeName("const sg_color *")] sg_color* color_b, float amount);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_color_lerp_precise", ExactSpelling = true)]
        public static extern sg_color color_lerp_precise([NativeTypeName("const sg_color *")] sg_color* color_a, [NativeTypeName("const sg_color *")] sg_color* color_b, float amount);

        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sg_color_multiply", ExactSpelling = true)]
        public static extern sg_color color_multiply([NativeTypeName("const sg_color *")] sg_color* color, float scale);

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_alice_blue = new sg_color
        {
            r = 0.941176471f,
            g = 0.97254902f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_antique_white = new sg_color
        {
            r = 0.980392157f,
            g = 0.921568627f,
            b = 0.843137255f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_aqua = new sg_color
        {
            r = 0.0f,
            g = 1.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_aquamarine = new sg_color
        {
            r = 0.498039216f,
            g = 1.0f,
            b = 0.831372549f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_azure = new sg_color
        {
            r = 0.941176471f,
            g = 1.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_beige = new sg_color
        {
            r = 0.960784314f,
            g = 0.960784314f,
            b = 0.862745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_bisque = new sg_color
        {
            r = 1.0f,
            g = 0.894117647f,
            b = 0.768627451f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_black = new sg_color
        {
            r = 0.0f,
            g = 0.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_blanched_almond = new sg_color
        {
            r = 1.0f,
            g = 0.921568627f,
            b = 0.803921569f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_blue = new sg_color
        {
            r = 0.0f,
            g = 0.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_blue_violet = new sg_color
        {
            r = 0.541176471f,
            g = 0.168627451f,
            b = 0.88627451f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_brown = new sg_color
        {
            r = 0.647058824f,
            g = 0.164705882f,
            b = 0.164705882f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_burlywood = new sg_color
        {
            r = 0.870588235f,
            g = 0.721568627f,
            b = 0.529411765f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_cadet_blue = new sg_color
        {
            r = 0.37254902f,
            g = 0.619607843f,
            b = 0.62745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_chartreuse = new sg_color
        {
            r = 0.498039216f,
            g = 1.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_chocolate = new sg_color
        {
            r = 0.823529412f,
            g = 0.411764706f,
            b = 0.117647059f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_coral = new sg_color
        {
            r = 1.0f,
            g = 0.498039216f,
            b = 0.31372549f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_cornflower_blue = new sg_color
        {
            r = 0.392156863f,
            g = 0.584313725f,
            b = 0.929411765f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_cornsilk = new sg_color
        {
            r = 1.0f,
            g = 0.97254902f,
            b = 0.862745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_crimson = new sg_color
        {
            r = 0.862745098f,
            g = 0.0784313725f,
            b = 0.235294118f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_cyan = new sg_color
        {
            r = 0.0f,
            g = 1.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_blue = new sg_color
        {
            r = 0.0f,
            g = 0.0f,
            b = 0.545098039f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_cyan = new sg_color
        {
            r = 0.0f,
            g = 0.545098039f,
            b = 0.545098039f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_goldenrod = new sg_color
        {
            r = 0.721568627f,
            g = 0.525490196f,
            b = 0.0431372549f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_gray = new sg_color
        {
            r = 0.662745098f,
            g = 0.662745098f,
            b = 0.662745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_green = new sg_color
        {
            r = 0.0f,
            g = 0.392156863f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_khaki = new sg_color
        {
            r = 0.741176471f,
            g = 0.717647059f,
            b = 0.419607843f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_magenta = new sg_color
        {
            r = 0.545098039f,
            g = 0.0f,
            b = 0.545098039f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_olive_green = new sg_color
        {
            r = 0.333333333f,
            g = 0.419607843f,
            b = 0.184313725f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_orange = new sg_color
        {
            r = 1.0f,
            g = 0.549019608f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_orchid = new sg_color
        {
            r = 0.6f,
            g = 0.196078431f,
            b = 0.8f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_red = new sg_color
        {
            r = 0.545098039f,
            g = 0.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_salmon = new sg_color
        {
            r = 0.91372549f,
            g = 0.588235294f,
            b = 0.478431373f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_sea_green = new sg_color
        {
            r = 0.560784314f,
            g = 0.737254902f,
            b = 0.560784314f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_slate_blue = new sg_color
        {
            r = 0.282352941f,
            g = 0.239215686f,
            b = 0.545098039f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_slate_gray = new sg_color
        {
            r = 0.184313725f,
            g = 0.309803922f,
            b = 0.309803922f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_turquoise = new sg_color
        {
            r = 0.0f,
            g = 0.807843137f,
            b = 0.819607843f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dark_violet = new sg_color
        {
            r = 0.580392157f,
            g = 0.0f,
            b = 0.82745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_deep_pink = new sg_color
        {
            r = 1.0f,
            g = 0.0784313725f,
            b = 0.576470588f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_deep_sky_blue = new sg_color
        {
            r = 0.0f,
            g = 0.749019608f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dim_gray = new sg_color
        {
            r = 0.411764706f,
            g = 0.411764706f,
            b = 0.411764706f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_dodger_blue = new sg_color
        {
            r = 0.117647059f,
            g = 0.564705882f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_firebrick = new sg_color
        {
            r = 0.698039216f,
            g = 0.133333333f,
            b = 0.133333333f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_floral_white = new sg_color
        {
            r = 1.0f,
            g = 0.980392157f,
            b = 0.941176471f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_forest_green = new sg_color
        {
            r = 0.133333333f,
            g = 0.545098039f,
            b = 0.133333333f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_fuchsia = new sg_color
        {
            r = 1.0f,
            g = 0.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_gainsboro = new sg_color
        {
            r = 0.862745098f,
            g = 0.862745098f,
            b = 0.862745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_ghost_white = new sg_color
        {
            r = 0.97254902f,
            g = 0.97254902f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_gold = new sg_color
        {
            r = 1.0f,
            g = 0.843137255f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_goldenrod = new sg_color
        {
            r = 0.854901961f,
            g = 0.647058824f,
            b = 0.125490196f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_gray = new sg_color
        {
            r = 0.745098039f,
            g = 0.745098039f,
            b = 0.745098039f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_web_gray = new sg_color
        {
            r = 0.501960784f,
            g = 0.501960784f,
            b = 0.501960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_green = new sg_color
        {
            r = 0.0f,
            g = 1.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_web_green = new sg_color
        {
            r = 0.0f,
            g = 0.501960784f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_green_yellow = new sg_color
        {
            r = 0.678431373f,
            g = 1.0f,
            b = 0.184313725f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_honeydew = new sg_color
        {
            r = 0.941176471f,
            g = 1.0f,
            b = 0.941176471f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_hot_pink = new sg_color
        {
            r = 1.0f,
            g = 0.411764706f,
            b = 0.705882353f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_indian_red = new sg_color
        {
            r = 0.803921569f,
            g = 0.360784314f,
            b = 0.360784314f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_indigo = new sg_color
        {
            r = 0.294117647f,
            g = 0.0f,
            b = 0.509803922f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_ivory = new sg_color
        {
            r = 1.0f,
            g = 1.0f,
            b = 0.941176471f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_khaki = new sg_color
        {
            r = 0.941176471f,
            g = 0.901960784f,
            b = 0.549019608f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_lavender = new sg_color
        {
            r = 0.901960784f,
            g = 0.901960784f,
            b = 0.980392157f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_lavender_blush = new sg_color
        {
            r = 1.0f,
            g = 0.941176471f,
            b = 0.960784314f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_lawn_green = new sg_color
        {
            r = 0.48627451f,
            g = 0.988235294f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_lemon_chiffon = new sg_color
        {
            r = 1.0f,
            g = 0.980392157f,
            b = 0.803921569f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_blue = new sg_color
        {
            r = 0.678431373f,
            g = 0.847058824f,
            b = 0.901960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_coral = new sg_color
        {
            r = 0.941176471f,
            g = 0.501960784f,
            b = 0.501960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_cyan = new sg_color
        {
            r = 0.878431373f,
            g = 1.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_goldenrod = new sg_color
        {
            r = 0.980392157f,
            g = 0.980392157f,
            b = 0.823529412f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_gray = new sg_color
        {
            r = 0.82745098f,
            g = 0.82745098f,
            b = 0.82745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_green = new sg_color
        {
            r = 0.564705882f,
            g = 0.933333333f,
            b = 0.564705882f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_pink = new sg_color
        {
            r = 1.0f,
            g = 0.71372549f,
            b = 0.756862745f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_salmon = new sg_color
        {
            r = 1.0f,
            g = 0.62745098f,
            b = 0.478431373f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_sea_green = new sg_color
        {
            r = 0.125490196f,
            g = 0.698039216f,
            b = 0.666666667f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_sky_blue = new sg_color
        {
            r = 0.529411765f,
            g = 0.807843137f,
            b = 0.980392157f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_slate_gray = new sg_color
        {
            r = 0.466666667f,
            g = 0.533333333f,
            b = 0.6f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_steel_blue = new sg_color
        {
            r = 0.690196078f,
            g = 0.768627451f,
            b = 0.870588235f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_light_yellow = new sg_color
        {
            r = 1.0f,
            g = 1.0f,
            b = 0.878431373f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_lime = new sg_color
        {
            r = 0.0f,
            g = 1.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_lime_green = new sg_color
        {
            r = 0.196078431f,
            g = 0.803921569f,
            b = 0.196078431f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_linen = new sg_color
        {
            r = 0.980392157f,
            g = 0.941176471f,
            b = 0.901960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_magenta = new sg_color
        {
            r = 1.0f,
            g = 0.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_maroon = new sg_color
        {
            r = 0.690196078f,
            g = 0.188235294f,
            b = 0.376470588f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_web_maroon = new sg_color
        {
            r = 0.501960784f,
            g = 0.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_aquamarine = new sg_color
        {
            r = 0.4f,
            g = 0.803921569f,
            b = 0.666666667f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_blue = new sg_color
        {
            r = 0.0f,
            g = 0.0f,
            b = 0.803921569f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_orchid = new sg_color
        {
            r = 0.729411765f,
            g = 0.333333333f,
            b = 0.82745098f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_purple = new sg_color
        {
            r = 0.576470588f,
            g = 0.439215686f,
            b = 0.858823529f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_sea_green = new sg_color
        {
            r = 0.235294118f,
            g = 0.701960784f,
            b = 0.443137255f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_slate_blue = new sg_color
        {
            r = 0.482352941f,
            g = 0.407843137f,
            b = 0.933333333f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_spring_green = new sg_color
        {
            r = 0.0f,
            g = 0.980392157f,
            b = 0.603921569f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_turquoise = new sg_color
        {
            r = 0.282352941f,
            g = 0.819607843f,
            b = 0.8f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_medium_violet_red = new sg_color
        {
            r = 0.780392157f,
            g = 0.0823529412f,
            b = 0.521568627f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_midnight_blue = new sg_color
        {
            r = 0.0980392157f,
            g = 0.0980392157f,
            b = 0.439215686f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_mint_cream = new sg_color
        {
            r = 0.960784314f,
            g = 1.0f,
            b = 0.980392157f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_misty_rose = new sg_color
        {
            r = 1.0f,
            g = 0.894117647f,
            b = 0.882352941f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_moccasin = new sg_color
        {
            r = 1.0f,
            g = 0.894117647f,
            b = 0.709803922f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_navajo_white = new sg_color
        {
            r = 1.0f,
            g = 0.870588235f,
            b = 0.678431373f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_navy_blue = new sg_color
        {
            r = 0.0f,
            g = 0.0f,
            b = 0.501960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_old_lace = new sg_color
        {
            r = 0.992156863f,
            g = 0.960784314f,
            b = 0.901960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_olive = new sg_color
        {
            r = 0.501960784f,
            g = 0.501960784f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_olive_drab = new sg_color
        {
            r = 0.419607843f,
            g = 0.556862745f,
            b = 0.137254902f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_orange = new sg_color
        {
            r = 1.0f,
            g = 0.647058824f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_orange_red = new sg_color
        {
            r = 1.0f,
            g = 0.270588235f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_orchid = new sg_color
        {
            r = 0.854901961f,
            g = 0.439215686f,
            b = 0.839215686f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_pale_goldenrod = new sg_color
        {
            r = 0.933333333f,
            g = 0.909803922f,
            b = 0.666666667f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_pale_green = new sg_color
        {
            r = 0.596078431f,
            g = 0.984313725f,
            b = 0.596078431f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_pale_turquoise = new sg_color
        {
            r = 0.68627451f,
            g = 0.933333333f,
            b = 0.933333333f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_pale_violet_red = new sg_color
        {
            r = 0.858823529f,
            g = 0.439215686f,
            b = 0.576470588f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_papaya_whip = new sg_color
        {
            r = 1.0f,
            g = 0.937254902f,
            b = 0.835294118f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_peach_puff = new sg_color
        {
            r = 1.0f,
            g = 0.854901961f,
            b = 0.725490196f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_peru = new sg_color
        {
            r = 0.803921569f,
            g = 0.521568627f,
            b = 0.247058824f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_pink = new sg_color
        {
            r = 1.0f,
            g = 0.752941176f,
            b = 0.796078431f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_plum = new sg_color
        {
            r = 0.866666667f,
            g = 0.62745098f,
            b = 0.866666667f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_powder_blue = new sg_color
        {
            r = 0.690196078f,
            g = 0.878431373f,
            b = 0.901960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_purple = new sg_color
        {
            r = 0.62745098f,
            g = 0.125490196f,
            b = 0.941176471f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_web_purple = new sg_color
        {
            r = 0.501960784f,
            g = 0.0f,
            b = 0.501960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_rebecca_purple = new sg_color
        {
            r = 0.4f,
            g = 0.2f,
            b = 0.6f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_red = new sg_color
        {
            r = 1.0f,
            g = 0.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_rosy_brown = new sg_color
        {
            r = 0.737254902f,
            g = 0.560784314f,
            b = 0.560784314f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_royal_blue = new sg_color
        {
            r = 0.254901961f,
            g = 0.411764706f,
            b = 0.882352941f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_saddle_brown = new sg_color
        {
            r = 0.545098039f,
            g = 0.270588235f,
            b = 0.0745098039f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_salmon = new sg_color
        {
            r = 0.980392157f,
            g = 0.501960784f,
            b = 0.447058824f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_sandy_brown = new sg_color
        {
            r = 0.956862745f,
            g = 0.643137255f,
            b = 0.376470588f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_sea_green = new sg_color
        {
            r = 0.180392157f,
            g = 0.545098039f,
            b = 0.341176471f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_seashell = new sg_color
        {
            r = 1.0f,
            g = 0.960784314f,
            b = 0.933333333f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_sienna = new sg_color
        {
            r = 0.62745098f,
            g = 0.321568627f,
            b = 0.176470588f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_silver = new sg_color
        {
            r = 0.752941176f,
            g = 0.752941176f,
            b = 0.752941176f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_sky_blue = new sg_color
        {
            r = 0.529411765f,
            g = 0.807843137f,
            b = 0.921568627f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_slate_blue = new sg_color
        {
            r = 0.415686275f,
            g = 0.352941176f,
            b = 0.803921569f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_slate_gray = new sg_color
        {
            r = 0.439215686f,
            g = 0.501960784f,
            b = 0.564705882f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_snow = new sg_color
        {
            r = 1.0f,
            g = 0.980392157f,
            b = 0.980392157f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_spring_green = new sg_color
        {
            r = 0.0f,
            g = 1.0f,
            b = 0.498039216f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_steel_blue = new sg_color
        {
            r = 0.274509804f,
            g = 0.509803922f,
            b = 0.705882353f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_tan = new sg_color
        {
            r = 0.823529412f,
            g = 0.705882353f,
            b = 0.549019608f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_teal = new sg_color
        {
            r = 0.0f,
            g = 0.501960784f,
            b = 0.501960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_thistle = new sg_color
        {
            r = 0.847058824f,
            g = 0.749019608f,
            b = 0.847058824f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_tomato = new sg_color
        {
            r = 1.0f,
            g = 0.388235294f,
            b = 0.278431373f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_transparent = new sg_color
        {
            r = 0.0f,
            g = 0.0f,
            b = 0.0f,
            a = 0.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_turquoise = new sg_color
        {
            r = 0.250980392f,
            g = 0.878431373f,
            b = 0.815686275f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_violet = new sg_color
        {
            r = 0.933333333f,
            g = 0.509803922f,
            b = 0.933333333f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_wheat = new sg_color
        {
            r = 0.960784314f,
            g = 0.870588235f,
            b = 0.701960784f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_white = new sg_color
        {
            r = 1.0f,
            g = 1.0f,
            b = 1.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_white_smoke = new sg_color
        {
            r = 0.960784314f,
            g = 0.960784314f,
            b = 0.960784314f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_yellow = new sg_color
        {
            r = 1.0f,
            g = 1.0f,
            b = 0.0f,
            a = 1.0f,
        };

        [NativeTypeName("const sg_color")]
        public static readonly sg_color sg_yellow_green = new sg_color
        {
            r = 0.603921569f,
            g = 0.803921569f,
            b = 0.196078431f,
            a = 1.0f,
        };
    }
}
